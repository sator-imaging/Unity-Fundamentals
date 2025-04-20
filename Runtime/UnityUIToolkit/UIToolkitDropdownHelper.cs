// (c) 2024-2025 Sator Imaging, Licensed under the MIT License
// https://github.com/sator-imaging/Unity-Fundamentals

/**
 * > [!NOTE]
 * > Depending on `StrictEnum`
 */

#if STMG_UITOOLKIT_EXISTS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    /// <remarks>
    /// NOTE: Enum value MUST start from 0 and increment by 1.
    /// </remarks>
    public static class UIToolkitDropdownHelper
    {
        const string SEPARATOR_LABEL = "_";

        readonly static Func<Type, string, ulong, string> DISPLAY_NAME_FACTORY = static (enumType, fieldName, index) =>
        {
            if (fieldName == null || fieldName.StartsWith('_'))
                return SEPARATOR_LABEL;

            var result = enumType.GetField(fieldName).GetCustomAttribute<InspectorNameAttribute>()?.displayName
                ?? StrictEnum.BeautifyFieldName(fieldName);

            //UnityEngine.Debug.Log($"{enumType.Name} #{index}: {result} ({fieldName})");

            return result;
        };


        // to make Cache<T> code size small
        static class CacheHelper
        {
            public static List<string?> InitializeChoices(Type enumType)
            {
                if (!StrictEnum.IsInt32Convertible(enumType))
                    throw new NotSupportedException("UI Toolkit doesn't support non-int32 convertible type of enum: " + enumType);

                if (!StrictEnum.IsOrdinalFromZero(enumType))
                    throw new NotSupportedException("enum value must be started from 0 and increment by 1");

                StrictEnum.CustomDisplayNameFactory(enumType, DISPLAY_NAME_FACTORY);

                FieldInfo? DisplayNames;
                var refl_displayNames = typeof(StrictEnum.EnumInfo<>).MakeGenericType(enumType).GetField(nameof(DisplayNames))
                    ?? throw new MissingFieldException(typeof(StrictEnum.EnumInfo<>).FullName, nameof(DisplayNames));

                var names = ((ReadOnlyMemory<string>)refl_displayNames.GetValue(null)).Span;

                List<string?> result = new(capacity: names.Length);
                for (int i = 0; i < names.Length; i++)
                {
                    result.Add(names[i].SequenceEqual(SEPARATOR_LABEL) ? null : names[i]);
                }

                return result;
            }
        }


        /*  cache  ================================================================ */

        static class Cache<T> where T : Enum
        {
            public readonly static List<string?> Choices = CacheHelper.InitializeChoices(typeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<string?> GetChoices<TEnum>() where TEnum : Enum => Cache<TEnum>.Choices;


        /*  Get  ================================================================ */

        public static T GetValueFromEvent<T>(ChangeEvent<string> e) where T : Enum
        {
            var value = e.newValue;

            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("null or whitespace", nameof(value));

            if (StrictEnum.TryGetValueByDisplayName<T>(value, out var result))
                return result;

            throw new KeyNotFoundException(value);
        }


        public static int GetIndexOrMinusOne(this DropdownField self, string choice, StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrWhiteSpace(choice))
                return -1;

            var choices = self.choices;
            for (int i = 0; i < choices.Count; i++)
            {
                if (choices[i].Equals(choice, comparison))
                    return i;
            }

            return -1;
        }


        /*  initialize  ================================================================ */

        /// <inheritdoc cref="Initialize{T}(DropdownField, string?)"/>
        public static void Initialize<T>(this DropdownField self, T initialValue) where T : Enum
            => Initialize<T>(self, StrictEnum.ToDisplayNameString(initialValue));

        /// <inheritdoc cref="Initialize{T}(DropdownField, string?)"/>
        public static void Initialize<T>(this DropdownField self, int initialIndex) where T : Enum
            => Initialize<T>(self, StrictEnum.ToDisplayNameString<T>(initialIndex));

        /// <summary>Initialize <c>.choices</c> and then set value without notify.</summary>
        public static void Initialize<T>(this DropdownField self, string? initialChoice) where T : Enum
        {
            self.choices = GetChoices<T>();
            self.SetValueWithoutNotify(initialChoice);
        }


        /*  callback  ================================================================ */

        /// <inheritdoc cref="RegisterCallbackAsEnum{T, TState}(VisualElement, Action{EventBase, T, TState}, TState, bool)"/>
        public static void RegisterCallbackAsEnum<T>(this VisualElement canNotifyChangeEvent_string, Action<EventBase, T, object?> callback, bool delayRequired)
            where T : Enum => RegisterCallbackAsEnum(canNotifyChangeEvent_string, callback, (object?)null, delayRequired);


        readonly static Action EMPTY_ACTION = () => { };

        /// <summary>
        /// Register change event as enum value.
        /// Note that callback delay is required if callback initiates GUI value update or layout animation.
        /// </summary>
        public static void RegisterCallbackAsEnum<T, TState>(this VisualElement canNotifyChangeEvent_string,
                                                             Action<EventBase, T, TState> callback,
                                                             TState state,
                                                             bool delayRequired
            )
            where T : Enum
        {
            if (canNotifyChangeEvent_string is not INotifyValueChanged<string>)
                throw new NotSupportedException("visual element does not implement INotifyValueChanged<string>: " + canNotifyChangeEvent_string);

            if (delayRequired)
            {
                canNotifyChangeEvent_string.RegisterCallback<ChangeEvent<string>, (VisualElement self, Action<EventBase, T, TState> callback, TState state)>(
                    static (e, data) =>
                    {
                        var value = GetValueFromEvent<T>(e);

                        bool waitOneFrame = true;
                        data.self.schedule
                            .Execute(EMPTY_ACTION)
                            .Until(() =>
                            {
                                if (waitOneFrame)
                                {
                                    waitOneFrame = false;
                                    return false;
                                }

                                data.callback.Invoke(e, value, data.state);
                                return true;
                            });
                    },
                    (canNotifyChangeEvent_string, callback, state));
            }
            else
            {
                canNotifyChangeEvent_string.RegisterCallback<ChangeEvent<string>, (Action<EventBase, T, TState> callback, TState state)>(static (e, data) =>
                {
                    var value = GetValueFromEvent<T>(e);
                    data.callback.Invoke(e, value, data.state);
                },
                (callback, state));
            }
        }

    }
}

#endif
