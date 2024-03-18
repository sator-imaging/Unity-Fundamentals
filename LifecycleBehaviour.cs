/** Simple Lifecycle Manager for Unity
 ** (c) 2024 github.com/sator-imaging
 ** Licensed under the MIT License

This library helps managing MonoBehaviour lifetime by CancellationToken introduced in Unity 2022.
Not only that but also has an ability to manage "Update" actions ordered and efficiently.

And also! library provides missing `destroyCancellationToken` feature for Unity 2021 LTS!!

**Feature Highlights**
- [Object Lifetime Management](#object-lifetime-management)
- [Update Function Manager](#update-function-manager)
    - Designed to address consideration written in the following article
    - https://blog.unity.com/engine-platform/10000-update-calls

**Installation**
- In Unity 2021 or later, Enter the following URL in Unity Package Manager (UPM)
    - https://github.com/sator-imaging/Unity-LifecycleManager.git


Object Lifetime Management
==========================
Here is example to bind instance lifetime to cancellation token or MonoBehaviour.

```csharp
using SatorImaging.LifecycleManager;

// works on Unity 2021 or later
IDisposable.DestroyWith(monoBehaviourOrCancellationToken);
unityObj.DestroyWith(cancellationToken);
component.DestroyWith(tokenOrMonoBehaviour);

// bind to scene lifecycle
var sceneLifecycle = SceneLifecycle.Get(gameObject.scene);
disposable.DestroyWith(sceneLifecycle);
sceneLifecycle.DestroyWith(cancellationToken);  // error
                                                // scene lifecycle is restricted from being bound

// nesting lifecycles
var root = LifecycleBehaviour.Create("Root Lifecycle");
var child = LifecycleBehaviour.Create("Child Lifecycle");
var grand = LifecycleBehaviour.Create("Grandchild Lifecycle");
child.DestroyWith(root);
grand.DestroyWith(child);
    // --> child and grand will be marked as DontDestroyOnLoad automatically

// action for debugging purpose which will be invoked before binding (when not null)
LifetimeExtensions.DebuggerAction = (obj, token, ticket) =>
{
    Debug.Log($"Target Object: {obj}");
    Debug.Log($"CancellationToken: {token}");
    Debug.Log($"CancellationTokenRegistration: {ticket}");
};
```


Technical Note
--------------

### Component Binding Notice

You can bind `UnityEngine.Component` lifetime to cancellation token or MonoBehaviour, but you
need to consider both situation component is destroyed by scene unloading OR by lifetime owner.
As a result, it makes thing really complex and scene will be spaghetti-ed.

Strongly recommended that binding GameObject lifetime instead of component.

> [!NOTE]
> As you know there is `DontDestroyOnLoad` feature in Unity but it is hard to determine that
> component.gameObject is safe to be marked as `DontDestroyOnLoad` programatically.

> [!TIP]
> Set preprocessor symbol `LIFECYCLE_DISALLOW_COMPONENT_BINDING` to disallow component binding.
> When not set, IDE shows underline on use to confirm "are you sure?".


### Inter-Scene Binding Notice

Lifetime binding across scenes is restricted. Nevertheless you want to bind lifetime to another
scene object, use `DestroyWith(CancellationToken)` method with `monoBehaviour.destroyCancellationToken`.

> [!WARNING]
> When Unity object bound to another scene object, it will be destroyed by both when scene which
> containing bound object is unloaded and lifetime owner is destroyed.


### Quick Tests

Select menu commands in `Unity Editor > LifecycleManager > ...` to test the features.



Order of Destruction
--------------------
Destroy actions will happen in LIFO order, that is, last lifetime-bound object is destroyed first.
(of course lifetime owner is destroyed before)

Note that C# class instances and GameObjects destruction order is stable whereas MonoBehaviours
destruction order is NOT stable. For reference, MonoBehaviours (components) will be destroyed earlier
when scene is unloaded otherwise destroyed based on binding order.

> [!NOTE]
> To make destruction order stable, lifetime bound GameObjects are marked as `DontDestroyOnLoad`.

> [!TIP]
> Set preprocessor symbol `LIFECYCLE_DISABLE_STABLE_DESTROY_ORDER` to disable automatic `DontDestroyOnLoad`.
> ie. Destruction order could be unstable and object may be destroyed by scene unloading.



Update Function Manager
=======================
In this feature, each "update" function has 5 stages, Initialize, Early, Normal, Late and Finalize.
Initialize and Finalize is designed for system usage, other 3 stages are for casual usage.

> [!NOTE]
> For optimization, removing registered action will swap items in list to prevent array reordering.
> ie. Execution order of stages are promised but registered action order is NOT promised.

```csharp
// create lifecycle behaviour
var lifecycle = LifecycleBehaviour.Create("My Lifecycle!!");

// register action to lifecycle stages
lifecycle.RegisterUpdateEarly(...);
lifecycle.RegisterUpdate(...);
lifecycle.RegisterUpdateLate(...);

lifecycle.RegisterLateUpdateEarly(...);
lifecycle.RegisterLateUpdate(...);
lifecycle.RegisterLateUpdateLate(...);

lifecycle.RegisterFixedUpdateEarly(...);
lifecycle.RegisterFixedUpdate(...);
lifecycle.RegisterFixedUpdateLate(...);

// to remove action manually, store and use instance which returned from register method
var entry = lifecycle.RegisterUpdateLate(...);
lifecycle.RemoveUpdateLate(entry);
```


Automatic Unregistration
------------------------
If action is depending on instance that will be destroyed with cancellation token, You can specify
same token to unregister action together when token is canceled.

```csharp
instance.DestroyWith(token);
lifecycle.RegisterUpdate(() => instance.NoErrorUntilDisposed(), token);  // <-- same token

// if don't, action will raise error after depending instance is destroyed
lifecycle.RegisterUpdate(() => instance.NoErrorUntilDisposed());  // <-- error!!
```


Controlling Order of Multiple Update Managers
---------------------------------------------
To meet your app requirement, it allows to change lifecycle execution order while keeping
registered actions order.

```csharp
// simple but effective! don't try to dive into UnityEngine.LowLevel.PlayerLoop system!!
class UpdateManagerOrganizer : MonoBehaviour
{
    public LifecycleBehaviour lifecycle1;
    public LifecycleBehaviour lifecycle2;
    public LifecycleBehaviour lifecycle3;

    void OnEnable()
    {
        lifecycle1.enabled = false;
        lifecycle2.enabled = false;
        lifecycle3.enabled = false;
    }
    void Update()
    {
        lifecycle3.Update();
        lifecycle1.Update();
        lifecycle2.Update();
    }
    void LateUpdate()
    {
        lifecycle1.LateUpdate();
        lifecycle3.LateUpdate();
        lifecycle2.LateUpdate();
    }
    void FixedUpdate()
    {
        lifecycle2.FixedUpdate();
        lifecycle3.FixedUpdate();
        lifecycle1.FixedUpdate();
    }
}
```

 */

