/** Observable Wrapper of `event Action<T>` for .NET / Unity
 ** (c) 2025 Sator Imaging, Licensed under the MIT License
 ** https://github.com/sator-imaging/Unity-Fundamentals

Similar to and different from `ObservableAction<T>`, this class completely
encapsulates actual event field inside to provide more elegant look & feel.

How to Use
==========
```cs
// keep raw instance private to prevent event invocation
private ObservableEvent<int> m_myEvent = new();

// provide readonly (sub/unsub) interface to consumers
public IObservableEvent<int> MyEvent => m_myEvent;

// invoke event
m_myEvent.Invoke();

// consumer can only perform subscribe/unsubscribe
MyEvent.Subscribe(...).BindTo(cancellationToken);
MyEvent.Subscribe(myAction).AddTo(disposableCollection);
MyEvent.Unsubscribe(myAction);

// clear all event handlers. note that event is still exist and accepts new handler
m_myEvent.Dispose();
```

 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    /// <summary>
    /// Sub/unsub only interface to prevent consumers from calling event.
    /// </summary>
    public interface IObservableEvent<T> : IObservable<T>
    {
        ObservableEvent<T>.Subscription Subscribe(Action<T> act, bool disallowDuplicate = true);
        void Unsubscribe(Action<T> act);
    }


    public class ObservableEvent<T> : IObservableEvent<T>, IDisposable
    {
        protected event Action<T>? RawEvent;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(T value) => RawEvent?.Invoke(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Unsubscribe(Action<T> act) => RawEvent -= act;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IDisposable Subscribe(IObserver<T> observer) => Subscribe(observer.OnNext);

        /// <summary>
        /// Subscribe event.
        /// </summary>
        /// <returns><see cref="IDisposable"/> object which represents subscription.</returns>
        public Subscription Subscribe(Action<T> act, bool disallowDuplicate = true)
        {
            if (disallowDuplicate)
            {
                RawEvent -= act;
            }
            RawEvent += act;

            return new(this, act);
        }


        /// <summary>Clear all event handlers.</summary>
        public void Dispose()
        {
            if (RawEvent == null)
                return;

            foreach (var cb in RawEvent.GetInvocationList())
            {
                if (cb is not Action<T> act)
                {
                    if (cb == null)
                        continue;

                    throw new NotSupportedException("unsupported event callback type: " + cb);
                }

                RawEvent -= act;
            }
        }


        /// <summary>
        /// Call <c>Dispose()</c> to unsubscribe event.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [StructLayout(LayoutKind.Auto)]
        public readonly struct Subscription : IDisposable, IEquatable<Subscription>
        {
            readonly ObservableEvent<T> observable;
            readonly Action<T> act;

            public Subscription(ObservableEvent<T> observable, Action<T> act)
            {
                this.observable = observable;
                this.act = act;
            }

            public void Dispose() => observable.Unsubscribe(act);

            public override int GetHashCode() => HashCode.Combine(this.observable, this.act);
            public override bool Equals(object? obj) => obj is Subscription other && this.Equals(other);
            public bool Equals(Subscription other)
            {
                return EqualityComparer<ObservableEvent<T>>.Default.Equals(this.observable, other.observable)
                    && EqualityComparer<Action<T>>.Default.Equals(this.act, other.act);
            }
            public static bool operator ==(Subscription left, Subscription right) => left.Equals(right);
            public static bool operator !=(Subscription left, Subscription right) => !(left == right);
        }
    }


    /// <summary>Extension methods for <see cref="ObservableEvent{T}"/>.</summary>
    public static class ObservableEventExtensions
    {
        public static void AddTo<T>(this in ObservableEvent<T>.Subscription self, ICollection<IDisposable> collection) => collection.Add(self);


        public static CancellationTokenRegistration BindTo<T>(this in ObservableEvent<T>.Subscription self,
                                                              CancellationToken cancellationToken,
                                                              bool useSynchronizationContext = false)
        {
            if (!cancellationToken.CanBeCanceled)
                return default;

            if (ExecutionContext.IsFlowSuppressed())
            {
                return cancellationToken.Register(InvokeDisposableDispose, self, useSynchronizationContext);
            }
            else
            {
                using (ExecutionContext.SuppressFlow())
                {
                    return cancellationToken.Register(InvokeDisposableDispose, self, useSynchronizationContext);
                }
            }
        }

        readonly static Action<object> InvokeDisposableDispose = static (obj) =>
        {
#if UNITY_EDITOR
            try
#endif
            {
                ((IDisposable)obj).Dispose();
            }
#if UNITY_EDITOR
            catch (Exception exc)
            {
                UnityEngine.Debug.LogException(exc);
            }
#endif
        };

    }
}




#region ////////  TEMPLATE: Debug menu for Unity Editor  ////////

#if UNITY_EDITOR

#pragma warning disable IDE1006  // naming style
#pragma warning disable CA1861   // avoid constant array

// TEMPLATE: namespace naming convention: <TEST_TARGET>.<ROOT_MENU_LABEL>.<SUB_MENU_LABEL>
namespace SatorImaging.UnityFundamentals.DEBUG.Observable_Event  // must be unique. don't reuse existing namespace
{
    static class UNITY_EDITOR_DEBUG  // don't change
    {
        const string MENU_ROOT = nameof(DEBUG) + "/" + nameof(Observable_Event) + "/";


        #region ////////  TEMPLATE: Debug Methods  ////////
        /*  TEMPLATE: Debug Methods  ================================================================ */

        class Server : IDisposable
        {
            readonly ObservableEvent<string> _event = new();
            readonly CancellationTokenSource tokenSource = new();

            public Server()
            {
                Task.Run(async () =>
                {
                    int count = 0;

                    var token = tokenSource.Token;
                    while (!token.IsCancellationRequested)
                    {
                        if ((count++ % 10) == 0)
                        {
                            UnityEngine.Debug.Log("> server is running: " + count);
                        }

                        _event.Invoke("server loop: " + count);
                        await Task.Delay(1000);
                    }
                });
            }

            public IObservableEvent<string> Event => _event;

            public void Dispose()
            {
                _event.Dispose();

                tokenSource.Cancel();
                tokenSource.Dispose();
            }
        }

        static Server? _activeServer;
        static CancellationTokenSource _tokenSource = new();

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Start_Server), priority = 0)]
        static void Start_Server()
        {
            Stop_Server();

            _activeServer = new Server();
            _activeServer.Event.Subscribe(val => UnityEngine.Debug.Log(val)).BindTo(_tokenSource.Token);
        }

        static int _handlerCount = 0;

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Register_Another_Handler), priority = 0)]
        static void Register_Another_Handler()
        {
            if (_activeServer == null)
            {
                UnityEngine.Debug.LogWarning("server hasn't yet ran");
                return;
            }

            var number = ++_handlerCount;
            _activeServer.Event.Subscribe(val => UnityEngine.Debug.Log($"handler#{number}: {val}")).BindTo(_tokenSource.Token);
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Cancel_Token), priority = int.MaxValue / 2)]
        static void Cancel_Token()
        {
            if (_activeServer == null)
            {
                UnityEngine.Debug.LogWarning("server hasn't yet ran");
                return;
            }

            _tokenSource.Cancel();
            _tokenSource.Dispose();
            _tokenSource = new();

            UnityEngine.Debug.Log("Token has canceled (server is still running)");
        }

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Stop_Server), priority = int.MaxValue / 2)]
        static void Stop_Server()
        {
            if (_activeServer == null)
            {
                UnityEngine.Debug.LogWarning("server hasn't yet ran");
                return;
            }

            _activeServer?.Dispose();
            _activeServer = null;

            UnityEngine.Debug.Log("Server has stopped");
        }


        /*  TEMPLATE: End of Debug  ================================================================ */
        #endregion    //  TEMPLATE: End of Debug


        /* TEMPLATE: copy & paste and replace argument for 'nameof()'

        [UnityEditor.MenuItem(MENU_ROOT + nameof(__Underscore_Separated_Method_Name__), priority = 0)]
        static void Basic_Tests()
        {
        }

        */


        // TEMPLATE: open script file
        [UnityEditor.MenuItem(MENU_ROOT + "Edit Debug Script...", priority = int.MaxValue - 310)]
        static void UnityEditorTests_EditDebugScript() => __EditDebugScript();

        static void __EditDebugScript(
            [System.Runtime.CompilerServices.CallerFilePath] string? filePath = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
            => UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(filePath, lineNumber);
    }
}
#endif
#endregion
