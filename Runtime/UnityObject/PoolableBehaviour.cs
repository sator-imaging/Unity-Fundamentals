/** Self-Contained MonoBehaviour Pool
 ** (c) 2024 Sator Imaging, Licensed under the MIT License
 ** https://github.com/sator-imaging/Unity-Fundamentals

Singly linked list based `GameObject` & `MonoBehaviour` pooling.

HOW TO USE
==========
```cs
// inherit PoolableBehaviour<TSelf>
public class MyPooledBehaviour: PoolableBehaviour<MyPooledBehaviour>
{
    // your code here
}

// new GameObject will be created for your component if pool is empty
var component = MyPooledBehaviour.Rent(activateGameObject: false, parent: null);

// to return instance to pool
component.ReturnToPool();
// --- or ---
component.ReturnToPoolIfCanceled(otherComponent.destroyCancellationToken);

// populate instances (aka. warmup)
MyPooledBehaviour.Populate(count: 10);

// purge unused instances from pool
MyPooledBehaviour.TrimExcess(maxInstanceCountInPool: 3);

// there is a helper function can be called in inheritance method
ThrowIfAlreadyReturnedToPool("error message");
```

 */

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    public interface IPoolableBehaviour
    {
        void ReturnToPool();
        void ReturnToPool(bool returnNestedPoolables);
        CancellationTokenRegistration ReturnToPoolIfCanceled(CancellationToken cancellationToken);


        // NOTE: to decrease scene.rootCount and DontDestroyOnLoad method calls
        readonly static Transform PooledInstanceContainer;
        readonly static List<IPoolableBehaviour> CachedListUsedInReturnToPool = new();

        static IPoolableBehaviour()
        {
            var go = new GameObject(nameof(IPoolableBehaviour));
            UnityEngine.Object.DontDestroyOnLoad(go);
            //go.SetActive(false);

            PooledInstanceContainer = go.transform;
        }


        // suffix number is not important. nice to see how many instances are created by this interface
        static int _totalNumberOfInstantiate = 1;
        static GameObject CreateHostGameObject(string prefix)
        {
            Span<char> span = stackalloc char[prefix.Length + 32];
            prefix.AsSpan().CopyTo(span);
            _totalNumberOfInstantiate.TryFormat(span.Slice(prefix.Length), out var written);
            _totalNumberOfInstantiate++;
            written += prefix.Length;

            var go = new GameObject(new(span.Slice(0, written)));
            go.SetActive(false);

            return go;
        }
    }


    /// <summary>
    /// Self-contained <see cref="MonoBehaviour"/> pool. Note that host <see cref="GameObject"/> is automatically managed.
    /// </summary>
    abstract public class PoolableBehaviour<TSelf> : MonoBehaviour, IPoolableBehaviour
            where TSelf : PoolableBehaviour<TSelf>
    {
        readonly static string GO_NAME_PREFIX = typeof(TSelf).Name + " ";

        static TSelf? pool_anchor;
        TSelf? pool_next;
        bool pool_hasReturned = false;

        /// <summary>
        /// Get pooled instance or create new if pool is empty.
        /// Call <c>ReturnToPool()</c> to reuse instance.
        /// </summary>
        /// <remarks>
        /// NOTE: if no <c>parent</c> is specified, host <c>GameObject</c> will be marked as <c>DontDestroyOnLoad</c> otherwise destroyed with parent or scene.
        /// (destroyed instance won't be reused)
        /// </remarks>
        /// <inheritdoc cref="Transform.SetParent(Transform, bool)"/>
        public static TSelf Rent(bool activateGameObject, Transform? parent = null, bool worldPositionStays = true)
        {
            if (parent != null)
            {
                if (parent.root == IPoolableBehaviour.PooledInstanceContainer)
                    throw new ArgumentException("under control of another poolable behaviour", nameof(parent));
            }
            else
            {
                parent = IPoolableBehaviour.PooledInstanceContainer;
            }

            var result = pool_anchor;
            if (result != null)
            {
                pool_anchor = result.pool_next;
                result.pool_next = null;
            }
            else
            {
                result = IPoolableBehaviour.CreateHostGameObject(GO_NAME_PREFIX).AddComponent<TSelf>();
            }

            // uGUI requires SetParent call
            result.transform.SetParent(parent, worldPositionStays);

            if (activateGameObject)
            {
                result.gameObject.SetActive(true);
            }

            result.pool_hasReturned = false;
            return result;
        }


        /// <summary>
        /// Return instance to pool. Note that host <c>GameObject</c> will be marked as <c>DontDestroyOnLoad</c>.
        /// </summary>
        public void ReturnToPool() => ReturnToPool(true);


        /// <summary>This method is only called if instance is not returned to pool.</summary>
        abstract protected void OnWillReturnToPool();

        /// <inheritdoc cref="ReturnToPool()"/>
        /// <param name="returnNestedPoolables">
        /// <see langword="true"/> to return nested GameObjects to pool which have <see cref="IPoolableBehaviour"/> component.
        /// (ie. poolable hierarchy will be flatten)
        /// <br/>
        /// NOTE: if this parameter ON and OFF are mixed for same type, it's not able to control to take whether instance with or without nested poolables.
        /// </param>
        public void ReturnToPool(bool returnNestedPoolables)
        {
            if (this.pool_hasReturned)
                return;

            OnWillReturnToPool();

            this.gameObject.SetActive(false);  // call before changing parent cuz component may expect original parent
            this.transform.SetParent(IPoolableBehaviour.PooledInstanceContainer, false);

            this.pool_hasReturned = true;

            // NOTE: nested components must be flatten
            if (returnNestedPoolables)
            {
                var cache = IPoolableBehaviour.CachedListUsedInReturnToPool;

                this.GetComponentsInChildren(true, cache);
                for (int i = 0; i < cache.Count; i++)
                {
                    var poolable = cache[i];

                    if (ReferenceEquals(poolable, this))
                        continue;

                    poolable.ReturnToPool(false);
                }

                cache.Clear();
            }

            this.pool_next = pool_anchor;
            pool_anchor = this as TSelf;
        }


        /// <summary>Bind instance to cancellation token.</summary>
        /// <remarks>NOTE: must be called on Unity main thread.</remarks>
        public CancellationTokenRegistration ReturnToPoolIfCanceled(CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
                return default;

            if (ExecutionContext.IsFlowSuppressed())
            {
                return cancellationToken.Register(OnTokenCanceled, this, useSynchronizationContext: true);
            }
            else
            {
                using (ExecutionContext.SuppressFlow())
                {
                    return cancellationToken.Register(OnTokenCanceled, this, useSynchronizationContext: true);
                }
            }
        }

        readonly static Action<object> OnTokenCanceled = (state) =>
        {
#if UNITY_EDITOR
            try
#endif
            {
                ((TSelf)state).ReturnToPool();
            }
#if UNITY_EDITOR
            catch (Exception exc)
            {
                UnityEngine.Debug.LogException(exc);
            }
#endif
        };


        /*  helper functions  ================================================================ */

        /// <summary>Throw exception if instance has already returned to pool.</summary>
        /// <exception cref="InvalidOperationException"></exception>
        protected void ThrowIfAlreadyReturnedToPool(string message)
        {
            if (this.pool_hasReturned)
            {
                throw new InvalidOperationException(message);
            }
        }


        /// <summary>Populate instances in pool. (aka. Warmup)</summary>
        /// <param name="execute_Awake_OnEnable">
        /// Execute <c>Awake()</c> and <c>OnEnable()</c> once on populate and then deactivate host GameObject.
        /// NOTE: <c>OnDisable()</c> is also executed on deactivate.
        /// </param>
        /// <remarks>NOTE: currently consumed instances are not considered.</remarks>
        public static void Populate(int count, bool execute_Awake_OnEnable = false)
        {
            // TODO: latest C# allows stackalloc-ing ref types.
            var rental = ArrayPool<IPoolableBehaviour>.Shared.Rent(count);
            try
            {
                var span = rental.AsSpan(0, count);

                for (int i = 0; i < span.Length; i++)
                {
                    span[i] = Rent(execute_Awake_OnEnable, null, false);
                }

                for (int i = 0; i < span.Length; i++)
                {
                    span[i].ReturnToPool();
                }
            }
            finally
            {
                ArrayPool<IPoolableBehaviour>.Shared.Return(rental, clearArray: true);  // must clear!!
            }
        }

        /// <summary>Trim instance pool to specified size.</summary>
        /// <remarks>NOTE: currently consumed instances are not considered.</remarks>
        public static void TrimExcess(int maxInstanceCountInPool)
        {
            int count = 0;
            var current = pool_anchor;
            while (current != null)
            {
                var next = current.pool_next;

                count++;
                if (count <= maxInstanceCountInPool)
                {
                    goto NEXT;
                }

                // must clear pool_next to avoid leaked managed shell potential
                current.pool_next = null;
                Destroy(current.gameObject);

            NEXT:
                current = next;
            }
        }

    }
}




