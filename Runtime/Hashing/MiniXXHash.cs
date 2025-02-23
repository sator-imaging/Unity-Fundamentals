/** Minimal xxHash Library for .NET / Unity (XXH32/XXH64)
 ** Copyright (c) 2023-2024 https://github.com/sator-imaging
 ** Licensed under the BSD 2-Clause License

xxHash (XXH32/XXH64) implementation for Unity in a single file with no dependency.

This is useful for processing UTF-8 string bytes that are not needed to be treated
as C# string (UTF-16) but required to compare difference. ex: Dictionary<T> key

XXH32 and XXH64 are older than XXH3 series but it still works enough on small data.


HOW TO USE
==========
```cs
var hash32 = MiniXXHash.XXH32(byteArrayOrSpan, seed);
var hash64 = MiniXXHash.XXH64(byteArrayOrSpan, length, seed);
var fromString = MiniXXHash.XXH64("XXH64", seed, clearSharedBuffer: false);

// use own buffer instead of shared array pool when hashing string repeatedly.
Span<byte> buffer = stackalloc byte[256];
var fromString = MiniXXHash.XXH32("use own buffer", seed, buffer);
```

To test reference implementation, select the following menu command in Unity.
- `Unity Editor > TEST > Minimal xxHash > Run Tests`


SEE ALSO
========

xxHash - Fast Hash algorithm
Copyright (C) 2012-2020 Yann Collet
Copyright (C) 2019-2020 Devin Hussey (easyaspi314)

BSD 2-Clause License (http://www.opensource.org/licenses/bsd-license.php)

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

* Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.
* Redistributions in binary form must reproduce the above
copyright notice, this list of conditions and the following disclaimer
in the documentation and/or other materials provided with the
distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

You can contact the author at :
- xxHash homepage: http://www.xxhash.com
- xxHash source repository : https://github.com/Cyan4973/xxHash

*/

