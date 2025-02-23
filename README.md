# Unity Scripting Fundamentals

Fundamental scripting library for Unity designed to be minimal, efficient & dependency-free as possible.
Most of scripts are C# / .NET compliant. See `using` statements in `.cs` files for details.

> [!TIP]
> Licensed under the MIT License unless otherwise described.



# Installation

Enter the following `git URL` in Unity Package Manager (UPM)
```
https://github.com/sator-imaging/Unity-Fundamentals.git
```

> Supported Unity version: Unity 2021.3+



# Plain C# Features

- **Non-Alloc String Splitter**
    - inspired by .NET 9 feature.
      [📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Text/NonAllocStringSplitter.cs.html)
- **Observable Action**
    - transform `event Action<T>` to `IObservable<T>`.
      [📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Reactive/ObservableAction.cs.html)
- *WIP*: **Unity Event Observable**
    - transform `UnityEvent` to `IObservable<T>`.
- `Defer`
    - providing function for `early-finally` pattern.
      [📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/System/Defer.cs.html)
- `StrictEnum`
    - unlike other enum utility, this class re-use system cache and also support parsing `Flags` value.
      [📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/System/StrictEnum.cs.html)
- `SpanList<T>`
    - list implementation of `Span<T>` especially designed to work with `Span<char>`.
      [📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/System/SpanList.cs.html)
- `MiniXXHash`
    - Minimal xxHash32 / xxHash64 implementation.
      [📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Hashing/MiniXXHash.cs.html)
    - **License: BSD 2-Clause**
- `UString`
    - lightning-fast non-alloc string builder faster than `DefaultInterpolatedStringHandler`
      [📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Text/UString.cs.html)
    - *NOTE*: depending on `StrictEnum`
    - **TODO**: benchmark
- `Poolable<T>`
    - self-contained singly linked list based object pool.
      [📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/System/Poolable.cs.html)
    - **TODO**: write tests
- `Sentinel`
    - fast & efficient exclusive or concurrent thread/event manager.
      [📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Threading/Sentinel.cs.html)
- `Run`
    - job runner for Unity.
      ~~📘~~
    - provides reliable un-async-ing functions.
    - *NOTE*: `InitializeMainThreadContext` may be required to being called on Unity startup. (it called automatically by default)
    - **TODO**: document for `OnMainThread` `InThreadPool` `SetExceptionHandler` `GetTimerToken` `GetElapsedTime` `Shutdown` `CreateNewScheduler` `SetConcurrentThreadCount`
- `ThreadSafeSingleton`
    - thread! safe!! singleton!!!
- `HalfUlid`
    - https://github.com/sator-imaging/Half-Ulid
- NUnit-compatible Framework for Unity Editor
    - crazy stuff. no need to use this anymore as unity asset store now accepts `package.json`.
      [📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/FancyStuff/NUnit.Framework.UnityStubs.cs.html)



# Unity Features

## Runtime
- Nullable support for `UnityEngine.Object`
    - reliable nullable (`??` `?.` ~~`??=`~~) support for `UnityEngine.Object`.
      [📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/UnityObject/NullableUnityObject.cs.html)
- `ManagedShell`
    - provides functions that avoid creating leaked managed shell.
      [📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/UnityObject/ManagedShell.cs.html)
- `PoolableBehaviour<T>`
  - self-contained singly linked list based `MonoBehaviour` pool.
    [📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/UnityObject/PoolableBehaviour.cs.html)
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
    - CancellationToken based Lifecycle Manager for Unity Editor
    - > Obsolete features still exist as `.txt` files.
      [📘](https://sator-imaging.github.io/Unity-Fundamentals/Obsolete/LifecycleBehaviour/README.html)


## Editor

- `UnityEditorMainToolbar`
    - provide access to `VisualElement` in Unity main toolbar.
      [📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Editor/Uncategorized/UnityEditorMainToolbar.cs.html)
- Leaked Managed Shell Detector
    - not perfect. just for reference.
      [📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Editor/Uncategorized/LeakedManagedShellDetector.cs.html)
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
