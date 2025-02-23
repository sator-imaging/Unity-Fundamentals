/** StrictEnum for Unity and C# / .NET
 ** (c) 2024 Sator Imaging, Licensed under the MIT License
 ** https://github.com/sator-imaging/Unity-Fundamentals

StrictEnum is an efficient library to reuse Enum caches stored in system library instead of
creating sophisticated dictionary. And also there are allocation-free methods for Unity.

How to Use
==========
```cs
// retrieve cached names and values
var names = StrictEnum.GetFieldNames<TEnum>();
var labels = StrictEnum.GetDisplayNames<TEnum>();
var values = StrictEnum.GetValues<TEnum>();
var numbers = StrictEnum.GetUnderlyingValues<TEnum>();           // ReadOnlySpan<ulong>
var asMemory = StrictEnum.GetUnderlyingValuesAsMemory<TEnum>();  // ReadOnlyMemory<ulong>

// get field/display name from enum value
var name = StrictEnum.ToFieldNameString<MyEnum>(val);     // or val.ToFieldNameString();
var label = StrictEnum.ToDisplayNameString<MyEnum>(val);  // or val.ToDisplayNameString();

// get enum value by field/display name. `TryGet*` methods are also available
val = StrictEnum.GetValueByFieldName<MyEnum>(text);
val = StrictEnum.GetValueByFieldName<MyEnum>(text, StringComparison.OrdinalIgnoreCase);
val = StrictEnum.GetValueByDisplayName<MyEnum>(text);

// to customize display names, set factory method *before* call methods
// note that duplicate entry is not allowed except for 0 or 1 character names
StrictEnum.CustomDisplayNameFactory(enumType, (enumType, fieldName, indexAsULong) =>
{
    return StrictEnum.BeautifyFieldName(fieldName);  // builtin helper method
});

// underlying type validation
_ = StrictEnum.IsInt32(typeof(MyEnum));
_ = StrictEnum.IsInt32Convertible(typeof(MyEnum));   // enum value can be casted to int32 (eg. short, byte)
_ = StrictEnum.IsUnderlyingValueSigned(typeof(MyEnum));
_ = StrictEnum.IsOrdinalFromZero(typeof(MyEnum));    // enum value is starting from 0 and increment by 1
                                                     // i.e. can be used as array index `names[(int)enumValue]`
```


TODO
====
- Support .NET 8 or greater version. (InternalGet** methods are gone)
    - https://github.com/dotnet/runtime/blob/v8.0.0/src/coreclr/nativeaot/System.Private.CoreLib/src/System/Enum.NativeAot.cs#L40
    - https://github.com/dotnet/runtime/blob/v8.0.0/src/libraries/System.Private.CoreLib/src/System/Enum.EnumInfo.cs

 */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Buffers;
using System.Linq;

#nullable enable

#if UNITY_5_3_OR_NEWER
using NUnit.Framework;
#endif

#if UNITY_2020_1_OR_NEWER
using Unsafe = Unity.Collections.LowLevel.Unsafe.UnsafeUtility;
#endif

#pragma warning disable IDE0079

#pragma warning disable SMA0020
#pragma warning disable SMA0021
#pragma warning disable SMA0022
#pragma warning disable SMA0023
#pragma warning disable SMA0024
#pragma warning disable SMA0025
#pragma warning disable SMA0026
#pragma warning disable SMA0027
#pragma warning disable SMA0028
#pragma warning disable SMA0029

namespace SatorImaging.UnityFundamentals
{
    /// <summary>
    /// Enum helper functions & extension methods.
    /// </summary>
    public static class StrictEnum
    {
        const StringComparison DEFAULT_STR_COMPARISON = StringComparison.Ordinal;

        /// <summary>
        /// By default, <see cref="EnumMemberAttribute"/> is used to generate display name if exists, otherwise beautify field name.
        /// Note that factory method is used only once and automatically cleared after display name generation.
        /// (In Unity, <c>InspectorName</c> attribute is priority)
        /// </summary>
        /// <remarks>
        /// NOTE: underlying value is casted to <see langword="ulong"/> with unchecked manner.
        /// </remarks>
        public static void CustomDisplayNameFactory(Type enumType, Func<Type, string, ulong, string> factory)
            => _customDisplayNameFactory[enumType] = factory;

        readonly static Dictionary<Type, Func<Type, string, ulong, string>> _customDisplayNameFactory = new();
        readonly static Func<Type, string, ulong, string> _defaultDisplayNameFactory = static (enumType, fieldName, index) =>
        {
            if (string.IsNullOrWhiteSpace(fieldName))
                return "<NO NAME>";

            var field = enumType.GetField(fieldName);

            return
#if UNITY_2019_4_OR_NEWER
                field.GetCustomAttribute<UnityEngine.InspectorNameAttribute>()?.displayName ??
#endif
                field.GetCustomAttribute<EnumMemberAttribute>()?.Value
                ?? BeautifyFieldName(fieldName);
        };


        /*  string op  ================================================================ */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsSeparatorChar(char c) => c is '_' /*or '-'*/ or '.' or '+' /* required --> */ or ' ';  // '+' used as inner class separator

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsSpaceRequiredChar(char c) => char.IsUpper(c) || char.IsDigit(c);  // or char.IsNumber?

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsNoSpaceAfterChar(char c) => c is ' ' or '-' || IsSpaceRequiredChar(c);  // hyphen is special


        readonly static SpanAction<char, string> cache_BeautifyShortName = static (span, name) =>
        {
            span[0] = char.ToUpperInvariant(name[0]);
            name.AsSpan(1).CopyTo(span.Slice(1));
        };