/* This is a compact, 100% standalone reference XXH32 single-run implementation.
 * Instead of focusing on performance hacks, this focuses on cleanliness,
 * conformance, portability and simplicity.
 *
 * This file aims to be 100% compatible with C90/C++98, with the additional
 * requirement of stdint.h. No library functions are used.
 */

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace SatorImaging.UnityFundamentals
{
    public static class MiniXXHash
    {
        #region ////////  XXH32  ////////

        const uint PRIME32_1 = 0x9E3779B1u;   /* 0b10011110001101110111100110110001 */
        const uint PRIME32_2 = 0x85EBCA77u;   /* 0b10000101111010111100101001110111 */
        const uint PRIME32_3 = 0xC2B2AE3Du;   /* 0b11000010101100101010111000111101 */
        const uint PRIME32_4 = 0x27D4EB2Fu;   /* 0b00100111110101001110101100101111 */
        const uint PRIME32_5 = 0x165667B1u;   /* 0b00010110010101100110011110110001 */

        /* Rotates value left by amt. */
        static uint XXH_rotl32(uint value, /*u*/int amt)
        {
            return (value << (amt % 32)) | (value >> (32 - (amt % 32)));
        }

        /* Portably reads a 32-bit little endian integer from data at the given offset. */
        static uint XXH_read32(ReadOnlySpan<byte> data, int offset)
        {
            return (uint)data[offset + 0]
                | ((uint)data[offset + 1] << 8)
                | ((uint)data[offset + 2] << 16)
                | ((uint)data[offset + 3] << 24);
        }

        /* Mixes input into acc. */
        static uint XXH32_round(uint acc, uint input)
        {
            acc += input * PRIME32_2;
            acc = XXH_rotl32(acc, 13);
            acc *= PRIME32_1;
            return acc;
        }

        /* Mixes all bits to finalize the hash. */
        static uint XXH32_avalanche(uint hash)
        {
            hash ^= hash >> 15;
            hash *= PRIME32_2;
            hash ^= hash >> 13;
            hash *= PRIME32_3;
            hash ^= hash >> 16;
            return hash;
        }


        /// <summary>
        /// The XXH32 hash function.
        /// </summary>
        /// <param name="input">The data to hash.</param>
        /// <param name="length">
        /// The length of input. It is undefined behavior to have length larger than the capacity of input.
        /// </param>
        /// <param name="seed">A 32-bit value to seed the hash with.</param>
        /// <returns>The 32-bit calculated hash value.</returns>
        public static uint XXH32_Raw(ReadOnlySpan<byte> input, int length, uint seed)
        {
            uint hash;
            int remaining = length;
            int offset = 0;

            /* Don't dereference a null pointer. The reference implementation notably doesn't
             * check for this by default. */
            if (input == null)
            {
                return XXH32_avalanche(seed + PRIME32_5);
            }

            if (remaining >= 16)
            {
                /* Initialize our accumulators */
                uint acc1 = seed + PRIME32_1 + PRIME32_2;
                uint acc2 = seed + PRIME32_2;
                uint acc3 = seed + 0;
                uint acc4 = seed - PRIME32_1;

                while (remaining >= 16)
                {
                    acc1 = XXH32_round(acc1, XXH_read32(input, offset));
                    offset += 4;
                    acc2 = XXH32_round(acc2, XXH_read32(input, offset));
                    offset += 4;
                    acc3 = XXH32_round(acc3, XXH_read32(input, offset));
                    offset += 4;
                    acc4 = XXH32_round(acc4, XXH_read32(input, offset));
                    offset += 4;
                    remaining -= 16;
                }

                hash = XXH_rotl32(acc1, 1) + XXH_rotl32(acc2, 7) + XXH_rotl32(acc3, 12) + XXH_rotl32(acc4, 18);
            }
            else
            {
                /* Not enough data for the main loop, put something in there instead. */
                hash = seed + PRIME32_5;
            }

            hash += (uint)length;

            /* Process the remaining data. */
            while (remaining >= 4)
            {
                hash += XXH_read32(input, offset) * PRIME32_3;
                hash = XXH_rotl32(hash, 17);
                hash *= PRIME32_4;
                offset += 4;
                remaining -= 4;
            }

            while (remaining != 0)
            {
                hash += (uint)input[offset] * PRIME32_5;
                hash = XXH_rotl32(hash, 11);
                hash *= PRIME32_1;
                --remaining;
                ++offset;
            }
            return XXH32_avalanche(hash);
        }


        // NOTE: in C# environment, unsigned type is not CLSCompliant. not only that but also casting to
        //       singed type is done in checked manner by default so it could throw exception
        //       these methods help playing with int.

        /// <inheritdoc cref="XXH32_Raw(ReadOnlySpan{byte}, int, uint)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int XXH32(ReadOnlySpan<byte> input, uint seed) => unchecked((int)XXH32_Raw(input, input.Length, seed));

        /// <inheritdoc cref="XXH32_Raw(ReadOnlySpan{byte}, int, uint)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int XXH32(ReadOnlySpan<byte> input, int length, uint seed) => unchecked((int)XXH32_Raw(input, length, seed));

        #endregion


        #region ////////  XXH64  ////////

        const ulong PRIME64_1 = 0x9E3779B185EBCA87ul;   /* 0b1001111000110111011110011011000110000101111010111100101010000111 */
        const ulong PRIME64_2 = 0xC2B2AE3D27D4EB4Ful;   /* 0b1100001010110010101011100011110100100111110101001110101101001111 */
        const ulong PRIME64_3 = 0x165667B19E3779F9ul;   /* 0b0001011001010110011001111011000110011110001101110111100111111001 */
        const ulong PRIME64_4 = 0x85EBCA77C2B2AE63ul;   /* 0b1000010111101011110010100111011111000010101100101010111001100011 */
        const ulong PRIME64_5 = 0x27D4EB2F165667C5ul;   /* 0b0010011111010100111010110010111100010110010101100110011111000101 */

        /* Rotates value left by amt bits. */
        static ulong XXH_rotl64(ulong value, /*u*/int amt)
        {
            return (value << (amt % 64)) | (value >> (64 - amt % 64));
        }

        ///* Portably reads a 32-bit little endian integer from data at the given offset. */
        //static uint XXH_read32(ReadOnlySpan<byte> data, int offset)
        //{
        //    return (uint)data[offset + 0]
        //        | ((uint)data[offset + 1] << 8)
        //        | ((uint)data[offset + 2] << 16)
        //        | ((uint)data[offset + 3] << 24);
        //}

        /* Portably reads a 64-bit little endian integer from data at the given offset. */
        static ulong XXH_read64(ReadOnlySpan<byte> data, int offset)
        {
            return (ulong)data[offset + 0]
                | ((ulong)data[offset + 1] << 8)
                | ((ulong)data[offset + 2] << 16)
                | ((ulong)data[offset + 3] << 24)
                | ((ulong)data[offset + 4] << 32)
                | ((ulong)data[offset + 5] << 40)
                | ((ulong)data[offset + 6] << 48)
                | ((ulong)data[offset + 7] << 56);
        }

        /* Mixes input into acc, this is mostly used in the first loop. */
        static ulong XXH64_round(ulong acc, ulong input)
        {
            acc += input * PRIME64_2;
            acc = XXH_rotl64(acc, 31);
            acc *= PRIME64_1;
            return acc;
        }

        /* Merges acc into hash to finalize */
        static ulong XXH64_mergeRound(ulong hash, ulong acc)
        {
            hash ^= XXH64_round(0, acc);
            hash *= PRIME64_1;
            hash += PRIME64_4;
            return hash;
        }

        /* Mixes all bits to finalize the hash. */
        static ulong XXH64_avalanche(ulong hash)
        {
            hash ^= hash >> 33;
            hash *= PRIME64_2;
            hash ^= hash >> 29;
            hash *= PRIME64_3;
            hash ^= hash >> 32;
            return hash;
        }


        /// <summary>
        /// The XXH64 hash function.
        /// </summary>
        /// <param name="input">The data to hash.</param>
        /// <param name="length">
        /// The length of input. It is undefined behavior to have length larger than the capacity of input.
        /// </param>
        /// <param name="seed">A 64-bit value to seed the hash with.</param>
        /// <returns>The 64-bit calculated hash value.</returns>
        public static ulong XXH64_Raw(ReadOnlySpan<byte> input, int length, ulong seed)
        {
            //uint8_t const *const data = (uint8_t const *) input;
            ulong hash;
            int remaining = length;
            int offset = 0;

            /* Don't dereference a null pointer. The reference implementation notably doesn't
             * check for this by default. */
            if (input == null)
            {
                return XXH64_avalanche(seed + PRIME64_5);
            }

            if (remaining >= 32)
            {
                /* Initialize our accumulators */
                ulong acc1 = seed + PRIME64_1 + PRIME64_2;
                ulong acc2 = seed + PRIME64_2;
                ulong acc3 = seed + 0;
                ulong acc4 = seed - PRIME64_1;

                while (remaining >= 32)
                {
                    acc1 = XXH64_round(acc1, XXH_read64(input, offset));
                    offset += 8;
                    acc2 = XXH64_round(acc2, XXH_read64(input, offset));
                    offset += 8;
                    acc3 = XXH64_round(acc3, XXH_read64(input, offset));
                    offset += 8;
                    acc4 = XXH64_round(acc4, XXH_read64(input, offset));
                    offset += 8;
                    remaining -= 32;
                }

                hash = XXH_rotl64(acc1, 1) + XXH_rotl64(acc2, 7) + XXH_rotl64(acc3, 12) + XXH_rotl64(acc4, 18);

                hash = XXH64_mergeRound(hash, acc1);
                hash = XXH64_mergeRound(hash, acc2);
                hash = XXH64_mergeRound(hash, acc3);
                hash = XXH64_mergeRound(hash, acc4);
            }
            else
            {
                /* Not enough data for the main loop, put something in there instead. */
                hash = seed + PRIME64_5;
            }

            hash += (ulong)length;

            /* Process the remaining data. */
            while (remaining >= 8)
            {
                hash ^= XXH64_round(0, XXH_read64(input, offset));
                hash = XXH_rotl64(hash, 27);
                hash *= PRIME64_1;
                hash += PRIME64_4;
                offset += 8;
                remaining -= 8;
            }

            if (remaining >= 4)
            {
                hash ^= (ulong)XXH_read32(input, offset) * PRIME64_1;
                hash = XXH_rotl64(hash, 23);
                hash *= PRIME64_2;
                hash += PRIME64_3;
                offset += 4;
                remaining -= 4;
            }

            while (remaining != 0)
            {
                hash ^= (ulong)input[offset] * PRIME64_5;
                hash = XXH_rotl64(hash, 11);
                hash *= PRIME64_1;
                ++offset;
                --remaining;
            }

            return XXH64_avalanche(hash);
        }


        // NOTE: in C# environment, unsigned type is not CLSCompliant. not only that but also casting to
        //       singed type is done in checked manner by default so it could throw exception
        //       these methods help playing with long.

        /// <inheritdoc cref="XXH64_Raw(ReadOnlySpan{byte}, int, ulong)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long XXH64(ReadOnlySpan<byte> input, ulong seed) => unchecked((long)XXH64_Raw(input, input.Length, seed));

        /// <inheritdoc cref="XXH64_Raw(ReadOnlySpan{byte}, int, ulong)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long XXH64(ReadOnlySpan<byte> input, int length, ulong seed) => unchecked((long)XXH64_Raw(input, length, seed));

        #endregion


        #region ////////  String  ////////

        /// <summary>
        /// The XXH32 hash function for string.
        /// </summary>
        /// <param name="buffer">
        /// Buffer used to convert string to UTF-8 byte array.<br/>
        /// Note that enough space must be supplied. (string.Length x3 is recommended.)
        /// </param>
        /// <returns>The 32-bit calculated hash value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int XXH32(string input, uint seed, Span<byte> buffer)
        {
            int len = System.Text.Encoding.UTF8.GetBytes(input, buffer);
            return XXH32(buffer.Slice(0, len), seed);
        }

        /// <summary>
        /// The XXH32 hash function for string.
        /// </summary>
        /// <param name="clearSharedBuffer">
        /// Clear buffer when return it to shared array pool.
        /// </param>
        /// <returns>The 32-bit calculated hash value.</returns>
        public static int XXH32(string input, uint seed, bool clearSharedBuffer = false)
        {
            var rental = ArrayPool<byte>.Shared.Rent(input.Length * 3);
            try
            {
                return XXH32(input, seed, rental);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rental, clearSharedBuffer);
            }
        }


        /// <summary>
        /// The XXH64 hash function for string.
        /// </summary>
        /// <param name="buffer">
        /// Buffer used to convert string to UTF-8 byte array.<br/>
        /// Note that enough space must be supplied. (string.Length x3 is recommended.)
        /// </param>
        /// <returns>The 64-bit calculated hash value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long XXH64(string input, ulong seed, Span<byte> buffer)
        {
            int len = System.Text.Encoding.UTF8.GetBytes(input, buffer);
            return XXH64(buffer.Slice(0, len), seed);
        }

        /// <summary>
        /// The XXH64 hash function for string.
        /// </summary>
        /// <param name="clearSharedBuffer">
        /// Clear buffer when return it to shared array pool.
        /// </param>
        /// <returns>The 64-bit calculated hash value.</returns>
        public static long XXH64(string input, ulong seed, bool clearSharedBuffer = false)
        {
            var rental = ArrayPool<byte>.Shared.Rent(input.Length * 3);
            try
            {
                return XXH64(input, seed, rental);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rental, clearSharedBuffer);
            }
        }

        #endregion


        /*  Tests  ================================================================ */

