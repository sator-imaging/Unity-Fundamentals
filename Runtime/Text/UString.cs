/** Fast & Non-Alloc String Builder for Unity
 ** (c) 2024 Sator Imaging, Licensed under the MIT License
 ** https://github.com/sator-imaging/Unity-Fundamentals

One liner API designed to easily achieve fast & allocation-free string operation.

```cs
// basic usage
var text = UString.Concat("Value: ", intVal);
var text = UString.Concat("Value: ", intVal.ToCharSpan("D5"));
//                                         ~~~~~~~~~~~~~~~~~ instant non-alloc formatter

// 'using' statement is required to use as StringBuilder alternative
using (var sb = UString.Rent())
{
    sb.Append("Value: ");
    sb.Append(intValue);

    UString.NewLineChars = "\r\n";
    sb.NewLine();

    var (array, consumed) = sb.GetRawBuffer();
    textMeshPro.SetCharArray(array, 0, consumed);
}
```


Technical Notes
===============

Prevent Allocation on Formatting Enum Types
-------------------------------------------
Install `StrictEnum` and set preprocessor symbol `STMG_USTRING_USE_STRICT_ENUM`.

Note that in .NET 8 or later version, `StrictEnum` is not required.


Overriding Formatter
--------------------
There is an ability to override formatter by setting `Override***Formatter`.


Formatter Precedence for Non-Primitive Types
--------------------------------------------
UString chooses applicable formatter in the following order.
- `Enum`
- `IFormattable`
- `Nullable<T>`
- `ValueType`
- `Object`

 */

#if UNITY_5_3_OR_NEWER
#define STMG_USTRING_USE_STRICT_ENUM
#endif

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MIO = System.Runtime.CompilerServices.MethodImplOptions;

#nullable enable

#if UNITY_5_3_OR_NEWER
using NUnit.Framework;
using System.Linq;
using System.Text;
#endif

#if UNITY_2020_1_OR_NEWER
using Unsafe = Unity.Collections.LowLevel.Unsafe.UnsafeUtility;
#endif

namespace SatorImaging.UnityFundamentals
{
    // NOTE: depending on 'StrictEnum'
    /// <inheritdoc cref="UltraFastString"/>
    public static class UString
    {
        /// <inheritdoc cref="Rent()"/>
        [Obsolete]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UltraFastString Rent(ReadOnlySpan<char> text)
        {
            var us = Rent();
            us.Append(text);
            return us;
        }


        /// <inheritdoc cref="UltraFastString"/>
        /// <remarks>
        /// NOTE: to reuse allocated buffer on next time, use with <see langword="using"/> statement or call <see cref="UltraFastString.ToStringAndDispose"/> explictly.
        /// </remarks>
        public static UltraFastString Rent() => new(Inlining_ReuseBufferIfPossible());


        /*  instant formatter  ================================================================ */

        public const int RING_BUFFER_SIZE = 256;

        [ThreadStatic] static char[]? ts_charSpanRingBuffer;
        [ThreadStatic] static int ts_charSpanRingBufferStartIndex;