        /// <summary>Reviced version of <c>UnityEditor.ObjectNames.NicifyVariableName</c>></summary>
        /// <returns>Empty or unchanged when input name is null or too short</returns>
        public static string BeautifyFieldName(string fieldName)
        {
            if (fieldName == null || fieldName.Length == 0)
            {
                return string.Empty;
            }

            if (fieldName.Length <= 4)
            {
                if (fieldName.Length == 1)
                {
                    return char.ToUpperInvariant(fieldName[0]).ToString();
                }
                else
                {
                    return string.Create(fieldName.Length, fieldName, cache_BeautifyShortName);
                }
            }


            Span<char> span = stackalloc char[fieldName.Length * 2];
            int consumed = fieldName.Length;
            fieldName.AsSpan().CopyTo(span);

            // major prefix
            if (span[0] == 'm' && span[1] == '_')
            {
                span = span.Slice(2);
                consumed -= 2;
            }
            else if (span[0] == 'k' && !(((span[1] is >= 'a' and <= 'z') || (span[1] is >= '0' and <= '9'))))
            {
                span = span.Slice(1);
                consumed -= 1;
            }

            //trim
            while (IsSeparatorChar(span[0]))
            {
                if (--consumed <= 0)
                    return fieldName;
                span = span.Slice(1);
            }
            while (IsSeparatorChar(span[consumed - 1]))
            {
                if (--consumed <= 0)
                    return fieldName;
                // don't need to trim --> span = span.Slice(0, span.Length - 1);
            }


            // from end of string, replace separator with space and remove repeating
            int pos = consumed - 1 - 1;  // last char has already checked above
            while (pos > 0)  // first char has already checked above
            {
                if (IsSeparatorChar(span[pos]))
                {
                    int next = pos + 1;
                    if (IsSeparatorChar(span[next]))
                    {
                        span.Slice(next).CopyTo(span.Slice(pos));
                        consumed--;
                    }
                    else
                    {
                        span[pos] = ' ';
                    }
                }

                pos--;
            }


            // from end, insert space before uppercase char and number
            pos = consumed - 1;
            while (pos > 0)  // don't check first char
            {
                if (IsSpaceRequiredChar(span[pos]))
                {
                    if (IsNoSpaceAfterChar(span[pos - 1]))
                        goto NEXT;

                    //ummmm...
                    if (pos == 1 && char.IsDigit(span[pos]))
                        goto NEXT;

                    span.Slice(pos, consumed - pos).CopyTo(span.Slice(pos + 1));
                    consumed++;

                    span[pos] = ' ';
                }

            NEXT:
                pos--;
            }


            // make char uppercase
            span = span.Slice(0, consumed);

            span[0] = char.ToUpperInvariant(span[0]);
            for (int i = 1; i < span.Length; i++)
            {
                if (span[i] != ' ')
                    continue;

                i++;
                if (i >= span.Length)
                    break;

                span[i] = char.ToUpperInvariant(span[i]);
            }

            return new(((ReadOnlySpan<char>)span).Trim());
        }


        /*  reflection  ================================================================ */

        // to keep generic type caching class small
        public static class CacheHelper
        {
            readonly static MethodInfo InternalGetNames;
            readonly static MethodInfo InternalGetValues;

            static CacheHelper()
            {
                //https://github.com/microsoft/referencesource/blob/master/mscorlib/system/enum.cs#L532
                InternalGetNames = typeof(Enum).GetMethod(nameof(InternalGetNames), BindingFlags.NonPublic | BindingFlags.Static)
                    ?? typeof(Enum).GetMethod(nameof(Enum.GetNames), BindingFlags.Public | BindingFlags.Static);

                //https://github.com/microsoft/referencesource/blob/master/mscorlib/system/enum.cs#L505
                InternalGetValues = typeof(Enum).GetMethod(nameof(InternalGetValues), BindingFlags.NonPublic | BindingFlags.Static)
                    ?? typeof(CacheHelper).GetMethod(nameof(GetValuesAsULong), BindingFlags.Public | BindingFlags.Static);
            }


            /// <summary>Fail-safe method for <see cref="InternalGetValues"/></summary>
            public static ulong[] GetValuesAsULong(Type enumType)
            {
                var values = Enum.GetValues(enumType);
                var result = new ulong[values.Length];

                unchecked
                {
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = (ulong)values.GetValue(i);
                    }
                }

                return result;
            }


            readonly static object[] cache_methodInfoArgs = new object[1];

            public static (bool hasFlagsAttr, TypeCode underlyingTypeCode, ulong[] values, string[] fieldNames, string[] displayNames)
                GetEnumInfo(Type enumType)
            {
                bool hasFlags;
                TypeCode underlying = TypeCode.Empty;
                ulong[] values;
                string[] fieldNames;
                string[] displayNames;

                hasFlags = enumType.GetCustomAttribute<FlagsAttribute>() != null;

                var ut = Enum.GetUnderlyingType(enumType);
#pragma warning disable format
#pragma warning disable IDE2001
                     if (ut == typeof(int))     underlying = TypeCode.Int32;
                else if (ut == typeof(byte))    underlying = TypeCode.Byte;
                else if (ut == typeof(uint))    underlying = TypeCode.UInt32;
                else if (ut == typeof(ulong))   underlying = TypeCode.UInt64;
                else if (ut == typeof(ushort))  underlying = TypeCode.UInt16;
                else if (ut == typeof(long))    underlying = TypeCode.Int64;
                else if (ut == typeof(short))   underlying = TypeCode.Int16;
                else if (ut == typeof(sbyte))   underlying = TypeCode.SByte;
#pragma warning restore IDE2001
#pragma warning restore format


                cache_methodInfoArgs[0] = enumType;

                values = InternalGetValues.Invoke(null, cache_methodInfoArgs) as ulong[]
                    ?? throw new NullReferenceException(nameof(InternalGetValues));

                fieldNames = InternalGetNames.Invoke(null, cache_methodInfoArgs) as string[]
                    ?? throw new NullReferenceException(nameof(InternalGetNames));

                if (fieldNames.Length != values.Length)
                    throw new IndexOutOfRangeException("name and value array lengths don't match");


                var displayNameFunc = _defaultDisplayNameFactory;

                //customized??
                if (_customDisplayNameFactory.TryGetValue(enumType, out var customFactory))
                {
                    displayNameFunc = customFactory;
                    _customDisplayNameFactory.Remove(enumType);  // never called anymore
                }

                displayNames = new string[fieldNames.Length];
                for (int i = 0; i < displayNames.Length; i++)
                {
                    displayNames[i] = displayNameFunc.Invoke(enumType, fieldNames[i], values[i]);
                }

                // NOTE: do not allow duplicate entry except for "" or "_" or any 1 char entries
                var check_displayNames = displayNames.Where(x => x != null && x.Length > 1);
                if (check_displayNames.Count() != check_displayNames.Distinct().Count())
                {
                    throw new Exception(nameof(StrictEnum) + ": display name has duplicate entry: " + enumType
                        + "\n" + string.Join("\n", displayNames.Select(x => x ?? "<NULL>")));
                }

                return (hasFlags, underlying, values, fieldNames, displayNames);
            }

        }


