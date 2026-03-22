/** Atom: Swift's actor implementation for .NET / Unity
 ** (c) 2026 Sator Imaging, Licensed under the MIT License
 ** https://github.com/sator-imaging/Unity-Fundamentals

Basic Usage
===========

```cs
var atom = new Atom<int>();

atom.Set = 310;
atom.Update = v => ++v;  // Increments (311)

atom.Set = 42;
atom.Read = v => Console.WriteLine(v);  // Prints 42

atom.WriteLock((x: 42, y: "Tuple"), static (args, current) =>
{
    return current + args.x + args.y.Length;
});
```

If `T` is reference type, the object properties can be modified in read lock context.

```cs
var atom = new Atom<MyClass>(value: new());

atom.ReadLock(foo, async static (foo, myClass) =>
{
    // NOTE: Lock is taken until the operation finished.
    //       (e.g., For a second, other thread cannot access to the atom value at all)
    await Task.Delay(1000);

    myClass.Data = foo.Value;
});
```

 */

using System;
using System.Runtime.CompilerServices;

#nullable enable
#pragma warning disable IDE0032  // Use auto-implemented property

namespace SatorImaging.UnityFundamentals
{
    /// <summary>
    /// Thread safe box for the <typeparamref name="T"/>.
    /// </summary>
    public sealed class Atom<T>
    {
        private readonly object sync = new();
        private T value;

        public Atom(T value = (((default)))!)
        {
            if (typeof(Task).IsAssignableFrom(typeof(T)) ||
                typeof(ValueTask).IsAssignableFrom(typeof(T)) ||
                typeof(ValueTask<>).IsAssignableFrom(typeof(T)))  // TODO: This check may not make sense
            {
                throw new NotSupportedException("Task-like type is not supported");
            }

            this.value = value;
        }

        public T Set
        {
            set
            {
                lock (sync)
                {
                    this.value = value;
                }
            }
        }

        public T UnsafeRead
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.value;
        }

        public Action<T> Read
        {
            set
            {
                lock (sync)
                {
                    value.Invoke(this.value);
                }
            }
        }

        public Func<T, T> Update
        {
            set
            {
                lock (sync)
                {
                    this.value = value.Invoke(this.value);
                }
            }
        }

        /// <typeparam name="TArgs">Single value or tuple.</typeparam>
        public void WriteLock<TArgs>(TArgs args, Func<TArgs, T, T> op)
        {
            lock (sync)
            {
                this.value = op.Invoke(args, this.value);
            }
        }

        /// <typeparam name="TArgs">Single value or tuple.</typeparam>
        public void ReadLock<TArgs>(TArgs args, Action<TArgs, T> op)
        {
            lock (sync)
            {
                op.Invoke(args, this.value);
            }
        }
    }
}