#if UNITY_EDITOR

        static int _testNumber = 0;
        const int TEST_DATA_SIZE = 101;

        /* Checks a hash value. */
        public static void UnityEditor_TestSequence32(ReadOnlySpan<byte> test_data, int length,
                                                      uint seed, int expected)
        {
            int result = XXH32(test_data, length, seed);
            if (result != expected)
            {
                UnityEngine.Debug.LogError(
                    string.Format("Error: Test {0}: XXH32 test failed!  Expected value: 0x{1:X8}. Actual value: 0x{2:X8}.",
                                  ++_testNumber,
                                  expected,
                                  result));
            }
        }

        /* Checks a hash value. */
        public static void UnityEditor_TestSequence64(ReadOnlySpan<byte> test_data, int length,
                                                      ulong seed, long expected)
        {
            long result = XXH64(test_data, length, seed);
            if (result != expected)
            {
                UnityEngine.Debug.LogError(
                    string.Format("Error: Test {0}: XXH64 test failed!  Expected value: 0x{1:X16}. Actual value: 0x{2:X16}.",
                                  ++_testNumber,
                                  expected,
                                  result));
            }
        }


        [UnityEditor.MenuItem("TEST/Minimal xxHash/Run Tests")]
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public static int UnityEditor_RunTests()
        {
            _testNumber = 0;

            //XXH32
            uint prime = PRIME32_1;
            byte[] test_data = new byte[TEST_DATA_SIZE];
            uint byte_gen = prime;
            int i = 0;

            /* Fill the test_data buffer with "random" data */
            for (; i < TEST_DATA_SIZE; i++)
            {
                test_data[i] = (byte)(byte_gen >> 24);
                byte_gen *= byte_gen;
            }

            unchecked
            {
                UnityEditor_TestSequence32(null, 0, 0, (int)0x02CC5D05U);
                UnityEditor_TestSequence32(null, 0, prime, (int)0x36B78AE7U);
                UnityEditor_TestSequence32(test_data, 1, 0, (int)0xB85CBEE5U);
                UnityEditor_TestSequence32(test_data, 1, prime, (int)0xD5845D64U);
                UnityEditor_TestSequence32(test_data, 14, 0, (int)0xE5AA0AB4U);
                UnityEditor_TestSequence32(test_data, 14, prime, (int)0x4481951DU);
                UnityEditor_TestSequence32(test_data, TEST_DATA_SIZE, 0, (int)0x1F1AA412U);
                UnityEditor_TestSequence32(test_data, TEST_DATA_SIZE, prime, (int)0x498EC8E2U);
                //// test error log
                //TestSequence32(test_data, TEST_DATA_SIZE, 0, 0);
            }

            UnityEngine.Debug.Log("XXH32 reference implementation: OK");


            //XXH64
            prime = PRIME32_1;
            test_data = new byte[TEST_DATA_SIZE];
            byte_gen = prime;
            i = 0;

            /* Fill in the test_data buffer with "random" data. */
            for (; i < TEST_DATA_SIZE; ++i)
            {
                test_data[i] = (byte)(byte_gen >> 24);
                byte_gen *= byte_gen;
            }

            unchecked
            {
                UnityEditor_TestSequence64(null, 0, 0, (long)0xEF46DB3751D8E999ul);
                UnityEditor_TestSequence64(null, 0, prime, (long)0xAC75FDA2929B17EFul);
                UnityEditor_TestSequence64(test_data, 1, 0, (long)0x4FCE394CC88952D8ul);
                UnityEditor_TestSequence64(test_data, 1, prime, (long)0x739840CB819FA723ul);
                UnityEditor_TestSequence64(test_data, 14, 0, (long)0xCFFA8DB881BC3A3Dul);
                UnityEditor_TestSequence64(test_data, 14, prime, (long)0x5B9611585EFCC9CBul);
                UnityEditor_TestSequence64(test_data, TEST_DATA_SIZE, 0, (long)0x0EAB543384F878ADul);
                UnityEditor_TestSequence64(test_data, TEST_DATA_SIZE, prime, (long)0xCAA65939306F1E21ul);
                //// test error log
                //TestSequence64(test_data, TEST_DATA_SIZE, 0, 0);
            }

            UnityEngine.Debug.Log("XXH64 reference implementation: OK");


            /*  perf  ================================================================ */
            // 1,000,000 calls of XXH32 w/byte[]:  0.040 seconds
            // 1,000,000 calls of XXH64 w/byte[]:  0.045 seconds
            // 1,000,000 calls of XXH32 w/string:  0.184 seconds
            // 1,000,000 calls of XXH64 w/string:  0.189 seconds
            // 1,000,000 calls of ASCII.GetString: 0.150 seconds
            // 1,000,000 calls of UTF8.GetString:  0.161 seconds

            string str = "XXH32 reference implementation: OK";
            var utf8 = System.Text.Encoding.UTF8.GetBytes(str);
            var timer = new System.Diagnostics.Stopwatch();
            int count = 1_000_000;

            //XXH32
            int hash = 0;
            timer.Restart();
            for (i = 0; i < count; i++)
            {
                hash = unchecked((int)XXH32(utf8, 12345u));
            }
            UnityEngine.Debug.Log($"{count:#,0} calls of XXH32 w/byte[]: {timer.ElapsedMilliseconds * 0.001f:0.000} seconds");


            //XXH64
            long hash64 = 0;
            timer.Restart();
            for (i = 0; i < count; i++)
            {
                hash64 = unchecked((long)XXH64(utf8, 12345u));
            }
            UnityEngine.Debug.Log($"{count:#,0} calls of XXH64 w/byte[]: {timer.ElapsedMilliseconds * 0.001f:0.000} seconds");


            //XXH32 from string
            timer.Restart();
            for (i = 0; i < count; i++)
            {
                hash = unchecked((int)XXH32(str, 12345u));
            }
            UnityEngine.Debug.Log($"{count:#,0} calls of XXH32 w/string: {timer.ElapsedMilliseconds * 0.001f:0.000} seconds");

            //XXH64 from string
            timer.Restart();
            for (i = 0; i < count; i++)
            {
                hash64 = unchecked((long)XXH64(str, 12345u));
            }
            UnityEngine.Debug.Log($"{count:#,0} calls of XXH64 w/string: {timer.ElapsedMilliseconds * 0.001f:0.000} seconds");


            //XXH64 from string with buffer clear
            timer.Restart();
            for (i = 0; i < count; i++)
            {
                hash64 = unchecked((long)XXH64(str, 12345u, clearSharedBuffer: true));
            }
            UnityEngine.Debug.Log($"{count:#,0} calls of XXH64 w/string: {timer.ElapsedMilliseconds * 0.001f:0.000} seconds  (clearSharedBuffer: true)");


            //ASCII
            timer.Restart();
            for (i = 0; i < count; i++)
            {
                str = System.Text.Encoding.ASCII.GetString(utf8);
            }
            UnityEngine.Debug.Log($"{count:#,0} calls of ASCII.GetString: {timer.ElapsedMilliseconds * 0.001f:0.000} seconds");

            //UTF8
            timer.Restart();
            for (i = 0; i < count; i++)
            {
                str = System.Text.Encoding.UTF8.GetString(utf8);
            }
            UnityEngine.Debug.Log($"{count:#,0} calls of UTF8.GetString: {timer.ElapsedMilliseconds * 0.001f:0.000} seconds");

            //sequenceEqual
            // - done fastest if byte array length is different.
            // - done faster if different appears early in array.
            var otherUTF8 = System.Text.Encoding.UTF8.GetBytes("XXH32 reference implementation: ok");  //diff: OK -> ok
            timer.Restart();
            for (i = 0; i < count; i++)
            {
                utf8.AsSpan().SequenceEqual(otherUTF8);
            }
            UnityEngine.Debug.Log($"{count:#,0} calls of Span.SequenceEqual: {timer.ElapsedMilliseconds * 0.001f:0.000} seconds");


            return 0;
        }

#endif

    }
}