        /// <summary>
        /// Cached enum info.
        /// </summary>
        public static class EnumInfo<TEnum> where TEnum : Enum
        {
            public static readonly bool HasFlagsAttribute;
            public static readonly TypeCode UnderlyingType;
            public static readonly ReadOnlyMemory<ulong> Values;
            public static readonly ReadOnlyMemory<string> FieldNames;
            public static readonly ReadOnlyMemory<string> DisplayNames;

            static EnumInfo() => (HasFlagsAttribute, UnderlyingType, Values, FieldNames, DisplayNames) = CacheHelper.GetEnumInfo(typeof(TEnum));

            public static TEnum[]? ActualValues { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; internal set; }
            public static Dictionary<TEnum, string>? FieldNameByValue { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; internal set; }
            public static Dictionary<TEnum, string>? DisplayNameByValue { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; internal set; }
        }


        /*  cache accessor  ================================================================ */

        /// <summary>
        /// Get enum entries as string array.
        /// </summary>
        /// <remarks>
        /// NOTE: entries are sorted by its underlying value.
        /// <br/>
        /// NOTE: enum entries are constant value fields and those names are changed when assembly is obfuscated.
        /// Consider add <see cref="ObfuscationAttribute"/> to enum type declaration to prevent name changes.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<string> GetFieldNames<TEnum>() where TEnum : Enum => EnumInfo<TEnum>.FieldNames.Span;

        /// <summary>
        /// To customize display names, call <see cref="CustomDisplayNameFactory"/> **BEFORE** calling other <see cref="StrictEnum"/> method.
        /// </summary>
        /// <inheritdoc cref="GetFieldNames{TEnum}"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<string> GetDisplayNames<TEnum>() where TEnum : Enum => EnumInfo<TEnum>.DisplayNames.Span;

        /// <summary>Get <see langword="ulong"/> representation of underlying values.</summary>
        /// <remarks>NOTE: <see langword="long"/> is casted in <see langword="unchecked"/> manner.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<ulong> GetUnderlyingValues<TEnum>() where TEnum : Enum => EnumInfo<TEnum>.Values.Span;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyMemory<ulong> GetUnderlyingValuesAsMemory<TEnum>() where TEnum : Enum => EnumInfo<TEnum>.Values;


        public static ReadOnlySpan<TEnum> GetValues<TEnum>() where TEnum : Enum
        {
            var result = EnumInfo<TEnum>.ActualValues;
            if (result != null)
                return result;

            var values = EnumInfo<TEnum>.Values.Span;

            result = new TEnum[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                result[i] = ToEnumValue<TEnum>(values[i]);
            }

            EnumInfo<TEnum>.ActualValues = result;
            return result;
        }


        public static Dictionary<TEnum, string> GetFieldNameDictionary<TEnum>() where TEnum : Enum
        {
            var result = EnumInfo<TEnum>.FieldNameByValue;
            if (result != null)
                return result;

            var values = EnumInfo<TEnum>.Values.Span;
            var names = EnumInfo<TEnum>.FieldNames.Span;

            result = new(capacity: values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                result.Add(ToEnumValue<TEnum>(values[i]), names[i]);
            }

            EnumInfo<TEnum>.FieldNameByValue = result;
            return result;
        }

        public static Dictionary<TEnum, string> GetDisplayNameDictionary<TEnum>() where TEnum : Enum
        {
            var result = EnumInfo<TEnum>.DisplayNameByValue;
            if (result != null)
                return result;

            var values = EnumInfo<TEnum>.Values.Span;
            var names = EnumInfo<TEnum>.DisplayNames.Span;

            result = new(capacity: values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                result.Add(ToEnumValue<TEnum>(values[i]), names[i]);
            }

            EnumInfo<TEnum>.DisplayNameByValue = result;
            return result;
        }


        /*  formatting  ================================================================ */

        /// <returns>
        /// Number of chars written.
        /// <br/>
        /// * 0: <c>TEnum</c> value is required to be formatted as string
        /// <br/>
        /// * -1: no enough capacity
        /// </returns>
        static int TryWriteUnderlyingValueOrThrow<TEnum>(ulong underlying, Span<char> span, ReadOnlySpan<char> format, IFormatProvider? provider)
            where TEnum : Enum
        {
            bool signed = IsUnderlyingValueSigned(typeof(TEnum));

            if (format.Length == 1)
            {
                int charsWritten;

                switch (format[0] | 0x20)  // to lowercase
                {
                    case 'g':
                    case 'f':  // try format 'Flags'
                        return 0;

                    case 'd':
                        if (signed)
                        {
                            if ((((unchecked((long)underlying)))).TryFormat(span, out charsWritten, (((default))), provider))
                                return charsWritten;
                            else
                                return -1;
                        }
                        else
                        {
                            if (underlying.TryFormat(span, out charsWritten, (((default))), provider))
                                return charsWritten;
                            else
                                return -1;
                        }

                    case 'x':
                        bool isSuccess;
                        switch (EnumInfo<TEnum>.UnderlyingType)
                        {
                            case TypeCode.Int32:
                            case TypeCode.UInt32:
                                if (signed)
                                {
                                    isSuccess = (((unchecked((long)underlying)))).TryFormat(span, out charsWritten, "X8", provider);
                                }
                                else
                                {
                                    isSuccess = underlying.TryFormat(span, out charsWritten, "X8", provider);
                                }
                                break;

                            case TypeCode.Int64:
                            case TypeCode.UInt64:
                                if (signed)
                                {
                                    isSuccess = (((unchecked((long)underlying)))).TryFormat(span, out charsWritten, "X16", provider);
                                }
                                else
                                {
                                    isSuccess = underlying.TryFormat(span, out charsWritten, "X16", provider);
                                }
                                break;

                            case TypeCode.Int16:
                            case TypeCode.UInt16:
                                if (signed)
                                {
                                    isSuccess = (((unchecked((long)underlying)))).TryFormat(span, out charsWritten, "X4", provider);
                                }
                                else
                                {
                                    isSuccess = underlying.TryFormat(span, out charsWritten, "X4", provider);
                                }
                                break;

                            default:
                                if (signed)
                                {
                                    isSuccess = (((unchecked((long)underlying)))).TryFormat(span, out charsWritten, "X2", provider);
                                }
                                else
                                {
                                    isSuccess = underlying.TryFormat(span, out charsWritten, "X2", provider);
                                }
                                break;
                        }

                        if (isSuccess)
                            return charsWritten;
                        else
                            return -1;
                }
            }

            throw new FormatException("format accepts only any of G, D, X, F or lowercase of those");
        }


