/** List implementation of `Span<T>` for .NET / Unity
 ** (c) 2025 Sator Imaging, Licensed under the MIT License
 ** https://github.com/sator-imaging/Unity-Fundamentals

Simple container especially designed to work in conjunction with multiple `Span<char>`s for
formatting and replacing text at once.

It's similar with `Memory<T>` but by contrast from that, `SpanList<T>` provides segmented range
access to its internal buffer so that it can be used for cache store of list of span objects.

Example Usage
=============
Formatting text by replacing named holes: https://messagetemplates.org/

```cs
// you can stored list of `Span<char>` on class or struct field.
private SpanList<char> m_fromList = new(null, 3, "{foo}", "{bar}", "{baz}");

// helper function that automatically manage array pool buffer.
using (SpanList.GetTransientScope(out var toList, 3, "Blah", "blah", "..."))
{
    // replace result
    // --> Blah blah ...
    var result = "{foo} {bar} {baz}".ReplaceNonAlloc(m_fromList, toList);

    // when 'from' list consists of tokens starting with '{' and ending with '}',
    // use `FormatNonAlloc` instead to achieve more efficient operation by reducing required buffer size.
    return "{foo} {bar} {baz}".FormatNonAlloc(m_fromList, toList);

    // buffer return to array pool automatically on using-block exit
}
```


Advanced Usage
==============
You can take direct reference to internal data of `SpanList<T>`.

```cs
// take 64 x 3 span from pool
using var _ SpanList.GetTransientScope(out var spanList, stackalloc int[] { 64, 64, 64 });

// you can perform validation and formatting without extra heap allocation
ref var range = ref spanList.GetSpanUnsafe(0, out var span);
~~~~~~~         ~~~ taking `ref Range`

// write validated result into taken span
int charsWritten = Validate(rawFoo, span);
range = range.SetLength(charsWritten);  // update internal span range directly by `ref var`

// thanks to C#'s `ref return` feature, we can set new length on the fly
spanList.GetSpanUnsafe(1, out span).SetLength(Validate(rawBar, span);

// but, there is safe API to write data into internal buffer ^_^
spanList.Write(2, "my text", static (span, arg) => { arg.AsSpan().CopyTo(span); return arg.Length; });
s = spanList[2];  // "my text"
spanList.Write(2, "updated text", static (span, arg) => { arg.AsSpan().CopyTo(span); return arg.Length; });
s = spanList[2];  // "updated text"

return "{foo} {bar} {baz}".FormatNonAlloc(m_fromList, spanList);
```

 */

