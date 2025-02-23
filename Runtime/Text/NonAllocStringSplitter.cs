/** Non-Alloc String Splitter for .NET / Unity
 ** (c) 2024 Sator Imaging, Licensed under the MIT License
 ** https://github.com/sator-imaging/Unity-Fundamentals

How to Use
==========
```cs
var result = new NonAllocStringSplitter("A,,B,C,", ',');  // 3 items. empty will be removed

// split by sequence or by any char. using extension method
result = "A, B, C".AsSpan().SplitNonAlloc(" ,");     // 1 item. no ' ,' sequence found
result = "A, B, C".AsSpan().SplitAnyNonAlloc(" ,");  // 3 items. split by ' ' or ','

// enumerate
for (int i = 0; i < result.Count; i++)
{
    var span = result[i];
}

foreach (var span in result)
{
    Console.WriteLine(span.ToString());
}

// can pass 'stackalloc char[]' directly
result = "A, B, C".AsSpan().SplitNonAlloc(stackalloc char[] { ' ', ',' });
```


Incremental String Splitter
===========================
Allocation-free incremental splitter processes only when consuming enumerator so it supports unlimited
length of input text and any number of items can be splitted. (inspired by .NET 9 preview feature)

```cs
var span = "ABC DEF".AsSpan();

foreach (var range in span.SplitEnumerator(' '))
{
    // use Range struct from enumerator to extract result
    Console.WriteLine(span[range].ToString());
}
```


TrySplit Extensions
===================
Designed to split input text into 2 items.

```cs
var span = "https://www.inter.net/path/to/file.html?opt=value&other=data#anchor";

// TrySplitLast searches splitter from end of text
if (!span.TrySplitLast('#', out var urlAndOptions, out var anchorSpan))
{
    urlAndOptions = span;
    anchorSpan = default;
}

Span<Range> optionNameRanges = default;
Span<Range> optionValueRanges = default;

if (urlAndOptions.TrySplit('?', out var url, out var allOptions)
{
    optionNameRanges = stackalloc Range[16];
    optionValueRanges = stackalloc Range[16];

    int i = -1;
    foreach (var range in allOptions.SplitEnumerator('&'))
    {
        i++;

        if (!allOptions[range].TrySplit('=', out var name, out var value)
        {
            name = allOptions[range];
            value = string.Empty;
        }

        optionNameRanges[i] = new(range.Start, range.Start + name.Length);
        optionValueRanges[i] = new(range.End - value.Length, range.End);
    }
}
```

 */

