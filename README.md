This library helps managing MonoBehaviour lifetime by CancellationToken introduced in Unity 2022.
Not only that but also has an ability to manage "Update" actions ordered and efficiently.

And also! library provides missing `destroyCancellationToken` feature for Unity 2021 LTS!!

**Feature Highlights**
- [Class Instance Lifetime Management](#object-lifetime-management)
- [Update Function Manager](#update-function-manager)
    - Designed to address consideration written in the following article
    - https://blog.unity.com/engine-platform/10000-update-calls
- [API Reference](https://sator-imaging.github.io/Unity-LifecycleManager)

**Installation**
- In Unity 2021 or later, Enter the following URL in Unity Package Manager (UPM)
    - https://github.com/sator-imaging/Unity-LifecycleManager.git


Object Lifetime Management
==========================
Here is example to bind instance lifetime to cancellation token or MonoBehaviour.

```csharp
using SatorImaging.LifecycleManager;

// works on Unity 2021 or later
disposable.DestroyWith(monoBehaviourOrCancellationToken);
gameObject.DestroyWith(cancellationToken);
unityObj.DestroyUnityObjectWith(tokenOrBehaviour);

// bind to unity scene lifetime
var sceneLifetime = SceneLifetime.Get(gameObject.scene);
disposable.DestroyWith(sceneLifetime);
sceneLifetime.Token.Register(() => DoSomethingOnSceneUnloading());

// lifecycle and its GameObject which will be destroyed on scene unloading
var sceneLC = SceneLifecycle.Get();

// nesting lifecycles
var root = LifecycleBehaviour.Create("Root Lifecycle");
var child = LifecycleBehaviour.Create("Child Lifecycle");
var grand = LifecycleBehaviour.Create("Grandchild Lifecycle");
child.DestroyWith(root);
grand.DestroyWith(child);
    // --> child and grand will be marked as DontDestroyOnLoad automatically

// action for debugging purpose which will be invoked before binding (when not null)
LifetimeExtensions.DebuggerAction = (obj, token, ticket, ownerOrNull) =>
{
    Debug.Log($"Target Object: {obj}");
    if (ownerOrNull != null)
        Debug.Log($"Lifetime Owner: {ownerOrNull}");
    Debug.Log($"CancellationToken: {token}");
    Debug.Log($"CancellationTokenRegistration: {ticket}");
};
```


Technical Note
--------------

### Unity Object/Component Binding Notice

You can bind `UnityEngine.Object` or component lifetime to cancellation token or MonoBehaviour by using
`DestroyUnityObjectWith` extension method instead of `DestroyWith`.

Note that when binding unity object lifetime to other, need to consider both situation that component is
destroyed by scene unloading OR by lifetime owner. As a result, it makes thing really complex and scene
will be spaghetti-ed.

Strongly recommended that binding GameObject lifetime instead of component, or implement `IDisposable`
on your unity engine object explicitly to preciously control behaviour.


### Inter-Scene Binding Notice

Lifetime binding across scenes is restricted. Nevertheless you want to bind lifetime to another
scene object, use `DestroyWith(CancellationToken)` method with `monoBehaviour.destroyCancellationToken`.

> [!WARNING]
> When Unity object bound to another scene object, it will be destroyed by both when lifetime owner is
> destroyed and scene which containing bound object is unloaded.


### Quick Tests

Select menu commands in `Unity Editor > LifecycleManager > ...` to test the features.



Order of Destruction
--------------------
Destroy actions will happen in LIFO order, that is, last lifetime-bound object is destroyed first.
(of course lifetime owner is destroyed before)

Note that C# class instances and GameObjects destruction order is stable whereas MonoBehaviours
destruction order is NOT stable. For reference, MonoBehaviours (components) will be destroyed earlier
when scene is unloaded otherwise destroyed based on binding order.

> [!NOTE]
> To make destruction order stable, extension method automatically mark lifetime bound GameObjects as
> `DontDestroyOnLoad`.



Update Function Manager
=======================
In this feature, each "update" function has 5 stages, Initialize, Early, Normal, Late and Finalize.
Initialize and Finalize is designed for system usage, other 3 stages are for casual usage.

```csharp
// create lifecycle behaviour
var lifecycle = LifecycleBehaviour.Create("My Lifecycle!!");

// register action to lifecycle stages
lifecycle.RegisterUpdateEarly(...);
lifecycle.RegisterUpdate(...);
lifecycle.RegisterUpdateLate(...);

lifecycle.RegisterLateUpdateEarly(...);
lifecycle.RegisterLateUpdate(...);
lifecycle.RegisterLateUpdateLate(...);

lifecycle.RegisterFixedUpdateEarly(...);
lifecycle.RegisterFixedUpdate(...);
lifecycle.RegisterFixedUpdateLate(...);

// to remove action manually, store and use instance which returned from register method
var entry = lifecycle.RegisterUpdateLate(...);
lifecycle.RemoveUpdateLate(entry);
```

> [!NOTE]
> For performance optimization, removing registered action will swap items in list instead of reordering
> whole items in list.
> ie. Order of update stages (early, late, etc) are promised but registered action order is NOT promised.
> (like Unity)


Automatic Unregistration
------------------------
If action is depending on instance that will be destroyed with cancellation token, You have to specify
same token to unregister action together when token is canceled.

```csharp
instance.DestroyWith(token);
lifecycle.RegisterUpdate(() => instance.NoErrorUntilDisposed(), token);  // <-- same token

// if don't, action will raise error after depending instance is destroyed
lifecycle.RegisterUpdate(() => instance.NoErrorUntilDisposed());  // <-- error!!
```


Controlling Order of Multiple Update Managers
---------------------------------------------
To meet your app requirement, it allows to change lifecycle execution order while keeping
registered actions order.

```csharp
// simple but effective! don't try to dive into UnityEngine.LowLevel.PlayerLoop system!!
class UpdateManagerOrganizer : MonoBehaviour
{
    public LifecycleBehaviour lifecycle1;
    public LifecycleBehaviour lifecycle2;
    public LifecycleBehaviour lifecycle3;

    void OnEnable()
    {
        lifecycle1.enabled = false;
        lifecycle2.enabled = false;
        lifecycle3.enabled = false;
    }
    void Update()
    {
        lifecycle3.Update();
        lifecycle1.Update();
        lifecycle2.Update();
    }
    void LateUpdate()
    {
        lifecycle1.LateUpdate();
        lifecycle3.LateUpdate();
        lifecycle2.LateUpdate();
    }
    void FixedUpdate()
    {
        lifecycle2.FixedUpdate();
        lifecycle3.FixedUpdate();
        lifecycle1.FixedUpdate();
    }
}
```
