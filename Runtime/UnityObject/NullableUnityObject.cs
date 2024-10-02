/** Nullable support for `UnityEngine.Object`
 ** (c) 2024 https://github.com/sator-imaging
 ** Licensed under the MIT License

How to Use
==========
Use `.Nullable()` extension method when using null-coalescing operator with `UnityEngine.Object` which may be null.

```cs
var rb = go.GetComponent<Rigidbody>().Nullable() ?? go.AddComponent<Rigidbody>();
var rb = go.GetComponent<Rigidbody>().Nullable() ?? throw new Exception();
```

 */

using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Object = UnityEngine.Object;

#nullable enable

namespace SatorImaging.UnityFundamentals
{

#pragma warning disable CS0618  // NOTE: to prevent misuse, NullableUnityObject<T> is marked as obsolete

    public static class NullableUnityObjectExtensions
    {
        // NOTE: don't use [DoesNotReturn] attribute!!
        //       it will completely stop nullability analysis in context after method call!!

        // TODO: not able to specify method return value by [NotNullIfNotNull("return")]
        //       any way to mark `self` is not null after this method is called?
        // NOTE: if first parameter is `this T self`, it shows warning when `self` type is `GameObject?` or other nullable ref type.
        //       [AllowNull] won't work as expected in this case. to suppress warning, it must be declared as `this T? self`.
        // NOTE: no performance gain so should not set inlining explicitly
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NullableUnityObject<T>? Nullable<T>([AllowNull] this T? self)  // must be `this T? self`
            where T : Object
        {
            return (self == null) ? null : new NullableUnityObject<T>(self);
        }


        /// <exception cref="NullReferenceException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NotNull]
        [Obsolete("use `.Nullable() ?? throw new...` instead")]
        public static T ThrowIfNull<T>([AllowNull] this T? self, string? message = null, Exception? innerException = null)  // must be `this T? self`
            where T : Object
        {
            if (self == null)
                throw new NullReferenceException(message, innerException);

            return self;
        }
    }
#pragma warning restore CS0618


    /*  typedef  ================================================================ */

    /// <summary>
    /// > [!CAUTION]
    /// > !!! this struct must not be used except for designated usage !!!
    /// </summary>
    [Obsolete("!!! this struct must not be used except for designated usage !!!")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [StructLayout(LayoutKind.Auto)]
    public readonly struct NullableUnityObject<T> : IEquatable<NullableUnityObject<T>>, IEquatable<T>
        where T : Object
    {
        readonly T value;

        [Obsolete("use `Nullable()` extension method for unity object instead of instantiating manually")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal NullableUnityObject(T value)
        {
            //if (value == null)
            //    throw new ArgumentNullException(nameof(value));

            this.value = value;
        }

        readonly public T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => value;
        }

        readonly public bool HasValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => value != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator T(NullableUnityObject<T> self) => self.value;

        readonly public override string ToString() => value.ToString();
        readonly public override int GetHashCode() => value.GetHashCode();

        // NOTE: UnityEngine.Object.Equals has NOT overriden!!
        //       use == or != operator!!
        readonly public override bool Equals(object? obj) => (obj is NullableUnityObject<T> nuo && nuo.value == value) || (obj is T uo && uo == value);
        readonly public bool Equals(T? other) => value == other;
        readonly public bool Equals(NullableUnityObject<T> other) => value == other.value;

        public static bool operator ==(NullableUnityObject<T> left, NullableUnityObject<T> right) => left.value == right.value;
        public static bool operator !=(NullableUnityObject<T> left, NullableUnityObject<T> right) => left.value != right.value;
        public static bool operator ==(NullableUnityObject<T> left, T? right) => left.value == right;
        public static bool operator !=(NullableUnityObject<T> left, T? right) => left.value != right;
        public static bool operator ==(T? left, NullableUnityObject<T> right) => left == right.value;
        public static bool operator !=(T? left, NullableUnityObject<T> right) => left != right.value;
    }
}