using NUnit.Framework;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    /// <param name="buffer">
    /// Use existing buffer instead of allocating new array.
    /// > [!TIP]
    /// > Recommend use <c>Transient()</c> that automatically manages array pool buffer with <c>using</c> statement.
    /// </param>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct SpanList<T> : IEquatable<SpanList<T>>  // cannot -> IEnumerable<ReadOnlySpan<T>>, IReadOnlyCollection<ReadOnlySpan<T>>
    {
        [DoesNotReturn] static void ThrowArgumentOutOfRange(string paramName) => throw new ArgumentOutOfRangeException(paramName);
        [DoesNotReturn] static void ThrowArgumentException(string message) => throw new ArgumentException(message);

        // this struct seems enough small. should use `in` modifier?
        public static bool operator ==(SpanList<T> left, SpanList<T> right) => left.Equals(right);
        public static bool operator !=(SpanList<T> left, SpanList<T> right) => !(left == right);

        public override int GetHashCode() => HashCode.Combine(this.buffer, this.fullRanges, this.activeRanges);
        public bool Equals(SpanList<T> other)
        {
            return this.activeRanges == other.activeRanges
                && this.fullRanges == other.fullRanges
                && this.buffer == other.buffer
                ;

            // NOTE: don't be valueObject
            return this.buffer.Length == other.buffer.Length
                && this.activeRanges.Length == other.activeRanges.Length
                && this.activeRanges.AsSpan().SequenceEqual(other.activeRanges)
                && this.fullRanges.AsSpan().SequenceEqual(other.fullRanges)
                && EqualityComparer<T[]>.Default.Equals(this.buffer, other.buffer)
                ;
        }
        public override bool Equals(object? obj) => obj is SpanList<T> other && this.Equals(other);


        readonly T[] buffer;
        readonly Range[] activeRanges;
        readonly Range[] fullRanges;

        // parameter 'count' is required because ReadOnlySpan<T> cannot be nullable. no way to detect the end.
        /// <param name="count">Number of input <see cref="ReadOnlySpan{T}"/> sources.</param>
        /// <inheritdoc cref="SpanList{T}"/>
        public SpanList(T[]? buffer,
                        int count,
                        ReadOnlySpan<T> ros0 = default,
                        ReadOnlySpan<T> ros1 = default,
                        ReadOnlySpan<T> ros2 = default,
                        ReadOnlySpan<T> ros3 = default,
                        ReadOnlySpan<T> ros4 = default,
                        ReadOnlySpan<T> ros5 = default,
                        ReadOnlySpan<T> ros6 = default,
                        ReadOnlySpan<T> ros7 = default,
                        ReadOnlySpan<T> ros8 = default,
                        ReadOnlySpan<T> ros9 = default)
        {
            if (checked(((uint)count) > 10))
                ThrowArgumentOutOfRange(nameof(count));

            int requiredBufferSize = 0;

            #pragma warning disable format
            #pragma warning disable IDE2001
            if (count > 0) requiredBufferSize += ros0.Length;
            if (count > 1) requiredBufferSize += ros1.Length;
            if (count > 2) requiredBufferSize += ros2.Length;
            if (count > 3) requiredBufferSize += ros3.Length;
            if (count > 4) requiredBufferSize += ros4.Length;
            if (count > 5) requiredBufferSize += ros5.Length;
            if (count > 6) requiredBufferSize += ros6.Length;
            if (count > 7) requiredBufferSize += ros7.Length;
            if (count > 8) requiredBufferSize += ros8.Length;
            if (count > 9) requiredBufferSize += ros9.Length;
            #pragma warning restore IDE2001
            #pragma warning restore format

            if (buffer != null)
            {
                if (buffer.Length < requiredBufferSize)
                    ThrowArgumentException("input sources require buffer size greater than " + requiredBufferSize);
            }
            else
            {
                buffer = new T[requiredBufferSize];
            }

            var activeRanges = new Range[count];
            var fullRanges = new Range[count];
            var span = buffer.AsSpan();
            int pos = 0;

            #pragma warning disable format
            #pragma warning disable IDE2001
            if (count > 0) { ros0.CopyTo(span.Slice(pos)); /**/ fullRanges[0] = activeRanges[0] = new(pos, pos + ros0.Length); /**/ pos += ros0.Length; }
            if (count > 1) { ros1.CopyTo(span.Slice(pos)); /**/ fullRanges[1] = activeRanges[1] = new(pos, pos + ros1.Length); /**/ pos += ros1.Length; }
            if (count > 2) { ros2.CopyTo(span.Slice(pos)); /**/ fullRanges[2] = activeRanges[2] = new(pos, pos + ros2.Length); /**/ pos += ros2.Length; }
            if (count > 3) { ros3.CopyTo(span.Slice(pos)); /**/ fullRanges[3] = activeRanges[3] = new(pos, pos + ros3.Length); /**/ pos += ros3.Length; }
            if (count > 4) { ros4.CopyTo(span.Slice(pos)); /**/ fullRanges[4] = activeRanges[4] = new(pos, pos + ros4.Length); /**/ pos += ros4.Length; }
            if (count > 5) { ros5.CopyTo(span.Slice(pos)); /**/ fullRanges[5] = activeRanges[5] = new(pos, pos + ros5.Length); /**/ pos += ros5.Length; }
            if (count > 6) { ros6.CopyTo(span.Slice(pos)); /**/ fullRanges[6] = activeRanges[6] = new(pos, pos + ros6.Length); /**/ pos += ros6.Length; }
            if (count > 7) { ros7.CopyTo(span.Slice(pos)); /**/ fullRanges[7] = activeRanges[7] = new(pos, pos + ros7.Length); /**/ pos += ros7.Length; }
            if (count > 8) { ros8.CopyTo(span.Slice(pos)); /**/ fullRanges[8] = activeRanges[8] = new(pos, pos + ros8.Length); /**/ pos += ros8.Length; }
            if (count > 9) { ros9.CopyTo(span.Slice(pos)); /**/ fullRanges[9] = activeRanges[9] = new(pos, pos + ros9.Length); /**/ pos += ros9.Length; }
            #pragma warning restore IDE2001
            #pragma warning restore format

            this.buffer = buffer;
            this.activeRanges = activeRanges;
            this.fullRanges = fullRanges;
        }


        /*  ctor IEnumerable<string>  ================================================================ */

        /// <inheritdoc cref="SpanList{T}"/>
        public SpanList(T[]? buffer, params IEnumerable<T>[]? sources) : this(buffer, (IEnumerable<IEnumerable<T>>?)sources) { }

        /// <inheritdoc cref="SpanList{T}"/>
        public SpanList(T[]? buffer, IEnumerable<IEnumerable<T>>? sources)
        {
            if (sources == null)
            {
                this.buffer = Array.Empty<T>();
                this.activeRanges = Array.Empty<Range>();
                this.fullRanges = Array.Empty<Range>();
                return;
            }

            int existingBufferLength = buffer?.Length ?? int.MaxValue;

            int count = 0;
            int requiredBufferSize = 0;
            foreach (var src in sources)
            {
                count++;

                int collectionSize = src switch
                {
                    string x => x.Length,
                    T[] x => x.Length,
                    ICollection x => x.Count,
                    IReadOnlyCollection<T> x => x.Count,
                    _ => -1,
                };

                if (collectionSize >= 0)
                {
                    requiredBufferSize += collectionSize;
                }
                else
                {
                    foreach (var _ in src)
                        requiredBufferSize++;
                }

                if (existingBufferLength < requiredBufferSize)
                {
                    ThrowArgumentException("input sources require buffer size greater than " + requiredBufferSize);
                }
            }

            buffer ??= new T[requiredBufferSize];

            var span = buffer.AsSpan();
            var activeRanges = new Range[count];
            var fullRanges = new Range[count];

            int pos = 0;
            int i = -1;
            foreach (var src in sources)
            {
                i++;

                int start = pos;

                switch (src)
                {
                    //case string x:
                    //    x.AsSpan().CopyTo(((ReadOnlySpan<char>)(ReadOnlySpan<T>)span).Slice(pos));
                    //    break;

                    case T[] x:
                        x.AsSpan().CopyTo(span.Slice(pos));
                        pos += x.Length;
                        break;

                    case ICollection x:
                        x.CopyTo(buffer, pos);
                        pos += x.Count;
                        break;

                    //case IReadOnlyCollection<T> x:
                    //    break;

                    default:
                        foreach (var x in src)
                        {
                            span[pos] = x;
                            pos++;
                        }
                        break;
                }

                fullRanges[i] =
                activeRanges[i] = new(start, start + (pos - start));
            }

            this.buffer = buffer;
            this.activeRanges = activeRanges;
            this.fullRanges = fullRanges;
        }


        /*  ctor internal  ================================================================ */

        // NOTE: to avoid solving complex overload resolution, adds unused signature for internal constructors
        internal SpanList(bool __INTERNAL_USE__, T[]? buffer, ReadOnlySpan<int> capacities)
        {
            if (capacities.Length == 0)
            {
                this.buffer = Array.Empty<T>();
                this.activeRanges = Array.Empty<Range>();
                this.fullRanges = Array.Empty<Range>();
                return;
            }

            int requiredBufferSize = 0;
            for (int i = 0; i < capacities.Length; i++)
            {
                var cap = capacities[i];
                if (cap < 0)
                    ThrowArgumentOutOfRange("capacity must be greater than or equal to 0: " + cap);

                requiredBufferSize += cap;
            }

            if (buffer != null)
            {
                if (buffer.Length < requiredBufferSize)
                    ThrowArgumentException("buffer size must be greater than " + requiredBufferSize);
            }
            else
            {
                buffer = new T[requiredBufferSize];
            }

            int count = capacities.Length;
            var activeRanges = new Range[count];
            var fullRanges = new Range[count];

            int start = 0;
            for (int i = 0; i < capacities.Length; i++)
            {
                var cap = capacities[i];

                activeRanges[i] = new(start, start);  // 0 length
                fullRanges[i] = new(start, start + cap);

                start += cap;
            }

            this.buffer = buffer;
            this.activeRanges = activeRanges;
            this.fullRanges = fullRanges;
        }


        /*  implicit  ================================================================ */

        // NOTE: implicit operator should not be defined! it leads hidden inefficient operation!!
        public static explicit operator SpanList<T>(IEnumerable<T>[]? sources) => new(null, (IEnumerable<IEnumerable<T>>?)sources);


        /*  accessors  ================================================================ */

        // TODO: remove null-checking
        // NOTE: support for case of `default` ex. FormatNonAlloc(default, default);
        public IEnumerable<Range> Ranges => activeRanges == null ? Array.Empty<Range>() : this.activeRanges;
        public int Count => activeRanges == null ? 0 : activeRanges.Length;   // item count. don't use name 'Length' that misleading.

        /// <summary>You can take non-readonly view by using <see cref="GetSpanUnsafe(int, out Span{T})"/>.</summary>
        public ReadOnlySpan<T> this[int index] => buffer == null ? ReadOnlySpan<T>.Empty : buffer.AsSpan(activeRanges[index]);


        /// <summary>
        /// This method returns direct reference to internal array item.
        /// You can trim or extend range by editing that.
        /// <para>
        /// > [!WARNING]
        /// > You need to be familiar with C#'s `ref return` language feature.
        /// </para>
        /// <code>
        /// // basic usage
        /// spanList.GetSpanUnsafe(i, out var span).SetLength(WriteToBuffer(span, data));
        /// </code>
        /// > [!CAUTION]
        /// > `ref Range` modification will have never been validated by this library.
        /// </summary>
        /// <returns>
        /// Returns <see langword="ref"/> <see cref="Range"/> that represents current consumed range of span at index.
        /// By contrast to return value, <see langword="out"/> <see cref="Span{T}"/> is pointing full available range of span.
        /// </returns>
        [Obsolete("Use `Write()` instead")]
        public ref Range GetSpanUnsafe(int index, out Span<T> fullRangeSpan)
        {
            fullRangeSpan = buffer.AsSpan(fullRanges[index]);
            return ref activeRanges[index];
        }

        /// <summary>
        /// Return full range for span at the index. It can be used to restore broken range on unsafe operation.
        /// </summary>
        [Obsolete("Use `Write()` instead")]
        public Range GetFullRange(int index) => fullRanges[index];

        /// <summary>
        /// Get available capacity for span at the index.
        /// </summary>
        public int GetCapacity(int index)
        {
            var full = fullRanges[index];
            return (full.End.Value - full.Start.Value);
        }


        /* =====  SpanWriter  ===== */

        /// <param name="writer">
        /// Note that returned length must be less than or equal to capacity at the index.
        /// <code>
        /// int SpanWriter&lt;TState>(Span&lt;T>, TState);
        /// </code>
        /// </param>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public void Write<TState>(int index, TState state, SpanWriter<TState> writer)
        {
            var fullRange = fullRanges[index];
            var written = writer.Invoke(this.buffer.AsSpan(fullRange), state);

            var range = activeRanges[index];
            var start = range.Start.Value;
            var end = start + written;
            if (end > fullRange.End.Value)
            {
                throw new IndexOutOfRangeException("returned length exceeds capacity: " + written);
            }

            activeRanges[index] = new(start, end);
        }

        public delegate int SpanWriter<TState>(Span<T> span, TState state);
    }


    /// <summary>
    /// Factory and extension methods for <see cref="SpanList{T}"/>.
    /// </summary>
    public static class SpanList
    {
        [DoesNotReturn] static void ThrowArgumentException(string message) => throw new ArgumentException(message);
        [DoesNotReturn] static void ThrowArgumentOutOfRange(string paramName) => throw new ArgumentOutOfRangeException(paramName);


        /*  Range helpers  ================================================================ */

        /// <summary>
        /// > [!WARNING]
        /// > This method will modify extension method receiver. (see method signature for more details)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Consider use `SpanList<T>.Write<TState>()` instead")]
        public static Range SetLength(ref this Range range, int newLength)
        {
            // isFromEnd is relative to source array length.
            // it's not easy to calculate new end position. just disallow it.
            if (range.Start.IsFromEnd)
                ThrowArgumentException("range start has 'IsFromEnd' set");

            var start = range.Start.Value;
            var result = new Range(start, start + newLength);

            range = result;
            return result;
        }


        /*  GetTransientScope  ================================================================ */

        // NOTE: DO NOT use `in` modifier. SpanList is enough small

        /// <inheritdoc cref="GetTransientScope{T}(out SpanList{T}, ReadOnlySpan{int}, bool)"/>
        public static ArrayPoolDisposable<T> GetTransientScope<T>(out SpanList<T> spanList,
                                                                  int count,
                                                                  ReadOnlySpan<T> ros0 = default,
                                                                  ReadOnlySpan<T> ros1 = default,
                                                                  ReadOnlySpan<T> ros2 = default,
                                                                  ReadOnlySpan<T> ros3 = default,
                                                                  ReadOnlySpan<T> ros4 = default,
                                                                  ReadOnlySpan<T> ros5 = default,
                                                                  ReadOnlySpan<T> ros6 = default,
                                                                  ReadOnlySpan<T> ros7 = default,
                                                                  ReadOnlySpan<T> ros8 = default,
                                                                  ReadOnlySpan<T> ros9 = default,
                                                                  bool clearArrayPoolBuffer = false)
        {
            if (checked(((uint)count) > 10))
                ThrowArgumentOutOfRange(nameof(count));

            int requiredBufferSize = 0;

            #pragma warning disable format
            #pragma warning disable IDE2001
            if (count > 0) requiredBufferSize += ros0.Length;
            if (count > 1) requiredBufferSize += ros1.Length;
            if (count > 2) requiredBufferSize += ros2.Length;
            if (count > 3) requiredBufferSize += ros3.Length;
            if (count > 4) requiredBufferSize += ros4.Length;
            if (count > 5) requiredBufferSize += ros5.Length;
            if (count > 6) requiredBufferSize += ros6.Length;
            if (count > 7) requiredBufferSize += ros7.Length;
            if (count > 8) requiredBufferSize += ros8.Length;
            if (count > 9) requiredBufferSize += ros9.Length;
            #pragma warning restore IDE2001
            #pragma warning restore format

            var rentalBuffer = ArrayPool<T>.Shared.Rent(requiredBufferSize);
            var result = new ArrayPoolDisposable<T>(rentalBuffer, clearArrayPoolBuffer);
            try
            {
                spanList = new(rentalBuffer, count, ros0, ros1, ros2, ros3, ros4, ros5, ros6, ros7, ros8, ros9);
            }
            catch
            {
                result.Dispose();
                throw;
            }
            return result;
        }


        /// <param name="clearArrayPoolBuffer"><c>true</c> to fill shared buffer by default value when return buffer to <see cref="ArrayPool{T}"/>.</param>
        /// <returns><see cref="IDisposable"/></returns>
        public static ArrayPoolDisposable<T> GetTransientScope<T>(out SpanList<T> spanList, ReadOnlySpan<int> capacities, bool clearArrayPoolBuffer = false)
        {
            if (capacities.Length == 0)
            {
                spanList = new(__INTERNAL_USE__: true, null, null);
                return new(rentalBuffer: null, clearArrayPoolBuffer: false);
            }

            int requiredBufferSize = 0;
            for (int i = 0; i < capacities.Length; i++)
            {
                var cap = capacities[i];
                if (cap < 0)
                    ThrowArgumentOutOfRange("capacity must be greater than or equal to 0: " + cap);

                requiredBufferSize += cap;
            }

            var rentalBuffer = ArrayPool<T>.Shared.Rent(requiredBufferSize);
            var result = new ArrayPoolDisposable<T>(rentalBuffer, clearArrayPoolBuffer);
            try
            {
                spanList = new(__INTERNAL_USE__: true, rentalBuffer, capacities);
            }
            catch
            {
                result.Dispose();
                throw;
            }
            return result;
        }


        /* =====  ROS char overloads  ===== */

        /// <inheritdoc cref="GetTransientScope{T}(out SpanList{T}, ReadOnlySpan{int}, bool)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayPoolDisposable<char> GetTransientScope(out SpanList<char> spanList, ReadOnlySpan<int> capacities, bool clearArrayPoolBuffer = false)
            => GetTransientScope<char>(out spanList, capacities, clearArrayPoolBuffer);


        /// <inheritdoc cref="GetTransientScope{T}(out SpanList{T}, ReadOnlySpan{int}, bool)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayPoolDisposable<char> GetTransientScope(out SpanList<char> spanList,
                                                                  int count,
                                                                  ReadOnlySpan<char> ros0 = default,
                                                                  ReadOnlySpan<char> ros1 = default,
                                                                  ReadOnlySpan<char> ros2 = default,
                                                                  ReadOnlySpan<char> ros3 = default,
                                                                  ReadOnlySpan<char> ros4 = default,
                                                                  ReadOnlySpan<char> ros5 = default,
                                                                  ReadOnlySpan<char> ros6 = default,
                                                                  ReadOnlySpan<char> ros7 = default,
                                                                  ReadOnlySpan<char> ros8 = default,
                                                                  ReadOnlySpan<char> ros9 = default,
                                                                  bool clearArrayPoolBuffer = false)
            => GetTransientScope<char>(out spanList, count, ros0, ros1, ros2, ros3, ros4, ros5, ros6, ros7, ros8, ros9, clearArrayPoolBuffer);


        /* =====  ArrayPool disposer  ===== */

        /// <summary>
        /// Return array to <see cref="ArrayPool{T}"/> on dispose.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public readonly struct ArrayPoolDisposable<T> : IDisposable
        {
            readonly T[]? rentalBuffer;
            readonly bool clearArrayPoolBuffer;

            internal ArrayPoolDisposable(T[]? rentalBuffer, bool clearArrayPoolBuffer = false)
            {
                this.rentalBuffer = rentalBuffer;
                this.clearArrayPoolBuffer = clearArrayPoolBuffer;
            }

            public void Dispose()
            {
                if (rentalBuffer == null)
                    return;

                ArrayPool<T>.Shared.Return(rentalBuffer, clearArray: clearArrayPoolBuffer);
            }
        }


        /*  string[]  ================================================================ */

        public static string[] ToArray(this SpanList<char> spanList)
        {
            var count = spanList.Count;
            var result = new string[count];

            for (int i = 0; i < count; i++)
            {
                result[i] = new(spanList[i]);
            }

            return result;
        }


        /*  ROS<char> and string helpers  ================================================================ */

        /* =====  MessageTemplate formatter  ===== */

        /// <inheritdoc cref="FormatNonAlloc(ReadOnlySpan{char}, SpanList{char}, SpanList{char}, bool)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string FormatNonAlloc(this string template,
                                            SpanList<char> tokens,
                                            SpanList<char> replacements,
                                            bool clearArrayPoolBuffer = false)
            => FormatNonAlloc(template.AsSpan(), tokens, replacements, clearArrayPoolBuffer);


        /// <summary>
        /// Batch replace with no unnecessary memory allocation.
        /// </summary>
        /// <param name="tokens">Tokens must start wtih <c>{</c> and end with <c>}</c>.</param>
        /// <inheritdoc cref="ReplaceNonAlloc(ReadOnlySpan{char}, SpanList{char}, SpanList{char}, int, bool)"/>
        public static string FormatNonAlloc(this ReadOnlySpan<char> template,
                                            SpanList<char> tokens,
                                            SpanList<char> replacements,
                                            bool clearArrayPoolBuffer = false)
        {
            if (tokens.Count != replacements.Count)
                ThrowArgumentException("'tokens' count doesn't match with 'replacements' count");

            // NOTE: for consistency, token validation must be done before early returns

            // check tokens and calculate max possible length
            int maxDifference = 0;
            for (int i = 0, count = tokens.Count; i < count; i++) //perf
            {
                var token = tokens[i];

                if (token.Length < 2 || token[0] is not '{' || token[^1] is not '}')
                    ThrowArgumentException("token must start with '{' and end with '}': " + token.ToString());

                var diff = replacements[i].Length - token.Length;
                if (maxDifference < diff)
                    maxDifference = diff;
            }

            if (template.Length == 0)
                return string.Empty;

            if (tokens.Count == 0)
                return template.Length == 0 ? string.Empty : new(template);


            int openCount = 0;
            for (int i = 0; i < template.Length; i++)
            {
                if (template[i] is '{')
                    openCount++;
            }

            maxDifference *= openCount;
            int maxPossibleLength = template.Length + maxDifference;

            return ReplaceNonAlloc(template, tokens, replacements, maxPossibleLength, clearArrayPoolBuffer);
        }


        /* =====  Replace()  ===== */

        /// <inheritdoc cref="ReplaceNonAlloc(ReadOnlySpan{char}, SpanList{char}, SpanList{char}, int, bool)"/>
        public static string ReplaceNonAlloc(this string original,
                                             SpanList<char> fromList,
                                             SpanList<char> toList,
                                             int maxPossibleLength = -1,
                                             bool clearArrayPoolBuffer = false)
            => ReplaceNonAlloc(original.AsSpan(), fromList, toList, maxPossibleLength, clearArrayPoolBuffer);


        /// <param name="maxPossibleLength">
        /// When not supplied, automatically estimated by the following formula:
        /// <c>(original.Length / fromListMinLength + 1) * (toListMaxLength - fromListMinLength)</c>
        /// </param>
        /// <param name="clearArrayPoolBuffer"><c>true</c> to fill shared buffer by default value when return buffer to <see cref="ArrayPool{T}"/>.</param>
        public static string ReplaceNonAlloc(this ReadOnlySpan<char> original,
                                             SpanList<char> fromList,
                                             SpanList<char> toList,
                                             int maxPossibleLength = -1,
                                             bool clearArrayPoolBuffer = false)
        {
            if (fromList.Count != toList.Count)
                ThrowArgumentException("'fromList' count doesn't match with 'toList' count");

            if (original.Length == 0)
                return string.Empty;

            if (fromList.Count == 0)
                return original.Length == 0 ? string.Empty : new(original);

            if (maxPossibleLength < 0)
            {
                int fromListMinLength = 1;
                for (int i = 0, count = fromList.Count; i < count; i++) //perf
                {
                    var item = fromList[i];
                    if (fromListMinLength > item.Length)
                        fromListMinLength = item.Length;
                }
                if (fromListMinLength < 1)
                    fromListMinLength = 1;

                int toListMaxLength = 0;
                for (int i = 0, count = toList.Count; i < count; i++) //perf
                {
                    var item = toList[i];
                    if (toListMaxLength < item.Length)
                        toListMaxLength = item.Length;
                }

                maxPossibleLength = (original.Length / fromListMinLength + 1) * (toListMaxLength - fromListMinLength);
            }

            if (maxPossibleLength < original.Length)
                maxPossibleLength = original.Length;

            // PERF: double buffering to reduce inefficient Memmove operations.
            //       * using 2 arrays will allow vectorizing copy operation.
            //       * replace within 1 array requires moving items in it to take room for replacement. it will not be vectorized if overlapping.
            char[]? source_array = null;
            char[]? result_array = null;
            try
            {
                const int STACKALLOC_THRESHOLD = 160;
                Span<char> source = (maxPossibleLength <= STACKALLOC_THRESHOLD) ? stackalloc char[maxPossibleLength] : Array.Empty<char>();
                Span<char> result = (maxPossibleLength <= STACKALLOC_THRESHOLD) ? stackalloc char[maxPossibleLength] : Array.Empty<char>();
                if (maxPossibleLength > STACKALLOC_THRESHOLD)
                {
                    source_array = ArrayPool<char>.Shared.Rent(maxPossibleLength);
                    result_array = ArrayPool<char>.Shared.Rent(maxPossibleLength);
                    source = source_array.AsSpan(0, maxPossibleLength);
                    result = result_array.AsSpan(0, maxPossibleLength);
                }

                original.CopyTo(source);

                bool hasReplaced = false;
                int currentLength = original.Length;

                for (int i = 0, count = fromList.Count; i < count; i++) //perf
                {
                    var sourceSliced = source.Slice(0, currentLength);
                    if (sourceSliced.IndexOf(fromList[i]) < 0)
                        continue;

                    hasReplaced = true;

                    CopyBufferWithSubstitution(sourceSliced, result, ref currentLength, fromList[i], toList[i]);

                    if (i < (count - 1))  // update source for next iteration
                    {
                        result.Slice(0, currentLength).CopyTo(source);
                    }
                }

                return new((hasReplaced ? result : source).Slice(0, currentLength));
            }
            finally
            {
                if (result_array != null)
                    ArrayPool<char>.Shared.Return(result_array, clearArray: clearArrayPoolBuffer);

                if (source_array != null)
                    ArrayPool<char>.Shared.Return(source_array, clearArray: clearArrayPoolBuffer);
            }
        }


        /// <param name="currentLength">Updated if something replaced.</param>
        static void CopyBufferWithSubstitution(ReadOnlySpan<char> source, Span<char> result, ref int currentLength, ReadOnlySpan<char> from, ReadOnlySpan<char> to)
        {
            int toLength = to.Length;
            int fromLength = from.Length;
            int diff = toLength - fromLength;

            int newLength = currentLength;
            int source_start = 0;
            int result_start = 0;
            int pos;
            while ((pos = source.Slice(source_start).IndexOf(from)) >= 0)
            {
                if (pos > 0)
                {
                    source.Slice(source_start, pos).CopyTo(result.Slice(result_start));
                    result_start += pos;
                }

                if (toLength > 0)
                {
                    to.CopyTo(result.Slice(result_start));
                    result_start += toLength;
                }

                newLength += diff;
                source_start += pos + fromLength;
            }

            // remaining
            if (source_start < source.Length)
            {
                source.Slice(source_start).CopyTo(result.Slice(result_start));
            }

            currentLength = newLength;
        }

    }
}




