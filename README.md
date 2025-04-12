# Unity Scripting Fundamentals

🇯🇵 [日本語版](https://qiita.com/sator_imaging/items/235c8447f4171ba5a522)

Fundamental scripting library for Unity designed to be minimal, efficient & dependency-free as possible.
Most of scripts are C# / .NET compliant. See `using` statements in `.cs` files for details.

> [!TIP]
> Licensed under the MIT License unless otherwise described.  
> Supported Unity version: Unity 2021.3+  


# Installation

Add the following `git URL` in Unity Package Manager (UPM)

![](https://docs.unity3d.com/2020.3/Documentation/uploads/Main/PackageManagerUI-GitURLPackageButton.png)


## Release version
```
https://github.com/sator-imaging/Unity-Fundamentals.git#v1.2.0
```
<sup>&nbsp; Append desired version at the end. (`#vX.Y.Z`)</sup>

### Latest version
```
https://github.com/sator-imaging/Unity-Fundamentals.git
```
<sup>&nbsp; very experimental, may have breaking changes without notice.</sup>



# Features

## Plain C# APIs

### Non-Alloc String Splitter
Split string without allocation.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Text/NonAllocStringSplitter.cs.html)
or [日本語版](https://qiita.com/sator_imaging/items/1393aa0efa3b064d77ec)


### Observable Action
Transform `event Action<T>` to `IObservable<T>`.

- `ObservableEvent<T>`
  [📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Reactive/ObservableEvent.cs.html)
  (recommended)
- `ObservableAction<T>`
  [📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Reactive/ObservableAction.cs.html)


### `WhenEachEnumerator`
`Task.WhenEach` for Unity / .NET Standard 2.1
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Threading/WhenEachEnumerator.cs.html)
or [日本語版](https://qiita.com/sator_imaging/items/0facece38f6e1c03bd19)


### `Defer`
Providing function for `early-finally` pattern.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/System/Defer.cs.html)

*TODO*: implement `IAsyncDisposable` overloads.
- ex) `await using var _ = Defer.New(async () => await ...);`


### `StrictEnum`
Unlike other enum utility, this class re-use system cache and also support parsing `Flags` value.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/System/StrictEnum.cs.html)


### `SpanList<T>`
List implementation of `Span<T>` especially designed to work with `Span<char>`.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/System/SpanList.cs.html)

*TODO*: add `(int, int) GetMinMaxLength()`
- to prevent enumerating repeatedly to get same result.
- add `bool _isFrozen` to determine recalculate is required. `Write` turns it off when changed.
- add `bool IsFormattable` with more strict token check: `{one}{two}` (currently accepted but must be rejected)
- one pass replacement for `FormatNonAlloc`: search for `{` then perform `.Slice(foundIndex, fromTokenMaxLength)`, check which one is match.


