# Unity Scripting Fundamentals

Fundamental scripting library for Unity designed to be minimal, efficient & dependency-free as possible.
Most of scripts are C# / .NET compliant. See `using` statements in `.cs` files for details.

> [!TIP]
> Licensed under the MIT License unless otherwise described.



# Installation

Enter the following `git URL` in Unity Package Manager (UPM)
```url
https://github.com/sator-imaging/Unity-Fundamentals.git
```

> Supported Unity version: Unity 2021.3+



# Plain C# Features

- **Non-Alloc String Splitter** [:blue_book:](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Text/NonAllocStringSplitter.cs.html)
    - inspired by .NET 9 feature.
- **Observable Action** [:blue_book:](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Reactive/ObservableAction.cs.html)
    - transform `event Action<T>` to `IObservable<T>`.
- *WIP*: **Unity Event Observable**
    - transform `UnityEvent` to `IObservable<T>`.
- `Defer` [:blue_book:](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/System/Defer.cs.html)
    - providing function for `early-finally` pattern.
- `StrictEnum` [:blue_book:](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/System/StrictEnum.cs.html)
    - unlike other enum utility, this class re-use system cache and also support parsing `Flags` value.
- `SpanList<T>` [:blue_book:](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/System/SpanList.cs.html)
    - list implementation of `Span<T>` especially designed to work with `Span<char>`.
- `MiniXXHash` [:blue_book:](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Hashing/MiniXXHash.cs.html)
    - Minimal xxHash32 / xxHash64 implementation.
    - **License: BSD 2-Clause**
- `UString` [:blue_book:](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Text/UString.cs.html)
    - lightning-fast non-alloc string builder faster than `DefaultInterpolatedStringHandler`
    - *NOTE*: depending on `StrictEnum`
    - **TODO**: benchmark
- `Poolable<T>` [:blue_book:](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/System/Poolable.cs.html)
    - self-contained singly linked list based object pool.
    - **TODO**: write tests
- `Sentinel` [:blue_book:](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Threading/Sentinel.cs.html)
    - fast & efficient exclusive or concurrent thread/event manager.
- `Run` ~~:blue_book:~~
    - job runner for Unity.
    - provides reliable un-async-ing functions.
    - *NOTE*: `InitializeMainThreadContext` may be required to being called on Unity startup. (it called automatically by default)
    - **TODO**: document for `OnMainThread` `InThreadPool` `SetExceptionHandler` `GetTimerToken` `GetElapsedTime` `Shutdown` `CreateNewScheduler` `SetConcurrentThreadCount`
- `ThreadSafeSingleton`
    - thread! safe!! singleton!!!
- `HalfUlid`
    - https://github.com/sator-imaging/Half-Ulid
- NUnit-compatible Framework for Unity Editor [:blue_book:](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/FancyStuff/NUnit.Framework.UnityStubs.cs.html)
    - crazy stuff. no need to use this anymore as unity asset store now accepts `package.json`.



# Unity Features

## Runtime
- Nullable support for `UnityEngine.Object` [:blue_book:](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/UnityObject/NullableUnityObject.cs.html)
    - reliable nullable (`??` `?.` ~~`??=`~~) support for `UnityEngine.Object`.
- `ManagedShell` [:blue_book:](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/UnityObject/ManagedShell.cs.html)
    - provides functions that avoid creating leaked managed shell.
- `PoolableBehaviour<T>` [:blue_book:](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/UnityObject/PoolableBehaviour.cs.html)
- UI Toolkit Helpers
    - UI Toolkit Core Extensions
        - `ExecuteAfter` extension method
            - unlike builtin `ExecuteLater` method, this method runs action right after specified number of *repaint* events.
        - `DisableStyleTransitionScope` extension method
            - this method temporarily turns off all transitions and turns them back on when leaving the `IDisposable` scope. useful for immediate style update without transition animation.
    - UI Toolkit Event Subscription
        - use extension method `RegisterCallbackAsSubscription` to register event as `IDisposable` interface. it allows easily unregister event later.
    - UI Toolkit Dropdown Helper
        - *NOTE*: depending on `StrictEnum`
        - extension method `RegisterCallbackAsEnum` allows implement typed callback. and also there is option to delay event to correctly handle dropdown event. (if no delay, dropdown in Unity editor behaves like 'stop-the-world' and some actions are not work as expected)
- *Obsolete*
    - CancellationToken based Lifecycle Manager for Unity Editor [:blue_book:](https://sator-imaging.github.io/Unity-Fundamentals/Obsolete/LifecycleBehaviour/README.html)
    - > Obsolete features still exist as `.txt` files.


## Editor

- `UnityEditorMainToolbar` [:blue_book:](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Editor/Uncategorized/UnityEditorMainToolbar.cs.html)
    - provide access to `VisualElement` in Unity main toolbar.
- Leaked Managed Shell Detector [:blue_book:](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Editor/Uncategorized/LeakedManagedShellDetector.cs.html)
    - not perfect. just for reference.
- Assembly Definitions Manager for Project Settings Panel
    - **TODO**: documentation
- *WIP*: `sealed`-able Type Finder
    - As IL2CPP bloats resulting C++ code if C# class is not marked with `sealed` modifier





# API Reference

https://sator-imaging.github.io/Unity-Fundamentals/api/index.html




&nbsp;  
&nbsp;  

# Devnote

## `DefineConstants`

How to set `DefineConstants` in *.csproj* file from `dotnet` command, see
[Directory.Build.Props](../CStrBenchmark/Directory.Build.props)
and
[Program.cs](../CStrBenchmark/Program.cs#L37)
for details.

Source: https://github.com/dotnet/sdk/issues/9562#issuecomment-1386955134