        public static bool TryFormat<TEnum>(this TEnum value,
                                            Span<char> span,
                                            out int charsWritten,
                                            ReadOnlySpan<char> format = default,
                                            IFormatProvider? provider = null
            )
            where TEnum : Enum
        {
            var underlying = ToULong(value);

            if (format.Length != 0)
            {
                charsWritten = TryWriteUnderlyingValueOrThrow<TEnum>(underlying, span, format, provider);

                if (charsWritten < 0)
                {
                    goto NO_ENOUGH_CAPACITY;
                }
                else if (charsWritten > 0)
                {
                    return true;
                }
            }

            if (!EnumInfo<TEnum>.HasFlagsAttribute || underlying == 0)  // 'Flags' enum can have 0
            {
                var text = ToFieldNameString(value);
                if (text == null)
                {
                    goto FORMAT_AS_NUMBER;
                }

                if (text.Length > span.Length)
                {
                    goto NO_ENOUGH_CAPACITY;
                }

                text.AsSpan().CopyTo(span);
                charsWritten = text.Length;
                return true;
            }

            //flags!!
            const string SEP = ", ";

            var values = EnumInfo<TEnum>.Values.Span;
            var names = EnumInfo<TEnum>.FieldNames.Span;

            var trimmed = span;

            ulong foundValues = 0;
            charsWritten = 0;
            for (int i = 0; i < values.Length; i++)
            {
                // ignore 0 in 'Flags', attribute check is done before
                if (values[i] == 0)
                    continue;

                if ((underlying & values[i]) == 0)
                    continue;

                //separator?
                if (charsWritten > 0)
                {
                    if (SEP.Length > trimmed.Length)
                    {
                        goto NO_ENOUGH_CAPACITY;
                    }

                    SEP.AsSpan().CopyTo(trimmed);
                    charsWritten += SEP.Length;
                    trimmed = trimmed.Slice(SEP.Length);  // advance!!
                }

                var text = names[i];
                if (text.Length > trimmed.Length)
                {
                    goto NO_ENOUGH_CAPACITY;
                }

                text.AsSpan().CopyTo(trimmed);
                charsWritten += text.Length;
                trimmed = trimmed.Slice(text.Length);  // advance!!

                foundValues |= values[i];
            }

            // nothing found or contains unknown value
            if (charsWritten == 0 || foundValues != underlying)
            {
                goto FORMAT_AS_NUMBER;
            }

            return true;


        FORMAT_AS_NUMBER:
            if (IsUnderlyingValueSigned(typeof(TEnum)))
            {
                return (((unchecked((long)underlying)))).TryFormat(span, out charsWritten, format, provider);
            }
            else
            {
                return underlying.TryFormat(span, out charsWritten, format, provider);
            }


        NO_ENOUGH_CAPACITY:
            charsWritten = 0;  // need to reset to zero
            return false;
        }


        /// <summary>
        /// Always ignore empty entries for Flags enum type. (e.g. <c>"Empty,,,Entries"</c> will return <c>'Empty | Entries'</c>)
        /// </summary>
        /// <param name="strictFlagsMatch">
        /// <see langword="false"/> to allow partial entry match. (ex. <c>"Found,NotFound"</c> will return <c>'Found'</c>)
        /// </param>
        public static bool TryParse<TEnum>(ReadOnlySpan<char> text,
                                           out TEnum result,
                                           StringComparison comparison = DEFAULT_STR_COMPARISON,
                                           bool strictFlagsMatch = true,
                                           string splitAny = ", "
            )
            where TEnum : Enum
        {
            if (!EnumInfo<TEnum>.HasFlagsAttribute)
            {
                goto SINGLE_ENTRY;
            }

            var remain = text;  // don't trim here!! single entry code path accepts only string!! --> //.Trim(splitAny);
            int sep = remain.IndexOfAny(splitAny);
            if (sep < 0)
            {
                goto SINGLE_ENTRY;
            }

            // update after check!!
            sep = 0;

            var names = EnumInfo<TEnum>.FieldNames.Span;
            var values = EnumInfo<TEnum>.Values.Span;

            ulong foundValues = 0;
            for (; ; )
            {
                remain = remain.Slice(sep).TrimStart(splitAny);
                sep = remain.IndexOfAny(splitAny);
                if (sep < 0)
                {
                    sep = remain.Length;
                }

                var current = remain.Slice(0, sep);
                if (current.Length == 0)
                    break;

                bool notFound = true;
                for (int i = 0; i < names.Length; i++)
                {
                    if (current.Equals(names[i], comparison))
                    {
                        foundValues |= values[i];
                        notFound = false;
                        break;
                    }
                }

                if (notFound && strictFlagsMatch)
                {
                    goto NOT_FOUND;
                }
            }

            result = ToEnumValue<TEnum>(foundValues);
            return true;


        NOT_FOUND:
            result = (((default)!));
            return false;


        SINGLE_ENTRY:
            return TryParseSingleEntry(text, out result, comparison);
        }