### `MiniXXHash`
Minimal xxHash32 / xxHash64 implementation.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Hashing/MiniXXHash.cs.html)
or [日本語版](https://qiita.com/sator_imaging/items/36c390c0536a6750b788)

<details><summary>License: <strong>BSD 2-Clause</strong></summary>

xxHash Clean C Reference Implementation
```
xxHash Library
Copyright (c) 2012-2020 Yann Collet
Copyright (c) 2019-2020 Devin Hussey (easyaspi314)
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice, this
  list of conditions and the following disclaimer in the documentation and/or
  other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

</details>



### `UString`
Lightning-fast non-alloc string builder faster than `DefaultInterpolatedStringHandler`
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Text/UString.cs.html)
or [技術的な解説](https://qiita.com/sator_imaging/items/edc5be1a55dd867e1c73)

*NOTE*: depending on `StrictEnum`
**TODO**: benchmark


### `Poolable<T>`
Self-contained singly linked list based object pool.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/System/Poolable.cs.html)
or [日本語版](https://qiita.com/sator_imaging/items/2a387a54a01e91e5d71d)

**TODO**: write tests


### `Sentinel`
Fast & efficient exclusive or concurrent thread/event manager.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Threading/Sentinel.cs.html)


### `Run`
Job runner for Unity providing reliable un-async-ing functions and more.

> [!NOTE]
> `InitializeMainThreadContext` may be required to being called on Unity startup. (it called automatically by default)

**TODO**: documentation for `OnMainThread`, `InThreadPool`, `SetExceptionHandler`, `GetTimerToken`, `GetElapsedTime`, `Shutdown`, `CreateNewScheduler`, `SetConcurrentThreadCount`


### `ThreadSafeSingleton`
Thread! safe!! singleton!!!
[日本語版](https://qiita.com/sator_imaging/items/6c60e462417a5235778a)


### `HalfUlid`
[実装の詳細](https://qiita.com/sator_imaging/items/576781d95367d5856624)

https://github.com/sator-imaging/Half-Ulid


### NUnit-compatible Framework for Unity Editor
Crazy stuff.
No need to use this anymore as unity asset store now accepts `package.json`.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/FancyStuff/NUnit.Framework.UnityStubs.cs.html)



## Unity Runtime APIs

### Observable `UnityEvent`
Transform `UnityEvent` to `IObservable<T>`.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Reactive/ObservableUnityEvent.cs.html)


### Nullable support for `UnityEngine.Object`
Reliable nullable (`??` `?.` ~~`??=`~~) support for `UnityEngine.Object`.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/UnityObject/NullableUnityObject.cs.html)


### `ManagedShell`
Provides functions that avoid creating leaked managed shell.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/UnityObject/ManagedShell.cs.html)
or [解説その１](https://qiita.com/sator_imaging/items/6bca6c642b6347bd68d8)
[その２](https://qiita.com/sator_imaging/items/1251fa3a0b51db87a82f)


### `PoolableBehaviour<T>`
Self-contained singly linked list based `MonoBehaviour` pool.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/UnityObject/PoolableBehaviour.cs.html)
or [日本語版](https://qiita.com/sator_imaging/items/80c712ee4fcabfce9163)


### UI Toolkit Core Extensions
- `ExecuteAfter` extension method
    - unlike builtin `ExecuteLater` method, this method runs action right after specified number of *repaint* events.
- `DisableStyleTransitionScope` extension method
    - this method temporarily turns off all transitions and turns them back on when leaving the `IDisposable` scope. useful for immediate style update without transition animation.

### UI Toolkit Event Subscription
Use extension method `RegisterCallbackAsSubscription` to register event as `IDisposable` interface.
It allows easily unregister event later.


### UI Toolkit Dropdown Helper
Extension method `RegisterCallbackAsEnum` allows implement typed callback.
And also there is option to delay event to correctly handle dropdown event.
(if no delay, dropdown in Unity editor behaves like 'stop-the-world' and some actions are not work as expected)

*NOTE*: depending on `StrictEnum`



## Unity Editor Scripts

### `UnityEditorMainToolbar`
Provide access to `VisualElement` in Unity main toolbar.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Editor/Uncategorized/UnityEditorMainToolbar.cs.html)
or [日本語版](https://qiita.com/sator_imaging/items/f1bdf82016117cd6c7bd)


### Leaked Managed Shell Detector
Not perfect.
Just for reference.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Editor/Uncategorized/LeakedManagedShellDetector.cs.html)


### Assembly Definitions Manager for Project Settings Panel
**TODO**: documentation


### *WIP*: `sealed`-able Type Finder
As IL2CPP bloats resulting C++ code if C# class is not marked with `sealed` modifier.



## *Obsolete*

### CancellationToken based Lifecycle Manager for Unity Editor
Obsolete features still exist as `.txt` files.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/Obsolete/LifecycleBehaviour/README.html)
or [日本語版](https://qiita.com/sator_imaging/items/48296a686e4bbff8676c)





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


## Gemini Code Assist for GitHub

### Commands
Post as a comment for pull request.
- `/gemini summary`
- `/gemini review`
- `/gemini`
- `/gemini help`

### How to Customize
https://developers.google.com/gemini-code-assist/docs/customize-gemini-behavior-github#style-guide