#nullable enable
//#undef UNITY_EDITOR           // uncomment to debug
//#undef UNITY_2022_2_OR_NEWER  // uncomment to debug
//#define LIFECYCLE_DISALLOW_COMPONENT_BINDING  // uncomment to raise error

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace SatorImaging.LifecycleManager
{
    /// <summary>
    /// The "Update Manager" functionality with `MonoBehaviour.destroyCancellationToken` support for Unity 2021.
    /// </summary>
    public class LifecycleBehaviour : MonoBehaviour
    {
        public bool IsSceneLifecycle { get; protected set; } = false;

#if UNITY_EDITOR
        /// <summary>
        /// See <see cref="LifetimeExtensions.DestroyWith(UnityEngine.Object, LifecycleBehaviour)"/>
        /// </summary>
        /// <seealso cref="LifetimeExtensions.DestroyWith(UnityEngine.Object, LifecycleBehaviour)"/>
        [Obsolete("Editor Only")]
        [Header("[Editor Only]\nList won't be updated even when object is disposed")]
        public List<string> LifetimeBoundObjectNames = new();
#endif

        // polyfill - allocate only when requested
#if false == UNITY_2022_2_OR_NEWER
        private CancellationTokenSource? polyfill_destroyToken;
        public CancellationToken destroyCancellationToken => (polyfill_destroyToken ??= new()).Token;
#endif

        public void OnDestroy()
        {
#if UNITY_EDITOR
            Debug.Log(nameof(LifecycleBehaviour) + ": going to be destroyed: " + this);
#endif
            _fixedUpdateStart.Clear();
            _fixedUpdateEarly.Clear();
            _fixedUpdateUsual.Clear();
            _fixedUpdateLater.Clear();
            _fixedUpdateFinal.Clear();
            _updateStart.Clear();
            _updateEarly.Clear();
            _updateUsual.Clear();
            _updateLater.Clear();
            _updateFinal.Clear();
            _lateUpdateStart.Clear();
            _lateUpdateEarly.Clear();
            _lateUpdateUsual.Clear();
            _lateUpdateLater.Clear();
            _lateUpdateFinal.Clear();

            // TODO: this should be implemented in derived class!!
            if (IsSceneLifecycle && this is SceneLifecycle sl)
                SceneLifecycle.RemoveFromCache(sl);

#if false == UNITY_2022_2_OR_NEWER
            polyfill_destroyToken?.Cancel();
#endif
        }


        // helper
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LifecycleBehaviour Create(string nameOfGameObject, bool dontDestroyOnLoad)
        {
            var go = new GameObject(nameOfGameObject);
            if (dontDestroyOnLoad)
                DontDestroyOnLoad(go);

            return go.AddComponent<LifecycleBehaviour>();
        }


        /*  the update manager  ================================================================ */

        // NOTE: lifecycle is long-living instance. always creating action list is better to avoid null check on all access.
        // NOTE: 5 letters suffix is for alignment. stage order: Initialize -> Early -> Usual -> Late -> Finalize
        protected readonly SlotReusingActionList _updateStart = new();
        protected readonly SlotReusingActionList _updateEarly = new();  // Earlier is antonym of later but Early
        protected readonly SlotReusingActionList _updateUsual = new();  // Plain, Usual, Exact, Right, etc...
        protected readonly SlotReusingActionList _updateLater = new();  // Late is antonym of early but Later
        protected readonly SlotReusingActionList _updateFinal = new();
        protected readonly SlotReusingActionList _lateUpdateStart = new();
        protected readonly SlotReusingActionList _lateUpdateEarly = new();
        protected readonly SlotReusingActionList _lateUpdateUsual = new();
        protected readonly SlotReusingActionList _lateUpdateLater = new();
        protected readonly SlotReusingActionList _lateUpdateFinal = new();
        protected readonly SlotReusingActionList _fixedUpdateStart = new();
        protected readonly SlotReusingActionList _fixedUpdateEarly = new();
        protected readonly SlotReusingActionList _fixedUpdateUsual = new();
        protected readonly SlotReusingActionList _fixedUpdateLater = new();
        protected readonly SlotReusingActionList _fixedUpdateFinal = new();

        /// <summary>
        /// Set `enabled` false and call this method explicitly in "manager of update managers" to control lifecycle execution order.
        /// </summary>
        public void Update()
        {
            _updateStart.Invoke();
            _updateEarly.Invoke();
            _updateUsual.Invoke();
            _updateLater.Invoke();
            _updateFinal.Invoke();
        }

        /// <inheritdoc cref="Update"/>
        public void LateUpdate()
        {
            _lateUpdateStart.Invoke();
            _lateUpdateEarly.Invoke();
            _lateUpdateUsual.Invoke();
            _lateUpdateLater.Invoke();
            _lateUpdateFinal.Invoke();
        }

        /// <inheritdoc cref="Update"/>
        public void FixedUpdate()
        {
            _fixedUpdateStart.Invoke();
            _fixedUpdateEarly.Invoke();
            _fixedUpdateUsual.Invoke();
            _fixedUpdateLater.Invoke();
            _fixedUpdateFinal.Invoke();
        }


        // NOTE: /**/ is for blocking auto formatter
        /// <inheritdoc cref="SlotReusingActionList.Add(Action)"/>
        public Action? RegisterFixedUpdateInitialize(Action act) /**/ => _fixedUpdateStart.Add(act);
        /// <inheritdoc cref="SlotReusingActionList.Add(Action)"/>
        public Action? RegisterFixedUpdateEarly(Action act)      /**/ => _fixedUpdateEarly.Add(act);
        /// <inheritdoc cref="SlotReusingActionList.Add(Action)"/>
        public Action? RegisterFixedUpdate(Action act)           /**/ => _fixedUpdateUsual.Add(act);
        /// <inheritdoc cref="SlotReusingActionList.Add(Action)"/>
        public Action? RegisterFixedUpdateLate(Action act)       /**/ => _fixedUpdateLater.Add(act);
        /// <inheritdoc cref="SlotReusingActionList.Add(Action)"/>
        public Action? RegisterFixedUpdateFinalize(Action act)   /**/ => _fixedUpdateFinal.Add(act);
        /// <inheritdoc cref="SlotReusingActionList.Add(Action)"/>
        public Action? RegisterUpdateInitialize(Action act)      /**/ => _updateStart.Add(act);
        /// <inheritdoc cref="SlotReusingActionList.Add(Action)"/>
        public Action? RegisterUpdateEarly(Action act)           /**/ => _updateEarly.Add(act);
        /// <inheritdoc cref="SlotReusingActionList.Add(Action)"/>
        public Action? RegisterUpdate(Action act)                /**/ => _updateUsual.Add(act);
        /// <inheritdoc cref="SlotReusingActionList.Add(Action)"/>
        public Action? RegisterUpdateLate(Action act)            /**/ => _updateLater.Add(act);
        /// <inheritdoc cref="SlotReusingActionList.Add(Action)"/>
        public Action? RegisterUpdateFinalize(Action act)        /**/ => _updateFinal.Add(act);
        /// <inheritdoc cref="SlotReusingActionList.Add(Action)"/>
        public Action? RegisterLateUpdateInitialize(Action act)  /**/ => _lateUpdateStart.Add(act);
        /// <inheritdoc cref="SlotReusingActionList.Add(Action)"/>
        public Action? RegisterLateUpdateEarly(Action act)       /**/ => _lateUpdateEarly.Add(act);
        /// <inheritdoc cref="SlotReusingActionList.Add(Action)"/>
        public Action? RegisterLateUpdate(Action act)            /**/ => _lateUpdateUsual.Add(act);
        /// <inheritdoc cref="SlotReusingActionList.Add(Action)"/>
        public Action? RegisterLateUpdateLate(Action act)        /**/ => _lateUpdateLater.Add(act);
        /// <inheritdoc cref="SlotReusingActionList.Add(Action)"/>
        public Action? RegisterLateUpdateFinalize(Action act)    /**/ => _lateUpdateFinal.Add(act);


        /// <inheritdoc cref="SlotReusingActionList.Remove(Action)"/>
        public void RemoveFixedUpdateInitialize(Action act) /**/ => _fixedUpdateStart.Remove(act);
        /// <inheritdoc cref="SlotReusingActionList.Remove(Action)"/>
        public void RemoveFixedUpdateEarly(Action act)      /**/ => _fixedUpdateEarly.Remove(act);
        /// <inheritdoc cref="SlotReusingActionList.Remove(Action)"/>
        public void RemoveFixedUpdate(Action act)           /**/ => _fixedUpdateUsual.Remove(act);
        /// <inheritdoc cref="SlotReusingActionList.Remove(Action)"/>
        public void RemoveFixedUpdateLate(Action act)       /**/ => _fixedUpdateLater.Remove(act);
        /// <inheritdoc cref="SlotReusingActionList.Remove(Action)"/>
        public void RemoveFixedUpdateFinalize(Action act)   /**/ => _fixedUpdateFinal.Remove(act);
        /// <inheritdoc cref="SlotReusingActionList.Remove(Action)"/>
        public void RemoveUpdateInitialize(Action act)      /**/ => _updateStart.Remove(act);
        /// <inheritdoc cref="SlotReusingActionList.Remove(Action)"/>
        public void RemoveUpdateEarly(Action act)           /**/ => _updateEarly.Remove(act);
        /// <inheritdoc cref="SlotReusingActionList.Remove(Action)"/>
        public void RemoveUpdate(Action act)                /**/ => _updateUsual.Remove(act);
        /// <inheritdoc cref="SlotReusingActionList.Remove(Action)"/>
        public void RemoveUpdateLate(Action act)            /**/ => _updateLater.Remove(act);
        /// <inheritdoc cref="SlotReusingActionList.Remove(Action)"/>
        public void RemoveUpdateFinalize(Action act)        /**/ => _updateFinal.Remove(act);
        /// <inheritdoc cref="SlotReusingActionList.Remove(Action)"/>
        public void RemoveLateUpdateInitialize(Action act)  /**/ => _lateUpdateStart.Remove(act);
        /// <inheritdoc cref="SlotReusingActionList.Remove(Action)"/>
        public void RemoveLateUpdateEarly(Action act)       /**/ => _lateUpdateEarly.Remove(act);
        /// <inheritdoc cref="SlotReusingActionList.Remove(Action)"/>
        public void RemoveLateUpdate(Action act)            /**/ => _lateUpdateUsual.Remove(act);
        /// <inheritdoc cref="SlotReusingActionList.Remove(Action)"/>
        public void RemoveLateUpdateLate(Action act)        /**/ => _lateUpdateLater.Remove(act);
        /// <inheritdoc cref="SlotReusingActionList.Remove(Action)"/>
        public void RemoveLateUpdateFinalize(Action act)    /**/ => _lateUpdateFinal.Remove(act);

        /// <inheritdoc cref="SlotReusingActionList.GetActions"/>
        public Action[] GetFixedUpdateInitialize()  /**/ => _fixedUpdateStart.GetActions()!;
        /// <inheritdoc cref="SlotReusingActionList.GetActions"/>
        public Action[] GetFixedUpdateEarly()       /**/ => _fixedUpdateEarly.GetActions()!;
        /// <inheritdoc cref="SlotReusingActionList.GetActions"/>
        public Action[] GetFixedUpdate()            /**/ => _fixedUpdateUsual.GetActions()!;
        /// <inheritdoc cref="SlotReusingActionList.GetActions"/>
        public Action[] GetFixedUpdateLate()        /**/ => _fixedUpdateLater.GetActions()!;
        /// <inheritdoc cref="SlotReusingActionList.GetActions"/>
        public Action[] GetFixedUpdateFinalize()    /**/ => _fixedUpdateFinal.GetActions()!;
        /// <inheritdoc cref="SlotReusingActionList.GetActions"/>
        public Action[] GetUpdateInitialize()       /**/ => _updateStart.GetActions()!;
        /// <inheritdoc cref="SlotReusingActionList.GetActions"/>
        public Action[] GetUpdateEarly()            /**/ => _updateEarly.GetActions()!;
        /// <inheritdoc cref="SlotReusingActionList.GetActions"/>
        public Action[] GetUpdate()                 /**/ => _updateUsual.GetActions()!;
        /// <inheritdoc cref="SlotReusingActionList.GetActions"/>
        public Action[] GetUpdateLate()             /**/ => _updateLater.GetActions()!;
        /// <inheritdoc cref="SlotReusingActionList.GetActions"/>
        public Action[] GetUpdateFinalize()         /**/ => _updateFinal.GetActions()!;
        /// <inheritdoc cref="SlotReusingActionList.GetActions"/>
        public Action[] GetLateUpdateInitialize()   /**/ => _lateUpdateStart.GetActions()!;
        /// <inheritdoc cref="SlotReusingActionList.GetActions"/>
        public Action[] GetLateUpdateEarly()        /**/ => _lateUpdateEarly.GetActions()!;
        /// <inheritdoc cref="SlotReusingActionList.GetActions"/>
        public Action[] GetLateUpdate()             /**/ => _lateUpdateUsual.GetActions()!;
        /// <inheritdoc cref="SlotReusingActionList.GetActions"/>
        public Action[] GetLateUpdateLate()         /**/ => _lateUpdateLater.GetActions()!;
        /// <inheritdoc cref="SlotReusingActionList.GetActions"/>
        public Action[] GetLateUpdateFinalize()     /**/ => _lateUpdateFinal.GetActions()!;


        /*  unregister by cancellation token  ================================================================ */

        readonly static Action<object> UnregisterByCancellationToken = obj =>
        {
            if (obj is Ticket ticket)
            {
                ticket.Dispose();
            }
        };

        sealed class Ticket //: IDisposable
        {
            readonly Action action;
            readonly SlotReusingActionList list;
            public Ticket(Action action, SlotReusingActionList list)
            {
                this.action = action;
                this.list = list;
            }

            public /*readonly*/ void Dispose()
            {
                list.Remove(action);
            }
        }

        /// <summary>Unregister action when token is canceled.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Register(Action act, CancellationToken token, SlotReusingActionList list)
        {
            list.Add(act);
            token.Register(UnregisterByCancellationToken, new Ticket(act, list));
        }

        /// <inheritdoc cref="Register(Action, CancellationToken, SlotReusingActionList)"/>
        public void RegisterFixedUpdateInitialize(Action act, CancellationToken ct)  /**/ => Register(act, ct, _fixedUpdateStart);
        /// <inheritdoc cref="Register(Action, CancellationToken, SlotReusingActionList)"/>
        public void RegisterFixedUpdateEarly(Action act, CancellationToken ct)       /**/ => Register(act, ct, _fixedUpdateEarly);
        /// <inheritdoc cref="Register(Action, CancellationToken, SlotReusingActionList)"/>
        public void RegisterFixedUpdate(Action act, CancellationToken ct)            /**/ => Register(act, ct, _fixedUpdateUsual);
        /// <inheritdoc cref="Register(Action, CancellationToken, SlotReusingActionList)"/>
        public void RegisterFixedUpdateLate(Action act, CancellationToken ct)        /**/ => Register(act, ct, _fixedUpdateLater);
        /// <inheritdoc cref="Register(Action, CancellationToken, SlotReusingActionList)"/>
        public void RegisterFixedUpdateFinalize(Action act, CancellationToken ct)    /**/ => Register(act, ct, _fixedUpdateFinal);
        /// <inheritdoc cref="Register(Action, CancellationToken, SlotReusingActionList)"/>
        public void RegisterUpdateInitialize(Action act, CancellationToken ct)       /**/ => Register(act, ct, _updateStart);
        /// <inheritdoc cref="Register(Action, CancellationToken, SlotReusingActionList)"/>
        public void RegisterUpdateEarly(Action act, CancellationToken ct)            /**/ => Register(act, ct, _updateEarly);
        /// <inheritdoc cref="Register(Action, CancellationToken, SlotReusingActionList)"/>
        public void RegisterUpdate(Action act, CancellationToken ct)                 /**/ => Register(act, ct, _updateUsual);
        /// <inheritdoc cref="Register(Action, CancellationToken, SlotReusingActionList)"/>
        public void RegisterUpdateLate(Action act, CancellationToken ct)             /**/ => Register(act, ct, _updateLater);
        /// <inheritdoc cref="Register(Action, CancellationToken, SlotReusingActionList)"/>
        public void RegisterUpdateFinalize(Action act, CancellationToken ct)         /**/ => Register(act, ct, _updateFinal);
        /// <inheritdoc cref="Register(Action, CancellationToken, SlotReusingActionList)"/>
        public void RegisterLateUpdateInitialize(Action act, CancellationToken ct)   /**/ => Register(act, ct, _lateUpdateStart);
        /// <inheritdoc cref="Register(Action, CancellationToken, SlotReusingActionList)"/>
        public void RegisterLateUpdateEarly(Action act, CancellationToken ct)        /**/ => Register(act, ct, _lateUpdateEarly);
        /// <inheritdoc cref="Register(Action, CancellationToken, SlotReusingActionList)"/>
        public void RegisterLateUpdate(Action act, CancellationToken ct)             /**/ => Register(act, ct, _lateUpdateUsual);
        /// <inheritdoc cref="Register(Action, CancellationToken, SlotReusingActionList)"/>
        public void RegisterLateUpdateLate(Action act, CancellationToken ct)         /**/ => Register(act, ct, _lateUpdateLater);
        /// <inheritdoc cref="Register(Action, CancellationToken, SlotReusingActionList)"/>
        public void RegisterLateUpdateFinalize(Action act, CancellationToken ct)     /**/ => Register(act, ct, _lateUpdateFinal);

    }


    /// <summary>
    /// [NOT Thread-Safe]
    /// </summary>
    /// <remarks>Item order will be changed when remove item from list.</remarks>
    public sealed class SlotReusingActionList
    {
        public const int INITIAL_CAPACITY = 1;
        /// <summary>Capacity expanded exponentially until reaches this value. (when 8 -> 1, 2, 4, 8, 16, 24, 32, 40)</summary>
        public const int MAX_CAPACITY_EXPANSION = 32;

        int _consumed = 0;
        Action?[]? _array;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _consumed = 0;
            _array = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void EnsureCapacity()
        {
            if (_array == null)
            {
                _array = new Action[INITIAL_CAPACITY];
                return;
            }
            else if (_array.Length > _consumed)
            {
                return;
            }

            Array.Resize(ref _array, _array.Length + Math.Min(_array.Length, MAX_CAPACITY_EXPANSION));
        }


        /// <summary>
        /// Do nothing when null action specified.
        /// </summary>
        /// <returns>
        /// Returns received action instance as-is. It is null when null is passed.
        /// <para>
        /// > [!NOTE]
        /// > `Add(instance.Method)` will create new Action instance call by call implicitly.
        /// > If try to remove action later, it requires to specify exactly same instance so need to keep returned instance.
        /// </para>
        /// </returns>
        public Action? Add(Action act)
        {
            if (act == null)
                return null;

            EnsureCapacity();

            _array![_consumed] = act;
            _consumed++;
            return act;
        }

        /// <summary>
        /// Do nothing when null action specified.
        /// <para>
        /// > [!WARNING]
        /// > Item order will be changed. See file header document for details.
        /// </para>
        /// </summary>
        public void Remove(Action act)
        {
            if (_array == null || act == null)
                return;

            for (int i = 0; i < _consumed; i++)
            {
                if (_array[i] == act)
                {
                    _array[i] = null;

                    var lastIndex = _consumed - 1;
                    if (i != lastIndex)
                    {
                        _array[i] = _array[lastIndex];
                        _array[lastIndex] = null;
                    }

                    _consumed--;
                    return;
                }
            }

            return;
        }


        /// <summary>Invoke registered actions.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke()
        {
            if (_array == null)
                return;

            for (int i = 0; i < _consumed; i++)
            {
                // not null, checked on add/remove
                _array[i]!.Invoke();
            }
        }

        /// <returns>Copy of internal array. Empty when internal array haven't yet allocated.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Action?[] GetActions() => _consumed == 0 ? Array.Empty<Action>() : _array.AsSpan(0, _consumed).ToArray();

    }


    /// <summary>
    /// Lifecycle per scene.
    /// Scene Lifecycle can be lifetime owner of other lifecycle but is restricted from being bound to token or other lifecycle.
    /// </summary>
    public sealed class SceneLifecycle : LifecycleBehaviour
    {
        // TODO: override OnDestroy here!!


        /*  static helpers  ================================================================ */

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        internal static void SceneLifecycleInitializer()
        {
            SceneManager.sceneUnloaded += (scene) =>
            {
#if UNITY_EDITOR
                Debug.Log(nameof(SceneLifecycle) + " [sceneUnloaded event] isLoaded: " + scene.isLoaded
                    + " / root objects: " + scene.GetRootGameObjects().Length + "\n> "
                    + string.Join("\n> ", scene.GetRootGameObjects().Select(x => x.ToString())) + "\n");
                Debug.LogWarning(nameof(SceneLifecycle) + ": " + DumpSceneLifecycleCacheInfo() + "\n");
#endif

                if (_sceneToLifecycle.ContainsKey(scene))
                {
                    var lifecycle = _sceneToLifecycle[scene];
                    if (lifecycle != null)
                    {
#if UNITY_EDITOR
                        if (UnityEditor.EditorApplication.isPlaying)
#endif
                            UnityEngine.Object.Destroy(lifecycle);
                    }

                    _sceneToLifecycle.Remove(scene);
                }

                // check
                foreach (var check in _sceneToLifecycle.Values.ToArray())
                {
                    if (check == null)
                    {
                        throw new Exception(nameof(SceneLifecycle) + " [null entry found] " + DumpSceneLifecycleCacheInfo());
                    }
                }
            };
        }


        readonly static Dictionary<Scene, SceneLifecycle> _sceneToLifecycle = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string DumpSceneLifecycleCacheInfo()
        {
            return "scene lifecycles: " + _sceneToLifecycle.Count + "\n> " + string.Join("\n> ", _sceneToLifecycle);
        }


        /// <remarks>
        /// > [!NOTE]
        /// > This method just remove instance from cache. You have responsibility on canceling token.
        /// </remarks>
        internal static void RemoveFromCache(SceneLifecycle lifecycle)
        {
            // dictionary has array inside, iterate through that first.
            if (!_sceneToLifecycle.ContainsValue(lifecycle))
                return;

            Scene found = default;
            foreach (var scene_lifecycle in _sceneToLifecycle)
            {
                if (scene_lifecycle.Value == lifecycle)
                {
                    found = scene_lifecycle.Key;
                    break;
                }
            }
            _sceneToLifecycle.Remove(found);
        }


        /// <summary>Get lifecycle of active scene.</summary>
        /// <returns>null when scene is invalid or unloaded.</returns>
        public static SceneLifecycle? Get() => Get(SceneManager.GetActiveScene());

        /// <summary>Get lifecycle of specified scene.</summary>
        /// <param name="scene">`gameObject.scene` or `SceneManager.Get...` or something.</param>
        /// <inheritdoc cref="Get()"/>
        public static SceneLifecycle? Get(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError(nameof(SceneLifecycle) + ": scene is invalid or unloaded: " + scene);
                return null;
            }

            if (_sceneToLifecycle.ContainsKey(scene))
                return _sceneToLifecycle[scene];

            var lifecycle = new GameObject(
                nameof(SceneLifecycle) + " for ID:" + scene.buildIndex + " Name:" + scene.name).AddComponent<SceneLifecycle>();
            lifecycle.IsSceneLifecycle = true;

            _sceneToLifecycle.Add(scene, lifecycle);
            return lifecycle;
        }

    }


    /// <summary>
    /// Lifetime extension methods. Use `DebuggerAction` to extend debugging functionality.
    /// </summary>
    public static class LifetimeExtensions
    {
        /// <summary>Invoked before object bound to token.</summary>
        public static Action<object, CancellationToken, CancellationTokenRegistration>? DebuggerAction
#if UNITY_EDITOR
            = (obj, token, ticket) => Tests.LifecycleBehaviourTests._ticketToTarget.Add(new(ticket), new(obj))
#endif
            ;


        /// <summary>To avoid allocation when register action to cancellation token.</summary>
        readonly public static Action<object> DisposerAction = obj =>
        {
            if (obj is UnityEngine.Object unityObj)  // Transform check is done already. don't check here
            {
#if UNITY_EDITOR
                if (UnityEditor.EditorApplication.isPlaying)
#endif
                    UnityEngine.Object.Destroy(unityObj);
            }
            else if (obj is IDisposable disposable)
            {
                disposable.Dispose();
            }
            else
            {
                Debug.LogError(nameof(LifetimeExtensions) + ": unsupported type: " + obj);
            }
        };


        /*  extension methods  ================================================================ */

        const string ERROR_TFORM = "UnityEngine.Transform is not supported to be disposed.";
        const string ERROR_SCENE = "SceneLifecycle is restricted from being bound.";
        const string WARN_COMP = "Are you sure? Did you consider bind GameObject instead? Binding UnityEngine.Component lifetime to other could make things complex!!";


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static IDisposable BindToToken(object obj, CancellationToken token)
        {
            if (obj == null)
                throw new NullReferenceException(nameof(obj));

            var ticket = token.Register(DisposerAction, obj);
            DebuggerAction?.Invoke(obj, token, ticket);
            return ticket;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ThrowIfInvalidOperationAndThenPrepareObject(UnityEngine.Object obj, Component? owner)
        {
            if (obj is Transform)
                throw new NotSupportedException(nameof(LifetimeExtensions) + ": " + ERROR_TFORM);

            var go = obj as GameObject;
            var comp = obj as Component;

            // check upcasted instance and components
            if (obj is SceneLifecycle
            || (obj is LifecycleBehaviour lifecycle && lifecycle.IsSceneLifecycle)
            || (go != null && go.TryGetComponent(out LifecycleBehaviour goLC) && goLC.IsSceneLifecycle)
            || (comp != null && comp.gameObject.TryGetComponent(out LifecycleBehaviour compLC) && compLC.IsSceneLifecycle)
            )
                throw new InvalidOperationException(nameof(LifetimeExtensions) + ": " + ERROR_SCENE);

            if (owner != null)
            {
                var ownerScene = owner.gameObject.scene;
                if (ownerScene.buildIndex != -1 || ownerScene.path != ownerScene.name || ownerScene.name != nameof(UnityEngine.Object.DontDestroyOnLoad))
                {
                    if ((go != null && go.scene != ownerScene)
                    || (comp != null && comp.gameObject.scene != ownerScene))
                    {
                        throw new InvalidOperationException(nameof(LifetimeExtensions)
                            + ": inter-scene binding is restricted. use cancellation token overload if you understand and accept side effects.");
                    }
                }
            }

            // NOTE: to promise order of destroy, need to mark GameObject as DontDestroyOnLoad
#if false == LIFECYCLE_DISABLE_STABLE_DESTROY_ORDER
            if (go != null)
            {
                UnityEngine.Object.DontDestroyOnLoad(go);
            }
#endif
        }


        //IDisposable
        /// <summary>Bind disposable lifetime to cancellation token.</summary>
        public static IDisposable DestroyWith(this IDisposable obj, CancellationToken token) => BindToToken(obj, token);
        /// <summary>Bind disposable lifetime to lifecycle owner.</summary>
        public static IDisposable DestroyWith(this IDisposable obj, LifecycleBehaviour owner) => BindToToken(obj, owner.destroyCancellationToken);
#if UNITY_2022_2_OR_NEWER
        /// <summary>Bind disposable lifetime to MonoBehaviour.</summary>
        public static IDisposable DestroyWith(this IDisposable obj, MonoBehaviour mono) => BindToToken(obj, mono.destroyCancellationToken);
#endif


        //UnityEngine.Object
        /// <summary>Bind unity object lifetime to cancellation token.</summary>
        public static IDisposable DestroyWith(this UnityEngine.Object obj, CancellationToken token)
        {
            ThrowIfInvalidOperationAndThenPrepareObject(obj, null);
            return BindToToken(obj, token);
        }

        /// <summary>Bind unity object lifetime to lifecycle owner.</summary>
        public static IDisposable DestroyWith(this UnityEngine.Object obj, LifecycleBehaviour owner)
        {
            ThrowIfInvalidOperationAndThenPrepareObject(obj, owner);

#if UNITY_EDITOR
            owner.LifetimeBoundObjectNames.Add(obj.ToString());
#endif
            return BindToToken(obj, owner.destroyCancellationToken);
        }

#if UNITY_2022_2_OR_NEWER
        /// <summary>Bind unity object lifetime to MonoBehaviour.</summary>
        public static IDisposable DestroyWith(this UnityEngine.Object obj, MonoBehaviour mono)
        {
            ThrowIfInvalidOperationAndThenPrepareObject(obj, mono);
            return BindToToken(obj, mono.destroyCancellationToken);
        }
#endif


        //SceneLifecycle
        [Obsolete(ERROR_SCENE, true)] public static void DestroyWith(this SceneLifecycle _, MonoBehaviour m) => throw new NotSupportedException(ERROR_SCENE);
        [Obsolete(ERROR_SCENE, true)] public static void DestroyWith(this SceneLifecycle _, CancellationToken c) => throw new NotSupportedException(ERROR_SCENE);
        [Obsolete(ERROR_SCENE, true)] public static void DestroyWith(this SceneLifecycle _, LifecycleBehaviour l) => throw new NotSupportedException(ERROR_SCENE);

        //Transform
        [Obsolete(ERROR_TFORM, true)] public static void DestroyWith(this Transform _, MonoBehaviour m) => throw new NotSupportedException(ERROR_TFORM);
        [Obsolete(ERROR_TFORM, true)] public static void DestroyWith(this Transform _, CancellationToken c) => throw new NotSupportedException(ERROR_TFORM);
        [Obsolete(ERROR_TFORM, true)] public static void DestroyWith(this Transform _, LifecycleBehaviour l) => throw new NotSupportedException(ERROR_TFORM);


        // component: redirect to UnityEngine.Object overload
        const bool DISALLOW_COMP_BINDING =
#if LIFECYCLE_DISALLOW_COMPONENT_BINDING
            true
#else
            false
#endif
            ;

        [Obsolete(WARN_COMP, DISALLOW_COMP_BINDING)]
        public static IDisposable DestroyWith(this Component obj, CancellationToken token) => DestroyWith((UnityEngine.Object)obj, token);

        [Obsolete(WARN_COMP, DISALLOW_COMP_BINDING)]
        public static IDisposable DestroyWith(this Component obj, LifecycleBehaviour owner) => DestroyWith((UnityEngine.Object)obj, owner);

#if UNITY_2022_2_OR_NEWER
        [Obsolete(WARN_COMP, DISALLOW_COMP_BINDING)]
        public static IDisposable DestroyWith(this Component obj, MonoBehaviour mono) => DestroyWith((UnityEngine.Object)obj, mono);
#endif

    }


    /*  tests  ================================================================ */

#if UNITY_EDITOR

    namespace Tests
    {
        internal static class LifecycleBehaviourTests
        {
            internal class TestDisposable : IDisposable
            {
                string? name;
                public TestDisposable(string name) => this.name = name;

                public void Dispose()
                {
                    Debug.Log(name + " is disposed.");
                    name = null;  // to raise error
                }

                public void PrintName() => Debug.Log(name.ToString());
            }

            internal class DestroyTimingChecker : MonoBehaviour
            {
                void OnDestroy() => Debug.Log(name + " is disposed.");
            }


            internal const string MENU_ROOT = "Tests/" + nameof(LifecycleManager) + "/";

            [UnityEditor.MenuItem(MENU_ROOT + nameof(Create_Test_Objects))]
            internal static void Create_Test_Objects()
            {
                if (!UnityEditor.EditorApplication.isPlaying)
                {
                    Debug.LogError(nameof(LifecycleBehaviourTests) + ": works only in Play mode.");
                    return;
                }

                _ = new GameObject(new string('-', 16));
                var rootOwner = SceneLifecycle.Get();
                if (rootOwner == null)
                    return;
                _ = new GameObject(new string('-', 16));

                // create component first to check destruction order
                List<GameObject> GOs = new();
                List<TestDisposable> PUREs = new();
                List<DestroyTimingChecker> COMPs = new();
                for (int i = 0; i < 30; i++)
                {
                    var alive = new GameObject("   > Component bound, GameObject keeps alive No." + i);
                    COMPs.Add(alive.AddComponent<DestroyTimingChecker>());

                    var go = new GameObject("   > GameObject No." + i);
                    GOs.Add(go);
                    PUREs.Add(new TestDisposable("   > Pure C# Class No." + i));
                }

                var childOwner = rootOwner as LifecycleBehaviour;
                for (int i = 0; i < 30; i++)
                {
                    if ((i % 10) == 0)
                    {
                        childOwner = LifecycleBehaviour.Create("> Child Lifetime Scope", false);
                        childOwner.gameObject.DestroyWith(rootOwner);
                    }

                    PUREs[i].DestroyWith(childOwner);
                    COMPs[i].DestroyWith(childOwner);
                    GOs[i].DestroyWith(childOwner);
                }

                // check Obsolete attribute
                //rootOwner.DestroyWith(rootOwner);

                UnityEditor.Selection.activeGameObject = rootOwner.gameObject;
                Report_Remaining_References();
            }


            [UnityEditor.MenuItem(MENU_ROOT + "Debug Check List", priority = int.MaxValue / 2)]

            [UnityEditor.MenuItem(MENU_ROOT + ">   Unregister by Token", priority = int.MaxValue / 2 + 1)]  //+1
            internal static void Unregister_by_Token()
            {
                var instance = new TestDisposable(nameof(Unregister_by_Token));
                var lc = LifecycleBehaviour.Create("Lifecycle", true);

                var cts = new CancellationTokenSource(3000);
                Debug.Log("token will be canceled after 3 seconds.");
                cts.Token.Register(() => Debug.Log("token canceled. remaining actions: " + lc.GetUpdate().Length));

                instance.DestroyWith(cts.Token);
                lc.RegisterUpdate(() => instance.PrintName(), cts.Token);
            }

            [UnityEditor.MenuItem(MENU_ROOT + ">   Don't Unregister by Token (raise error)", priority = int.MaxValue / 2 + 2)]  //+2
            internal static void Dont_Unregister_by_Token()
            {
                var instance = new TestDisposable(nameof(Dont_Unregister_by_Token));
                var lc = LifecycleBehaviour.Create("Lifecycle", true);

                var cts = new CancellationTokenSource(3000);
                Debug.Log("token will be canceled after 3 seconds.");
                cts.Token.Register(() => Debug.Log("token canceled. remaining actions: " + lc.GetUpdate().Length));

                instance.DestroyWith(cts.Token);
                lc.RegisterUpdate(() => instance.PrintName());
            }


            [UnityEditor.MenuItem(MENU_ROOT + ">   Inter-Scene Binding must throw \t by hand", priority = int.MaxValue / 2 + 5)]
            [UnityEditor.MenuItem(MENU_ROOT + ">   Scene Lifecycle Binding must throw \t by hand", priority = int.MaxValue / 2 + 5)]
            [UnityEditor.MenuItem(MENU_ROOT + ">   Transform Binding must throw \t by hand", priority = int.MaxValue / 2 + 5)]
            [UnityEditor.MenuItem(MENU_ROOT + "Select Transform of active GameObject", priority = int.MaxValue / 2 + 10)] //+10
            internal static void Debug_Check_List()
            {
                var go = UnityEditor.Selection.activeGameObject;
                if (go != null)
                {
                    UnityEditor.Selection.objects = new UnityEngine.Object[] { go.transform };
                    Debug.Log("Selection updated: " + string.Join(", ", UnityEditor.Selection.objects.AsEnumerable()));
                }
            }


            internal readonly static Dictionary<WeakReference, WeakReference> _ticketToTarget = new();

            [UnityEditor.MenuItem(MENU_ROOT + nameof(Report_Remaining_References), priority = int.MaxValue)]
            internal static void Report_Remaining_References()
            {
                GC.Collect();

                foreach (var key in _ticketToTarget.Keys.ToArray())
                {
                    if (!key.IsAlive && (!_ticketToTarget[key].IsAlive || (_ticketToTarget[key].Target as UnityEngine.Object) == null))
                    {
                        _ticketToTarget.Remove(key);
                    }
                }

                Debug.LogWarning(nameof(LifecycleBehaviourTests) + ": # of references remaining: " + _ticketToTarget.Count + "\n> "
                    + string.Join("\n> ", _ticketToTarget.Select(x => $"{x.Key.Target ?? "<NULL>"} -> {x.Value.Target ?? "<NULL>"}")) + "\n");
            }


            const int MENU_OPERATE_PRIORITY = 1050;

            [UnityEditor.MenuItem(MENU_ROOT + nameof(Bind_Lifetime_to_Last_Selection), priority = MENU_OPERATE_PRIORITY)]
            internal static void Bind_Lifetime_to_Last_Selection()
            {
                var sel = UnityEditor.Selection.objects;
                if (sel.Length < 2)
                    return;


                var last = sel.Length - 1;
                if (sel[last] is not GameObject go)
                    throw new Exception("last selection is not GameObject");

                if (!go.TryGetComponent(out LifecycleBehaviour lifecycle))
                    throw new Exception("last selection doesn't have " + nameof(LifecycleBehaviour));

                for (int i = 0; i < last; i++)
                {
                    sel[i].DestroyWith(lifecycle);
                    Debug.Log($"Bound: {sel[i]} -> {lifecycle}");
                }
            }

            [UnityEditor.MenuItem(MENU_ROOT + nameof(Get_Scene_Lifecycle), priority = MENU_OPERATE_PRIORITY)]
            internal static void Get_Scene_Lifecycle()
            {
                _ = SceneLifecycle.Get();
            }

            static int _currentLifecycleID = 1;
            [UnityEditor.MenuItem(MENU_ROOT + nameof(Create_New_Lifecycle), priority = MENU_OPERATE_PRIORITY)]
            internal static void Create_New_Lifecycle()
            {
                LifecycleBehaviour.Create(
                    nameof(LifecycleBehaviour) + " #" + _currentLifecycleID++,
                    UnityEditor.EditorUtility.DisplayDialog(nameof(Create_New_Lifecycle), "Don't Destroy on Load?", "Yes", "No")
                    );
            }


            const int MENU_SCENE_PRIORITY = 1100;

            static int _currentSceneID = -310;
            [UnityEditor.MenuItem(MENU_ROOT + nameof(Create_Blank_Scene), priority = MENU_SCENE_PRIORITY)]
            internal static void Create_Blank_Scene()
            {
                var scene = SceneManager.CreateScene("BLANK" + _currentSceneID--);
                SceneManager.SetActiveScene(scene);
            }

            [UnityEditor.MenuItem(MENU_ROOT + nameof(Create_Blank_Scene_and_Unload_Current), priority = MENU_SCENE_PRIORITY)]
            internal static void Create_Blank_Scene_and_Unload_Current()
            {
                var current = SceneManager.GetActiveScene();
                Create_Blank_Scene();
                SceneManager.UnloadSceneAsync(current);
            }

            [UnityEditor.MenuItem(MENU_ROOT + nameof(Unload_Active_Scene), priority = MENU_SCENE_PRIORITY)]
            internal static void Unload_Active_Scene()
            {
                SceneManager.UnloadSceneAsync(SceneManager.GetActiveScene());
            }


            [UnityEditor.MenuItem(MENU_ROOT + nameof(Bind_Lifetime_to_Last_Selection), true)]
            [UnityEditor.MenuItem(MENU_ROOT + nameof(Create_New_Lifecycle), true)]
            [UnityEditor.MenuItem(MENU_ROOT + nameof(Get_Scene_Lifecycle), true)]
            [UnityEditor.MenuItem(MENU_ROOT + nameof(Create_Blank_Scene_and_Unload_Current), true)]
            [UnityEditor.MenuItem(MENU_ROOT + nameof(Create_Blank_Scene), true)]
            [UnityEditor.MenuItem(MENU_ROOT + nameof(Unload_Active_Scene), true)]
            [UnityEditor.MenuItem(MENU_ROOT + nameof(Create_UpdateManagerOrganizer), true)]
            internal static bool MenuValidate_IsPlaying() => UnityEditor.EditorApplication.isPlaying;


            [UnityEditor.MenuItem(MENU_ROOT + nameof(Create_UpdateManagerOrganizer))]
            internal static void Create_UpdateManagerOrganizer()
            {
                var go = new GameObject(nameof(UpdateManagerOrganizer));
                go.SetActive(false);

                var lc1 = LifecycleBehaviour.Create("Lifecycle 1", false);
                var lc2 = LifecycleBehaviour.Create("My Lifecycle B", false);
                var lc3 = LifecycleBehaviour.Create("LC 3", false);

                lc1.RegisterUpdateEarly(() => Debug.Log(lc1.name + ": " + nameof(lc1.RegisterUpdateEarly)));
                lc2.RegisterUpdateEarly(() => Debug.Log(lc2.name + ": " + nameof(lc2.RegisterUpdateEarly)));
                lc3.RegisterUpdateEarly(() => Debug.Log(lc3.name + ": " + nameof(lc3.RegisterUpdateEarly)));

                lc1.RegisterFixedUpdateLate(() => Debug.Log(lc1.name + ": " + nameof(lc1.RegisterFixedUpdateLate)));
                lc2.RegisterFixedUpdateLate(() => Debug.Log(lc2.name + ": " + nameof(lc2.RegisterFixedUpdateLate)));
                lc3.RegisterFixedUpdateLate(() => Debug.Log(lc3.name + ": " + nameof(lc3.RegisterFixedUpdateLate)));

                lc1.RegisterLateUpdate(() => Debug.Log(lc1.name + ": " + nameof(lc1.RegisterLateUpdate)));
                lc2.RegisterLateUpdate(() => Debug.Log(lc2.name + ": " + nameof(lc2.RegisterLateUpdate)));
                lc3.RegisterLateUpdate(() => Debug.Log(lc3.name + ": " + nameof(lc3.RegisterLateUpdate)));

                var manager = go.AddComponent<UpdateManagerOrganizer>();
                manager.lifecycle1 = lc1;
                manager.lifecycle2 = lc2;
                manager.lifecycle3 = lc3;
                go.SetActive(true);
            }

            internal class UpdateManagerOrganizer : MonoBehaviour
            {
                public LifecycleBehaviour lifecycle1;
                public LifecycleBehaviour lifecycle2;
                public LifecycleBehaviour lifecycle3;

                void OnEnable()
                {
                    lifecycle1.enabled = false;
                    lifecycle2.enabled = false;
                    lifecycle3.enabled = false;
                }
                void Update()
                {
                    lifecycle3.Update();
                    lifecycle1.Update();
                    lifecycle2.Update();
                }
                void LateUpdate()
                {
                    lifecycle1.LateUpdate();
                    lifecycle3.LateUpdate();
                    lifecycle2.LateUpdate();
                }
                void FixedUpdate()
                {
                    lifecycle2.FixedUpdate();
                    lifecycle3.FixedUpdate();
                    lifecycle1.FixedUpdate();
                }
            }

        }
    }

#endif

}

#nullable restore