#region ////////  TEMPLATE: Unity Editor Tests  ////////

#if UNITY_EDITOR

#pragma warning disable IDE1006  // naming style
#pragma warning disable CA1861   // avoid constant array

// TEMPLATE: namespace naming convention: <TEST_TARGET>.<ROOT_MENU_LABEL>.<SUB_MENU_LABEL>
namespace SatorImaging.UnityFundamentals.TEST.Span_List  // must be unique. don't reuse existing namespace
{
    static class UNITY_EDITOR_TESTS  // don't change
    {
        const string MENU_ROOT = nameof(TEST) + "/" + nameof(Span_List) + "/";


        #region ////////  TEMPLATE: Test Methods  ////////
        /*  TEMPLATE: Test Methods  ================================================================ */

        [UnityEditor.MenuItem(MENU_ROOT + nameof(FormatNonAlloc_Tests), priority = 0)]
        [Test]
        static void FormatNonAlloc_Tests()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            Assert.That(() => "".FormatNonAlloc(new(null, 1), new(null, 2)), Throws.TypeOf<ArgumentException>());
            Assert.That(() => "".FormatNonAlloc(new(null, 2), new(null, 1)), Throws.TypeOf<ArgumentException>());
            Assert.That(() => "".FormatNonAlloc(new(null, "a", "b"), new(null, 2, "x", "y")), Throws.TypeOf<ArgumentException>());
            Assert.That(() => "".FormatNonAlloc(new(null, "{a}", "b"), new(null, 2, "x", "y")), Throws.TypeOf<ArgumentException>());
            Assert.That(() => "".FormatNonAlloc(new(null, "a", "{b}"), new(null, 2, "x", "y")), Throws.TypeOf<ArgumentException>());