        /// <summary>
        /// Allocation-free instant formatter.
        /// </summary>
        /// <remarks>
        /// NOTE: using ring buffer internally so that it cannot be called repeatedly in short term.
        /// ie. resulting span must be evaluated before another calls that could change array items in range referenced by resulting span.
        /// <br/>
        /// See <see cref="RING_BUFFER_SIZE"/> to see available ring buffer size or call <see cref="RingBufferScope(char[])"/> to use your own buffer.
        /// </remarks>
        public static ReadOnlySpan<char> ToCharSpan<T>(this T value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            where T : IFormattable
        {
            var span = (ts_charSpanRingBuffer ??= new char[RING_BUFFER_SIZE]).AsSpan(ts_charSpanRingBufferStartIndex);
            int written = Formatter<T>.Default.WriteUnsafe(value, span, format, provider);
            if (written < 0)
            {
                span = ts_charSpanRingBuffer.AsSpan();
                written = Formatter<T>.Default.WriteUnsafe(value, span, format, provider);
                if (written < 0)
                {
                    throw new OutOfMemoryException("resulting text is larger than ring buffer size: " + span.Length);
                }
            }

            ts_charSpanRingBufferStartIndex += written;
            if (ts_charSpanRingBufferStartIndex >= RING_BUFFER_SIZE)
                ts_charSpanRingBufferStartIndex = 0;

            return span.Slice(0, written);
        }


        /// <summary>
        /// Use your own ring buffer for <see cref="ToCharSpan{T}(T, ReadOnlySpan{char}, IFormatProvider?)"/> method.
        /// <code>
        /// // example usage
        /// using (UString.RingBufferScope(new char[2048]))
        /// {
        ///     var text = intVal.ToCharSpan();
        /// }
        /// </code>
        /// </summary>
        public static RingBufferScopeHandler RingBufferScope(char[] alternativeRingBuffer) => new(alternativeRingBuffer);

        [StructLayout(LayoutKind.Auto)]
        public readonly ref struct RingBufferScopeHandler
        {
            readonly char[]? restoreBuffer;
            readonly int restoreStartIndex;

            internal RingBufferScopeHandler(char[] scopeBuffer)
            {
                restoreBuffer = ts_charSpanRingBuffer;
                restoreStartIndex = ts_charSpanRingBufferStartIndex;

                ts_charSpanRingBuffer = scopeBuffer;
                ts_charSpanRingBufferStartIndex = 0;
            }

            readonly
            public void Dispose()
            {
                ts_charSpanRingBuffer = restoreBuffer;
                ts_charSpanRingBufferStartIndex = restoreStartIndex;
            }
        }


        /*  settings  ================================================================ */

        // TODO: verify value in setter

        /// <summary>
        /// Initial capacity for newly allocated internal buffer.
        /// </summary>
        public static int InitialCapacity { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; } = 128;  // should be greater than default formatted value max possible length * 4

        /// <summary>
        /// Expansion size limit used when extending internal buffer capacity.
        /// (no error if trying to add larger size text than this limit)
        /// <br/>
        /// Smaller size reduces memory usage but more expansion operations happen. (calling <c>memmove</c> internally)
        /// </summary>
        public static int CapacityExpansionLimit { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; } = int.MaxValue;


        /// <summary>Used by <see cref="UltraFastString.NewLine"/></summary>
        public static string NewLineChars { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; } = "\n";


        /*  pooling & buffering  ================================================================ */

        // NOTE: ArrayPool<T>.Shared is EXTREMELY SLOW especially in Unity environment!!!
        //       seems that it takes a lot of time to process when used simultaneously in same thread

        [ThreadStatic] static char[]? ts_buffer;

        /// <summary>Clear per-thread pooled buffer.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClearPooledBuffer() => ts_buffer = null;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static char[] Inlining_ReuseBufferIfPossible()
        {
            var result = ts_buffer;
            ts_buffer = null;

            if (result == null)
            {
                return new char[InitialCapacity];
            }

            return result;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Inlining_ReturnToPoolIfLargerThanExisting(char[] buffer)
        {
            if (ts_buffer != null && ts_buffer.Length >= buffer.Length)
                return;

            ts_buffer = buffer;
        }


        /*  handler  ================================================================ */

        /// <summary>
        /// Lightning-fast &amp; memory-efficient string builder achieving better performance than <c>DefaultInterpolatedStringHandler</c>.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public struct UltraFastString : IDisposable
        {
            char[] _buffer;
            int _consumed;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal UltraFastString(char[] buffer)
            {
                this._buffer = buffer;
                this._consumed = 0;
            }


            readonly
            public int Length => _consumed < 0 ? 0 : this._consumed;


            /// <summary>Return instance to pool.</summary>
            public void Dispose()
            {
                if (this._consumed < 0)
                    return;

                // to throw exception after dispose
                this._consumed = int.MinValue;

                Inlining_ReturnToPoolIfLargerThanExisting(this._buffer);
            }

            readonly
            void ThrowIfAlreadyDisposed()
            {
                if (this._consumed < 0)
                    throw new InvalidOperationException("has already been disposed");
            }


            /// <summary>Note that clearing content WON'T release internal buffer.</summary>
            public void Clear()
            {
                ThrowIfAlreadyDisposed();
                this._consumed = 0;
            }


            /// <summary>Get resulting string and return instance to pool.</summary>
            public string ToStringAndDispose()
            {
                ThrowIfAlreadyDisposed();

                string result = new(this._buffer.AsSpan(0, this._consumed));
                Dispose();
                return result;
            }


            /*  prevent ToString()  ================================================================ */

#pragma warning disable CS0809
            const string USAGE_HELP = "\nUse `" + nameof(ToStringAndDispose) + "()` method to get result and reuse internal buffer.";
            [Obsolete(USAGE_HELP, false)] public override readonly string ToString() => new(AsSpan());
            [Obsolete(USAGE_HELP, true)] public static implicit operator string(UltraFastString self) => self.ToString();
#pragma warning restore CS0809


            /*  buffer accessor  ================================================================ */

            /// <summary>Current snapshot of internal buffer excluding unused portion.</summary>
            readonly
            public ReadOnlySpan<char> AsSpan() => this._buffer.AsSpan(0, this._consumed);

            /// <summary>Direct reference to internal buffer.</summary>
            readonly
            public (char[] array, int consumed) GetRawBuffer() => (this._buffer, this._consumed);


            /*  capacity handling  ================================================================ */

            /// <summary>
            /// Expand internal buffer capacity by twice or size limited by <see cref="CapacityExpansionLimit"/>.
            /// Resulting buffer promised that has empty space larger than specified size.
            /// </summary>
            void ExpandCapacity(int requiredRemainingLength)
            {
                var bufferToReturn = this._buffer;

                var expansion = CapacityExpansionLimit;  // load property first
                if (expansion > this._buffer.Length)
                    expansion = this._buffer.Length;
                if (expansion < requiredRemainingLength)
                    expansion = requiredRemainingLength;

                this._buffer = new char[this._buffer.Length + expansion];

                // little bit slower --> bufferToReturn.AsSpan(0, Consumed).CopyTo(Buffer);
                Array.Copy(bufferToReturn, this._buffer, this._consumed);

                //worth??
                Inlining_ReturnToPoolIfLargerThanExisting(bufferToReturn);

                /* /// <summary>Return value as is if that's power of 2</summary>
                static int FindNextPowerOfTwo(int value)
                {
                    if ((value & (value - 1)) == 0)
                        return value;

                    // fill lower bits with 1
                    value |= value >> 01;
                    value |= value >> 02;
                    value |= value >> 04;
                    value |= value >> 08;
                    value |= value >> 16;
                    value++;

                    return value;
                }
                */
            }


            /// <summary>
            /// Expand internal buffer if required and return unused portion of internal buffer.
            /// </summary>
            /// <param name="requiredRemainingLength">Required length of data to be written. (not total length of array)</param>
            public Span<char> EnsureRemainingCapacityAndGetSpanToWrite(int requiredRemainingLength)
            {
                int totalLength = this._consumed + requiredRemainingLength;
                if (totalLength > this._buffer.Length)
                {
                    ExpandCapacity(requiredRemainingLength);
                }

                return this._buffer.AsSpan(this._consumed);
            }


            /*  syntax sugar  ================================================================ */

            /// <summary>Add new line char(s) set by <see cref="NewLineChars"/>.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void NewLine() => Append(NewLineChars);


            /*  Append methods  ================================================================ */

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Append(char val) => Append(val, 1);

            public void Append(char val, int repeat)
            {
                if (repeat > this._buffer.Length - this._consumed)
                    ExpandCapacity(repeat);

                this._buffer.AsSpan(this._consumed, repeat).Fill(val);
                this._consumed += repeat;
            }


            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Append(bool val) => Append(val ? "True".AsSpan() : "False".AsSpan());

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Append(string? text) => Append(text.AsSpan());

            public void Append(ReadOnlySpan<char> text)
            {
                if (!text.TryCopyTo(this._buffer.AsSpan(this._consumed)))
                {
                    ExpandCapacity(text.Length);
                    text.CopyTo(this._buffer.AsSpan(this._consumed));
                }
                this._consumed += text.Length;
            }


            // NOTE: unity object has implicit 'bool' cast operator so need to explicitly cast to 'object?'

#if UNITY_5_3_OR_NEWER
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Append(UnityEngine.Object? val, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
                => Append((object?)val, format, provider);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AppendLine(UnityEngine.Object? val, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                Append((object?)val, format, provider);
                NewLine();
            }
#endif

            // NOTE: okay to use estimated possible length
            //       internal buffer is expanded by twice and initial capacity has enough length
            //       going into failsafe code path is rarely edge case
            const int POSSIBLE_FORMATTED_LENGTH = 32;

            // NOTE: 'object?' overload must specify all parameters to avoid method ambiguous error
            public void Append<T>(T val, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                int written = Formatter<T>.Default.WriteUnsafe(val, this._buffer.AsSpan(this._consumed), format, provider);
                if (written < 0)
                {
                    ExpandCapacity(POSSIBLE_FORMATTED_LENGTH);
                    written = Formatter<T>.Default.WriteUnsafe(val, this._buffer.AsSpan(this._consumed), format, provider);
                    if (written < 0)
                    {
                        Append_FailSafe(val, format, provider);
                        return;
                    }
                }

                this._consumed += written;
            }


            // to reduce public Append method code size. don't mind 'format' string allocation. it's edge cases
            void Append_FailSafe(object? obj, ReadOnlySpan<char> format, IFormatProvider? provider)
            {
                if (obj == null)
                    return;

                var text = obj switch
                {
                    string str => str,
                    IFormattable formattable => formattable.ToString(format.Length == 0 ? string.Empty : new(format), provider),
                    _ => obj.ToString(),
                };

                Append(text.AsSpan());
            }


            // perf: fast path for calls from Concat()
            public void Append<T>(T val)
            {
                // perf: This prediction is significantly boost the performance of Concat() method. (20% or more, in nano seconds)
                //       Reason of that, as of Concat() doesn't have format parameter and almost input types are major primitives, however
                //       Append<T>(val, format, provider) overload will call actual append method thru IFormatter<T> interface.
                //       By calling actual method here directly removes unnecessary nested method calls.
                if (typeof(T) == typeof(string))
                {
                    Append(Unsafe.As<T, string>(ref val).AsSpan());
                }

                #pragma warning disable format
                #pragma warning disable IDE2001
                else if (typeof(T) == typeof(int))      Append(Unsafe.As<T, int>(ref val));
                else if (typeof(T) == typeof(float))    Append(Unsafe.As<T, float>(ref val));
                else if (typeof(T) == typeof(long))     Append(Unsafe.As<T, long>(ref val));
                else if (typeof(T) == typeof(double))   Append(Unsafe.As<T, double>(ref val));
                //else if (typeof(T) == typeof(byte))     Append(Unsafe.As<T, byte>(ref val));
                //else if (typeof(T) == typeof(short))    Append(Unsafe.As<T, short>(ref val));
                //else if (typeof(T) == typeof(decimal))  Append(Unsafe.As<T, decimal>(ref val));
                //else if (typeof(T) == typeof(uint))     Append(Unsafe.As<T, uint>(ref val));
                //else if (typeof(T) == typeof(ulong))    Append(Unsafe.As<T, ulong>(ref val));
                //else if (typeof(T) == typeof(ushort))   Append(Unsafe.As<T, ushort>(ref val));
                //else if (typeof(T) == typeof(sbyte))    Append(Unsafe.As<T, sbyte>(ref val));
                #pragma warning restore IDE2001
                #pragma warning restore format

                else
                {
                    Append(val, default, null);
                }
            }


            // perf: integers - no formatting parameter
            public void Append(sbyte val)
            {
                /* =====  copy & paste  ===== */
                int written = FastFormatter.WriteUnsafe(val, this._buffer.AsSpan(this._consumed));
                if (written < 0)
                {
                    ExpandCapacity(POSSIBLE_FORMATTED_LENGTH);
                    written = FastFormatter.WriteUnsafe(val, this._buffer.AsSpan(this._consumed));
                    if (written < 0)
                    {
                        Append_FailSafe(val, default, null);
                        return;
                    }
                }
                this._consumed += written;
            }
            public void Append(byte val)
            {
                /* =====  copy & paste  ===== */
                int written = FastFormatter.WriteUnsafe(val, this._buffer.AsSpan(this._consumed));
                if (written < 0)
                {
                    ExpandCapacity(POSSIBLE_FORMATTED_LENGTH);
                    written = FastFormatter.WriteUnsafe(val, this._buffer.AsSpan(this._consumed));
                    if (written < 0)
                    {
                        Append_FailSafe(val, default, null);
                        return;
                    }
                }
                this._consumed += written;
            }
            public void Append(short val)
            {
                /* =====  copy & paste  ===== */
                int written = FastFormatter.WriteUnsafe(val, this._buffer.AsSpan(this._consumed));
                if (written < 0)
                {
                    ExpandCapacity(POSSIBLE_FORMATTED_LENGTH);
                    written = FastFormatter.WriteUnsafe(val, this._buffer.AsSpan(this._consumed));
                    if (written < 0)
                    {
                        Append_FailSafe(val, default, null);
                        return;
                    }
                }
                this._consumed += written;
            }
            public void Append(ushort val)
            {
                /* =====  copy & paste  ===== */
                int written = FastFormatter.WriteUnsafe(val, this._buffer.AsSpan(this._consumed));
                if (written < 0)
                {
                    ExpandCapacity(POSSIBLE_FORMATTED_LENGTH);
                    written = FastFormatter.WriteUnsafe(val, this._buffer.AsSpan(this._consumed));
                    if (written < 0)
                    {
                        Append_FailSafe(val, default, null);
                        return;
                    }
                }
                this._consumed += written;
            }
            public void Append(int val)
            {
                /* =====  copy & paste  ===== */
                int written = FastFormatter.WriteUnsafe(val, this._buffer.AsSpan(this._consumed));
                if (written < 0)
                {
                    ExpandCapacity(POSSIBLE_FORMATTED_LENGTH);
                    written = FastFormatter.WriteUnsafe(val, this._buffer.AsSpan(this._consumed));
                    if (written < 0)
                    {
                        Append_FailSafe(val, default, null);
                        return;
                    }
                }
                this._consumed += written;
            }
            public void Append(uint val)
            {
                /* =====  copy & paste  ===== */
                int written = FastFormatter.WriteUnsafe(val, this._buffer.AsSpan(this._consumed));
                if (written < 0)
                {
                    ExpandCapacity(POSSIBLE_FORMATTED_LENGTH);
                    written = FastFormatter.WriteUnsafe(val, this._buffer.AsSpan(this._consumed));
                    if (written < 0)
                    {
                        Append_FailSafe(val, default, null);
                        return;
                    }
                }
                this._consumed += written;
            }
            public void Append(long val)
            {
                /* =====  copy & paste  ===== */
                int written = FastFormatter.WriteUnsafe(val, this._buffer.AsSpan(this._consumed));
                if (written < 0)
                {
                    ExpandCapacity(POSSIBLE_FORMATTED_LENGTH);
                    written = FastFormatter.WriteUnsafe(val, this._buffer.AsSpan(this._consumed));
                    if (written < 0)
                    {
                        Append_FailSafe(val, default, null);
                        return;
                    }
                }
                this._consumed += written;
            }
            public void Append(ulong val)
            {
                /* =====  copy & paste  ===== */
                int written = FastFormatter.WriteUnsafe(val, this._buffer.AsSpan(this._consumed));
                if (written < 0)
                {
                    ExpandCapacity(POSSIBLE_FORMATTED_LENGTH);
                    written = FastFormatter.WriteUnsafe(val, this._buffer.AsSpan(this._consumed));
                    if (written < 0)
                    {
                        Append_FailSafe(val, default, null);
                        return;
                    }
                }
                this._consumed += written;
            }
            // perf: floating points
            public void Append(float val)
            {
                /* =====  copy & paste  ===== */
                if (!val.TryFormat(this._buffer.AsSpan(this._consumed), out var written))
                {
                    ExpandCapacity(POSSIBLE_FORMATTED_LENGTH);
                    if (!val.TryFormat(this._buffer.AsSpan(this._consumed), out written))
                    {
                        Append_FailSafe(val, default, null);
                        return;
                    }
                }
                this._consumed += written;
            }
            public void Append(double val)
            {
                /* =====  copy & paste  ===== */
                if (!val.TryFormat(this._buffer.AsSpan(this._consumed), out var written))
                {
                    ExpandCapacity(POSSIBLE_FORMATTED_LENGTH);
                    if (!val.TryFormat(this._buffer.AsSpan(this._consumed), out written))
                    {
                        Append_FailSafe(val, default, null);
                        return;
                    }
                }
                this._consumed += written;
            }
            public void Append(decimal val)
            {
                /* =====  copy & paste  ===== */
                if (!val.TryFormat(this._buffer.AsSpan(this._consumed), out var written))
                {
                    ExpandCapacity(POSSIBLE_FORMATTED_LENGTH);
                    if (!val.TryFormat(this._buffer.AsSpan(this._consumed), out written))
                    {
                        Append_FailSafe(val, default, null);
                        return;
                    }
                }
                this._consumed += written;
            }


            #pragma warning disable format
            #pragma warning disable IDE2001

            // newline
            [MethodImpl(MIO.AggressiveInlining)] public void AppendLine(char val, int repeat) { Append(val, repeat); NewLine(); }
            [MethodImpl(MIO.AggressiveInlining)] public void AppendLine(char val) { Append(val); NewLine(); }
            [MethodImpl(MIO.AggressiveInlining)] public void AppendLine(bool val) { Append(val); NewLine(); }
            [MethodImpl(MIO.AggressiveInlining)] public void AppendLine(string? val) { Append(val); NewLine(); }
            [MethodImpl(MIO.AggressiveInlining)] public void AppendLine(ReadOnlySpan<char> val) { Append(val); NewLine(); }

            [MethodImpl(MIO.AggressiveInlining)] public void AppendLine(sbyte val) { Append(val); NewLine(); }
            [MethodImpl(MIO.AggressiveInlining)] public void AppendLine(byte val) { Append(val); NewLine(); }
            [MethodImpl(MIO.AggressiveInlining)] public void AppendLine(short val) { Append(val); NewLine(); }
            [MethodImpl(MIO.AggressiveInlining)] public void AppendLine(ushort val) { Append(val); NewLine(); }
            [MethodImpl(MIO.AggressiveInlining)] public void AppendLine(int val) { Append(val); NewLine(); }
            [MethodImpl(MIO.AggressiveInlining)] public void AppendLine(uint val) { Append(val); NewLine(); }
            [MethodImpl(MIO.AggressiveInlining)] public void AppendLine(long val) { Append(val); NewLine(); }
            [MethodImpl(MIO.AggressiveInlining)] public void AppendLine(ulong val) { Append(val); NewLine(); }

            [MethodImpl(MIO.AggressiveInlining)] public void AppendLine(float val) { Append(val); NewLine(); }
            [MethodImpl(MIO.AggressiveInlining)] public void AppendLine(double val) { Append(val); NewLine(); }
            [MethodImpl(MIO.AggressiveInlining)] public void AppendLine(decimal val) { Append(val); NewLine(); }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AppendLine<T>(T val, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                Append(val, format, provider);
                NewLine();
            }

            #pragma warning restore IDE2001
            #pragma warning restore format

        }  // end of internal helper


        /*  EqualityComparer like implementation  ================================================================ */

        // NOTE: contravariant is required to reduce formatter instances created for unsupported object types
        public interface IFormatter<in T>
        {
            /// <returns><c>-1</c> if span doesn't have enough capacity</returns>
            int WriteUnsafe(T value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null);
        }

        public abstract class Formatter<T> : IFormatter<T>
        {
            // perf: static field will be devirtualized in runtime
            //       https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Buffers/ArrayPool.cs#L23
            public readonly static IFormatter<T> Default;

            static Formatter()
            {
                var formatter = CreateFormatter(typeof(T));
                Default = formatter as IFormatter<T>
                    ?? throw new Exception($"unknown formatter was created for '{typeof(T).Name}': {formatter}");
            }

            abstract public int WriteUnsafe(T value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null);
        }


        // NOTE: to remove large code from static .ctor in generic type (Formatter<T>)
        //       * don't make this method generic. it bloats code size. return as object and cast it to Formatter<T> in .ctor
        static object CreateFormatter(Type type)
        {
            if (type == typeof(string))
                return new StringFormatter();

            if (type == typeof(bool))
                return new BooleanFormatter();

            if (type == typeof(char))
                return new CharFormatter();


            //if (type == typeof(Enum))
            //    return new EnumFormatter<Enum>();

            //if (type == typeof(ValueType))
            //    return new ValueTypeFormatter<ValueType>();


            if (type == typeof(IFormattable))
                return new FormattableFormatter<IFormattable>();

            if (type == typeof(object))
                return new ObjectFormatter<object>();


            #region ////////  Generated Factory Source Code  ////////
            #pragma warning disable format
            #pragma warning disable IDE2001

            if (type == typeof(sbyte)) return new SByteFormatter(); 
            if (type == typeof(byte)) return new ByteFormatter(); 
            if (type == typeof(short)) return new ShortFormatter(); 
            if (type == typeof(ushort)) return new UShortFormatter(); 
            if (type == typeof(int)) return new IntFormatter(); 
            if (type == typeof(uint)) return new UIntFormatter(); 
            if (type == typeof(long)) return new LongFormatter(); 
            if (type == typeof(ulong)) return new ULongFormatter(); 
            if (type == typeof(float)) return new FloatFormatter();
            if (type == typeof(double)) return new DoubleFormatter();
            if (type == typeof(decimal)) return new DecimalFormatter();
            if (type == typeof(TimeSpan)) return new TimeSpanFormatter();
            if (type == typeof(DateTime)) return new DateTimeFormatter();
            if (type == typeof(DateTimeOffset)) return new DateTimeOffsetFormatter();
            if (type == typeof(Guid)) return new GuidFormatter();

            #pragma warning restore IDE2001
            #pragma warning restore format
            #endregion


            // NOTE: must be checked before IFormattable due to Enum type implements it
            if (type.IsEnum)
            {
                var enumFormatter = OverrideEnumFormatter ?? typeof(EnumFormatter<>);

                return Activator.CreateInstance(enumFormatter.MakeGenericType(type))
                    ?? throw new Exception("cannot instantiate Enum formatter: " + type.Name);
            }


            // NOTE: check interfaces before concrete type fallbacks
            var isValueType = type.IsValueType;

            //IFormattable?
            if (typeof(IFormattable).IsAssignableFrom(type))
            {
                var formattableFormatter = OverrideFormattableFormatter;
                if (isValueType)
                {
                    // type variance is not supported
                    formattableFormatter = typeof(FormattableFormatter<>);
                }
                else
                {
                    if (formattableFormatter == null)
                        return Formatter<IFormattable>.Default;
                }

                return Activator.CreateInstance(formattableFormatter.MakeGenericType(type))
                    ?? throw new Exception("cannot instantiate IFormattable formatter: " + type.Name);
            }


            if (isValueType)
            {
                //nullable??
                var underlying = Nullable.GetUnderlyingType(type);
                if (underlying != null)
                {
                    var nullableFormatter = OverrideNullableFormatter ?? typeof(NullableFormatter<>);

                    return Activator.CreateInstance(nullableFormatter.MakeGenericType(underlying))
                        ?? throw new Exception("cannot instantiate Nullable formatter: " + type.Name);
                }

                // NOTE: value type cannot use contravariant formatter
                // can't!! --> return Formatter<ValueType>.Default;

                var valueTypeFormatter = OverrideValueTypeFormatter ?? typeof(ValueTypeFormatter<>);

                return Activator.CreateInstance(valueTypeFormatter.MakeGenericType(type))
                    ?? throw new Exception("cannot instantiate ValueType formatter: " + type.Name);
            }


            //object!!
            var objectFormatter = OverrideObjectFormatter;
            if (objectFormatter == null)
                return Formatter<object>.Default;

            return Activator.CreateInstance(objectFormatter.MakeGenericType(type))
                ?? throw new Exception("cannot instantiate Object formatter: " + type.Name);
        }


        //override
        public static Type? OverrideEnumFormatter { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; }
        public static Type? OverrideFormattableFormatter { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; }
        public static Type? OverrideNullableFormatter { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; }
        public static Type? OverrideValueTypeFormatter { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; }
        public static Type? OverrideObjectFormatter { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; }


        /* =====  default formatters  ===== */

        sealed class StringFormatter : Formatter<string>
        {
            public override int WriteUnsafe(string? value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (value == null)
                    return 0;

                if (value.Length > span.Length)
                    return -1;

                value.AsSpan().CopyTo(span);
                return value.Length;
            }
        }

        sealed class BooleanFormatter : Formatter<bool>
        {
            public override int WriteUnsafe(bool value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                const string TRUE = "True";
                const string FALSE = "False";

                if (value)
                {
                    if (TRUE.Length > span.Length)
                        return -1;

                    TRUE.AsSpan().CopyTo(span);
                    return TRUE.Length;
                }
                else
                {
                    if (FALSE.Length > span.Length)
                        return -1;

                    FALSE.AsSpan().CopyTo(span);
                    return FALSE.Length;
                }
            }
        }

        sealed class CharFormatter : Formatter<char>
        {
            public override int WriteUnsafe(char value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (span.Length == 0)
                    return -1;

                span[0] = value;
                return 1;
            }
        }


        // TODO: formatter for ISpanFormattable
        sealed class FormattableFormatter<T> : Formatter<T> where T : IFormattable
        {
            public override int WriteUnsafe(T/*?*/ value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (!typeof(T).IsValueType)
                {
                    if (value == null)
                        return 0;
                }

                var text = value.ToString(format.Length == 0 ? string.Empty : new(format), provider);
                if (text.Length > span.Length)
                    return -1;

                text.AsSpan().CopyTo(span);
                return text.Length;
            }
        }

        sealed class ObjectFormatter<T> : Formatter<T>
        {
            public override int WriteUnsafe(T? value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (value == null)
                    return 0;

                var text = value.ToString() ?? string.Empty;
                if (text.Length > span.Length)
                    return -1;

                text.AsSpan().CopyTo(span);
                return text.Length;
            }
        }


        // NOTE: to allow unsupported value types use same formatter instance, constrain to 'notnull' instead of 'struct'
        sealed class ValueTypeFormatter<T> : Formatter<T> where T : notnull //struct
        {
            public override int WriteUnsafe(T value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                var text = value.ToString();
                if (text == null)
                    return 0;

                if (text.Length > span.Length)
                    return -1;

                text.AsSpan().CopyTo(span);
                return text.Length;
            }
        }

        sealed class NullableFormatter<T> : Formatter<Nullable<T>> where T : struct
        {
            public override int WriteUnsafe(Nullable<T> value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (value == null)
                    return 0;

                return Formatter<T>.Default.WriteUnsafe(value.Value, span, format, provider);
            }
        }


        sealed class EnumFormatter<T> : Formatter<T> where T : struct, Enum
        {
            public override int WriteUnsafe(T value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                // NOTE: .net 7 doesn't have TryFormat
                //       https://github.com/dotnet/runtime/blob/v7.0.0/src/libraries/System.Private.CoreLib/src/System/Enum.cs
#if NET8_0_OR_GREATER

                // cannot use in .NET standard 2.1...
                if (Enum.TryFormat(value, span, out var written, format))//, provider))
                    return written;
                else
                    return -1;

#elif STMG_USTRING_USE_STRICT_ENUM

                if (StrictEnum.TryFormat(value, span, out var written, format, provider))
                    return written;
                else
                    return -1;
#else
                var text = value.ToString(format.Length == 0 ? string.Empty : format.ToString());//, provider);
                if (text.Length > span.Length)
                    return -1;

                text.AsSpan().CopyTo(span);
                return text.Length;
#endif
            }
        }


        #region ////////  Generated Formatter Source Code  ////////

        sealed class SByteFormatter : Formatter<sbyte>
        {
            public override int WriteUnsafe(sbyte value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (format.Length == 0)
                    return FastFormatter.WriteUnsafe(value, span);

                if (value.TryFormat(span, out var written, format, provider))
                    return written;
                else
                    return -1;
            }
        }

        sealed class ByteFormatter : Formatter<byte>
        {
            public override int WriteUnsafe(byte value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (format.Length == 0)
                    return FastFormatter.WriteUnsafe(value, span);

                if (value.TryFormat(span, out var written, format, provider))
                    return written;
                else
                    return -1;
            }
        }

        sealed class ShortFormatter : Formatter<short>
        {
            public override int WriteUnsafe(short value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (format.Length == 0)
                    return FastFormatter.WriteUnsafe(value, span);

                if (value.TryFormat(span, out var written, format, provider))
                    return written;
                else
                    return -1;
            }
        }

        sealed class UShortFormatter : Formatter<ushort>
        {
            public override int WriteUnsafe(ushort value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (format.Length == 0)
                    return FastFormatter.WriteUnsafe(value, span);

                if (value.TryFormat(span, out var written, format, provider))
                    return written;
                else
                    return -1;
            }
        }

        sealed class IntFormatter : Formatter<int>
        {
            public override int WriteUnsafe(int value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (format.Length == 0)
                    return FastFormatter.WriteUnsafe(value, span);

                if (value.TryFormat(span, out var written, format, provider))
                    return written;
                else
                    return -1;
            }
        }

        sealed class UIntFormatter : Formatter<uint>
        {
            public override int WriteUnsafe(uint value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (format.Length == 0)
                    return FastFormatter.WriteUnsafe(value, span);

                if (value.TryFormat(span, out var written, format, provider))
                    return written;
                else
                    return -1;
            }
        }

        sealed class LongFormatter : Formatter<long>
        {
            public override int WriteUnsafe(long value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (format.Length == 0)
                    return FastFormatter.WriteUnsafe(value, span);

                if (value.TryFormat(span, out var written, format, provider))
                    return written;
                else
                    return -1;
            }
        }

        sealed class ULongFormatter : Formatter<ulong>
        {
            public override int WriteUnsafe(ulong value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (format.Length == 0)
                    return FastFormatter.WriteUnsafe(value, span);

                if (value.TryFormat(span, out var written, format, provider))
                    return written;
                else
                    return -1;
            }
        }

        sealed class FloatFormatter : Formatter<float>
        {
            public override int WriteUnsafe(float value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (value.TryFormat(span, out var written, format, provider))
                    return written;
                else
                    return -1;
            }
        }

        sealed class DoubleFormatter : Formatter<double>
        {
            public override int WriteUnsafe(double value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (value.TryFormat(span, out var written, format, provider))
                    return written;
                else
                    return -1;
            }
        }

        sealed class DecimalFormatter : Formatter<decimal>
        {
            public override int WriteUnsafe(decimal value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (value.TryFormat(span, out var written, format, provider))
                    return written;
                else
                    return -1;
            }
        }

        sealed class TimeSpanFormatter : Formatter<TimeSpan>
        {
            public override int WriteUnsafe(TimeSpan value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (value.TryFormat(span, out var written, format, provider))
                    return written;
                else
                    return -1;
            }
        }

        sealed class DateTimeFormatter : Formatter<DateTime>
        {
            public override int WriteUnsafe(DateTime value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (value.TryFormat(span, out var written, format, provider))
                    return written;
                else
                    return -1;
            }
        }

        sealed class DateTimeOffsetFormatter : Formatter<DateTimeOffset>
        {
            public override int WriteUnsafe(DateTimeOffset value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (value.TryFormat(span, out var written, format, provider))
                    return written;
                else
                    return -1;
            }
        }

        sealed class GuidFormatter : Formatter<Guid>
        {
            public override int WriteUnsafe(Guid value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (value.TryFormat(span, out var written, format))//, provider))
                    return written;
                else
                    return -1;
            }
        }

        #endregion


        /*  fast integer writer  ================================================================ */

        public static class FastFormatter
        {
            /// <summary>Threshold for integer fast path.</summary>
            public const int INT_MAX = 9999999;  // NOTE: hard coded limit of unrolled loops (min/max exclusive)
                                                 //       this value must be greater than short.MaxValue cuz no range check in short/ushort overload!!
                                                 //       * see the benchmark result to find best threshold
            /// <inheritdoc cref="INT_MAX"/>
            public const int INT_MIN = -INT_MAX;


            /// <summary>
            /// Write integer as a text pretty fast.
            /// </summary>
            /// <returns><c>-1</c> if span doesn't have enough capacity</returns>
            public static int WriteUnsafe(int value, Span<char> enoughCapacity)
            {
                const int CHARCODE_OFFSET = (int)'0';

                int charsWritten;

                if (value > INT_MAX || value < INT_MIN)
                {
                    value.TryFormat(enoughCapacity, out charsWritten);
                    return charsWritten;
                }

                charsWritten = 0;
                int remaining = enoughCapacity.Length;

                // NOTE: one liner 'span[charsWritten++] = ...' will increase code size 1 byte!!
                if (value < 0)
                {
                    if (remaining < 1)
                        return -1;
                    remaining--;

                    enoughCapacity[charsWritten] = '-';
                    charsWritten++;
                    value = -value;
                }

                int pow;
                int div;


                /* =====  generated by loop unroller (see Benchmark/Program.cs)  ===== */

                #pragma warning disable IDE2001
                #pragma warning disable format
                if (value < 10) if (remaining < 1) return -1; else goto DIGIT_1;
                if (value < 100) if (remaining < 2) return -1; else goto DIGIT_2;
                if (value < 1000) if (remaining < 3) return -1; else goto DIGIT_3;
                if (value < 10000) if (remaining < 4) return -1; else goto DIGIT_4;
                if (value < 100000) if (remaining < 5) return -1; else goto DIGIT_5;
                if (value < 1000000) if (remaining < 6) return -1; else goto DIGIT_6;
                #pragma warning restore format
                #pragma warning restore IDE2001

                if (remaining < 7)
                    return -1;

                // max possible digits //DIGIT_7:
                pow = 1000000;
                value -= ((((div = value / pow))) * pow);
                // your code here
                enoughCapacity[charsWritten] = (char)(div + CHARCODE_OFFSET);
                charsWritten++;

            DIGIT_6:
                pow = 100000;
                value -= ((((div = value / pow))) * pow);
                // your code here
                enoughCapacity[charsWritten] = (char)(div + CHARCODE_OFFSET);
                charsWritten++;

            DIGIT_5:
                pow = 10000;
                value -= ((((div = value / pow))) * pow);
                // your code here
                enoughCapacity[charsWritten] = (char)(div + CHARCODE_OFFSET);
                charsWritten++;

            DIGIT_4:
                pow = 1000;
                value -= ((((div = value / pow))) * pow);
                // your code here
                enoughCapacity[charsWritten] = (char)(div + CHARCODE_OFFSET);
                charsWritten++;

            DIGIT_3:
                pow = 100;
                value -= ((((div = value / pow))) * pow);
                // your code here
                enoughCapacity[charsWritten] = (char)(div + CHARCODE_OFFSET);
                charsWritten++;

            DIGIT_2:
                pow = 10;
                value -= ((((div = value / pow))) * pow);
                // your code here
                enoughCapacity[charsWritten] = (char)(div + CHARCODE_OFFSET);
                charsWritten++;

            DIGIT_1:  /* 'mod' has least digit */
                // your code here
                enoughCapacity[charsWritten] = (char)(value + CHARCODE_OFFSET);
                charsWritten++;

                /* =====  end of loop unroller  ===== */

                return charsWritten;
            }


            /// <inheritdoc cref="WriteUnsafe(int, Span{char})"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int WriteUnsafe(uint value, Span<char> enoughCapacity)
            {
                unchecked
                {
                    if ((uint)value <= (uint)INT_MAX)
                    {
                        return WriteUnsafe((int)value, enoughCapacity);
                    }
                    else
                    {
                        value.TryFormat(enoughCapacity, out var written);
                        return written;
                    }
                }
            }

            /// <inheritdoc cref="WriteUnsafe(int, Span{char})"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int WriteUnsafe(long value, Span<char> enoughCapacity)
            {
                unchecked
                {
                    if (value <= INT_MAX && value >= INT_MIN)
                    {
                        return WriteUnsafe((int)value, enoughCapacity);
                    }
                    else
                    {
                        value.TryFormat(enoughCapacity, out var written);
                        return written;
                    }
                }
            }

            /// <inheritdoc cref="WriteUnsafe(int, Span{char})"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int WriteUnsafe(ulong value, Span<char> enoughCapacity)
            {
                unchecked
                {
                    if ((uint)value <= (uint)INT_MAX)
                    {
                        return WriteUnsafe((int)value, enoughCapacity);
                    }
                    else
                    {
                        value.TryFormat(enoughCapacity, out var written);
                        return written;
                    }
                }
            }

        }


        /*  major use cases  ================================================================ */

        #region ////////  Generated Concat() Overloads  ////////
        #pragma warning disable format
        #pragma warning disable IDE2001
        #pragma warning disable SMA0040

        public static string Concat<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12) { var us = Rent(); try { us.Append(v1); us.Append(v2); us.Append(v3); us.Append(v4); us.Append(v5); us.Append(v6); us.Append(v7); us.Append(v8); us.Append(v9); us.Append(v10); us.Append(v11); us.Append(v12); return us.ToStringAndDispose(); } catch { us.Dispose(); throw; } }
        public static string Concat<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11) { var us = Rent(); try { us.Append(v1); us.Append(v2); us.Append(v3); us.Append(v4); us.Append(v5); us.Append(v6); us.Append(v7); us.Append(v8); us.Append(v9); us.Append(v10); us.Append(v11); return us.ToStringAndDispose(); } catch { us.Dispose(); throw; } }
        public static string Concat<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10) { var us = Rent(); try { us.Append(v1); us.Append(v2); us.Append(v3); us.Append(v4); us.Append(v5); us.Append(v6); us.Append(v7); us.Append(v8); us.Append(v9); us.Append(v10); return us.ToStringAndDispose(); } catch { us.Dispose(); throw; } }
        public static string Concat<T1, T2, T3, T4, T5, T6, T7, T8, T9>(T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9) { var us = Rent(); try { us.Append(v1); us.Append(v2); us.Append(v3); us.Append(v4); us.Append(v5); us.Append(v6); us.Append(v7); us.Append(v8); us.Append(v9); return us.ToStringAndDispose(); } catch { us.Dispose(); throw; } }
        public static string Concat<T1, T2, T3, T4, T5, T6, T7, T8>(T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8) { var us = Rent(); try { us.Append(v1); us.Append(v2); us.Append(v3); us.Append(v4); us.Append(v5); us.Append(v6); us.Append(v7); us.Append(v8); return us.ToStringAndDispose(); } catch { us.Dispose(); throw; } }
        public static string Concat<T1, T2, T3, T4, T5, T6, T7>(T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7) { var us = Rent(); try { us.Append(v1); us.Append(v2); us.Append(v3); us.Append(v4); us.Append(v5); us.Append(v6); us.Append(v7); return us.ToStringAndDispose(); } catch { us.Dispose(); throw; } }
        public static string Concat<T1, T2, T3, T4, T5, T6>(T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6) { var us = Rent(); try { us.Append(v1); us.Append(v2); us.Append(v3); us.Append(v4); us.Append(v5); us.Append(v6); return us.ToStringAndDispose(); } catch { us.Dispose(); throw; } }
        public static string Concat<T1, T2, T3, T4, T5>(T1 v1, T2 v2, T3 v3, T4 v4, T5 v5) { var us = Rent(); try { us.Append(v1); us.Append(v2); us.Append(v3); us.Append(v4); us.Append(v5); return us.ToStringAndDispose(); } catch { us.Dispose(); throw; } }
        public static string Concat<T1, T2, T3, T4>(T1 v1, T2 v2, T3 v3, T4 v4) { var us = Rent(); try { us.Append(v1); us.Append(v2); us.Append(v3); us.Append(v4); return us.ToStringAndDispose(); } catch { us.Dispose(); throw; } }
        public static string Concat<T1, T2, T3>(T1 v1, T2 v2, T3 v3) { var us = Rent(); try { us.Append(v1); us.Append(v2); us.Append(v3); return us.ToStringAndDispose(); } catch { us.Dispose(); throw; } }
        public static string Concat<T1, T2>(T1 v1, T2 v2) { var us = Rent(); try { us.Append(v1); us.Append(v2); return us.ToStringAndDispose(); } catch { us.Dispose(); throw; } }
        public static string Concat<T1>(T1 v1) { var us = Rent(); try { us.Append(v1); return us.ToStringAndDispose(); } catch { us.Dispose(); throw; } }