using NUnit.Framework;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    /// <summary>
    /// Non-allocation string splitter.
    /// </summary>
    /// <remarks>
    /// NOTE: max 10 items are allowed to be splitted otherwise throws <see cref="IndexOutOfRangeException"/>.
    /// <br/>
    /// * able to retrieve error position in input text by splitting exception message with <c>'@'</c>.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public readonly ref struct NonAllocStringSplitter
    {
        readonly public ReadOnlySpan<char> Value0;
        readonly public ReadOnlySpan<char> Value1;
        readonly public ReadOnlySpan<char> Value2;
        readonly public ReadOnlySpan<char> Value3;
        readonly public ReadOnlySpan<char> Value4;
        readonly public ReadOnlySpan<char> Value5;
        readonly public ReadOnlySpan<char> Value6;
        readonly public ReadOnlySpan<char> Value7;
        readonly public ReadOnlySpan<char> Value8;
        readonly public ReadOnlySpan<char> Value9;
        readonly public int Count;

        NonAllocStringSplitter(nint _)  // TODO: C# 9.0 requires at least one parameter
        {
            this.Count = 0;
            this.Value0 = default;
            this.Value1 = default;
            this.Value2 = default;
            this.Value3 = default;
            this.Value4 = default;
            this.Value5 = default;
            this.Value6 = default;
            this.Value7 = default;
            this.Value8 = default;
            this.Value9 = default;
        }

        /// <inheritdoc cref="ThrowHelper(int)"/>
        public NonAllocStringSplitter(ReadOnlySpan<char> text, char splitter) : this(default)
        {
            var span = text.TrimEnd(splitter);

            int start = 0;
            int pos;
            int length;
            while (true)
            {
                length = span.Slice(start).IndexOf(splitter);

                #region ////////  COPY & PASTE  ////////

                if (length < 0)
                {
                    length = span.Length - start;
                }
                pos = length + start;  // set before goto NEXT

                if (length == 0)
                    goto NEXT;

                switch (this.Count)
                {
                    case 0:
                        this.Value0 = span.Slice(start, length);
                        break;
                    case 1:
                        this.Value1 = span.Slice(start, length);
                        break;
                    case 2:
                        this.Value2 = span.Slice(start, length);
                        break;
                    case 3:
                        this.Value3 = span.Slice(start, length);
                        break;
                    case 4:
                        this.Value4 = span.Slice(start, length);
                        break;
                    case 5:
                        this.Value5 = span.Slice(start, length);
                        break;
                    case 6:
                        this.Value6 = span.Slice(start, length);
                        break;
                    case 7:
                        this.Value7 = span.Slice(start, length);
                        break;
                    case 8:
                        this.Value8 = span.Slice(start, length);
                        break;
                    case 9:
                        this.Value9 = span.Slice(start, length);
                        break;

                    default:
                        ThrowHelper(start);
                        break;
                }

                this.Count++;
            #endregion

            NEXT:
                start = pos + 1;

                if (start >= span.Length)
                    break;
            }
        }

        /// <inheritdoc cref="ThrowHelper(int)"/>
        /// <exception cref="ArgumentException"></exception>
        public NonAllocStringSplitter(ReadOnlySpan<char> text, ReadOnlySpan<char> sequence, bool splitByAnyChar) : this(default)
        {
            if (sequence.Length == 0)
                throw new ArgumentException("empty", nameof(sequence));

            ReadOnlySpan<char> span;
            if (splitByAnyChar)
            {
                span = text.TrimEnd(sequence);
            }
            else
            {
                #region ////////  trim sequence at end (copy&paste)  ////////

                int foundEnd = text.Length;
                int foundPos;
                while (foundEnd >= sequence.Length)
                {
                    foundPos = text.Slice(0, foundEnd).LastIndexOf(sequence);
                    if (foundPos < 0)
                        break;

                    if (foundPos != foundEnd - sequence.Length)
                        break;

                    foundEnd = foundPos;
                }

                #endregion

                span = text.Slice(0, foundEnd);
            }

            int start = 0;
            int pos;
            int length;
            while (true)
            {
                if (splitByAnyChar)
                    length = span.Slice(start).IndexOfAny(sequence);
                else
                    length = span.Slice(start).IndexOf(sequence);

                #region ////////  COPY & PASTE  ////////

                if (length < 0)
                {
                    length = span.Length - start;
                }
                pos = length + start;  // set before goto NEXT

                if (length == 0)
                    goto NEXT;

                switch (this.Count)
                {
                    case 0:
                        this.Value0 = span.Slice(start, length);
                        break;
                    case 1:
                        this.Value1 = span.Slice(start, length);
                        break;
                    case 2:
                        this.Value2 = span.Slice(start, length);
                        break;
                    case 3:
                        this.Value3 = span.Slice(start, length);
                        break;
                    case 4:
                        this.Value4 = span.Slice(start, length);
                        break;
                    case 5:
                        this.Value5 = span.Slice(start, length);
                        break;
                    case 6:
                        this.Value6 = span.Slice(start, length);
                        break;
                    case 7:
                        this.Value7 = span.Slice(start, length);
                        break;
                    case 8:
                        this.Value8 = span.Slice(start, length);
                        break;
                    case 9:
                        this.Value9 = span.Slice(start, length);
                        break;

                    default:
                        ThrowHelper(start);
                        break;
                }

                this.Count++;
            #endregion

            NEXT:
                start = pos + (splitByAnyChar ? 1 : sequence.Length);

                if (start >= span.Length)
                    break;
            }
        }


        /// <exception cref="IndexOutOfRangeException"></exception>
        static void ThrowHelper(int position)
        {
            // ability to take failed position by splitting messasge with '@'
            throw new IndexOutOfRangeException($"item count exceeded (max 10 items): failed @{position}");
        }


        /// <summary>Use `.Value0~9` instead to achieve little bit performance gain.</summary>
        /// <exception cref="IndexOutOfRangeException"></exception>
        readonly public ReadOnlySpan<char> this[int index]
        {
            get
            {
                if (unchecked((uint)index >= (uint)Count))
                    throw new IndexOutOfRangeException();

                var result = index switch
                {
                    0 => Value0,
                    1 => Value1,
                    2 => Value2,
                    3 => Value3,
                    4 => Value4,
                    5 => Value5,
                    6 => Value6,
                    7 => Value7,
                    8 => Value8,
                    9 => Value9,
                    _ => throw new IndexOutOfRangeException(),
                };

                return result;
            }
        }

        // don't add `GetEnumerator` here to keep struct simple enough
    }


    // thx: https://qiita.com/sator_imaging/items/1393aa0efa3b064d77ec#comment-7f0bdf816e0b06256eac
    /// <summary>To allow splitter enumerated using <see langword="foreach"/> statement.</summary>
    [StructLayout(LayoutKind.Auto)]
    public /*readonly*/ ref struct NonAllocStringSplitterEnumerator //: duck typing!!
    {
        readonly NonAllocStringSplitter splitter;
        int index;

        public NonAllocStringSplitterEnumerator(NonAllocStringSplitter splitter)
        {
            this.splitter = splitter;
            this.index = -1;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        /*readonly*/
        public bool MoveNext() => (++index < splitter.Count);

        [EditorBrowsable(EditorBrowsableState.Never)]
        readonly public ReadOnlySpan<char> Current => splitter[index];
    }


    /// <summary>
    /// Non-alloc, incremental string splitter designed for use with <see langword="foreach"/> statement.
    /// This enumerator searches text only when requested so good to use with larger text.
    /// </summary>
    /// <seealso cref="https://github.com/dotnet/core/blob/main/release-notes/9.0/preview/preview7/libraries.md#enumerate-over-readonlyspancharsplit-segments"/>
    [StructLayout(LayoutKind.Auto)]
    public /*readonly*/ ref struct NonAllocIncrementalStringSplitter //: duck typing!!
    {
        readonly ReadOnlySpan<char> text;
        readonly ReadOnlySpan<char> sequence;
        readonly int lengthNoTrailingSplitters;
        readonly char splitChar;
        readonly bool splitByAnyChar;

        NonAllocIncrementalStringSplitter(ReadOnlySpan<char> text)
        {
            this.text = text;
            this.splitChar = default;
            this.splitByAnyChar = true;  // must be true for (char) overload!!
            this.sequence = default;

            this.lengthNoTrailingSplitters = -1;  // -1 to check .ctor initialization

            this._lastSeek = 0;
            this._currentSeek = 0;
        }

        public NonAllocIncrementalStringSplitter(ReadOnlySpan<char> text, char splitChar) : this(text)
        {
            this.splitChar = splitChar;
            this.lengthNoTrailingSplitters = text.TrimEnd(splitChar).Length;
        }

        /// <exception cref="ArgumentException"></exception>
        public NonAllocIncrementalStringSplitter(ReadOnlySpan<char> text, ReadOnlySpan<char> sequence, bool splitByAnyChar) : this(text)
        {
            if (sequence.Length == 0)
                throw new ArgumentException("empty", nameof(sequence));

            this.sequence = sequence;
            this.splitByAnyChar = splitByAnyChar;

            if (splitByAnyChar)
            {
                this.lengthNoTrailingSplitters = text.TrimEnd(sequence).Length;
            }
            else
            {
                #region ////////  trim sequence at end (copy&paste)  ////////

                int foundEnd = text.Length;
                int foundPos;
                while (foundEnd >= sequence.Length)
                {
                    foundPos = text.Slice(0, foundEnd).LastIndexOf(sequence);
                    if (foundPos < 0)
                        break;

                    if (foundPos != foundEnd - sequence.Length)
                        break;

                    foundEnd = foundPos;
                }

                #endregion

                this.lengthNoTrailingSplitters = foundEnd;
            }
        }


        int _lastSeek;     // last item end position that will be starting point of next search.
                           // note that it will be advanced if splitter found at [0]
        int _currentSeek;  // end position of current search

        [EditorBrowsable(EditorBrowsableState.Never)]
        readonly public Range Current => new(_lastSeek, _currentSeek);

        [EditorBrowsable(EditorBrowsableState.Never)]
        /*readonly*/
        public bool MoveNext()
        {
            if (this._currentSeek >= this.lengthNoTrailingSplitters)
                return false;

            int length;
            int pos;
            ReadOnlySpan<char> span;

        CONTINUE:
            span = text.Slice(this._currentSeek, this.lengthNoTrailingSplitters - this._currentSeek);

            if (this.sequence != null)
            {
                if (splitByAnyChar)
                    length = span.IndexOfAny(this.sequence);
                else
                    length = span.IndexOf(this.sequence);
            }
            else
            {
                length = span.IndexOf(this.splitChar);
            }

            if (length < 0)
            {
                length = span.Length;
            }
            pos = length + this._currentSeek;

            // if not found, advance both seek position
            if (length == 0)
            {
                pos += this.splitByAnyChar ? 1 : sequence.Length;

                _lastSeek = pos;
                _currentSeek = pos;
                goto CONTINUE;
            }

            _lastSeek = _currentSeek;
            _currentSeek = pos;

            return true;
        }


        [EditorBrowsable(EditorBrowsableState.Never)]
        readonly public NonAllocIncrementalStringSplitter GetEnumerator() => this;

    }


    /// <summary>
    /// Extension methods for <see cref="NonAllocStringSplitter"/>
    /// </summary>
    public static class NonAllocStringSplitterExtensions
    {
        /// <inheritdoc cref="NonAllocStringSplitter"/>
        /// <returns>use <c>.Count</c> and indexer <c>[int]</c> to enumerate splitted spans.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NonAllocStringSplitter SplitNonAlloc(this ReadOnlySpan<char> text, char splitter) => new(text, splitter);

        /// <inheritdoc cref="SplitNonAlloc(ReadOnlySpan{char}, char)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NonAllocStringSplitter SplitNonAlloc(this ReadOnlySpan<char> text, ReadOnlySpan<char> sequence)
            => new(text, sequence, false);

        /// <inheritdoc cref="SplitNonAlloc(ReadOnlySpan{char}, char)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NonAllocStringSplitter SplitAnyNonAlloc(this ReadOnlySpan<char> text, ReadOnlySpan<char> splitAny)
            => new(text, splitAny, true);


        /// <returns><c>ReadOnlySpan&lt;char&gt;</c> enumerator for use with <c>foreach</c> statement.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NonAllocStringSplitterEnumerator GetEnumerator(this in NonAllocStringSplitter splitter) => new(splitter);


        /*  incremental splitter  ================================================================ */

        /// <inheritdoc cref="NonAllocIncrementalStringSplitter"/>
        /// <returns><c>Range</c> enumerator</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NonAllocIncrementalStringSplitter SplitEnumerator(this ReadOnlySpan<char> text, char splitter) => new(text, splitter);

        /// <inheritdoc cref="SplitEnumerator(ReadOnlySpan{char}, char)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NonAllocIncrementalStringSplitter SplitEnumerator(this ReadOnlySpan<char> text, ReadOnlySpan<char> sequence, bool splitByAnyChar)
            => new(text, sequence, splitByAnyChar);


        /*  simple helpers  ================================================================ */

        /// <summary>
        /// <code>
        /// 1. "0  1 2  " by ' ' --> "0" and "1 2  "  (removes repeating split chars)
        /// 2. "  0 1 2 " by ' ' --> "" and "0 1 2 "  (doesn't care about starting splitter. trim it before on your needs)
        /// </code>
        /// </summary>
        public static bool TrySplit(this ReadOnlySpan<char> text, char splitter, out ReadOnlySpan<char> before, out ReadOnlySpan<char> after)
        {
            var trimmed = text.TrimStart(splitter);
            if (trimmed.Length != text.Length)
            {
                before = string.Empty;
                after = trimmed;
                return true;
            }

            var pos = text.IndexOf(splitter);
            if (pos < 0)
            {
                before = default;
                after = default;
                return false;
            }

            before = text.Slice(0, pos);
            after = text.Slice(pos + 1).TrimStart(splitter);
            return true;
        }

        /// <summary>
        /// <code>
        /// 1. "  0 1  2" by ' ' --> "  0 1" and "2"  (removes repeating split chars)
        /// 2. " 0 1 2  " by ' ' --> " 0 1 2" and ""  (doesn't care about ending splitter. trim it before on your needs)
        /// </code>
        /// </summary>
        public static bool TrySplitLast(this ReadOnlySpan<char> text, char splitter, out ReadOnlySpan<char> before, out ReadOnlySpan<char> after)
        {
            var trimmed = text.TrimEnd(splitter);
            if (trimmed.Length != text.Length)
            {
                before = trimmed;
                after = string.Empty;
                return true;
            }

            var pos = text.LastIndexOf(splitter);
            if (pos < 0)
            {
                before = default;
                after = default;
                return false;
            }

            before = text.Slice(0, pos).TrimEnd(splitter);
            after = text.Slice(pos + 1);
            return true;
        }

    }

}