#region ////////  TEMPLATE: Debug menu for Unity Editor  ////////

#if UNITY_EDITOR

#pragma warning disable IDE1006

// TEMPLATE: namespace naming convention: <TEST_TARGET>.<ROOT_MENU_LABEL>.<SUB_MENU_LABEL>
namespace SatorImaging.UnityFundamentals.DEBUG.Poolable_Behaviour  // must be unique. don't reuse existing namespace
{
    public static class UNITY_EDITOR_DEBUG  // don't change
    {
        const string MENU_ROOT = nameof(DEBUG) + "/" + nameof(Poolable_Behaviour) + "/";


        #region ////////  TEMPLATE: Debug Methods  ////////
        /*  TEMPLATE: Debug Methods  ================================================================ */

        public class TestBehaviour : PoolableBehaviour<TestBehaviour>
        {
            void Awake() => UnityEngine.Debug.Log(nameof(Awake) + ": " + name);
            void OnEnable() => UnityEngine.Debug.Log(nameof(OnEnable));
            void Start() => UnityEngine.Debug.Log(nameof(Start));
            //void Update() => UnityEngine.Debug.Log(nameof(name));
            void OnDisable() => UnityEngine.Debug.Log(nameof(OnDisable));
            void OnDestroy() => UnityEngine.Debug.Log(nameof(OnDestroy));