        #pragma warning restore SMA0040
        #pragma warning restore IDE2001
        #pragma warning restore format
        #endregion


        /*  Unity TextMesh Pro extensions  ================================================================ */

#if STMG_TEXTMESHPRO_EXISTS

        #region ////////  Generated TextMeshPro Overloads  ////////
        #pragma warning disable format
        #pragma warning disable IDE2001

        public static void SetTextNonAlloc<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(this global::TMPro.TMP_Text textMeshText, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11, T12 v12) { using var us = Rent(); us.Append(v1); us.Append(v2); us.Append(v3); us.Append(v4); us.Append(v5); us.Append(v6); us.Append(v7); us.Append(v8); us.Append(v9); us.Append(v10); us.Append(v11); us.Append(v12); var (buffer, consumed) = us.GetRawBuffer(); textMeshText.SetCharArray(buffer, 0, consumed); }
        public static void SetTextNonAlloc<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(this global::TMPro.TMP_Text textMeshText, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10, T11 v11) { using var us = Rent(); us.Append(v1); us.Append(v2); us.Append(v3); us.Append(v4); us.Append(v5); us.Append(v6); us.Append(v7); us.Append(v8); us.Append(v9); us.Append(v10); us.Append(v11); var (buffer, consumed) = us.GetRawBuffer(); textMeshText.SetCharArray(buffer, 0, consumed); }
        public static void SetTextNonAlloc<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this global::TMPro.TMP_Text textMeshText, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9, T10 v10) { using var us = Rent(); us.Append(v1); us.Append(v2); us.Append(v3); us.Append(v4); us.Append(v5); us.Append(v6); us.Append(v7); us.Append(v8); us.Append(v9); us.Append(v10); var (buffer, consumed) = us.GetRawBuffer(); textMeshText.SetCharArray(buffer, 0, consumed); }
        public static void SetTextNonAlloc<T1, T2, T3, T4, T5, T6, T7, T8, T9>(this global::TMPro.TMP_Text textMeshText, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8, T9 v9) { using var us = Rent(); us.Append(v1); us.Append(v2); us.Append(v3); us.Append(v4); us.Append(v5); us.Append(v6); us.Append(v7); us.Append(v8); us.Append(v9); var (buffer, consumed) = us.GetRawBuffer(); textMeshText.SetCharArray(buffer, 0, consumed); }
        public static void SetTextNonAlloc<T1, T2, T3, T4, T5, T6, T7, T8>(this global::TMPro.TMP_Text textMeshText, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7, T8 v8) { using var us = Rent(); us.Append(v1); us.Append(v2); us.Append(v3); us.Append(v4); us.Append(v5); us.Append(v6); us.Append(v7); us.Append(v8); var (buffer, consumed) = us.GetRawBuffer(); textMeshText.SetCharArray(buffer, 0, consumed); }
        public static void SetTextNonAlloc<T1, T2, T3, T4, T5, T6, T7>(this global::TMPro.TMP_Text textMeshText, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6, T7 v7) { using var us = Rent(); us.Append(v1); us.Append(v2); us.Append(v3); us.Append(v4); us.Append(v5); us.Append(v6); us.Append(v7); var (buffer, consumed) = us.GetRawBuffer(); textMeshText.SetCharArray(buffer, 0, consumed); }
        public static void SetTextNonAlloc<T1, T2, T3, T4, T5, T6>(this global::TMPro.TMP_Text textMeshText, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6) { using var us = Rent(); us.Append(v1); us.Append(v2); us.Append(v3); us.Append(v4); us.Append(v5); us.Append(v6); var (buffer, consumed) = us.GetRawBuffer(); textMeshText.SetCharArray(buffer, 0, consumed); }
        public static void SetTextNonAlloc<T1, T2, T3, T4, T5>(this global::TMPro.TMP_Text textMeshText, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5) { using var us = Rent(); us.Append(v1); us.Append(v2); us.Append(v3); us.Append(v4); us.Append(v5); var (buffer, consumed) = us.GetRawBuffer(); textMeshText.SetCharArray(buffer, 0, consumed); }
        public static void SetTextNonAlloc<T1, T2, T3, T4>(this global::TMPro.TMP_Text textMeshText, T1 v1, T2 v2, T3 v3, T4 v4) { using var us = Rent(); us.Append(v1); us.Append(v2); us.Append(v3); us.Append(v4); var (buffer, consumed) = us.GetRawBuffer(); textMeshText.SetCharArray(buffer, 0, consumed); }
        public static void SetTextNonAlloc<T1, T2, T3>(this global::TMPro.TMP_Text textMeshText, T1 v1, T2 v2, T3 v3) { using var us = Rent(); us.Append(v1); us.Append(v2); us.Append(v3); var (buffer, consumed) = us.GetRawBuffer(); textMeshText.SetCharArray(buffer, 0, consumed); }
        public static void SetTextNonAlloc<T1, T2>(this global::TMPro.TMP_Text textMeshText, T1 v1, T2 v2) { using var us = Rent(); us.Append(v1); us.Append(v2); var (buffer, consumed) = us.GetRawBuffer(); textMeshText.SetCharArray(buffer, 0, consumed); }
        public static void SetTextNonAlloc<T1>(this global::TMPro.TMP_Text textMeshText, T1 v1) { using var us = Rent(); us.Append(v1); var (buffer, consumed) = us.GetRawBuffer(); textMeshText.SetCharArray(buffer, 0, consumed); }