        static bool TryParseSingleEntry<TEnum>(ReadOnlySpan<char> text, out TEnum result, StringComparison comparison)
            where TEnum : Enum
        {
            if (TryGetValueByFieldName(text, out result, comparison))
            {
                return true;
            }

            // try parse by underlying value
            ulong underlying;
            if (!ulong.TryParse(text, out underlying))
            {
                if (!long.TryParse(text, out var candidate))
                {
                    goto NOT_FOUND;
                }
                underlying = unchecked((ulong)candidate);
            }

            var values = EnumInfo<TEnum>.Values.Span;

            if (!EnumInfo<TEnum>.HasFlagsAttribute)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i] == underlying)
                    {
                        result = ToEnumValue<TEnum>(underlying);
                        return true;
                    }
                }

                goto NOT_FOUND;
            }

            // are all values found??
            ulong remain = underlying;
            for (int i = 0; i < values.Length; i++)
            {
                if ((underlying & values[i]) != 0)
                {
                    remain &= ~values[i];
                }
            }

            if (remain == 0)
            {
                result = ToEnumValue<TEnum>(underlying);
                return true;
            }


        NOT_FOUND:
            result = (((default)!));
            return false;
        }


        /*  unsafe  ================================================================ */

        public static ulong ToULong<TEnum>(this TEnum value) where TEnum : Enum
        {
            unchecked
            {
                return EnumInfo<TEnum>.UnderlyingType switch
                {
                    TypeCode.Int32 =>   /**/ (ulong)Unsafe.As<TEnum, int>(ref value),
                    TypeCode.Byte =>    /**/        Unsafe.As<TEnum, byte>(ref value),
                    TypeCode.UInt32 =>  /**/        Unsafe.As<TEnum, uint>(ref value),
                    TypeCode.UInt64 =>  /**/        Unsafe.As<TEnum, ulong>(ref value),
                    TypeCode.UInt16 =>  /**/        Unsafe.As<TEnum, ushort>(ref value),
                    TypeCode.Int64 =>   /**/ (ulong)Unsafe.As<TEnum, long>(ref value),
                    TypeCode.Int16 =>   /**/ (ulong)Unsafe.As<TEnum, short>(ref value),
                    TypeCode.SByte =>   /**/ (ulong)Unsafe.As<TEnum, sbyte>(ref value),
                    _ => throw new NotSupportedException("unsupported enum type: " + typeof(TEnum))
                };
            }
        }

        [Obsolete]
        public static long ToLong<TEnum>(this TEnum value) where TEnum : Enum
        {
            unchecked
            {
                return EnumInfo<TEnum>.UnderlyingType switch
                {
                    TypeCode.Int32 =>   /**/       Unsafe.As<TEnum, int>(ref value),
                    TypeCode.Byte =>    /**/       Unsafe.As<TEnum, byte>(ref value),
                    TypeCode.UInt32 =>  /**/       Unsafe.As<TEnum, uint>(ref value),
                    TypeCode.UInt64 =>  /**/ (long)Unsafe.As<TEnum, ulong>(ref value),
                    TypeCode.UInt16 =>  /**/       Unsafe.As<TEnum, ushort>(ref value),
                    TypeCode.Int64 =>   /**/       Unsafe.As<TEnum, long>(ref value),
                    TypeCode.Int16 =>   /**/       Unsafe.As<TEnum, short>(ref value),
                    TypeCode.SByte =>   /**/       Unsafe.As<TEnum, sbyte>(ref value),
                    _ => throw new NotSupportedException("unsupported enum type: " + typeof(TEnum))
                };
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TEnum ToEnumValue<TEnum>(long value) where TEnum : Enum => ToEnumValue<TEnum>(unchecked((ulong)value));

        public static TEnum ToEnumValue<TEnum>(ulong value) where TEnum : Enum
        {
            return EnumInfo<TEnum>.UnderlyingType switch
            {
                TypeCode.Int32 =>   /**/ Unsafe.As<int, TEnum>(ref Unsafe.As<ulong, int>(ref value)),
                TypeCode.Byte =>    /**/ Unsafe.As<byte, TEnum>(ref Unsafe.As<ulong, byte>(ref value)),
                TypeCode.UInt32 =>  /**/ Unsafe.As<uint, TEnum>(ref Unsafe.As<ulong, uint>(ref value)),
                TypeCode.UInt64 =>  /**/ Unsafe.As<ulong, TEnum>(ref value),
                TypeCode.UInt16 =>  /**/ Unsafe.As<ushort, TEnum>(ref Unsafe.As<ulong, ushort>(ref value)),
                TypeCode.Int64 =>   /**/ Unsafe.As<long, TEnum>(ref Unsafe.As<ulong, long>(ref value)),
                TypeCode.Int16 =>   /**/ Unsafe.As<short, TEnum>(ref Unsafe.As<ulong, short>(ref value)),
                TypeCode.SByte =>   /**/ Unsafe.As<sbyte, TEnum>(ref Unsafe.As<ulong, sbyte>(ref value)),
                _ => throw new NotSupportedException("unsupported enum type: " + typeof(TEnum))
            };
        }


        /*  enum extensions  ================================================================ */

        // NOTE: 'String' suffix is redundant but required to being listed in suggestion by typing 'ToString'

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string? ToFieldNameString<TEnum>(this TEnum value) where TEnum : Enum => ToFieldNameString<TEnum>(value.ToULong());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string? ToDisplayNameString<TEnum>(this TEnum value) where TEnum : Enum => ToDisplayNameString<TEnum>(value.ToULong());


        /* =====  by integer  ===== */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string? ToFieldNameString<TEnum>(long value) where TEnum : Enum => ToFieldNameString<TEnum>(unchecked((ulong)value));

        public static string? ToFieldNameString<TEnum>(ulong value) where TEnum : Enum
        {
            int index = EnumInfo<TEnum>.Values.Span.IndexOf(value);
            return index < 0 ? null : EnumInfo<TEnum>.FieldNames.Span[index];
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string? ToDisplayNameString<TEnum>(long value) where TEnum : Enum => ToDisplayNameString<TEnum>(unchecked((ulong)value));

        public static string? ToDisplayNameString<TEnum>(ulong value) where TEnum : Enum
        {
            int index = EnumInfo<TEnum>.Values.Span.IndexOf(value);
            return index < 0 ? null : EnumInfo<TEnum>.DisplayNames.Span[index];
        }


        /*  value by string  ================================================================ */

        /* =      field name      = */

        public static TEnum GetValueByFieldName<TEnum>(ReadOnlySpan<char> text, StringComparison comparison = DEFAULT_STR_COMPARISON, TEnum valueIfNotFound = (((default)!)))
             where TEnum : Enum
        {
            if (TryGetValueByFieldName<TEnum>(text, out var result, comparison))
                return result;

            return valueIfNotFound;
        }


        public static bool TryGetValueByFieldName<TEnum>(ReadOnlySpan<char> text, out TEnum result, StringComparison comparison = DEFAULT_STR_COMPARISON)
             where TEnum : Enum
        {
            var names = EnumInfo<TEnum>.FieldNames.Span;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].AsSpan().Equals(text, comparison))
                {
                    result = ToEnumValue<TEnum>(EnumInfo<TEnum>.Values.Span[i]);
                    return true;
                }
            }

            result = (((default)!));
            return false;
        }


        /* =      display name      = */

        public static TEnum GetValueByDisplayName<TEnum>(ReadOnlySpan<char> text, StringComparison comparison = DEFAULT_STR_COMPARISON, TEnum valueIfNotFound = (((default)!)))
             where TEnum : Enum
        {
            if (TryGetValueByDisplayName<TEnum>(text, out var result, comparison))
                return result;

            return valueIfNotFound;
        }

        public static bool TryGetValueByDisplayName<TEnum>(ReadOnlySpan<char> text, out TEnum result, StringComparison comparison = DEFAULT_STR_COMPARISON)
             where TEnum : Enum
        {
            var names = EnumInfo<TEnum>.DisplayNames.Span;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].AsSpan().Equals(text, comparison))
                {
                    result = ToEnumValue<TEnum>(EnumInfo<TEnum>.Values.Span[i]);
                    return true;
                }
            }

            result = (((default)!));
            return false;
        }


        /*  type validation  ================================================================ */

        // NOTE: don't make these methods generic. it bloats assembly code size

        public static bool IsInt32(Type enumType) => Enum.GetUnderlyingType(enumType) == typeof(int);


        // C# only allows 8 types: (s)byte, (u)short, (u)int and (u)long

        /// <summary>Whether underlying value can be converted to <see langword="int"/> or not.</summary>
        /// <remarks>NOTE: false when underlying type is <see langword="uint"/>.</remarks>
        public static bool IsInt32Convertible(Type enumType)
        {
            var ut = Enum.GetUnderlyingType(enumType);
            return (
                ut == typeof(int)
             || ut == typeof(byte)
             || ut == typeof(short)
             || ut == typeof(ushort)
             || ut == typeof(sbyte)
                );
        }


        public static bool IsUnderlyingValueSigned(Type enumType)
        {
            var ut = Enum.GetUnderlyingType(enumType);
            return (
                ut == typeof(int)
             || ut == typeof(long)
             || ut == typeof(short)
             || ut == typeof(sbyte)
                );
        }


        /// <remarks>NOTE: inefficient method so do not try to call repeatedly</remarks>
        /// <returns>
        /// return <see langword="false"/> if enum has entries more than <see cref="Array.MaxLength"/>
        /// </returns>
        public static bool IsOrdinalFromZero(Type enumType)
        {
            //https://github.com/dotnet/runtime/blob/v6.0.0/src/libraries/System.Private.CoreLib/src/System/Array.cs#L1960
            const int MAX_LENGTH = 0X7FFFFFC7;  // not in .NET standard 2.1

            var values = Enum.GetValues(enumType);
            if (values.LongLength > MAX_LENGTH)
                return false;

            unchecked
            {
                // most common case
                if (IsInt32Convertible(enumType))
                {
                    var len = values.Length;
                    for (int i = 0; i < len; i++)
                    {
                        if (i != Convert.ToInt32(values.GetValue(i)))
                            return false;
                    }
                }
                else
                {
                    bool signed = IsUnderlyingValueSigned(enumType);

                    var len = (ulong)values.Length;  // use .Length, range check is done on start of this method
                    for (ulong i = 0; i < len; i++)
                    {
                        if (signed)
                        {
                            if (i != (ulong)Convert.ToInt64(values.GetValue((long)i)))
                                return false;
                        }
                        else
                        {
                            if (i != Convert.ToUInt64(values.GetValue((long)i)))
                                return false;
                        }
                    }
                }
            }

            return true;
        }

    }
}