            string template = "{a}{b}";
            Assert.That(template.FormatNonAlloc(default, default), Is.EqualTo(template));
            Assert.That(template.FormatNonAlloc(new(null, "{x}"), (SpanList<char>)new[] { "a" }), Is.EqualTo(template));
            Assert.That(template.FormatNonAlloc(new(null, "{a }"), (SpanList<char>)new[] { "x" }), Is.EqualTo(template));
            Assert.That(template.FormatNonAlloc(new(null, "{ a}"), (SpanList<char>)new[] { "x" }), Is.EqualTo(template));
            Assert.That(template.FormatNonAlloc(new(null, "{ a }"), (SpanList<char>)new[] { "x" }), Is.EqualTo(template));

            Assert.That(template.FormatNonAlloc((SpanList<char>)new[] { "{a}" }, (SpanList<char>)new[] { "x" }), Is.EqualTo("x{b}"));
            Assert.That(template.FormatNonAlloc((SpanList<char>)new[] { "{b}" }, (SpanList<char>)new[] { "y" }), Is.EqualTo("{a}y"));
            Assert.That(template.FormatNonAlloc((SpanList<char>)new[] { "{a}", "{b}" }, (SpanList<char>)new[] { "x", "y" }), Is.EqualTo("xy"));
            Assert.That(template.FormatNonAlloc((SpanList<char>)new[] { "{a}", "{b}" }, (SpanList<char>)new[] { "", "" }), Is.EqualTo(""));

