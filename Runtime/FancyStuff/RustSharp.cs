/** Move/Borrow Semantics for .NET / Unity
 ** (c) 2025 Sator Imaging, Licensed under the MIT License
 ** https://github.com/sator-imaging/Unity-Fundamentals

Rust-like move/borrow semantics helper for .NET / Unity providing runtime protection by
throwing `NullReferenceException`. Note that it doesn't provide compile time protection.

Basic Usage
===========
```cs
// absolute ownership should be stored in readonly field.
readonly AbsoluteOwnership<int[]> _arrayOwnership;

// foo takes ownership thus bar ownership will be nullified.
foo._arrayOwnership.Take(bar._arrayOwnership);

// if method takes int[], first you need to move ownership.
var rawData = foo._arrayOwnership.Move();  // ownership is nullified
var result = DoSomething(rawData);

// and then take back ownership.
foo._arrayOwnership.Take(ref result);  // result is nullified

// for limited lifetime usage, it can be implicitly converted to Borrow<T>.
// * Borrow<T> is ref struct
DoSomething(foo._arrayOwnership);

// Borrow has `Read` and `Clone()` to express semantics.
// there is not functional difference between those semantic expressions.
T DoSomething(Borrow<int[]> array)
{
    for (int i = 0; i < array.Read.Length; i++)
    {
        array.Read[i] = i;
    }
    return array.Clone();
}
```

 */

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    /// <summary>
    /// Limited lifetime ownership borrowed from <see cref="AbsoluteOwnership{T}"/>.
    /// </summary>
    public readonly ref struct Borrow<T>
        where T : class
    {
        public readonly T Read;
        internal Borrow(T value) => Read = value;

        // NOTE: `ref` field requires C# 11.0
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public /*ref*/ T Clone() => Read;
    }

    /// <summary>
    /// > [!IMPORTANT]
    /// > Absolute ownership should be stored in <see langword="readonly"/> field and
    /// > it should not be passed to method parameter. (i.e., should not be copied)
    /// </summary>
    public struct AbsoluteOwnership<T>
        : IDisposable
        , IEquatable<AbsoluteOwnership<T>>
        where T : class
    {
        private T _value;
        public AbsoluteOwnership(T value) => _value = value;

        public void Dispose()
        {
            var temp = _value;
            _value = (((default)))!;

            (temp as IDisposable)?.Dispose();
        }

        public T Move()
        {
            var ret = _value;
            _value = (((default)))!;
            return ret;
        }

        public void Take(AbsoluteOwnership<T> other) => _value = other.Move();

        public void Take([DisallowNull][MaybeNull] ref T other)
        {
            var temp = other;
            other = (((default)))!;
            _value = temp;
        }

        public static implicit operator Borrow<T>(AbsoluteOwnership<T> self) => new(self._value);

        // IEquatable
        readonly public override int GetHashCode() => this._value?.GetHashCode() ?? 0;  // may be null!!
        readonly public override bool Equals(object? obj) => obj is AbsoluteOwnership<T> other && this.Equals(other);
        readonly public bool Equals(AbsoluteOwnership<T> other)
        {
            // NOTE: in semantics, Equals checks those hold SAME REFERENCE or not.
            //       so this method should return FALSE when both self and other hold null.
            //       * it's really confusing if returns TRUE when both hold null.
            //         --> why those own ownership of SAME reference!? (actually not, both are null)
            return (this._value != null && other._value != null)
                && ReferenceEquals(this._value, other._value);
        }
        public static bool operator ==(AbsoluteOwnership<T> left, AbsoluteOwnership<T> right) => left.Equals(right);
        public static bool operator !=(AbsoluteOwnership<T> left, AbsoluteOwnership<T> right) => !(left == right);
    }
}