        #pragma warning restore IDE2001
        #pragma warning restore format
        #endregion

#endif

    }
}



#pragma warning disable SMA0040

#region ////////  TEMPLATE: Unity Editor Tests  ////////

#if UNITY_EDITOR

#pragma warning disable IDE1006

// TEMPLATE: namespace naming convention: <TEST_TARGET>.<ROOT_MENU_LABEL>.<SUB_MENU_LABEL>
namespace SatorImaging.UnityFundamentals.TEST.U_String  // must be unique. don't reuse existing namespace
{
    static class UNITY_EDITOR_TESTS  // don't change
    {
        const string MENU_ROOT = nameof(TEST) + "/" + nameof(U_String) + "/";


        #region ////////  TEMPLATE: Test Methods  ////////
        /*  TEMPLATE: Test Methods  ================================================================ */

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Basic_Tests), priority = 0)]
        [Test]
        public static void Basic_Tests()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            UString.UltraFastString us;

            us = UString.Rent();
            us.Append("Value: ");
            us.Append(310);
            UnityEngine.Debug.Log(string.Join(", ", us.ToStringAndDispose().ToCharArray().Select(x => ((byte)x).ToString("X2"))));

            // implicit cast?

            us = UString.Rent();
            us.Append("Value: ");
            us.Append(310);
            Assert.That(us.ToStringAndDispose(), Is.EqualTo("Value: 310"));
            us = UString.Rent("Value: ");
            us.Append(310);
            Assert.That(us.ToStringAndDispose(), Is.EqualTo("Value: 310"));

            // return()?
            us = UString.Rent("Value: ");
            us.Append(310, "D5");
            Assert.That(us.ToStringAndDispose(), Is.EqualTo("Value: 00310"));

            //char??
            us = UString.Rent();
            us.Append('A', 7);
            Assert.That(us.ToStringAndDispose(), Is.EqualTo("AAAAAAA"));


            //newLine?
            var restoreNewLine = UString.NewLineChars;

            us = UString.Rent("Value: ");
            us.Append(310, "D5");
            us.NewLine();
            Assert.That(us.ToStringAndDispose(), Is.EqualTo("Value: 00310\n"));

            UString.NewLineChars = "\r\n";
            us = UString.Rent("Value: ");
            us.Append(310, "D5");
            us.NewLine();
            Assert.That(us.ToStringAndDispose(), Is.EqualTo("Value: 00310\r\n"));

            UString.NewLineChars = restoreNewLine;


            //concat??
            Assert.That(UString.Concat("A", "B ", "C", -1, 1.1f), Is.EqualTo("AB C-11.1"));


            //formatter?
            for (int i = 0; i < 10000; i++)
            {
                Assert.That(UString.ToCharSpan(i, "D5").ToString(), Is.EqualTo(i.ToString("D5")));
                Assert.That(StringSplitOptions.RemoveEmptyEntries.ToCharSpan().ToString(), Is.EqualTo(nameof(StringSplitOptions.RemoveEmptyEntries)));
            }


            //overload??
            us = UString.Rent();
            us.Append(1.1);
            us.Append(-2.2m);
            us.Append(-3.3f, "");
            Assert.That(us.ToStringAndDispose(), Is.EqualTo("1.1-2.2-3.3"));


            // can append text larger than initial capacity?
            us = UString.Rent();
            us.Append(new string('@', UString.InitialCapacity * 2));
            Assert.That(us.ToStringAndDispose(), Is.EqualTo(new string('@', UString.InitialCapacity * 2)));


            // warning on IDE?
            using (var sb = UString.Rent())
            {
                sb.Append("1234567890");
                _ = sb.ToString() == "not";  // remove .ToString will show error
                sb.ToString();
                sb.ToString();

                var (array, consumed) = sb.GetRawBuffer();
                Assert.That(consumed, Is.EqualTo(10));
                Assert.That(array.AsSpan(0, consumed).ToString(), Is.EqualTo("1234567890"));
            }


            //exception?
            var disposeCanCallMultipleTimes = UString.Rent();
            disposeCanCallMultipleTimes.Dispose();
            disposeCanCallMultipleTimes.Dispose();
            Assert.That(() => disposeCanCallMultipleTimes.ToStringAndDispose(), Throws.TypeOf<InvalidOperationException>());

            var toStringTwice = UString.Rent();
            toStringTwice.ToStringAndDispose();
            Assert.That(() => toStringTwice.ToStringAndDispose(), Throws.TypeOf<InvalidOperationException>());

            int largerThanRingBuffer = UString.RING_BUFFER_SIZE * 2;
            var formattable = new TestFormattable();
            Assert.That(() => UString.ToCharSpan(formattable, largerThanRingBuffer.ToString()), Throws.TypeOf<OutOfMemoryException>());

            // using own buffer to solve the problem
            using (UString.RingBufferScope(new char[largerThanRingBuffer]))
            {
                Assert.That(UString.ToCharSpan(formattable, largerThanRingBuffer.ToString()).ToString() != null);
            }


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Large_Text), priority = 0)]
        [Test]
        public static void Large_Text()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            const string LOREM = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";
            const string M_KENJI = "　あのイーハトーヴォのすきとおった風、夏でも底に冷たさをもつ青いそら、うつくしい森で飾られたモリーオ市、郊外のぎらぎらひかる草の波。";
            const string SURROGATE = "\ud800 ~ \udbff 彅、𠮷、㐂、繫、𠀋 \udc00 ~ \udfff";
            int DUMMY_LENGTH = LOREM.Length + M_KENJI.Length + SURROGATE.Length;

            var SystemString = (((LOREM + 310 + M_KENJI + 3.10 + SURROGATE + DateTime.UnixEpoch)));
            var UStr = UString.Rent(LOREM);
            UStr.Append(310);
            UStr.Append(M_KENJI);
            UStr.Append(3.10);
            UStr.Append(SURROGATE);
            UStr.Append(DateTime.UnixEpoch);
            Assert.That(UStr.ToStringAndDispose(), Is.EqualTo(SystemString));

            const int NUM_DUPLICATES = 310_0;

            //using
            var sb = UString.Rent();
            {
                for (int i = 0; i < NUM_DUPLICATES; i++)
                {
                    sb.Append(LOREM);
                    sb.Append(M_KENJI);
                    sb.Append(SURROGATE);
                }


                var buffer = sb.AsSpan();

                int pos = 0;
                for (int i = 0; i < NUM_DUPLICATES; i++)
                {
                    Assert.That(buffer.Slice(pos, LOREM.Length).SequenceEqual(LOREM));
                    pos += LOREM.Length;
                    Assert.That(buffer.Slice(pos, M_KENJI.Length).SequenceEqual(M_KENJI));
                    pos += M_KENJI.Length;
                    Assert.That(buffer.Slice(pos, SURROGATE.Length).SequenceEqual(SURROGATE));
                    pos += SURROGATE.Length;
                }
            }


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Data_Types), priority = 0)]
        [Test]
        public static void Data_Types()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            var minMaxValues = UString.Rent();
            minMaxValues.Append(sbyte.MinValue);
            minMaxValues.Append(sbyte.MaxValue);
            minMaxValues.Append(byte.MinValue);
            minMaxValues.Append(byte.MaxValue);
            minMaxValues.Append(short.MinValue);
            minMaxValues.Append(short.MaxValue);
            minMaxValues.Append(ushort.MinValue);
            minMaxValues.Append(ushort.MaxValue);
            minMaxValues.Append(int.MinValue);
            minMaxValues.Append(int.MaxValue);
            minMaxValues.Append(uint.MinValue);
            minMaxValues.Append(uint.MaxValue);
            minMaxValues.Append(long.MinValue);
            minMaxValues.Append(long.MaxValue);
            minMaxValues.Append(ulong.MinValue);
            minMaxValues.Append(ulong.MaxValue);

            Assert.That(minMaxValues.ToStringAndDispose(),
                Is.EqualTo("-1281270255-3276832767065535-2147483648214748364704294967295-92233720368547758089223372036854775807018446744073709551615"));

            //using
            var sb = UString.Rent();
            sb.Append(((sbyte)-10));
            sb.Append(((byte)10));
            sb.Append(((short)-20));
            sb.Append(((ushort)20));
            sb.Append(((int)-30));
            sb.Append(((uint)30));
            sb.Append(((long)-40));
            sb.Append(((ulong)40));
            sb.Append(((float)-1.1));
            sb.Append(((double)-2.2));
            sb.Append(((decimal)-3.3));
            sb.Append(" ".AsSpan());
            sb.Append(((bool)true));
            sb.Append(((string)" // "));
            sb.Append(((DateTime)(DateTime.UnixEpoch)));
            sb.Append(((char)' '));
            sb.Append(((DateTimeOffset)(new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.FromHours(9)))));
            sb.Append(((char)' '));
            sb.Append(((TimeSpan)TimeSpan.FromSeconds(310)));
            sb.Append(((char)' '));
            sb.Append(((Guid)new Guid(310, 31, 3, 1, 2, 3, 4, 5, 6, 7, 8)));
            sb.NewLine();
            sb.NewLine();

            Assert.That(sb.ToString(),
                Is.EqualTo("-1010-2020-3030-4040-1.1-2.2-3.3 True // 1970/01/01 0:00:00 1970/01/01 0:00:00 +09:00 00:05:10 00000136-001f-0003-0102-030405060708\n\n"));


            // generic
            sb = UString.Rent();
            sb.Append<sbyte>(((sbyte)-10));
            sb.Append<byte>(((byte)10));
            sb.Append<short>(((short)-20));
            sb.Append<ushort>(((ushort)20));
            sb.Append<int>(((int)-30));
            sb.Append<uint>(((uint)30));
            sb.Append<long>(((long)-40));
            sb.Append<ulong>(((ulong)40));
            sb.Append<float>(((float)-1.1));
            sb.Append<double>(((double)-2.2));
            sb.Append<decimal>(((decimal)-3.3));
            sb.Append(" ".AsSpan());
            sb.Append<bool>(((bool)true));
            sb.Append<string>(((string)" // "));
            sb.Append<DateTime>(((DateTime)(DateTime.UnixEpoch)));
            sb.Append<char>(((char)' '));
            sb.Append<DateTimeOffset>(((DateTimeOffset)(new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.FromHours(9)))));
            sb.Append<char>(((char)' '));
            sb.Append<TimeSpan>(((TimeSpan)TimeSpan.FromSeconds(310)));
            sb.Append<char>(((char)' '));
            sb.Append<Guid>(((Guid)new Guid(310, 31, 3, 1, 2, 3, 4, 5, 6, 7, 8)));
            sb.NewLine();
            sb.NewLine();

            Assert.That(sb.ToString(),
                Is.EqualTo("-1010-2020-3030-4040-1.1-2.2-3.3 True // 1970/01/01 0:00:00 1970/01/01 0:00:00 +09:00 00:05:10 00000136-001f-0003-0102-030405060708\n\n"));


            // (object)
            sb = UString.Rent();
            sb.Append(((object)(sbyte)-10));
            sb.Append(((object)(byte)10));
            sb.Append(((object)(short)-20));
            sb.Append(((object)(ushort)20));
            sb.Append(((object)(int)-30));
            sb.Append(((object)(uint)30));
            sb.Append(((object)(long)-40));
            sb.Append(((object)(ulong)40));
            sb.Append(((object)(float)-1.1));
            sb.Append(((object)(double)-2.2));
            sb.Append(((object)(decimal)-3.3));
            sb.Append(" ".AsSpan());
            sb.Append(((object)(bool)true));
            sb.Append(((object)(string)" // "));
            sb.Append(((object)(DateTime)(DateTime.UnixEpoch)));
            sb.Append(((object)(char)' '));
            sb.Append(((object)(DateTimeOffset)(new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.FromHours(9)))));
            sb.Append(((object)(char)' '));
            sb.Append(((object)(TimeSpan)TimeSpan.FromSeconds(310)));
            sb.Append(((object)(char)' '));
            sb.Append(((object)(Guid)new Guid(310, 31, 3, 1, 2, 3, 4, 5, 6, 7, 8)));
            sb.NewLine();
            sb.NewLine();

            Assert.That(sb.ToString(),
                Is.EqualTo("-1010-2020-3030-4040-1.1-2.2-3.3 True // 1970/01/01 0:00:00 1970/01/01 0:00:00 +09:00 00:05:10 00000136-001f-0003-0102-030405060708\n\n"));


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Concat_Tests), priority = 0)]
        [Test]
        public static void Concat_Tests()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            const string STR = "I'm a string.";
            const int INTEGER = -310;
            const long LONG = -310_310_310_310_310_310L;

            // string and int has fast path, other types just call Append<T> generic method
            Assert.That(UString.Concat(STR, STR, STR, STR), Is.EqualTo("I'm a string.I'm a string.I'm a string.I'm a string."));
            Assert.That(UString.Concat(INTEGER, INTEGER, INTEGER, INTEGER), Is.EqualTo("-310-310-310-310"));
            Assert.That(UString.Concat(int.MinValue, int.MinValue, int.MinValue, int.MinValue), Is.EqualTo("-2147483648-2147483648-2147483648-2147483648"));
            Assert.That(UString.Concat(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue), Is.EqualTo("2147483647214748364721474836472147483647"));
            Assert.That(UString.Concat(LONG, LONG, LONG, LONG), Is.EqualTo("-310310310310310310-310310310310310310-310310310310310310-310310310310310310"));

            Assert.That(UString.Concat(STR, STR, STR), Is.EqualTo("I'm a string.I'm a string.I'm a string."));
            Assert.That(UString.Concat(INTEGER, INTEGER, INTEGER), Is.EqualTo("-310-310-310"));
            Assert.That(UString.Concat(int.MinValue, int.MinValue, int.MinValue), Is.EqualTo("-2147483648-2147483648-2147483648"));
            Assert.That(UString.Concat(int.MaxValue, int.MaxValue, int.MaxValue), Is.EqualTo("214748364721474836472147483647"));
            Assert.That(UString.Concat(LONG, LONG, LONG), Is.EqualTo("-310310310310310310-310310310310310310-310310310310310310"));

            Assert.That(UString.Concat(STR, STR), Is.EqualTo("I'm a string.I'm a string."));
            Assert.That(UString.Concat(INTEGER, INTEGER), Is.EqualTo("-310-310"));
            Assert.That(UString.Concat(int.MinValue, int.MinValue), Is.EqualTo("-2147483648-2147483648"));
            Assert.That(UString.Concat(int.MaxValue, int.MaxValue), Is.EqualTo("21474836472147483647"));
            Assert.That(UString.Concat(LONG, LONG), Is.EqualTo("-310310310310310310-310310310310310310"));

            Assert.That(UString.Concat(STR), Is.EqualTo("I'm a string."));
            Assert.That(UString.Concat(INTEGER), Is.EqualTo("-310"));
            Assert.That(UString.Concat(int.MinValue), Is.EqualTo("-2147483648"));
            Assert.That(UString.Concat(int.MaxValue), Is.EqualTo("2147483647"));
            Assert.That(UString.Concat(LONG), Is.EqualTo("-310310310310310310"));


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }


        /* =====  formatter test  ===== */

        struct TestStruct
        {
            public override string ToString() => nameof(TestStruct);
        }

        class TestClass
        {
            public override string ToString() => nameof(TestClass);
        }

        class TestFormattable : IFormattable
        {
            public override string ToString() => nameof(TestFormattable);
            public string ToString(string format, IFormatProvider formatProvider)
            {
                if (format == null || format.Length == 0)
                    return ToString();

                int length = int.Parse(format);
                return new string('@', length);
            }
        }

        struct StructFormattable : IFormattable
        {
            public override string ToString() => nameof(StructFormattable);
            public string ToString(string format, IFormatProvider formatProvider) => ToString();
        }

        enum TestEnum : long
        {
            Default,
            Other,
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Formatter_Tests), priority = 0)]
        [Test]
        public static void Formatter_Tests()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            //string
            Assert.That(UString.Formatter<string?>.Default.GetType().Name, Is.EqualTo("StringFormatter"));
            Assert.That(UString.Formatter<string?>.Default.GetType().BaseType.GenericTypeArguments.First().Name, Is.EqualTo("String"));

            //nullable
            Assert.That(UString.Concat((int?)0), Is.EqualTo("0"));
            Assert.That(UString.Concat((int?)null), Is.EqualTo(""));
            Assert.That(UString.Formatter<int?>.Default.GetType().Name, Is.EqualTo("NullableFormatter`1"));
            Assert.That(UString.Formatter<int?>.Default.GetType()/*.BaseType*/.GenericTypeArguments.First().Name, Is.EqualTo("Int32"));

            // user-defined
            Assert.That(UString.Concat(new TestClass()), Is.EqualTo("TestClass"));
            Assert.That(UString.Concat(new TestStruct()), Is.EqualTo("TestStruct"));
            Assert.That(UString.Concat(new TestFormattable()), Is.EqualTo("TestFormattable"));
            Assert.That(UString.Concat(new StructFormattable()), Is.EqualTo("StructFormattable"));

            Assert.That(UString.Formatter<TestClass>.Default.GetType().Name, Is.EqualTo("ObjectFormatter`1"));
            Assert.That(UString.Formatter<TestStruct>.Default.GetType().Name, Is.EqualTo("ValueTypeFormatter`1"));
            Assert.That(UString.Formatter<TestFormattable>.Default.GetType().Name, Is.EqualTo("FormattableFormatter`1"));
            Assert.That(UString.Formatter<StructFormattable>.Default.GetType().Name, Is.EqualTo("FormattableFormatter`1"));

            Assert.That(UString.Formatter<TestClass>.Default.GetType().BaseType.GenericTypeArguments.First().Name, Is.EqualTo("Object"));
            Assert.That(UString.Formatter<TestStruct>.Default.GetType().BaseType.GenericTypeArguments.First().Name, Is.EqualTo("TestStruct"));
            Assert.That(UString.Formatter<TestFormattable>.Default.GetType().BaseType.GenericTypeArguments.First().Name, Is.EqualTo("IFormattable"));
            Assert.That(UString.Formatter<StructFormattable>.Default.GetType().BaseType.GenericTypeArguments.First().Name, Is.EqualTo("StructFormattable"));

            //enum
            Assert.That(UString.Concat(TestEnum.Default), Is.EqualTo("Default"));
            Assert.That(UString.Concat(TestEnum.Other), Is.EqualTo("Other"));
            Assert.That(UString.Formatter<TestEnum>.Default.GetType().Name, Is.EqualTo("EnumFormatter`1"));
            Assert.That(UString.Formatter<TestClass>.Default.GetType().BaseType.GenericTypeArguments.First().Name, Is.EqualTo("Object"));


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }


        /* =====  source generator  ===== */

        // integers
        public sealed class SByteFormatter : UnityFundamentals.UString.Formatter<sbyte>
        {
            public override int WriteUnsafe(sbyte value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (format.Length == 0)
                    return UString.FastFormatter.WriteUnsafe(value, span);

                if (value.TryFormat(span, out var written, format, provider))
                    return written;
                else
                    return -1;
            }
        }

        // others
        public sealed class FloatFormatter : UnityFundamentals.UString.Formatter<float>
        {
            public override int WriteUnsafe(float value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            {
                if (value.TryFormat(span, out var written, format, provider))
                    return written;
                else
                    return -1;
            }
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Source_Generator), priority = int.MinValue)]
        //[Test]
        public static void Source_Generator()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            var sb = new StringBuilder();
            sb.AppendLine(@"#region ////////  Generated Formatter Source Code  ////////");

            var factory = new StringBuilder();
            factory.AppendLine(@"#region ////////  Generated Factory Source Code  ////////");
            factory.AppendLine(@"#pragma warning disable format");
            factory.AppendLine(@"#pragma warning disable IDE2001");
            factory.AppendLine();

            //integers
            foreach (var formatterPrefix in new[] {
                "SByte",
                "Byte",
                "Short",
                "UShort",
                "Int",
                "UInt",
                "Long",
                "ULong",
            })
            {
                var typeName = formatterPrefix.ToLowerInvariant();

                sb.Append(@$"
sealed class {formatterPrefix}Formatter : Formatter<{typeName}>
{{
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int {nameof(UString.IFormatter<int>.WriteUnsafe)}({typeName} value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {{
        if (format.Length == 0)
            return FastFormatter.WriteUnsafe(value, span);

        if (value.TryFormat(span, out var written, format, provider))
            return written;
        else
            return -1;
    }}
}}
"
                );

                factory.AppendLine(@$"if (type == typeof({typeName})) return new {formatterPrefix}Formatter(); ");
            }


            // others
            foreach (var (formatterPrefix, typeName) in new[] {
                ("Float", "float"),
                ("Double", "double"),
                ("Decimal", "decimal"),
                ("TimeSpan", "TimeSpan"),
                ("DateTime", "DateTime"),
                ("DateTimeOffset", "DateTimeOffset"),
                ("Guid", "Guid"),
            })
            {
                sb.Append(@$"
sealed class {formatterPrefix}Formatter : Formatter<{typeName}>
{{
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int {nameof(UString.IFormatter<int>.WriteUnsafe)}({typeName} value, Span<char> span, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {{
        if (value.TryFormat(span, out var written, format, provider))
            return written;
        else
            return -1;
    }}
}}
"
                );

                factory.AppendLine(@$"if (type == typeof({typeName})) return new {formatterPrefix}Formatter();");
            }

            sb.AppendLine();
            sb.AppendLine(@"#endregion");

            factory.AppendLine();
            factory.AppendLine(@"#pragma warning restore IDE2001");
            factory.AppendLine(@"#pragma warning restore format");
            factory.AppendLine(@"#endregion");


            UnityEditor.EditorApplication.delayCall += () =>
            {
                UnityEngine.Debug.Log("===  <b>Formatter Source Code</b>  ===\n" + sb.ToString());
                UnityEngine.Debug.Log("===  <b>Factory Method Source Code</b>  ===\n" + factory.ToString());
            };


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }


        /* =====  Concat generator  ===== */

        const int GENERATED_OVERLOAD_COUNT = 12;

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Concat_Overload_Generator), priority = int.MinValue)]
        //[Test]
        public static void Concat_Overload_Generator()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            var sb = new StringBuilder();
            sb.AppendLine(@"#region ////////  Generated Concat() Overloads  ////////");
            sb.AppendLine(@"#pragma warning disable format");
            sb.AppendLine(@"#pragma warning disable IDE2001");
            sb.AppendLine(@"#pragma warning disable SMA0040");
            sb.AppendLine();

            const int UNSAFE_THRESHOLD = 0;
            const int INLINING_THRESHOLD = 0;
            for (int i = GENERATED_OVERLOAD_COUNT; i >= 1; i--)
            {
                if (i <= UNSAFE_THRESHOLD)
                    sb.AppendLine();

                if (i <= INLINING_THRESHOLD && i > UNSAFE_THRESHOLD)
                    sb.AppendLine($"[global::{typeof(MethodImplAttribute).FullName}(global::{typeof(MethodImplOptions)}.{nameof(MethodImplOptions.AggressiveInlining)})]");

                sb.Append("public static string Concat<");

                for (int TArg = 1; TArg <= i; TArg++)
                {
                    if (TArg > 1)
                    {
                        sb.Append(", ");
                    }
                    sb.Append("T" + TArg);
                }

                sb.Append(">(");

                for (int Param = 1; Param <= i; Param++)
                {
                    if (Param > 1)
                    {
                        sb.Append(", ");
                    }
                    sb.Append($"T{Param} v{Param}");
                }

                // NOTE: hand write try-catch pattern to prevent unnecessary .Dispose() call
                if (i <= UNSAFE_THRESHOLD)
                {
                    sb.AppendLine(")");
                    sb.AppendLine("{");
                    sb.AppendLine("    var us = Rent();");
                    sb.AppendLine("    try");
                    sb.AppendLine("    {");
                    sb.AppendLine();

                    for (int Append = 1; Append <= i; Append++)
                    {
                        var typeArg = "T" + Append;
                        var param = "v" + Append;
                        sb.AppendLine($@"
        //{typeArg}
        {{
            if (typeof({typeArg}) == typeof(string))
            {{
                us.Append(Unsafe.As<{typeArg}, string>(ref {param}).AsSpan());
            }}
            else if (typeof({typeArg}) == typeof(int))
            {{
                us.Append(Unsafe.As<{typeArg}, int>(ref {param}));
            }}
            else if (typeof({typeArg}) == typeof(float))
            {{
                us.Append(Unsafe.As<{typeArg}, float>(ref {param}));
            }}
            else
            {{
                us.Append({param});
            }}
        }}
");
                    }

                    sb.AppendLine("        return us.ToStringAndDispose();");
                    sb.AppendLine("    }");
                    sb.AppendLine("    catch");
                    sb.AppendLine("    {");
                    sb.AppendLine("        us.Dispose();");
                    sb.AppendLine("        throw;");
                    sb.AppendLine("    }");
                    sb.AppendLine();
                    sb.Append("}");
                }
                else
                {
                    sb.Append(") { var us = Rent(); try {");

                    for (int Append = 1; Append <= i; Append++)
                    {
                        sb.Append($" us.Append(v{Append});");
                    }

                    sb.Append(" return us.ToStringAndDispose(); } catch { us.Dispose(); throw; } }");
                }
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine(@"#pragma warning restore SMA0040");
            sb.AppendLine(@"#pragma warning restore IDE2001");
            sb.AppendLine(@"#pragma warning restore format");
            sb.AppendLine(@"#endregion");


            UnityEditor.EditorApplication.delayCall += () =>
            {
                UnityEngine.Debug.Log("===  <b>Concat() Overload Source Code</b>  ===\n" + sb.ToString());
            };


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }


        /* =====  TextMesh Pro generator  ===== */

        [UnityEditor.MenuItem(MENU_ROOT + nameof(TextMeshPro_Overload_Generator), priority = int.MinValue)]
        //[Test]
        public static void TextMeshPro_Overload_Generator()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            var sb = new StringBuilder();
            sb.AppendLine(@"#region ////////  Generated TextMeshPro Overloads  ////////");
            sb.AppendLine(@"#pragma warning disable format");
            sb.AppendLine(@"#pragma warning disable IDE2001");
            sb.AppendLine();

            for (int i = GENERATED_OVERLOAD_COUNT; i >= 1; i--)
            {
                sb.Append("public static void SetTextNonAlloc<");

                for (int TArg = 1; TArg <= i; TArg++)
                {
                    if (TArg > 1)
                    {
                        sb.Append(", ");
                    }
                    sb.Append("T");
                    sb.Append(TArg);
                }

#if STMG_TEXTMESHPRO_EXISTS
                sb.Append($">(this global::{typeof(TMPro.TMP_Text).FullName} textMeshText");
#endif
                for (int Param = 1; Param <= i; Param++)
                {
                    sb.Append($", T{Param} v{Param}");
                }

                sb.Append(") { using var us = Rent();");

                for (int Append = 1; Append <= i; Append++)
                {
                    sb.Append($" us.Append(v{Append});");
                }

                sb.Append(" var (buffer, consumed) = us.GetRawBuffer(); textMeshText.SetCharArray(buffer, 0, consumed); }");
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine(@"#pragma warning restore IDE2001");
            sb.AppendLine(@"#pragma warning restore format");
            sb.AppendLine(@"#endregion");


            UnityEditor.EditorApplication.delayCall += () =>
            {
                UnityEngine.Debug.Log("===  <b>Concat() Overload Source Code</b>  ===\n" + sb.ToString());
            };


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }


        /*  TEMPLATE: End of Tests  ================================================================ */
        #endregion    //  TEMPLATE: End of Tests


        /* TEMPLATE: add 'using NUnit.Framework;' to header of script to fix error */

        /* TEMPLATE: copy & paste and replace argument for 'nameof()'

        [UnityEditor.MenuItem(MENU_ROOT + nameof(__Underscore_Separated_Method_Name__), priority = 0)]
        [Test]
        public static void Basic_Tests()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }

        */


        // TEMPLATE: run all tests in this class
        [UnityEditor.MenuItem(MENU_ROOT + "Run All Tests", priority = int.MinValue + 310)]
        public static void UnityEditorTests_RunAllTests()
        {
            const int REPEAT_COUNT = 3;

            foreach (var method in UnityEditor.TypeCache.GetMethodsWithAttribute<TestAttribute>())
            {
                if (method.DeclaringType != typeof(UNITY_EDITOR_TESTS))
                    continue;

                for (var i = 1; i <= REPEAT_COUNT; i++)
                {
                    UnityEngine.Debug.Log($"=======   {method.Name}  Repeat: {i}/{REPEAT_COUNT}   =======");
                    method.Invoke(null, null);
                }
            }

            UnityEngine.Debug.Log(@"  <b>\\\  Tests Passed  ///</b>  " + typeof(UNITY_EDITOR_TESTS).Namespace);
        }

        // TEMPLATE: open script file
        [UnityEditor.MenuItem(MENU_ROOT + "Edit Tests...", priority = int.MaxValue - 310)]
        public static void UnityEditorTests_EditTests() => __EditTests();

        static void __EditTests(
            [System.Runtime.CompilerServices.CallerFilePath] string? filePath = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
            => UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(filePath, lineNumber);
    }
}
#endif
#endregion