            template = "{abc}{abc}abc{abc}";
            Assert.That(template.FormatNonAlloc((SpanList<char>)new[] { "{abc}" }, (SpanList<char>)new[] { "" }), Is.EqualTo("abc"));  // to shorter
            Assert.That(template.FormatNonAlloc((SpanList<char>)new[] { "{abc}" }, (SpanList<char>)new[] { "11" }), Is.EqualTo("1111abc11"));  // to shorter
            Assert.That(template.FormatNonAlloc((SpanList<char>)new[] { "{abc}" }, (SpanList<char>)new[] { "12345" }), Is.EqualTo("1234512345abc12345"));  // to same length
            Assert.That(template.FormatNonAlloc((SpanList<char>)new[] { "{abc}" }, (SpanList<char>)new[] { "0123456789" }), Is.EqualTo("01234567890123456789abc0123456789"));

            template = "  {{{{{a}aslkdfj }}}}}}{b}{b}{b}{a}{a}{0123456789} }{}{}{ {____dfeaef_  ///-x8d77asd*9*&(%%  } }}}";
            Assert.That(template.FormatNonAlloc(
                new(null, "{a}", "{b}", "{0123456789}", "{____dfeaef_  ///-x8d77asd*9*&(%%  }"),
                new(null, "XX", "YY", "ZZ", "  763742878&^*&^&*6874    3578++0989807()()()(   ")
                ),
                Is.EqualTo("  {{{{XXaslkdfj }}}}}}YYYYYYXXXXZZ }{}{}{   763742878&^*&^&*6874    3578++0989807()()()(    }}}"));


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(ReplaceNonAlloc_Tests), priority = 0)]
        [Test]
        static void ReplaceNonAlloc_Tests()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            Assert.That(() => "".ReplaceNonAlloc(new(null, 1), new(null, 2)), Throws.TypeOf<ArgumentException>());
            Assert.That(() => "".ReplaceNonAlloc(new(null, 2), new(null, 1)), Throws.TypeOf<ArgumentException>());
            //Assert.That(() => "".ReplaceNonAlloc(new(null, 2, "a", "b"), new(null, 2, "x", "y")), Throws.TypeOf<ArgumentException>());
            //Assert.That(() => "".ReplaceNonAlloc(new(null, 2, "{a}", "b"), new(null, 2, "x", "y")), Throws.TypeOf<ArgumentException>());
            //Assert.That(() => "".ReplaceNonAlloc(new(null, 2, "a", "{b}"), new(null, 2, "x", "y")), Throws.TypeOf<ArgumentException>());

            string template = "{a}{b}";
            Assert.That(template.ReplaceNonAlloc(default, default), Is.EqualTo(template));
            Assert.That(template.ReplaceNonAlloc(new(null, "{x}"), (SpanList<char>)new[] { "a" }), Is.EqualTo(template));
            Assert.That(template.ReplaceNonAlloc(new(null, "{a }"), (SpanList<char>)new[] { "x" }), Is.EqualTo(template));
            Assert.That(template.ReplaceNonAlloc(new(null, "{ a}"), (SpanList<char>)new[] { "x" }), Is.EqualTo(template));
            Assert.That(template.ReplaceNonAlloc(new(null, "{ a }"), (SpanList<char>)new[] { "x" }), Is.EqualTo(template));

            Assert.That(template.ReplaceNonAlloc((SpanList<char>)new[] { "{a}" }, (SpanList<char>)new[] { "x" }), Is.EqualTo("x{b}"));
            Assert.That(template.ReplaceNonAlloc((SpanList<char>)new[] { "{b}" }, (SpanList<char>)new[] { "y" }), Is.EqualTo("{a}y"));
            Assert.That(template.ReplaceNonAlloc((SpanList<char>)new[] { "{a}", "{b}" }, (SpanList<char>)new[] { "x", "y" }), Is.EqualTo("xy"));
            Assert.That(template.ReplaceNonAlloc((SpanList<char>)new[] { "{a}", "{b}" }, (SpanList<char>)new[] { "", "" }), Is.EqualTo(""));

            template = "{abc}{abc}abc{abc}";
            Assert.That(template.ReplaceNonAlloc((SpanList<char>)new[] { "{abc}" }, (SpanList<char>)new[] { "" }), Is.EqualTo("abc"));  // to shorter
            Assert.That(template.ReplaceNonAlloc((SpanList<char>)new[] { "{abc}" }, (SpanList<char>)new[] { "11" }), Is.EqualTo("1111abc11"));  // to shorter
            Assert.That(template.ReplaceNonAlloc((SpanList<char>)new[] { "{abc}" }, (SpanList<char>)new[] { "12345" }), Is.EqualTo("1234512345abc12345"));  // to same length
            Assert.That(template.ReplaceNonAlloc((SpanList<char>)new[] { "{abc}" }, (SpanList<char>)new[] { "0123456789" }), Is.EqualTo("01234567890123456789abc0123456789"));

            template = "  {{{{{a}aslkdfj }}}}}}{b}{b}{b}{a}{a}{0123456789} }{}{}{ {____dfeaef_  ///-x8d77asd*9*&(%%  } }}}";
            Assert.That(template.ReplaceNonAlloc(
                new(null, "{a}", "{b}", "{0123456789}", "{____dfeaef_  ///-x8d77asd*9*&(%%  }"),
                new(null, "XX", "YY", "ZZ", "  763742878&^*&^&*6874    3578++0989807()()()(   "))
                ,
                Is.EqualTo("  {{{{XXaslkdfj }}}}}}YYYYYYXXXXZZ }{}{}{   763742878&^*&^&*6874    3578++0989807()()()(    }}}"));


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Transient_Scope_Tests), priority = 0)]
        [Test]
        static void Transient_Scope_Tests()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            using (SpanList.GetTransientScope(out var test, 3, "123", "12345", "1234567"))
            {
                var i = 0;
                Assert.That(test[i].ToString(), Is.EqualTo("123"));
                var range = test.GetSpanUnsafe(i, out var span);
                var fullRange = test.GetFullRange(i);
                var capacity = test.GetCapacity(i);
                Assert.That(capacity, Is.EqualTo(3));
                Assert.That(range.Start.Value, Is.EqualTo(0));
                Assert.That(range.End.Value, Is.EqualTo(3));
                Assert.That(fullRange.Start.Value, Is.EqualTo(0));
                Assert.That(fullRange.End.Value, Is.EqualTo(3));
                Assert.That(span.Length, Is.EqualTo(3));

                i = 1;
                Assert.That(test[i].ToString(), Is.EqualTo("12345"));
                range = test.GetSpanUnsafe(i, out span);
                fullRange = test.GetFullRange(i);
                capacity = test.GetCapacity(i);
                Assert.That(capacity, Is.EqualTo(5));
                Assert.That(range.Start.Value, Is.EqualTo(0 + 3));
                Assert.That(range.End.Value, Is.EqualTo(0 + 3 + 5));
                Assert.That(fullRange.Start.Value, Is.EqualTo(0 + 3));
                Assert.That(fullRange.End.Value, Is.EqualTo(0 + 3 + 5));
                Assert.That(span.Length, Is.EqualTo(5));

                i = 2;
                Assert.That(test[i].ToString(), Is.EqualTo("1234567"));
                range = test.GetSpanUnsafe(i, out span);
                fullRange = test.GetFullRange(i);
                capacity = test.GetCapacity(i);
                Assert.That(capacity, Is.EqualTo(7));
                Assert.That(range.Start.Value, Is.EqualTo(0 + 3 + 5));
                Assert.That(range.End.Value, Is.EqualTo(0 + 3 + 5 + 7));
                Assert.That(fullRange.Start.Value, Is.EqualTo(0 + 3 + 5));
                Assert.That(fullRange.End.Value, Is.EqualTo(0 + 3 + 5 + 7));
                Assert.That(span.Length, Is.EqualTo(7));
            }

            using (SpanList.GetTransientScope(out var test, stackalloc int[] { 12, 34, 56 }))
            {
                int i = 0;
                Assert.That(test[i].Length, Is.EqualTo(0));
                var range = test.GetSpanUnsafe(i, out var span);
                var fullRange = test.GetFullRange(i);
                var capacity = test.GetCapacity(i);
                Assert.That(capacity, Is.EqualTo(12));
                Assert.That(range.Start.Value, Is.EqualTo(0));
                Assert.That(range.End.Value, Is.EqualTo(0));
                Assert.That(fullRange.Start.Value, Is.EqualTo(0));
                Assert.That(fullRange.End.Value, Is.EqualTo(12));
                Assert.That(span.Length, Is.EqualTo(12));

                i = 1;
                Assert.That(test[i].Length, Is.EqualTo(0));
                range = test.GetSpanUnsafe(i, out span);
                fullRange = test.GetFullRange(i);
                capacity = test.GetCapacity(i);
                Assert.That(capacity, Is.EqualTo(34));
                Assert.That(range.Start.Value, Is.EqualTo(0 + 12));
                Assert.That(range.End.Value, Is.EqualTo(0 + 12));
                Assert.That(fullRange.Start.Value, Is.EqualTo(0 + 12));
                Assert.That(fullRange.End.Value, Is.EqualTo(0 + 12 + 34));
                Assert.That(span.Length, Is.EqualTo(34));

                i = 2;
                Assert.That(test[i].Length, Is.EqualTo(0));
                range = test.GetSpanUnsafe(i, out span);
                fullRange = test.GetFullRange(i);
                capacity = test.GetCapacity(i);
                Assert.That(capacity, Is.EqualTo(56));
                Assert.That(range.Start.Value, Is.EqualTo(0 + 12 + 34));
                Assert.That(range.End.Value, Is.EqualTo(0 + 12 + 34));
                Assert.That(fullRange.Start.Value, Is.EqualTo(0 + 12 + 34));
                Assert.That(fullRange.End.Value, Is.EqualTo(0 + 12 + 34 + 56));
                Assert.That(span.Length, Is.EqualTo(56));

                // non-ref variable update won't affect internal data
                i = 0;
                var nonRef = test.GetSpanUnsafe(i, out span);
                fullRange = test.GetFullRange(i);
                nonRef.SetLength(1);
                fullRange.SetLength(1);
                span = span.Slice(0, 1);
                Assert.That(nonRef.End.Value, Is.EqualTo(1));
                Assert.That(fullRange.End.Value, Is.EqualTo(1));
                Assert.That(span.Length, Is.EqualTo(1));

                // take again
                nonRef = test.GetSpanUnsafe(i, out span);
                fullRange = test.GetFullRange(i);
                Assert.That(nonRef.End.Value, Is.EqualTo(0));
                Assert.That(fullRange.End.Value, Is.EqualTo(12));
                Assert.That(span.Length, Is.EqualTo(12));

                // ref variable
                i = 1;
                ref var refVar = ref test.GetSpanUnsafe(i, out span);
                fullRange = test.GetFullRange(i);
                refVar.SetLength(2);
                Assert.That(refVar.End.Value, Is.EqualTo(12 + 2));

                // 'ref' keyword required? <-- NO
                refVar = test.GetSpanUnsafe(i, out span);
                fullRange = test.GetFullRange(i);
                Assert.That(refVar.End.Value, Is.EqualTo(12 + 2));
                // ref var correctly update the value
                Assert.That(test[i].Length, Is.EqualTo(2));  // length == (end - start)
                refVar.SetLength(310_000);
                Assert.That(refVar.End.Value, Is.EqualTo(12 + 310_000));
                Assert.That(() => test[i].Length, Throws.TypeOf<ArgumentOutOfRangeException>());

                // Write() test
                i = 2;
                var msg = "The test message";
                test.Write(i, msg, static (span, state) =>
                {
                    state.AsSpan().CopyTo(span);
                    return state.Length;
                });
                Assert.That(test[i].Length, Is.EqualTo(msg.Length));

                Assert.That(() =>
                {
                    test.Write(i, msg, static (span, state) =>
                    {
                        state.AsSpan().CopyTo(span);
                        return 310_000;
                    });
                },
                Throws.TypeOf<IndexOutOfRangeException>());

                Assert.That(() =>
                {
                    msg = new string('@', 1024);
                    test.Write(i, msg, static (span, state) =>
                    {
                        state.AsSpan().CopyTo(span);
                        return state.Length;
                    });
                },
                Throws.TypeOf<ArgumentException>());
            }


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }


        /*  TEMPLATE: End of Tests  ================================================================ */
        #endregion    //  TEMPLATE: End of Tests


        /* TEMPLATE: add 'using NUnit.Framework;' to header of script to fix error */

        /* TEMPLATE: copy & paste and replace argument for 'nameof()'

        [UnityEditor.MenuItem(MENU_ROOT + nameof(__Underscore_Separated_Method_Name__), priority = 0)]
        [Test]
        static void Basic_Tests()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }

        */


        // TEMPLATE: run all tests in this class
        [UnityEditor.MenuItem(MENU_ROOT + "Run All Tests", priority = int.MinValue + 310)]
        static void UnityEditorTests_RunAllTests()
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
        static void UnityEditorTests_EditTests() => __EditTests();

        static void __EditTests(
            [System.Runtime.CompilerServices.CallerFilePath] string? filePath = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
            => UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(filePath, lineNumber);
    }
}
#endif
#endregion
