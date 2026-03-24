[![DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/sator-imaging/Unity-Fundamentals)
[![🇯🇵](https://img.shields.io/badge/API-Reference-blue)](https://sator-imaging.github.io/Unity-Fundamentals/api/index.html)
&nbsp;
[![🇯🇵](https://img.shields.io/badge/🇯🇵-日本語_※詳説-789)](https://qiita.com/sator_imaging/items/235c8447f4171ba5a522)
<!--
[![🇯🇵](https://img.shields.io/badge/🇯🇵-日本語-789)](./README.ja.md)
[![🇨🇳](https://img.shields.io/badge/🇨🇳-简体中文-789)](./README.zh-CN.md)
[![🇺🇸](https://img.shields.io/badge/🇺🇸-English-789)](./README.md)
-->


Unity Scripting Fundamentals is designed to be minimal, efficient & dependency-free as possible.
Most of scripts are .NET / Plain C# compliant. See `using` statements in `.cs` files for details.

- 🧱 [Plain C# APIs](#-plain-c-apis)
- 🎮 [Unity Runtime APIs](#-unity-runtime-apis)
- 🖱️ [Unity Editor Extensions](#️-unity-editor-scripts)

> [!TIP]
> Licensed under the MIT License unless otherwise described.  
> Supported Unity version: Unity 2021.3+  


# 📦 Installation

Add the following `git URL` in Unity Package Manager (UPM).

![UPM](https://docs.unity3d.com/2020.3/Documentation/uploads/Main/PackageManagerUI-GitURLPackageButton.png)


## 🏷️ Release version

Use latest stable release.

```
https://github.com/sator-imaging/Unity-Fundamentals.git#latest
```

or use specific version (append desired version at the end: `#vX.Y.Z`).

```
https://github.com/sator-imaging/Unity-Fundamentals.git#v1.7.2
```


## 🦜 Canary/Nightly Build

> [!IMPORTANT]
> It's very experimental and may have breaking changes, bug or errors without notice.

```
https://github.com/sator-imaging/Unity-Fundamentals.git
```





&nbsp;

# 🧱 Plain C# APIs

- `Fibers`  
Microthreading Library for .NET / Unity.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Threading/Fibers.cs.html)
or
[日本語版](https://zenn.dev/sator_imaging/articles/0236e98fbbbc98)


- `FiberScheduler`  
Task scheduler with concurrency level control.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/api/SatorImaging.UnityFundamentals.FiberScheduler.html)


- **Non-Alloc String Splitter**  
Split string without allocation.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Text/NonAllocStringSplitter.cs.html)
or
[日本語版](https://qiita.com/sator_imaging/items/1393aa0efa3b064d77ec)


- **Rx (Reactive Extensions) / Observable**  
Transform `event Action<T>` to `IObservable<T>`.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Reactive/ObservableEvent.cs.html)


- **Easing Functions**: `EasingFloat` and `EasingDouble`  
High performance easing functions (Unity Burst Compiler ready).
[📘](https://x.com/sator_imaging/status/2035921437433966891)


- `NetworkClock`  
Believe-worthy, device-independent time provider based on `HTTPS` connection.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/System/NetworkClock.cs.html)
or
[解説](https://zenn.dev/sator_imaging/articles/6e1bd822a82d31)


- `HalfUlid`  
https://github.com/sator-imaging/Half-Ulid
&nbsp;
[実装の詳細](https://qiita.com/sator_imaging/items/576781d95367d5856624)


- `Atom<T>`  
Thread safe primitive.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Threading/Atom.cs.html)
or
[解説](https://zenn.dev/sator_imaging/articles/ed6dac717f5038)


- `MiniXXHash`  
Minimal xxHash32 / xxHash64 implementation.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Hashing/MiniXXHash.cs.html)
or
[日本語版](https://qiita.com/sator_imaging/items/36c390c0536a6750b788)

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


- `SpanList<T>`  
List implementation of `Span<T>` especially designed to work with `Span<char>`.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/System/SpanList.cs.html)

    - *TODO*: Add `(int, int) GetMinMaxLength()`
        - To prevent enumerating repeatedly to get same result.
        - Add `bool _isFrozen` to determine recalculate is required. `Write` turns it off when changed.
        - Add `bool IsFormattable` with more strict token check: `{one}{two}` (currently accepted but must be rejected).
        - One pass replacement for `FormatNonAlloc`: search for `{` then perform `.Slice(foundIndex, fromTokenMaxLength)`, check which one is match.


- `RustSharp`  
Move semantics for .NET / Unity.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/FancyStuff/RustSharp.cs.html)
or
[解説](https://zenn.dev/sator_imaging/articles/e6428d4f1c8158)


- `UString`  
Lightning-fast non-alloc string builder faster than `DefaultInterpolatedStringHandler`.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Text/UString.cs.html)
or
[技術的な解説](https://qiita.com/sator_imaging/items/edc5be1a55dd867e1c73)

    *NOTE*: Depending on `StrictEnum`
    *TODO*: Benchmark


- `Poolable<T>`  
Self-contained singly linked list based object pool.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/System/Poolable.cs.html)
or
[日本語版](https://qiita.com/sator_imaging/items/2a387a54a01e91e5d71d)

    *TODO*: Write tests


- `Sentinel`  
Fast & efficient exclusive or concurrent thread/event manager.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Threading/Sentinel.cs.html)


- `WhenEachEnumerator`  
`Task.WhenEach` for Unity / .NET Standard 2.1.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Threading/WhenEachEnumerator.cs.html)
or
[日本語版](https://qiita.com/sator_imaging/items/0facece38f6e1c03bd19)


- `Defer`  
Providing function for `early-finally` pattern.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/System/Defer.cs.html)

    - *TODO*: Implement `IAsyncDisposable` overloads.
        - ex: `await using var _ = Defer.New(async () => await ...);`


- `StrictEnum`  
Unlike other enum utility, this class reuses system cache and also support parsing `Flags` value.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/System/StrictEnum.cs.html)

    See also:
    [FinalEnumGenerator](https://github.com/sator-imaging/FGenerator/tree/main/FinalEnumGenerator)


- `ThreadSafeSingleton`  
Thread! safe!! singleton!!!
[日本語版](https://qiita.com/sator_imaging/items/6c60e462417a5235778a)


- NUnit-compatible Framework for Unity Editor  
Crazy stuff.
No need to use this anymore as unity asset store now accepts `package.json`.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/FancyStuff/NUnit.Framework.UnityStubs.cs.html)





&nbsp;

# 🎮 Unity Runtime APIs

- **Observable `UnityEvent`**  
Transform `UnityEvent` to `IObservable<T>`.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Reactive/ObservableUnityEvent.cs.html)


- **Nullable support for `UnityEngine.Object`**  
Reliable nullable (`??` `?.` ~~`??=`~~) support for `UnityEngine.Object`.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/UnityObject/NullableUnityObject.cs.html)


- `ManagedShell`  
Provides functions that avoid creating leaked managed shell.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/UnityObject/ManagedShell.cs.html)
or
[解説その１](https://qiita.com/sator_imaging/items/6bca6c642b6347bd68d8)
[その２](https://qiita.com/sator_imaging/items/1251fa3a0b51db87a82f)


- `PoolableBehaviour<T>`  
Self-contained singly linked list based `MonoBehaviour` pool.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/UnityObject/PoolableBehaviour.cs.html)
or
[日本語版](https://qiita.com/sator_imaging/items/80c712ee4fcabfce9163)


- **UI Toolkit Core Extensions**
    - `ExecuteAfter` extension method
        - unlike builtin `ExecuteLater` method, this method runs action right after specified number of *repaint* events.
    - `DisableStyleTransitionScope` extension method
        - this method temporarily turns off all transitions and turns them back on when leaving the `IDisposable` scope. useful for immediate style update without transition animation.

    - UI Toolkit Event Subscription  
    Use extension method `RegisterCallbackAsSubscription` to register event as `IDisposable` interface.
    It allows easily unregister event later.

    - UI Toolkit Dropdown Helper  
    Extension method `RegisterCallbackAsEnum` allows implement typed callback.
    And also there is option to delay event to correctly handle dropdown event.
    (if no delay, dropdown in Unity editor behaves like 'stop-the-world' and some actions are not work as expected)

        *NOTE*: Depending on `StrictEnum`


- `ReusableCoroutine`  
Non-alloc Unity coroutine implementation.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/api/SatorImaging.UnityFundamentals.ReusableCoroutine.html)
or
[日本語版](https://zenn.dev/sator_imaging/articles/b0ab667808d563#【unity】再利用可能なコルーチン)



&nbsp;

# 🖱️ Unity Editor Scripts

- **UPM Package Test Kit**  
Generate clean environment and build script for selected UPM package.
[📘](Editor/UpmPackage)
or
[日本語マニュアル](https://zenn.dev/sator_imaging/articles/feadbd5b1281d8)

    *TODO*: Unit test automation.


- **C# API Documentation for IDE**  
Download C# API Documentation from Nuget.org.
[📘](Editor/Nuget)

    License: <strong>Apache License version 2.0</strong>


- `UnityEditorMainToolbar`  
Provide access to `VisualElement` in Unity main toolbar.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Editor/Uncategorized/UnityEditorMainToolbar.cs.html)
or
[日本語版](https://qiita.com/sator_imaging/items/f1bdf82016117cd6c7bd)


- **Leaked Managed Shell Detector**  
Not perfect.
Just for reference.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Editor/Uncategorized/LeakedManagedShellDetector.cs.html)


- **Assembly Definitions Manager** for Project Settings Panel  
*TODO*: Documentation


- *WIP*: `sealed`-able Type Finder  
As IL2CPP bloats resulting C++ code if C# class is not marked with `sealed` modifier.





&nbsp;

# 🗑️ *Obsolete*

Files are found in [Obsolete/](./Obsolete/) folder with `.txt` extension.


- **CancellationToken based Lifecycle Manager**  
Obsolete features still exist as `.txt` files.
[📘](https://sator-imaging.github.io/Unity-Fundamentals/Obsolete/LifecycleBehaviour/README.html)
or
[日本語版](https://qiita.com/sator_imaging/items/48296a686e4bbff8676c)


- `Run`  
Job runner for Unity providing reliable un-async-ing functions and more.

    > [!NOTE]
    > `InitializeMainThreadContext` may be required to being called on Unity startup. (it called automatically by default)

    *TODO*: documentation for `OnMainThread`, `InThreadPool`, `SetExceptionHandler`, `GetTimerToken`, `GetElapsedTime`, `Shutdown`, `CreateNewScheduler`, `SetConcurrentThreadCount`


- `ObservableAction<T>`
[📘](https://sator-imaging.github.io/Unity-Fundamentals/HeaderDocs/Runtime/Reactive/ObservableAction.cs.html)





&nbsp;

# 📝 Devnote

## ⚙️ `DefineConstants`

How to set `DefineConstants` in *.csproj* file from `dotnet` command, see [Directory.Build.Props](../CStrBenchmark/Directory.Build.props) and [Program.cs](../CStrBenchmark/Program.cs#L37) for details.

Source: https://github.com/dotnet/sdk/issues/9562#issuecomment-1386955134
