using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    public static class UIToolkitCoreExtensions
    {
        // make it easy to track where delay occur
        public enum ExecutionDelay
        {
            [Obsolete("Are you sure?")]
            Zero,
            One,
            Two,
            Three,
            Four,
        }


        readonly static Action EMPTY_ACTION = static () => { };

        /// <summary>
        /// Execute job after specified number of editor update happens.
        /// </summary>
        public static IVisualElementScheduledItem ExecuteAfter(this IVisualElementScheduler self, ExecutionDelay numEditorUpdate, Action act)
        {
            if (numEditorUpdate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(numEditorUpdate),
                    "scheduling with no delay will cause unstable execution order");
            }

            // declaring variables for lambda expression
            int count = 0;
            int wait = (int)numEditorUpdate;

            // NOTE: there are no efficient way to create scheduled item.
            //       if declare custom IVisualElementScheduledItem class or struct, there are no way to schedule it.
            //       in builtin VisualElement class, it will use non-public elementPanel's scheduler.
            var job = self.Execute(EMPTY_ACTION);
            job.Until(() =>
            {
                if (count++ < wait)
                    return false;

                try
                {
                    act.Invoke();
                }
                catch (Exception exc)
                {
                    UnityEngine.Debug.LogException(exc);
                }
                finally
                {
                    job.Pause();
                }

                return true;
            });

            return job;
        }


        /*  unwrap  ================================================================ */

        public static VisualElement UnwrapChildIfPossible(this TemplateContainer template)
        {
            if (template.childCount != 1)
                return template;

            var result = template[0];
            template.Remove(result);

            var styles = template.styleSheets;
            for (int i = 0; i < styles.count; i++)
            {
                result.styleSheets.Add(styles[i]);
            }

            return result;
        }


        /*  style.backgroundImage  ================================================================ */

        public static void SetBackground(this IStyle style, Texture2D? texture, bool destroyExisting)
        {
            var existing = style.backgroundImage.value.texture;
            //// NOTE: it is not resolvedStyle
            //if (existing == texture)
            //    return;

            style.backgroundImage = (texture != null) ? texture : StyleKeyword.Null;

            if (existing != null)
            {
                if (destroyExisting)
                {
#if UNITY_EDITOR
                    if (Application.IsPlaying(existing))
                    {
                        UnityEngine.Object.Destroy(existing);
                    }
                    else if (string.IsNullOrEmpty(UnityEditor.AssetDatabase.GetAssetPath(existing)))
                    {
                        UnityEngine.Object.DestroyImmediate(existing);
                    }
#else
                    UnityEngine.Object.Destroy(existing);
#endif
                }
            }
        }


        /*  enable/disable control  ================================================================ */

        /// <summary>
        /// Register control enable/disable callback and also set current state on registration.
        /// </summary>
        public static void RegisterToggleControlEnableDisableCallback(this VisualElement canNotifyChangeEvent_bool, VisualElement target, bool enableWhen)
        //where T : VisualElement, INotifyValueChanged<bool>
        {
            if (canNotifyChangeEvent_bool is not INotifyValueChanged<bool> boolNotifier)
                throw new NotSupportedException("visual element does not implement INotifyValueChanged<bool>: " + canNotifyChangeEvent_bool);

            canNotifyChangeEvent_bool.RegisterCallback(SetControlEnableOnToggleChanged, (target, enableWhen));
            target.SetEnabled(boolNotifier.value == enableWhen);
        }

        readonly static EventCallback<ChangeEvent<bool>, (VisualElement target, bool enableWhen)> SetControlEnableOnToggleChanged
            = static (e, args) => args.target.SetEnabled(e.newValue == args.enableWhen);


        /*  connect display style with opacity  ================================================================ */

        readonly static EventCallback<TransitionEndEvent, VisualElement> AutoDisplayNoneWhenOpacityIsZeroOnTransitionEnd = static (e, ve) =>
        {
            if (ve.resolvedStyle.opacity > 0)
                return;

            ve.style.display = DisplayStyle.None;
        };

        public static void RegisterAutoDisplayNoneWhenOpacityIsZeroCallback(this VisualElement ve, bool checkTransitionProperty = true)
        {
            if (checkTransitionProperty)
            {
                if (!(
                    ve.resolvedStyle.transitionDuration.Any(static x => x.value > 0) ||
                    ve.style.transitionDuration.value.Any(static x => x.value > 0)
                ))
                {
                    UnityEngine.Debug.LogError(nameof(RegisterAutoDisplayNoneWhenOpacityIsZeroCallback) + ": no transition event: " + ve);
                    return;
                }
            }

            ve.RegisterCallback(AutoDisplayNoneWhenOpacityIsZeroOnTransitionEnd, ve);
        }


        /*  transition helpers  ================================================================ */

        public static DisableStyleTransitionDisposable DisableStyleTransitionScope(this VisualElement ve, bool delayRestoration) => new DisableStyleTransitionDisposable(ve, delayRestoration);

        // StyleList is mutable struct
        readonly static StyleList<TimeValue> EMPTY_LIST_TIME_VALUE = new List<TimeValue>(capacity: 0);
        readonly static StyleList<StylePropertyName> EMPTY_LIST_PROP_NAME = new List<StylePropertyName>(capacity: 0);
        readonly static StyleList<EasingFunction> EMPTY_LIST_EASING_FUNC = new List<EasingFunction>(capacity: 0);
        const StyleKeyword DEFAULT_TRANSITION_KEYWORD = StyleKeyword.Undefined;

        [StructLayout(LayoutKind.Auto)]
        public readonly struct DisableStyleTransitionDisposable : IDisposable
        {
            readonly VisualElement ve;
            readonly StyleList<TimeValue>? restoreDelay;
            readonly StyleList<TimeValue>? restoreDuration;
            readonly StyleList<StylePropertyName>? restoreProperty;
            readonly StyleList<EasingFunction>? restoreTimingFunction;
            readonly bool delayRestoration;

            public DisableStyleTransitionDisposable(VisualElement ve, bool delayRestoration)
            {
                this.ve = ve;
                this.delayRestoration = delayRestoration;

                var style = ve.style;
                this.restoreDelay = style.transitionDelay;
                this.restoreDuration = style.transitionDuration;
                this.restoreProperty = style.transitionProperty;
                this.restoreTimingFunction = style.transitionTimingFunction;

                style.transitionDelay = EMPTY_LIST_TIME_VALUE;
                style.transitionDuration = EMPTY_LIST_TIME_VALUE;
                style.transitionProperty = EMPTY_LIST_PROP_NAME;
                style.transitionTimingFunction = EMPTY_LIST_EASING_FUNC;
            }

            [EditorBrowsable(EditorBrowsableState.Never)]
            readonly public void Dispose()
            {
                var ve = this.ve;
                var delay = this.restoreDelay;
                var duration = this.restoreDuration;
                var property = this.restoreProperty;
                var timingFunction = this.restoreTimingFunction;

                var style = ve.style;
                if (delayRestoration)
                {
                    ve.ExecuteAfter(ExecutionDelay.One, () =>
                    {
                        style.transitionDelay = delay ?? DEFAULT_TRANSITION_KEYWORD;
                        style.transitionDuration = duration ?? DEFAULT_TRANSITION_KEYWORD;
                        style.transitionProperty = property ?? DEFAULT_TRANSITION_KEYWORD;
                        style.transitionTimingFunction = timingFunction ?? DEFAULT_TRANSITION_KEYWORD;
                    });
                }
                else
                {
                    style.transitionDelay = delay ?? DEFAULT_TRANSITION_KEYWORD;
                    style.transitionDuration = duration ?? DEFAULT_TRANSITION_KEYWORD;
                    style.transitionProperty = property ?? DEFAULT_TRANSITION_KEYWORD;
                    style.transitionTimingFunction = timingFunction ?? DEFAULT_TRANSITION_KEYWORD;
                }
            }

        }

    }
}