            protected override void OnWillReturnToPool()
            {
            }
        }

        public class OtherBehaviour : PoolableBehaviour<OtherBehaviour>
        {
            protected override void OnWillReturnToPool()
            {
            }
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Populate_TestBehaviour), priority = 0)]
        public static void Populate_TestBehaviour()
        {
            TestBehaviour.Populate(3, true);
            UnityEngine.Debug.Log("Populate 3 instances");

            TestBehaviour.Populate(10);
            UnityEngine.Debug.Log("Populate 10 instances");
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Trim_TestBehaviour_Pool), priority = 0)]
        public static void Trim_TestBehaviour_Pool()
        {
            TestBehaviour.TrimExcess(7);
            UnityEngine.Debug.Log("Trim to 7 instances");

            TestBehaviour.TrimExcess(3);
            UnityEngine.Debug.Log("Trim to 3 instances");
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Rent_and_Parent_to_Selection__Test), priority = int.MinValue / 2)]
        public static void Rent_and_Parent_to_Selection__Test()
        {
            var tform = UnityEditor.Selection.activeTransform;
            TestBehaviour.Rent(true, tform);
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Rent_and_Parent_to_Selection__Other), priority = int.MinValue / 2)]
        public static void Rent_and_Parent_to_Selection__Other()
        {
            var tform = UnityEditor.Selection.activeTransform;
            OtherBehaviour.Rent(true, tform);
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Return_Selected_Hierarchy), priority = int.MinValue / 3)]
        public static void Return_Selected_Hierarchy()
        {
            var go = UnityEditor.Selection.activeGameObject;
            if (go != null && go.TryGetComponent<IPoolableBehaviour>(out var result))
            {
                result.ReturnToPool();
            }
        }

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Return_Selected_Only), priority = int.MinValue / 3)]
        public static void Return_Selected_Only()
        {
            var go = UnityEditor.Selection.activeGameObject;
            if (go != null && go.TryGetComponent<IPoolableBehaviour>(out var result))
            {
                result.ReturnToPool(false);
            }
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Return_by_CancellationToken), priority = int.MinValue / 3)]
        public static void Return_by_CancellationToken()
        {
            var instance = TestBehaviour.Rent(true);
            var cts = new CancellationTokenSource(3000);

            UnityEngine.Debug.Log("Return to pool after 3 seconds.");

            instance.ReturnToPoolIfCanceled(cts.Token);
        }


        /*  TEMPLATE: End of Debug  ================================================================ */
        #endregion    //  TEMPLATE: End of Debug


        /* TEMPLATE: copy & paste and replace argument for 'nameof()'

        [UnityEditor.MenuItem(MENU_ROOT + nameof(__Underscore_Separated_Method_Name__), priority = 0)]
        public static void Basic_Tests()
        {
        }

        */


        // TEMPLATE: open script file
        [UnityEditor.MenuItem(MENU_ROOT + "Edit Debug Script...", priority = int.MaxValue - 310)]
        public static void UnityEditorTests_EditDebugScript() => __EditDebugScript();

        static void __EditDebugScript(
            [System.Runtime.CompilerServices.CallerFilePath] string? filePath = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
            => UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(filePath, lineNumber);
    }
}
#endif
#endregion