#region ////////  TEMPLATE: Unity Editor Tests  ////////

#if UNITY_EDITOR

#pragma warning disable IDE1006

// TEMPLATE: namespace naming convention: <TEST_TARGET>.<ROOT_MENU_LABEL>.<SUB_MENU_LABEL>
namespace SatorImaging.UnityFundamentals.TEST.Non_Alloc_String_Splitter  // must be unique, don't reuse existing namespace
{
    static class UNITY_EDITOR_TESTS  // don't change
    {
        const string MENU_ROOT = nameof(TEST) + "/" + nameof(Non_Alloc_String_Splitter) + "/";


        #region ////////  TEMPLATE: Test Methods  ////////
        /*  TEMPLATE: Test Methods  ================================================================ */

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Basic_Tests), priority = 0)]
        [Test]
        public static void Basic_Tests()
        {
            // TEST: write test code here
            //       > `Assert.That(..., Is/Throws)` can be used
            // empty text
            TestKit(0, " ", "");
            TestKit(0, " ", default);
            TestKit(1, " ,/", "");
            TestKit(1, " ,/", default);
            TestKit(2, " yb ", "");
            TestKit(2, " yb ", default);

            // empty separator
            Assert.That(() => "TEST".AsSpan().SplitAnyNonAlloc(""), Throws.TypeOf<ArgumentException>());
            Assert.That(() => "TEST".AsSpan().SplitAnyNonAlloc(default), Throws.TypeOf<ArgumentException>());
            Assert.That(() => "TEST".AsSpan().SplitNonAlloc(""), Throws.TypeOf<ArgumentException>());
            Assert.That(() => "TEST".AsSpan().SplitNonAlloc(default(ReadOnlySpan<char>)), Throws.TypeOf<ArgumentException>());
            Assert.That(() => "TEST".AsSpan().SplitEnumerator("", true), Throws.TypeOf<ArgumentException>());
            Assert.That(() => "TEST".AsSpan().SplitEnumerator("", false), Throws.TypeOf<ArgumentException>());
            Assert.That(() => "TEST".AsSpan().SplitEnumerator(default, true), Throws.TypeOf<ArgumentException>());
            Assert.That(() => "TEST".AsSpan().SplitEnumerator(default, false), Throws.TypeOf<ArgumentException>());

            TestKit(0, " ", "ABC DEF", "ABC", "DEF");
            TestKit(0, " ", "  MORE  MORE      SPACES  ", "MORE", "MORE", "SPACES");

            TestKit(1, " ,/", ",,,,,,,//////,,,,,,,,");
            TestKit(1, " ,/", ",,,,,,,,///multiple-splitters///,,,,,,,,", "multiple-splitters");

            TestKit(1, " yb ", "split by sequence", "split", "sequence");
            TestKit(2, " yb ", "split by sequence", "split by sequence");

            TestKit(2, "by", "byby split byby sequence byby", " split ", " sequence ");
            TestKit(2, "by", "  byby split by by sequence byby  ", "  ", " split ", " ", " sequence ", "  ");

            var input = "310 311 312 313 314 315 316 317 318 319";  // OK: 10 items
            var result = new NonAllocStringSplitter(input, ' ');
            Assert.AreEqual(result.Count, 10);

            input += " 320";  // NG: 11 items...!!
            try
            {
                result = new NonAllocStringSplitter(input, ' ');
            }
            catch (IndexOutOfRangeException exc)
            {
                // can iterate even if error occurred
                Assert.AreEqual(result.Count, 10);

                for (int i = 0; i < result.Count; i++)
                {
                    Assert.AreEqual(result[i].ToString(), (310 + i).ToString());
                }

                // retrieve error position
                var errorPos = int.Parse(exc.Message.AsSpan().SplitNonAlloc('@').Value1);
                UnityEngine.Debug.LogWarning($"EXPECTED EXCEPTION: error at {errorPos} / " + exc.Message);

                Assert.AreEqual(input.AsSpan(errorPos).ToString(), "320");
            }


            // TrySplit tests
            bool tryResult;
            ReadOnlySpan<char> before;
            ReadOnlySpan<char> after;

            tryResult = "ABCDE".AsSpan().TrySplit(' ', out before, out after);
            Assert.That(tryResult == false);
            tryResult = "ABCDE".AsSpan().TrySplitLast(' ', out before, out after);
            Assert.That(tryResult == false);

            tryResult = "0  1 2  ".AsSpan().TrySplit(' ', out before, out after);
            Assert.That(tryResult);
            Assert.That(before.ToString(), Is.EqualTo("0"));
            Assert.That(after.ToString(), Is.EqualTo("1 2  "));

            tryResult = "  0 1 2 ".AsSpan().TrySplit(' ', out before, out after);
            Assert.That(tryResult);
            Assert.That(before.ToString(), Is.EqualTo(""));
            Assert.That(after.ToString(), Is.EqualTo("0 1 2 "));


            // TrySplit tests
            tryResult = "  0 1  2".AsSpan().TrySplitLast(' ', out before, out after);
            Assert.That(tryResult);
            Assert.That(before.ToString(), Is.EqualTo("  0 1"));
            Assert.That(after.ToString(), Is.EqualTo("2"));

            tryResult = " 0 1 2  ".AsSpan().TrySplitLast(' ', out before, out after);
            Assert.That(tryResult);
            Assert.That(before.ToString(), Is.EqualTo(" 0 1 2"));
            Assert.That(after.ToString(), Is.EqualTo(""));


            // syntax test
            var split = "ABC, DEF, GHI, JKL".AsSpan().SplitAnyNonAlloc(stackalloc char[] { ' ', ',' });
            Assert.That(split.Count, Is.EqualTo(4), ".SplitAnyNonAlloc(stackalloc char[]...)");


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }


        static void TestKit(int SplitMode_char_any_sequence,
                    ReadOnlySpan<char> splitters,
                    ReadOnlySpan<char> text,
                    params string[]? expected
            )
        {
            if (SplitMode_char_any_sequence == 0 && splitters.Length == 0)
                return;

            expected ??= Array.Empty<string>();
            var message = $"mode: {SplitMode_char_any_sequence} \"{splitters.ToString()}\"  expected: \"{text.ToString()}\" --> {expected.Length} [ \"{string.Join("\" | \"", expected)}\" ]";

            var result = SplitMode_char_any_sequence switch
            {
                0 => new NonAllocStringSplitter(text, splitters[0]),
                1 => new NonAllocStringSplitter(text, splitters, true),
                2 => new NonAllocStringSplitter(text, splitters, false),
                _ => throw new NotImplementedException(),
            };

            Assert.AreEqual(result.Count, expected.Length, message);

            for (int i = 0; i < result.Count; i++)
            {
                Assert.AreEqual(result[i].ToString(), expected[i], message);
            }

            int index = 0;
            foreach (var span in result)
            {
                Assert.AreEqual(span.ToString(), expected[index++], message);
            }


            var enumerator = SplitMode_char_any_sequence switch
            {
                0 => text.SplitEnumerator(splitters[0]),
                1 => text.SplitEnumerator(splitters, true),
                2 => text.SplitEnumerator(splitters, false),
                _ => throw new NotImplementedException(),
            };

            // check count
            index = 0;
            foreach (var _ in enumerator)
                index++;

            Assert.AreEqual(index, expected.Length, message);

            index = 0;
            foreach (var range in enumerator)
            {
                Assert.AreEqual(text[range].ToString(), expected[index++], message);
            }
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