#region ////////  TEMPLATE: Unity Editor Tests  ////////

#if UNITY_EDITOR

#pragma warning disable IDE1006

// TEMPLATE: namespace naming convention: <TEST_TARGET>.<ROOT_MENU_LABEL>.<SUB_MENU_LABEL>
namespace SatorImaging.UnityFundamentals.TEST.Strict_Enum  // must be unique, don't reuse existing namespace
{
    public static class UNITY_EDITOR_TESTS  // don't change
    {
        const string MENU_ROOT = nameof(TEST) + "/" + nameof(Strict_Enum) + "/";


        #region ////////  TEMPLATE: Test Methods  ////////
        /*  TEMPLATE: Test Methods  ================================================================ */

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Test_BeautifyFieldName), priority = 0)]
        [Test]
        public static void Test_BeautifyFieldName()
        {
            var source_expected = new string[]
            {
                "x", "X",
                "abc", "Abc",
                "_____", "_____",
                "AB123", "AB123",
                "Ab123", "Ab 123",
                "uppercaseCharAtLastZ", "Uppercase Char At Last Z",
                "aWord-UppercaseAt2nd", "A Word-Uppercase At 2nd",
                "m___MUnder       score      __Starting!!___", "MUnder Score Starting!!",
                "---Hyphen-Allowed-&-No-Space!!---", "---Hyphen-Allowed-&-No-Space!!---",
                "konst   @name100!??", "Konst @name 100!??",
                "k0nst", "K0nst",
                "kConstantVar____+___innerClass", "Constant Var Inner Class",
                "...dot.separated...string...", "Dot Separated String",
            };
            for (int i = 0; i < source_expected.Length; i += 2)
            {
                Assert.That(StrictEnum.BeautifyFieldName(source_expected[i]), Is.EqualTo(source_expected[i + 1]));
            }
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Test_UnsafeUtility_SizeOf), priority = 0)]
        [Test]
        public static void Test_UnsafeUtility_SizeOf()
        {
            Assert.That(Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<EULong>(), Is.EqualTo(8));
            Assert.That(Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<ELong>(), Is.EqualTo(8));
            Assert.That(Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<EInt>(), Is.EqualTo(4));
            Assert.That(Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<EShort>(), Is.EqualTo(2));
            Assert.That(Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<EByte>(), Is.EqualTo(1));
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(TryFormat_Tests), priority = 0)]
        [Test]
        public static void TryFormat_Tests()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            int written;
            Span<char> span = stackalloc char[1];

            Assert.That(!StringComparison.InvariantCulture.TryFormat(span, out written));
            Assert.That(written, Is.EqualTo(0));

            Assert.That(!MethodImplOptions.AggressiveInlining.TryFormat(span, out written));
            Assert.That(written, Is.EqualTo(0));

            Assert.That(!(MethodImplOptions.AggressiveInlining | MethodImplOptions.NoOptimization).TryFormat(span, out written));
            Assert.That(written, Is.EqualTo(0));


            span = new char[2048];

            Assert.That(StringComparison.InvariantCulture.TryFormat(span, out written));
            Assert.That(span.Slice(0, written).ToString(), Is.EqualTo(StringComparison.InvariantCulture.ToString()));

            Assert.That(MethodImplOptions.AggressiveInlining.TryFormat(span, out written));
            Assert.That(span.Slice(0, written).ToString(), Is.EqualTo(MethodImplOptions.AggressiveInlining.ToString()));

            Assert.That((MethodImplOptions.AggressiveInlining | MethodImplOptions.NoOptimization).TryFormat(span, out written));
            Assert.That(span.Slice(0, written).ToString(), Is.EqualTo((MethodImplOptions.AggressiveInlining | MethodImplOptions.NoOptimization).ToString()));


            //number??
            Assert.That(StringComparison.InvariantCulture.TryFormat(span, out written, "d"));
            Assert.That(span.Slice(0, written).ToString(), Is.EqualTo(StringComparison.InvariantCulture.ToString("d")));
            Assert.That(StringComparison.InvariantCulture.TryFormat(span, out written, "D"));
            Assert.That(span.Slice(0, written).ToString(), Is.EqualTo(StringComparison.InvariantCulture.ToString("D")));

            Assert.That((MethodImplOptions.AggressiveInlining | MethodImplOptions.InternalCall).TryFormat(span, out written, "d"));
            Assert.That(span.Slice(0, written).ToString(), Is.EqualTo((MethodImplOptions.AggressiveInlining | MethodImplOptions.InternalCall).ToString("d")));
            Assert.That((MethodImplOptions.AggressiveInlining | MethodImplOptions.InternalCall).TryFormat(span, out written, "D"));
            Assert.That(span.Slice(0, written).ToString(), Is.EqualTo((MethodImplOptions.AggressiveInlining | MethodImplOptions.InternalCall).ToString("D")));


            //hex??
            Assert.That(StringComparison.InvariantCulture.TryFormat(span, out written, "x"));
            Assert.That(span.Slice(0, written).ToString(), Is.EqualTo(StringComparison.InvariantCulture.ToString("x")));
            Assert.That(StringComparison.InvariantCulture.TryFormat(span, out written, "X"));
            Assert.That(span.Slice(0, written).ToString(), Is.EqualTo(StringComparison.InvariantCulture.ToString("X")));

            Assert.That((MethodImplOptions.AggressiveInlining | MethodImplOptions.InternalCall).TryFormat(span, out written, "x"));
            Assert.That(span.Slice(0, written).ToString(), Is.EqualTo((MethodImplOptions.AggressiveInlining | MethodImplOptions.InternalCall).ToString("x")));
            Assert.That((MethodImplOptions.AggressiveInlining | MethodImplOptions.InternalCall).TryFormat(span, out written, "X"));
            Assert.That(span.Slice(0, written).ToString(), Is.EqualTo((MethodImplOptions.AggressiveInlining | MethodImplOptions.InternalCall).ToString("X")));


            // unknown values
            Assert.That(((StringComparison)(-1)).TryFormat(span, out written));
            Assert.That(span.Slice(0, written).ToString(), Is.EqualTo(((StringComparison)(-1)).ToString()));

            Assert.That(((MethodImplOptions)(-1)).TryFormat(span, out written));
            Assert.That(span.Slice(0, written).ToString(), Is.EqualTo(((MethodImplOptions)(-1)).ToString()));

            Assert.That((unchecked((EULong)(-1))).TryFormat(span, out written));
            Assert.That(span.Slice(0, written).ToString(), Is.EqualTo((unchecked((EULong)(-1))).ToString()));
            Assert.That((unchecked((EULong)(-2L))).TryFormat(span, out written));
            Assert.That(span.Slice(0, written).ToString(), Is.EqualTo((unchecked((EULong)(-2L))).ToString()));

            Assert.That((unchecked((ELong)(int.MinValue))).TryFormat(span, out written));
            Assert.That(span.Slice(0, written).ToString(), Is.EqualTo((unchecked((ELong)(int.MinValue))).ToString()));
            Assert.That((unchecked((ELong)(ulong.MaxValue))).TryFormat(span, out written));
            Assert.That(span.Slice(0, written).ToString(), Is.EqualTo((unchecked((ELong)(ulong.MaxValue))).ToString()));


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(TryParse_Tests), priority = 0)]
        [Test]
        public static void TryParse_Tests()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            StringComparison strComparison;

            Assert.That(!StrictEnum.TryParse("@", out strComparison));
            Assert.That(strComparison, Is.EqualTo(default(StringComparison)));

            // non-Flags enum
            Assert.That(!StrictEnum.TryParse(nameof(StringComparison.Ordinal) + ", " + nameof(StringComparison.InvariantCulture), out strComparison));
            Assert.That(strComparison, Is.EqualTo(default(StringComparison)));

            // success
            Assert.That(StrictEnum.TryParse("Ordinal", out strComparison));
            Assert.That(strComparison, Is.EqualTo(StringComparison.Ordinal));
            Assert.That(StrictEnum.TryParse("OrdinalIgnoreCase", out strComparison));
            Assert.That(strComparison, Is.EqualTo(StringComparison.OrdinalIgnoreCase));
            //comparison
            Assert.That(!StrictEnum.TryParse("ordinalignorecase", out strComparison));
            Assert.That(StrictEnum.TryParse("ordinalignorecase", out strComparison, StringComparison.OrdinalIgnoreCase));
            Assert.That(strComparison, Is.EqualTo(StringComparison.OrdinalIgnoreCase));


            MethodImplOptions implOptions;

            Assert.That(!StrictEnum.TryParse("@", out implOptions));
            Assert.That(implOptions, Is.EqualTo(default(MethodImplOptions)));

            // single entry
            Assert.That(StrictEnum.TryParse(nameof(MethodImplOptions.ForwardRef), out implOptions));
            Assert.That(implOptions, Is.EqualTo(MethodImplOptions.ForwardRef));
            Assert.That(StrictEnum.TryParse(" ,  " + nameof(MethodImplOptions.ForwardRef) + " ,  ", out implOptions));
            Assert.That(implOptions, Is.EqualTo(MethodImplOptions.ForwardRef));

            //flags??
            Assert.That(StrictEnum.TryParse(nameof(MethodImplOptions.ForwardRef) + ", " + nameof(MethodImplOptions.NoInlining), out implOptions));
            Assert.That(implOptions, Is.EqualTo(MethodImplOptions.ForwardRef | MethodImplOptions.NoInlining));
            Assert.That(StrictEnum.TryParse(nameof(MethodImplOptions.NoInlining) + "   ,,,,,    " + nameof(MethodImplOptions.ForwardRef), out implOptions));
            Assert.That(implOptions, Is.EqualTo(MethodImplOptions.ForwardRef | MethodImplOptions.NoInlining));
            //conparison
            Assert.That(!StrictEnum.TryParse(" ,,,,   noinlining  ,,,, forwardref ,,,   ,", out implOptions));
            Assert.That(StrictEnum.TryParse(" ,, ,,   noinlining  ,  , forwardref ,,,   ,", out implOptions, StringComparison.OrdinalIgnoreCase));
            Assert.That(implOptions, Is.EqualTo(MethodImplOptions.ForwardRef | MethodImplOptions.NoInlining));


            // try parse as number?
            Assert.That(!StrictEnum.TryParse(ulong.MaxValue.ToString(), out implOptions));
            Assert.That(!StrictEnum.TryParse(long.MinValue.ToString(), out implOptions));
            Assert.That(!StrictEnum.TryParse(long.MaxValue.ToString(), out implOptions));
            Assert.That(!StrictEnum.TryParse("987654321", out implOptions));

            Assert.That(!StrictEnum.TryParse("16, 8", out implOptions));
            Assert.That(StrictEnum.TryParse("16", out implOptions));
            Assert.That(implOptions, Is.EqualTo(MethodImplOptions.ForwardRef));

            Assert.That(!StrictEnum.TryParse("16, 8", out implOptions));
            Assert.That(StrictEnum.TryParse("24", out implOptions));
            Assert.That(implOptions, Is.EqualTo(MethodImplOptions.ForwardRef | MethodImplOptions.NoInlining));


            //strict??
            Assert.That(!StrictEnum.TryParse("NoInlining,NotFound", out implOptions, strictFlagsMatch: true));
            Assert.That(StrictEnum.TryParse("NoInlining,NotFound", out implOptions, strictFlagsMatch: false));
            Assert.That(implOptions.ToString(), Is.EqualTo("NoInlining"));


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(ULong_Value_Tests), priority = 0)]
        [Test]
        public static void ULong_Value_Tests()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            unchecked
            {
                var values = StrictEnum.GetUnderlyingValues<EValueCheck>();
                var expected = new ulong[] { 0, 1, (ulong)int.MinValue, (ulong)(int)(-1) };

                UnityEngine.Debug.Log(string.Join(", ", values.ToArray()));
                UnityEngine.Debug.Log(string.Join(", ", expected));

                Assert.That(values.SequenceEqual(expected));
            }


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Validation_Tests), priority = 0)]
        [Test]
        public static void Validation_Tests()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            Assert.That(StrictEnum.IsInt32(typeof(EInt)));
            Assert.That(!StrictEnum.IsInt32(typeof(EUInt)));

            Assert.That(StrictEnum.IsInt32Convertible(typeof(ESByte)));
            Assert.That(StrictEnum.IsInt32Convertible(typeof(EByte)));
            Assert.That(StrictEnum.IsInt32Convertible(typeof(EShort)));
            Assert.That(StrictEnum.IsInt32Convertible(typeof(EUShort)));
            Assert.That(StrictEnum.IsInt32Convertible(typeof(EInt)));
            Assert.That(!StrictEnum.IsInt32Convertible(typeof(EUInt)));
            Assert.That(!StrictEnum.IsInt32Convertible(typeof(ELong)));
            Assert.That(!StrictEnum.IsInt32Convertible(typeof(EULong)));

            Assert.That(StrictEnum.IsUnderlyingValueSigned(typeof(ESByte)));
            Assert.That(StrictEnum.IsUnderlyingValueSigned(typeof(EShort)));
            Assert.That(StrictEnum.IsUnderlyingValueSigned(typeof(EInt)));
            Assert.That(StrictEnum.IsUnderlyingValueSigned(typeof(ELong)));
            Assert.That(!StrictEnum.IsUnderlyingValueSigned(typeof(EByte)));
            Assert.That(!StrictEnum.IsUnderlyingValueSigned(typeof(EUShort)));
            Assert.That(!StrictEnum.IsUnderlyingValueSigned(typeof(EUInt)));
            Assert.That(!StrictEnum.IsUnderlyingValueSigned(typeof(EULong)));

            Assert.That(!StrictEnum.IsOrdinalFromZero(typeof(EValueCheck)));
            Assert.That(StrictEnum.IsOrdinalFromZero(typeof(EOrdinalLong)));

            Assert.That(!StrictEnum.IsOrdinalFromZero(typeof(EULong)));
            Assert.That(!StrictEnum.IsOrdinalFromZero(typeof(ELong)));
            Assert.That(!StrictEnum.IsOrdinalFromZero(typeof(EUInt)));
            Assert.That(!StrictEnum.IsOrdinalFromZero(typeof(EInt)));
            Assert.That(!StrictEnum.IsOrdinalFromZero(typeof(EUShort)));
            Assert.That(!StrictEnum.IsOrdinalFromZero(typeof(EShort)));
            Assert.That(!StrictEnum.IsOrdinalFromZero(typeof(EByte)));
            Assert.That(!StrictEnum.IsOrdinalFromZero(typeof(ESByte)));


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }


        enum EValueCheck : int
        {
            NoBits = 0,
            AllBits = -1,
            MostSignificant = int.MinValue,
            LeastSignificant = 1,
        }

        enum EOrdinalLong : long
        {
            Zero,
            One,
            Two,
            Three,
            Four,
        }

        enum EULong : ulong { Value = 31, MAX = ulong.MaxValue }
        enum ELong : long { Value = 32, MIN = long.MinValue }
        enum EUInt : uint { Value = 33 }
        enum EInt : int { Value = 34 }
        enum EUShort : ushort { Value = 35, MAX = ushort.MaxValue }
        enum EShort : short { Value = 36, MIN = short.MinValue }
        enum EByte : byte { Value = 37 }
        enum ESByte : sbyte { Value = 38, DisplayNameChecker }


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
