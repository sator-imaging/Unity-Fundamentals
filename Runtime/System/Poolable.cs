/** Self-Contained Object Pool for .NET / Unity
 ** (c) 2024 Sator Imaging, Licensed under the MIT License
 ** https://github.com/sator-imaging/Unity-Fundamentals

Thread-safe & efficient singly linked list based object pooling.

HOW TO USE
==========
```cs
// inherit Poolable<TSelf>
public class MyPooledClass : Poolable<MyPooledClass>
{
    // your code here
}

// `using` to take instance from pool and return it on dispose
using (var x = MyPooledClass.GetInstance())
{
    x.DoSomething();
}

// there is a throw helper function
ThrowIfAlreadyReturnedToPool("error message");
```

 */

using System;
using System.Threading;

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    abstract public class Poolable<TSelf> : IDisposable
            where TSelf : Poolable<TSelf>, new()
    {
        volatile static TSelf? pool_anchor;
        volatile TSelf? pool_next;
        bool pool_hasReturned = false;

        /// <summary>
        /// [Thread-Safe]
        /// Get pooled instance or create new if pool is empty.
        /// Call <c>Dispose()</c> to return instance to pool for reuse.
        /// </summary>
        public static TSelf GetInstance()
        {
            var result = pool_anchor;
            if (result == null)
            {
                return new();
            }

            // try compare & swap and if result doesn't equal to comprand, swapping is failed (other thread did swap)
            if (Interlocked.CompareExchange(ref pool_anchor, result.pool_next, result) != result)
            {
                result = GetInstance_SlowPath();
            }

            result.pool_next = null;
            result.pool_hasReturned = false;
            return result;
        }

        static TSelf GetInstance_SlowPath()
        {
            var spin = new SpinWait();

            TSelf? result;
            while ((result = pool_anchor) != null)
            {
                if (Interlocked.CompareExchange(ref pool_anchor, result.pool_next, result) == result)
                {
                    return result;
                }

                spin.SpinOnce();
            }

            return new();
        }


        /// <summary>This method is only called if instance is not returned to pool.</summary>
        abstract protected void OnWillReturnToPool();

        /// <summary>
        /// [Thread-Safe]
        /// Return instance to pool.
        /// </summary>
        public void Dispose()
        {
            if (this.pool_hasReturned)
                return;

            OnWillReturnToPool();
            this.pool_hasReturned = true;  // set after OnWillReturnToPool and before Interlocked

            // build linked list before swap, then try compare & swap to build linked list
            var current = pool_anchor;
            this.pool_next = current;

            // if result doesn't equal to comprand, other thread did swap
            if (Interlocked.CompareExchange(ref pool_anchor, this as TSelf, current) != current)
            {
                Dispose_SlowPath();
            }
        }

        void Dispose_SlowPath()
        {
            var spin = new SpinWait();

            TSelf? current;
            while (true)
            {
                spin.SpinOnce();

                current = pool_anchor;
                this.pool_next = current;

                if (Interlocked.CompareExchange(ref pool_anchor, this as TSelf, current) == current)
                {
                    return;
                }
            }
        }


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

        /// <summary>Purge unused instances from pool. NOTE: active instances remain alive and return to pool on dispose.</summary>
        /// <returns>Purged instance count</returns>
        public static int ClearUnusedInstancesFromPool()
        {
            var current = Interlocked.Exchange(ref pool_anchor, null);

            int count = 0;
            while (current != null)
            {
                var next = current.pool_next;
                current.pool_next = null;

                count++;
                current = next;
            }

            return count;
        }

    }
}
