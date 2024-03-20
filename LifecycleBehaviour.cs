/** Simple Lifecycle Manager for Unity
 ** (c) 2024 github.com/sator-imaging
 ** Licensed under the MIT License

This library helps managing MonoBehaviour lifetime by CancellationToken introduced in Unity 2022.
Not only that but also has an ability to manage "Update" actions ordered and efficiently.

And also! library provides missing `destroyCancellationToken` feature for Unity 2021 LTS!!

**Feature Highlights**
- [Class Instance Lifetime Management](#object-lifetime-management)
- [Update Function Manager](#update-function-manager)
    - Designed to address consideration written in the following article
    - https://blog.unity.com/engine-platform/10000-update-calls
- [API Reference](https://sator-imaging.github.io/Unity-LifecycleManager)

**Installation**
- In Unity 2021 or later, Enter the following URL in Unity Package Manager (UPM)
    - https://github.com/sator-imaging/Unity-LifecycleManager.git


Object Lifetime Management
==========================
Here is example to bind instance lifetime to cancellation token or MonoBehaviour.

```csharp
using SatorImaging.LifecycleManager;

// works on Unity 2021 or later
disposable.DestroyWith(monoBehaviourOrCancellationToken);
gameObject.DestroyWith(cancellationToken);
unityObj.DestroyUnityObjectWith(tokenOrBehaviour);

// bind to unity scene lifetime
var sceneLifetime = SceneLifetime.Get(gameObject.scene);
disposable.DestroyWith(sceneLifetime);
sceneLifetime.Token.Register(() => DoSomethingOnSceneUnloading());

// lifecycle and its GameObject which will be destroyed on scene unloading
var sceneLC = SceneLifecycle.Get();

// nesting lifecycles
var root = LifecycleBehaviour.Create("Root Lifecycle");
var child = LifecycleBehaviour.Create("Child Lifecycle");
var grand = LifecycleBehaviour.Create("Grandchild Lifecycle");
child.DestroyWith(root);
grand.DestroyWith(child);
    // --> child and grand will be marked as DontDestroyOnLoad automatically

// action for debugging purpose which will be invoked before binding (when not null)
LifetimeExtensions.DebuggerAction = (obj, token, ticket, ownerOrNull) =>
{
    Debug.Log($"Target Object: {obj}");
    if (ownerOrNull != null)
        Debug.Log($"Lifetime Owner: {ownerOrNull}");
    Debug.Log($"CancellationToken: {token}");
    Debug.Log($"CancellationTokenRegistration: {ticket}");
};
```


Technical Note
--------------

### Unity Object/Component Binding Notice

You can bind `UnityEngine.Object` or component lifetime to cancellation token or MonoBehaviour by using
`DestroyUnityObjectWith` extension method instead of `DestroyWith`.

Note that when binding unity object lifetime to other, need to consider both situation that component is
destroyed by scene unloading OR by lifetime owner. As a result, it makes thing really complex and scene
will be spaghetti-ed.

Strongly recommended that binding GameObject lifetime instead of component, or implement `IDisposable`
on your unity engine object explicitly to preciously control behaviour.


### Inter-Scene Binding Notice

Lifetime binding across scenes is restricted. Nevertheless you want to bind lifetime to another
scene object, use `DestroyWith(CancellationToken)` method with `monoBehaviour.destroyCancellationToken`.

> [!WARNING]
> When Unity object bound to another scene object, it will be destroyed by both when lifetime owner is
> destroyed and scene which containing bound object is unloaded.


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
> To make destruction order stable, extension method automatically mark lifetime bound GameObjects as
> `DontDestroyOnLoad`.



Update Function Manager
=======================
In this feature, each "update" function has 5 stages, Initialize, Early, Normal, Late and Finalize.
Initialize and Finalize is designed for system usage, other 3 stages are for casual usage.

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

> [!NOTE]
> For performance optimization, removing registered action will swap items in list instead of reordering
> whole items in list.
> ie. Order of update stages (early, late, etc) are promised but registered action order is NOT promised.
> (like Unity)


Automatic Unregistration
------------------------
If action is depending on instance that will be destroyed with cancellation token, You have to specify
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
    [DisallowMultipleComponent]  // NOTE: don't remove DisallowMultipleComponent attribute!!
                                 //       LifetimeOwnerCount logic is searching only 1 lifecycle on GameObject
    public class LifecycleBehaviour : MonoBehaviour
    {
        const string LOG_PREFIX = "[" + nameof(LifecycleBehaviour) + "] ";

        // TODO: it's hard and complicated to increment owner count.
        //       when lifecycle's game object is nested in other hierarchy which is bound to other lifecycle,
        //       count must be increment but when parenting object is performed after bound to lifecycle,
        //       count will be incorrect. need to monitor any hierachy change to fix lifetime owner count...
        //[field: SerializeField]
        //public int LifetimeOwnerCount { get; internal set; } = 0;

#if false == UNITY_2022_2_OR_NEWER
        // polyfill - allocate only when requested
        private CancellationTokenSource? polyfill_destroyToken;
        public CancellationToken destroyCancellationToken => (polyfill_destroyToken ??= new()).Token;
#endif

        public void OnDestroy()
        {
#if false == UNITY_2022_2_OR_NEWER
            polyfill_destroyToken?.Cancel();
#endif

#if UNITY_EDITOR
            Debug.Log(LOG_PREFIX + "going to be destroyed: " + this);
#endif
            _fixedUpdateStart?.Clear();
            _fixedUpdateEarly?.Clear();
            _fixedUpdateUsual?.Clear();
            _fixedUpdateLater?.Clear();
            _fixedUpdateFinal?.Clear();
            _updateStart?.Clear();
            _updateEarly?.Clear();
            _updateUsual?.Clear();
            _updateLater?.Clear();
            _updateFinal?.Clear();
            _lateUpdateStart?.Clear();
            _lateUpdateEarly?.Clear();
            _lateUpdateUsual?.Clear();
            _lateUpdateLater?.Clear();
            _lateUpdateFinal?.Clear();
        }


        // helper
        public static LifecycleBehaviour Create(string nameOfGameObject, bool dontDestroyOnLoad)
        {
            var go = new GameObject(nameOfGameObject);
            if (dontDestroyOnLoad)
                DontDestroyOnLoad(go);

            return go.AddComponent<LifecycleBehaviour>();
        }


        /*  the update manager  ================================================================ */

        // NOTE: 5-letter words were chosen for source code alignment!!
        //       stage order: Initialize -> Early -> Usual -> Late -> Finalize
        protected UnorderedActionList? _fixedUpdateStart;
        protected UnorderedActionList? _fixedUpdateEarly;
        protected UnorderedActionList? _fixedUpdateUsual;
        protected UnorderedActionList? _fixedUpdateLater;
        protected UnorderedActionList? _fixedUpdateFinal;
        protected UnorderedActionList? _updateStart;
        protected UnorderedActionList? _updateEarly;
        protected UnorderedActionList? _updateUsual;
        protected UnorderedActionList? _updateLater;
        protected UnorderedActionList? _updateFinal;
        protected UnorderedActionList? _lateUpdateStart;
        protected UnorderedActionList? _lateUpdateEarly;
        protected UnorderedActionList? _lateUpdateUsual;
        protected UnorderedActionList? _lateUpdateLater;
        protected UnorderedActionList? _lateUpdateFinal;

        /// <summary>
        /// > [!TIP]
        /// > Set `enabled` of this mono behaviour false and call this method explicitly in "manager of update managers" to
        /// > manage multiple update managers execution order while keeping registered actions order.
        /// </summary>
        public void Update()
        {
            _updateStart?.Invoke();
            _updateEarly?.Invoke();
            _updateUsual?.Invoke();
            _updateLater?.Invoke();
            _updateFinal?.Invoke();
        }

        /// <inheritdoc cref="Update"/>
        public void LateUpdate()
        {
            _lateUpdateStart?.Invoke();
            _lateUpdateEarly?.Invoke();
            _lateUpdateUsual?.Invoke();
            _lateUpdateLater?.Invoke();
            _lateUpdateFinal?.Invoke();
        }

        /// <inheritdoc cref="Update"/>
        public void FixedUpdate()
        {
            _fixedUpdateStart?.Invoke();
            _fixedUpdateEarly?.Invoke();
            _fixedUpdateUsual?.Invoke();
            _fixedUpdateLater?.Invoke();
            _fixedUpdateFinal?.Invoke();
        }


        // NOTE: /**/ is for blocking auto formatter

        /// <inheritdoc cref="UnorderedActionList.Remove(Action)"/>
        public void RemoveFixedUpdateInitialize(Action act) /**/ => _fixedUpdateStart?.Remove(act);
        /// <inheritdoc cref="UnorderedActionList.Remove(Action)"/>
        public void RemoveFixedUpdateEarly(Action act)      /**/ => _fixedUpdateEarly?.Remove(act);
        /// <inheritdoc cref="UnorderedActionList.Remove(Action)"/>
        public void RemoveFixedUpdate(Action act)           /**/ => _fixedUpdateUsual?.Remove(act);
        /// <inheritdoc cref="UnorderedActionList.Remove(Action)"/>
        public void RemoveFixedUpdateLate(Action act)       /**/ => _fixedUpdateLater?.Remove(act);
        /// <inheritdoc cref="UnorderedActionList.Remove(Action)"/>
        public void RemoveFixedUpdateFinalize(Action act)   /**/ => _fixedUpdateFinal?.Remove(act);
        /// <inheritdoc cref="UnorderedActionList.Remove(Action)"/>
        public void RemoveUpdateInitialize(Action act)      /**/ => _updateStart?.Remove(act);
        /// <inheritdoc cref="UnorderedActionList.Remove(Action)"/>
        public void RemoveUpdateEarly(Action act)           /**/ => _updateEarly?.Remove(act);
        /// <inheritdoc cref="UnorderedActionList.Remove(Action)"/>
        public void RemoveUpdate(Action act)                /**/ => _updateUsual?.Remove(act);
        /// <inheritdoc cref="UnorderedActionList.Remove(Action)"/>
        public void RemoveUpdateLate(Action act)            /**/ => _updateLater?.Remove(act);
        /// <inheritdoc cref="UnorderedActionList.Remove(Action)"/>
        public void RemoveUpdateFinalize(Action act)        /**/ => _updateFinal?.Remove(act);
        /// <inheritdoc cref="UnorderedActionList.Remove(Action)"/>
        public void RemoveLateUpdateInitialize(Action act)  /**/ => _lateUpdateStart?.Remove(act);
        /// <inheritdoc cref="UnorderedActionList.Remove(Action)"/>
        public void RemoveLateUpdateEarly(Action act)       /**/ => _lateUpdateEarly?.Remove(act);
        /// <inheritdoc cref="UnorderedActionList.Remove(Action)"/>
        public void RemoveLateUpdate(Action act)            /**/ => _lateUpdateUsual?.Remove(act);
        /// <inheritdoc cref="UnorderedActionList.Remove(Action)"/>
        public void RemoveLateUpdateLate(Action act)        /**/ => _lateUpdateLater?.Remove(act);
        /// <inheritdoc cref="UnorderedActionList.Remove(Action)"/>
        public void RemoveLateUpdateFinalize(Action act)    /**/ => _lateUpdateFinal?.Remove(act);

        /// <inheritdoc cref="UnorderedActionList.GetActions"/>
        public Action[] GetFixedUpdateInitialize()  /**/ => _fixedUpdateStart?.GetActions() ?? Array.Empty<Action>();
        /// <inheritdoc cref="UnorderedActionList.GetActions"/>
        public Action[] GetFixedUpdateEarly()       /**/ => _fixedUpdateEarly?.GetActions() ?? Array.Empty<Action>();
        /// <inheritdoc cref="UnorderedActionList.GetActions"/>
        public Action[] GetFixedUpdate()            /**/ => _fixedUpdateUsual?.GetActions() ?? Array.Empty<Action>();
        /// <inheritdoc cref="UnorderedActionList.GetActions"/>
        public Action[] GetFixedUpdateLate()        /**/ => _fixedUpdateLater?.GetActions() ?? Array.Empty<Action>();
        /// <inheritdoc cref="UnorderedActionList.GetActions"/>
        public Action[] GetFixedUpdateFinalize()    /**/ => _fixedUpdateFinal?.GetActions() ?? Array.Empty<Action>();
        /// <inheritdoc cref="UnorderedActionList.GetActions"/>
        public Action[] GetUpdateInitialize()       /**/ => _updateStart?.GetActions() ?? Array.Empty<Action>();
        /// <inheritdoc cref="UnorderedActionList.GetActions"/>
        public Action[] GetUpdateEarly()            /**/ => _updateEarly?.GetActions() ?? Array.Empty<Action>();
        /// <inheritdoc cref="UnorderedActionList.GetActions"/>
        public Action[] GetUpdate()                 /**/ => _updateUsual?.GetActions() ?? Array.Empty<Action>();
        /// <inheritdoc cref="UnorderedActionList.GetActions"/>
        public Action[] GetUpdateLate()             /**/ => _updateLater?.GetActions() ?? Array.Empty<Action>();
        /// <inheritdoc cref="UnorderedActionList.GetActions"/>
        public Action[] GetUpdateFinalize()         /**/ => _updateFinal?.GetActions() ?? Array.Empty<Action>();
        /// <inheritdoc cref="UnorderedActionList.GetActions"/>
        public Action[] GetLateUpdateInitialize()   /**/ => _lateUpdateStart?.GetActions() ?? Array.Empty<Action>();
        /// <inheritdoc cref="UnorderedActionList.GetActions"/>
        public Action[] GetLateUpdateEarly()        /**/ => _lateUpdateEarly?.GetActions() ?? Array.Empty<Action>();
        /// <inheritdoc cref="UnorderedActionList.GetActions"/>
        public Action[] GetLateUpdate()             /**/ => _lateUpdateUsual?.GetActions() ?? Array.Empty<Action>();
        /// <inheritdoc cref="UnorderedActionList.GetActions"/>
        public Action[] GetLateUpdateLate()         /**/ => _lateUpdateLater?.GetActions() ?? Array.Empty<Action>();
        /// <inheritdoc cref="UnorderedActionList.GetActions"/>
        public Action[] GetLateUpdateFinalize()     /**/ => _lateUpdateFinal?.GetActions() ?? Array.Empty<Action>();


        /*  unregister by cancellation token  ================================================================ */

        readonly protected static Action<object> UnregisterByCancellationToken = obj =>
        {
            if (obj is Ticket ticket)
            {
                ticket.Dispose();
            }
        };

        // don't define struct. it will be boxed into object anyway
        protected sealed class Ticket //: IDisposable
        {
            readonly Action action;
            readonly UnorderedActionList list;
            public Ticket(Action action, UnorderedActionList list)
            {
                this.action = action;
                this.list = list;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public /*readonly*/ void Dispose()
            {
                list.Remove(action);
            }
        }


        /// <inheritdoc cref="UnorderedActionList.Add(Action)"/>
        /// <summary>
        /// If action is depending on instance that will be destroyed with cancellation token, You have to specify
        /// same token to unregister action together. If not, action will cause error due to depending instance is gone.
        /// </summary>
        /// <param name="ct">
        /// Token to unregister specified action when canceled.
        /// If you don't require action to be unregistered, use `CancellationToken.None` or leave unspecified.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected Action? Register(Action act, CancellationToken ct, UnorderedActionList list)
        {
            if (act == null)
                return null;

            list.Add(act);

            // NOTE: CanBeCanceled is not set on both `default(CancellationToken)` and `CancellationToken.None`
            //       ie. won't be unregistered
            if (ct.CanBeCanceled)
            {
                // canceled CancellationTokenSource.Token has both CanBeCanceled and IsCancellationRequested are set.
                // this callback will run immediately when canceled CTS.Token is specified.
                ct.Register(UnregisterByCancellationToken, new Ticket(act, list));
            }

            return act;
        }


        // to avoid use of ref method parameter, list must be allocated on call even when action is null
        // okay no internal array is allocated when list instantiated
        /// <inheritdoc cref="Register(Action, CancellationToken, UnorderedActionList)"/>
        public Action? RegisterFixedUpdateInitialize(Action act, CancellationToken ct = default)  /**/ => Register(act, ct, _fixedUpdateStart ??= new());
        /// <inheritdoc cref="Register(Action, CancellationToken, UnorderedActionList)"/>
        public Action? RegisterFixedUpdateEarly(Action act, CancellationToken ct = default)       /**/ => Register(act, ct, _fixedUpdateEarly ??= new());
        /// <inheritdoc cref="Register(Action, CancellationToken, UnorderedActionList)"/>
        public Action? RegisterFixedUpdate(Action act, CancellationToken ct = default)            /**/ => Register(act, ct, _fixedUpdateUsual ??= new());
        /// <inheritdoc cref="Register(Action, CancellationToken, UnorderedActionList)"/>
        public Action? RegisterFixedUpdateLate(Action act, CancellationToken ct = default)        /**/ => Register(act, ct, _fixedUpdateLater ??= new());
        /// <inheritdoc cref="Register(Action, CancellationToken, UnorderedActionList)"/>
        public Action? RegisterFixedUpdateFinalize(Action act, CancellationToken ct = default)    /**/ => Register(act, ct, _fixedUpdateFinal ??= new());
        /// <inheritdoc cref="Register(Action, CancellationToken, UnorderedActionList)"/>
        public Action? RegisterUpdateInitialize(Action act, CancellationToken ct = default)       /**/ => Register(act, ct, _updateStart ??= new());
        /// <inheritdoc cref="Register(Action, CancellationToken, UnorderedActionList)"/>
        public Action? RegisterUpdateEarly(Action act, CancellationToken ct = default)            /**/ => Register(act, ct, _updateEarly ??= new());
        /// <inheritdoc cref="Register(Action, CancellationToken, UnorderedActionList)"/>
        public Action? RegisterUpdate(Action act, CancellationToken ct = default)                 /**/ => Register(act, ct, _updateUsual ??= new());
        /// <inheritdoc cref="Register(Action, CancellationToken, UnorderedActionList)"/>
        public Action? RegisterUpdateLate(Action act, CancellationToken ct = default)             /**/ => Register(act, ct, _updateLater ??= new());
        /// <inheritdoc cref="Register(Action, CancellationToken, UnorderedActionList)"/>
        public Action? RegisterUpdateFinalize(Action act, CancellationToken ct = default)         /**/ => Register(act, ct, _updateFinal ??= new());
        /// <inheritdoc cref="Register(Action, CancellationToken, UnorderedActionList)"/>
        public Action? RegisterLateUpdateInitialize(Action act, CancellationToken ct = default)   /**/ => Register(act, ct, _lateUpdateStart ??= new());
        /// <inheritdoc cref="Register(Action, CancellationToken, UnorderedActionList)"/>
        public Action? RegisterLateUpdateEarly(Action act, CancellationToken ct = default)        /**/ => Register(act, ct, _lateUpdateEarly ??= new());
        /// <inheritdoc cref="Register(Action, CancellationToken, UnorderedActionList)"/>
        public Action? RegisterLateUpdate(Action act, CancellationToken ct = default)             /**/ => Register(act, ct, _lateUpdateUsual ??= new());
        /// <inheritdoc cref="Register(Action, CancellationToken, UnorderedActionList)"/>
        public Action? RegisterLateUpdateLate(Action act, CancellationToken ct = default)         /**/ => Register(act, ct, _lateUpdateLater ??= new());
        /// <inheritdoc cref="Register(Action, CancellationToken, UnorderedActionList)"/>
        public Action? RegisterLateUpdateFinalize(Action act, CancellationToken ct = default)     /**/ => Register(act, ct, _lateUpdateFinal ??= new());


        // NOTE: MUST keep this class non-public because null checking must be done by caller to avoid double-check.
        //       ie. action list could have null entry if consumer doesn't care.
        /// <remarks>
        /// [NOT Thread-Safe]
        /// Item order will be changed when remove item from list.
        /// </remarks>
        protected sealed class UnorderedActionList
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


            /// <remarks>
            /// Do nothing when null action specified.
            /// </remarks>
            /// <returns>
            /// Returns received action instance as-is. It is null when null is passed.
            /// <para>
            /// > [!NOTE]
            /// > `Add(instance.Method)` will create new Action instance call by call implicitly.
            /// > If try to remove action later, it requires to specify exactly same instance so need to keep returned instance.
            /// </para>
            /// </returns>
            public Action Add(Action act)
            {
                // NOTE: check is done in caller, don't check here.
                //if (act == null)
                //    return null;

                if (_array == null)
                {
                    _array = new Action[INITIAL_CAPACITY];
                }
                else if (_array.Length <= _consumed)
                {
                    Array.Resize(ref _array, _array.Length + Math.Min(_array.Length, MAX_CAPACITY_EXPANSION));
                }

                _array[_consumed] = act;
                _consumed++;
                return act;
            }

            /// <remarks>
            /// Do nothing when null action specified.
            /// <para>
            /// > [!WARNING]
            /// > Item order will be changed. See file header document for details.
            /// </para>
            /// </remarks>
            public void Remove(Action act)
            {
                if (act == null)
                    return;

                var span = GetSpanOrEmpty();
                for (int i = 0; i < span.Length; i++)
                {
                    if (span[i] == act)
                    {
                        span[i] = null;

                        var lastIndex = _consumed - 1;
                        if (i != lastIndex)
                        {
                            span[i] = span[lastIndex];
                            span[lastIndex] = null;
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
                var span = GetSpanOrEmpty();
                for (int i = 0; i < span.Length; i++)
                {
                    // not null, checked on add/remove
                    span[i]!.Invoke();
                }
            }


            /// <returns>Copy of internal array. Empty when internal array haven't yet allocated.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Action[] GetActions() => GetSpanOrEmpty().ToArray() as Action[];

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            Span<Action?> GetSpanOrEmpty() => _array == null ? Span<Action?>.Empty : _array.AsSpan(0, _consumed);

        }

    }


    /// <summary>
    /// Represents unity scene lifetime.
    /// </summary>
    public sealed class SceneLifetime : IEquatable<SceneLifetime>  // NOTE: don't implement IDisposable!
                                                                   //       it will allow scene lifetime to be bound to other!!
    {
        const string LOG_PREFIX = "[" + nameof(SceneLifetime) + "] ";

        readonly string _sceneInfo;
        internal SceneLifetime(Scene scene)
        {
            if (_sceneToLifetime.ContainsKey(scene))
            {
                throw new InvalidOperationException(
                    LOG_PREFIX + "scene lifetime has already been created. use `Get` method instead: " + scene);
            }

            if (!scene.IsValid())
                throw new NotSupportedException(LOG_PREFIX + "scene is invalid: " + scene);

            _sceneInfo = "BuildIndex:" + scene.buildIndex + " " + scene.name;
            _sceneToLifetime.Add(scene, this);
        }


        public override string ToString() => _sceneInfo;
        public bool Equals(SceneLifetime? other) => other is SceneLifetime sl && ReferenceEquals(this, sl);


        private CancellationTokenSource? _tokenSource;
        public CancellationToken Token => (_tokenSource ??= new()).Token;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CancelToken() => _tokenSource?.Cancel();


        /*  static helpers  ================================================================ */

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        internal static void SceneLifetimeInitializer()
        {
            SceneManager.sceneUnloaded += (scene) =>
            {
#if UNITY_EDITOR
                Debug.Log(LOG_PREFIX + "[sceneUnloaded event] isLoaded:" + scene.isLoaded
                    + " / root objects: " + scene.GetRootGameObjects().Length + "\n> "
                    + string.Join("\n> ", scene.GetRootGameObjects().Select(x => x.ToString())) + "\n");
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (_sceneToLifetime.Count > 0)
                        Debug.LogWarning(LOG_PREFIX + DumpDatabaseInfo() + "\n");
                    else
                        Debug.Log(LOG_PREFIX + "no scene lifetime in database");
                };
#endif

                if (_sceneToLifetime.ContainsKey(scene))
                {
                    var lifetime = _sceneToLifetime[scene];
                    lifetime.CancelToken();
                    _sceneToLifetime.Remove(scene);
                }

                // validate
                ThrowIfCacheIsInvalid();
            };
        }


        // call this method on each helper calls
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ThrowIfCacheIsInvalid()
        {
            foreach (var scene in _sceneToLifetime.Keys)
            {
                if (!scene.IsValid())
                {
                    throw new Exception(LOG_PREFIX + "database contains invalid scene: " + DumpDatabaseInfo());
                }
            }
        }


        readonly static Dictionary<Scene, SceneLifetime> _sceneToLifetime = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string DumpDatabaseInfo()
        {
            return "scene lifetime database: " + _sceneToLifetime.Count + "\n> " + string.Join("\n> ", _sceneToLifetime
                .Select(x => $"{x.Key.name} (valid/loaded:{x.Key.IsValid()}/{x.Key.isLoaded}) -> {x.Value}"));
        }


        /// <summary>Get lifetime of active scene.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SceneLifetime Get() => Get(SceneManager.GetActiveScene());

        /// <summary>Get lifetime of specified scene.</summary>
        /// <param name="scene">`gameObject.scene` or `SceneManager.Get...` or something.</param>
        public static SceneLifetime Get(Scene scene)
        {
            ThrowIfCacheIsInvalid();

            if (_sceneToLifetime.ContainsKey(scene))
                return _sceneToLifetime[scene];

            return new SceneLifetime(scene);
        }

    }


    /// <summary>
    /// Scene-bound lifecycle factory.
    /// </summary>
    public static class SceneLifecycle
    {
        /// <summary>Get scene-bound lifecycle of active scene.</summary>
        /// <inheritdoc cref="Get(Scene)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LifecycleBehaviour Get() => Get(SceneManager.GetActiveScene());

        /// <summary>Get scene-bound lifecycle of specified scene.</summary>
        /// <returns>Lifecycle and its GameObject will be destroyed automatically on sceneUnloaded event.</returns>
        /// <inheritdoc cref="SceneLifetime.Get(Scene)"/>
        public static LifecycleBehaviour Get(Scene scene)
        {
            var lifetime = SceneLifetime.Get(scene);
            var result = LifecycleBehaviour.Create(nameof(SceneLifecycle) + lifetime.ToString(), true);

            result.gameObject.DestroyWith(lifetime.Token);
            return result;
        }

    }


    /// <summary>
    /// Lifetime extension methods. Use `DebuggerAction` to extend debugging functionality.
    /// </summary>
    public static class LifetimeExtensions
    {
        const string LOG_PREFIX = "[" + nameof(LifetimeExtensions) + "] ";

        /// <summary>
        /// Arguments: (obj, token, ticket, ownerOrNull)
        /// <para>
        /// Invoked before object bound to token.
        /// </para>
        /// </summary>
        public static Action<object, CancellationToken, CancellationTokenRegistration, object?>? DebuggerAction
#if UNITY_EDITOR
            = (obj, token, ticket, ownerOrNull) =>
            {
                // could be UnityEngine.Object, don't use `??`
                var ownerOrTicket = ownerOrNull != null ? ownerOrNull : ticket;
                Tests.LifecycleBehaviourTests._ticketOrOwnerToTarget.Add(new(ownerOrTicket), new(obj));
                Debug.Log(LOG_PREFIX + $"Lifetime bound (obj->owner): {obj}  -->  {ownerOrTicket}");
            }
#endif
            ;


        /*  deep destroy  ================================================================ */

        const int LAYER_DEFAULT = 0;
        const string TAG_DEFAULT = "Untagged";
        const string NAME_PREFIX = "__LIFECYCLE_GONE__";

        /// <summary>
        /// As of actual object deletion is happened at the end of frame, GameObject.Find(),
        /// GameObject.FindWithTag() or other Unity native functions unexpectedly retrieve
        /// destroyed objects. thus need to "hide" destroyed object from those functions.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DeepDestroy_NoCheck(UnityEngine.Object obj)
        {
            // NOTE: already checked
            //if (obj == null || obj is Transform)
            //{
            //    return;
            //}

            // component.name will change gameObject.name. ignore it.
            if (obj is not Component)
            {
                // 1) must be unique due to obj.name may be used as dictionary key
                // 2) empty or short string causes problem when slicing or something w/o bounds check
                obj.name = NAME_PREFIX + obj.GetHashCode();
            }

            if (obj is GameObject go)
            {
                go.SetActive(false);
                // to hide from GetComponentsInChildren<T>(true)
                go.transform.SetParent(null, false);
                go.tag = TAG_DEFAULT;
                go.layer = LAYER_DEFAULT;
                //go.hideFlags = ;
            }

#if UNITY_EDITOR
            if (Application.IsPlaying(obj))
#endif
                UnityEngine.Object.Destroy(obj);
        }


        /*  extension methods  ================================================================ */

        /// <summary>Designed to avoid allocation when register callback to cancellation token.</summary>
        readonly static Action<object> DisposerAction = obj =>
        {
            // NOTE: when object is bound to multiple tokens, it could be null
            if (obj == null)
                return;

            // mono behaviour might implement IDisposable. super ultra rare case.
            (obj as IDisposable)?.Dispose();

            // IDisposable just disposes, UnityEngine.Object (managed shell) may still exist.
            if (obj is UnityEngine.Object unityObj && unityObj != null)  // Transform check is done already. don't check here
            {
                DeepDestroy_NoCheck(unityObj);
            }
        };


        const string ERROR_TFORM = "UnityEngine.Transform is not supported to be disposed.";
        const string WARN_UNITY_OBJ = "Are you sure? Did you consider bind GameObject instead? Binding UnityEngine.Object lifetime to other could make things complex!!";


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static IDisposable BindToToken(object obj, CancellationToken token, object? ownerOrNull)
        {
            if (obj == null)
                throw new NullReferenceException(nameof(obj));

            var ticket = token.Register(DisposerAction, obj);
            DebuggerAction?.Invoke(obj, token, ticket, ownerOrNull);
            return ticket;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool HasntMarkedAsDontDestroyOnLoad(GameObject go)  // !HasMarkedAsDont... vs HasntMarkedAsDont...
        {
            var scene = go.scene;
            return scene.buildIndex != -1 || scene.path != scene.name || scene.name != nameof(UnityEngine.Object.DontDestroyOnLoad);
        }

        static void ThrowIfInvalidOperationThenPrepareObject(UnityEngine.Object obj, Component? owner)
        {
            if (obj is Transform)
                throw new NotSupportedException(LOG_PREFIX + ERROR_TFORM);

            var go = obj as GameObject;
            if (owner != null)
            {
                if (HasntMarkedAsDontDestroyOnLoad(owner.gameObject))
                {
                    var comp = obj as Component;
                    var ownerScene = owner.gameObject.scene;

                    // disallow binding when both obj and owner are NOT marked as DontDestroyOnLoad
                    if ((go != null && go.scene != ownerScene && HasntMarkedAsDontDestroyOnLoad(go))
                    || (comp != null && comp.gameObject.scene != ownerScene && HasntMarkedAsDontDestroyOnLoad(comp.gameObject))
                    )
                    {
                        throw new InvalidOperationException(LOG_PREFIX
                            + "inter-scene binding is restricted. use cancellation token overload if you understand and accept side effects.");
                    }
                }
            }

            // NOTE: to promise order of destruction, need to mark GameObject as DontDestroyOnLoad
            if (go != null)
            {
                UnityEngine.Object.DontDestroyOnLoad(go);
            }
        }


        //IDisposable
        /// <summary>Bind disposable lifetime to cancellation token.</summary>
        public static IDisposable DestroyWith(this IDisposable obj, CancellationToken token) => BindToToken(obj, token, null);
        /// <summary>Bind disposable lifetime to unity scene.</summary>
        public static IDisposable DestroyWith(this IDisposable obj, SceneLifetime scene) => BindToToken(obj, scene.Token, scene);
        /// <summary>Bind disposable lifetime to lifecycle owner.</summary>
        public static IDisposable DestroyWith(this IDisposable obj, LifecycleBehaviour owner) => BindToToken(obj, owner.destroyCancellationToken, owner);
#if UNITY_2022_2_OR_NEWER
        /// <summary>Bind disposable lifetime to MonoBehaviour.</summary>
        public static IDisposable DestroyWith(this IDisposable obj, MonoBehaviour mono) => BindToToken(obj, mono.destroyCancellationToken, mono);
#endif


        /* =      UnityEngine.Object      = */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static IDisposable BindUnityObjectToToken(UnityEngine.Object obj, CancellationToken token)
        {
            ThrowIfInvalidOperationThenPrepareObject(obj, null);
            return BindToToken(obj, token, null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static IDisposable BindUnityObjectToToken(UnityEngine.Object obj, SceneLifetime scene)
        {
            ThrowIfInvalidOperationThenPrepareObject(obj, null);
            return BindToToken(obj, scene.Token, scene);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static IDisposable BindUnityObjectToToken(UnityEngine.Object obj, LifecycleBehaviour owner)
        {
            ThrowIfInvalidOperationThenPrepareObject(obj, owner);
            return BindToToken(obj, owner.destroyCancellationToken, owner);
        }

#if UNITY_2022_2_OR_NEWER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static IDisposable BindUnityObjectToToken(UnityEngine.Object obj, MonoBehaviour mono)
        {
            ThrowIfInvalidOperationThenPrepareObject(obj, mono);
            return BindToToken(obj, mono.destroyCancellationToken, mono);
        }
#endif


        //Transform
        [Obsolete(ERROR_TFORM, true)] public static void DestroyWith(this Transform _, MonoBehaviour m) => throw new NotSupportedException(ERROR_TFORM);
        [Obsolete(ERROR_TFORM, true)] public static void DestroyWith(this Transform _, SceneLifetime s) => throw new NotSupportedException(ERROR_TFORM);
        [Obsolete(ERROR_TFORM, true)] public static void DestroyWith(this Transform _, CancellationToken c) => throw new NotSupportedException(ERROR_TFORM);
        [Obsolete(ERROR_TFORM, true)] public static void DestroyWith(this Transform _, LifecycleBehaviour l) => throw new NotSupportedException(ERROR_TFORM);


        //GameObject
        /// <summary>Bind GameObject lifetime to cancellation token.</summary>
        public static IDisposable DestroyWith(this GameObject obj, CancellationToken token) => BindUnityObjectToToken(obj, token);

        /// <summary>Bind GameObject lifetime to unity scene.</summary>
        public static IDisposable DestroyWith(this GameObject obj, SceneLifetime scene) => BindUnityObjectToToken(obj, scene);

        /// <summary>Bind GameObject lifetime to lifecycle owner.</summary>
        public static IDisposable DestroyWith(this GameObject obj, LifecycleBehaviour owner) => BindUnityObjectToToken(obj, owner);

#if UNITY_2022_2_OR_NEWER
        /// <summary>Bind GameObject lifetime to MonoBehaviour.</summary>
        public static IDisposable DestroyWith(this GameObject obj, MonoBehaviour mono) => BindUnityObjectToToken(obj, mono);
#endif


        // NOTE: UnityEngine.Object binding will make things complicated.
        //       need to explicitly call different method to accept side effects.

        [Obsolete(WARN_UNITY_OBJ)]
        public static IDisposable DestroyUnityObjectWith(this UnityEngine.Object obj, CancellationToken token) => BindUnityObjectToToken(obj, token);
        [Obsolete(WARN_UNITY_OBJ)]
        public static IDisposable DestroyUnityObjectWith(this UnityEngine.Object obj, SceneLifetime scene) => BindUnityObjectToToken(obj, scene);
        [Obsolete(WARN_UNITY_OBJ)]
        public static IDisposable DestroyUnityObjectWith(this UnityEngine.Object obj, LifecycleBehaviour owner) => BindUnityObjectToToken(obj, owner);
#if UNITY_2022_2_OR_NEWER
        [Obsolete(WARN_UNITY_OBJ)]
        public static IDisposable DestroyUnityObjectWith(this UnityEngine.Object obj, MonoBehaviour mono) => BindUnityObjectToToken(obj, mono);
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
                var rootOwner = LifecycleBehaviour.Create("Root Lifecycle", false);
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
                    COMPs[i].DestroyUnityObjectWith(childOwner);
                    GOs[i].DestroyWith(childOwner);
                }

                UnityEditor.Selection.activeGameObject = rootOwner.gameObject;
                Report_Remaining_References();
            }


            [UnityEditor.MenuItem(MENU_ROOT + "Debug Check List", priority = int.MaxValue / 3)]
            internal static void DebugCheckList_DoNothing() { }

            [UnityEditor.MenuItem(MENU_ROOT + ">   Unregister by Token", priority = int.MaxValue / 3 + 1)]  //+1
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

            [UnityEditor.MenuItem(MENU_ROOT + ">   Don't Unregister by Token (raise error)", priority = int.MaxValue / 3 + 2)]  //+2
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


            [UnityEditor.MenuItem(MENU_ROOT + ">   Inter-Scene Binding must throw \t by hand", priority = int.MaxValue / 3 + 5)]
            [UnityEditor.MenuItem(MENU_ROOT + ">   Transform Binding must throw \t by hand", priority = int.MaxValue / 3 + 5)]
            internal static void DebugCheckList_DoNothing_2() { }

            [UnityEditor.MenuItem(MENU_ROOT + "Select Transform of active GameObject", priority = int.MaxValue / 3 + 10)] //+10
            internal static void SelectTransformOfActiveGameObject()
            {
                var go = UnityEditor.Selection.activeGameObject;
                if (go != null)
                {
                    UnityEditor.Selection.objects = new UnityEngine.Object[] { go.transform };
                    Debug.Log("Selection updated: " + string.Join(", ", UnityEditor.Selection.objects.AsEnumerable()));
                }
            }


            internal readonly static Dictionary<WeakReference, WeakReference> _ticketOrOwnerToTarget = new();

            [UnityEditor.MenuItem(MENU_ROOT + nameof(Report_Remaining_References), priority = int.MaxValue / 2)]
            internal static void Report_Remaining_References()
            {
                GC.Collect();

                foreach (var key in _ticketOrOwnerToTarget.Keys.ToArray())
                {
                    // IsAlive is still true even if unity object has been destroyed.
                    if ((!key.IsAlive || (key.Target as UnityEngine.Object == null))
                    && (!_ticketOrOwnerToTarget[key].IsAlive || (_ticketOrOwnerToTarget[key].Target as UnityEngine.Object) == null)
                    )
                    {
                        _ticketOrOwnerToTarget.Remove(key);
                    }
                }

                var msg = (nameof(LifecycleBehaviourTests) + ": # of references remaining: " + _ticketOrOwnerToTarget.Count + "\n> "
                    + string.Join("\n> ", _ticketOrOwnerToTarget.Select(x => $"{x.Key.Target ?? "<NULL>"}  =>  {x.Value.Target ?? "<NULL>"}")) + "\n");

                if (_ticketOrOwnerToTarget.Count > 0)
                    Debug.LogWarning(msg);
                else
                    Debug.Log(msg);
            }

            [UnityEditor.MenuItem(MENU_ROOT + nameof(Report_Scene_Lifetime_Database), priority = int.MaxValue / 2)]
            internal static void Report_Scene_Lifetime_Database()
            {
                Debug.Log(nameof(LifecycleBehaviourTests) + ": " + SceneLifetime.DumpDatabaseInfo() + "\n");
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
                    sel[i].DestroyUnityObjectWith(lifecycle);
                }

                Report_Remaining_References();
            }

            [UnityEditor.MenuItem(MENU_ROOT + nameof(Bind_Lifetime_to_Unity_Scene), priority = MENU_OPERATE_PRIORITY)]
            internal static void Bind_Lifetime_to_Unity_Scene()
            {
                var sel = UnityEditor.Selection.objects;
                if (sel.Length < 1)
                    return;

                var sceneLC = SceneLifetime.Get();
                for (int i = 0; i < sel.Length; i++)
                {
                    sel[i].DestroyUnityObjectWith(sceneLC);
                }

                Report_Remaining_References();
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

            [UnityEditor.MenuItem(MENU_ROOT + nameof(Create_New_Scene_Lifecycle), priority = MENU_OPERATE_PRIORITY)]
            internal static void Create_New_Scene_Lifecycle()
            {
                _ = SceneLifecycle.Get();
                Report_Scene_Lifetime_Database();
            }

            [UnityEditor.MenuItem(MENU_ROOT + nameof(Get_SceneLifetime), priority = MENU_OPERATE_PRIORITY)]
            internal static void Get_SceneLifetime()
            {
                _ = SceneLifetime.Get();
                Report_Scene_Lifetime_Database();
            }


            const int MENU_SCENE_PRIORITY = 1100;

            static int _currentSceneID = -310;
            [UnityEditor.MenuItem(MENU_ROOT + nameof(Create_Blank_Scene), priority = MENU_SCENE_PRIORITY)]
            internal static void Create_Blank_Scene()
            {
                var scene = SceneManager.CreateScene("BLANK" + _currentSceneID--);
                SceneManager.SetActiveScene(scene);
            }

            [UnityEditor.MenuItem(MENU_ROOT + nameof(Create_Blank_Scene_and_Unload_All), priority = MENU_SCENE_PRIORITY)]
            internal static void Create_Blank_Scene_and_Unload_All()
            {
                Create_Blank_Scene();
                var current = SceneManager.GetActiveScene();
                var count = SceneManager.sceneCount;
                for (var i = 0; i < count; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene != current && scene.isLoaded && scene.IsValid())
                        SceneManager.UnloadSceneAsync(scene);
                }
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
            [UnityEditor.MenuItem(MENU_ROOT + nameof(Bind_Lifetime_to_Unity_Scene), true)]
            [UnityEditor.MenuItem(MENU_ROOT + nameof(Create_New_Lifecycle), true)]
            [UnityEditor.MenuItem(MENU_ROOT + nameof(Create_New_Scene_Lifecycle), true)]
            [UnityEditor.MenuItem(MENU_ROOT + nameof(Get_SceneLifetime), true)]
            [UnityEditor.MenuItem(MENU_ROOT + nameof(Create_Blank_Scene_and_Unload_All), true)]
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
