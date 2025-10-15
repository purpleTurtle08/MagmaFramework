MagmaFlow Framework
Welcome to the MagmaFlow Framework for Unity! This framework is designed to accelerate your game development by providing a robust set of core functionalities, utilities, and workflow enhancements. It's built to be modular, efficient, and easy to integrate into any Unity project.

The core philosophy of this framework is to replace Unity's standard MonoBehaviour with an extended BaseBehaviour class, which automatically hooks into the framework's systems and provides a suite of powerful tools.

🚀 Core Features
1. MagmaFramework Core
The heart of the system is the MagmaFramework singleton. This central manager handles essential game-wide operations and persists across scenes.

Singleton Manager: Ensures a single, persistent instance manages core systems.

Game State Control: Easily pause and unpause the game (PauseGame), with optional control over Time.timeScale.

Application Lifecycle: Quit the application or stop play mode in the editor with a single Quit() call.

Display and Graphics:

VSync Control: Fine-tune vertical sync with SetVSync.

Resolution Settings: Change screen resolution and fullscreen mode via SetScreenResolution.

Cursor Management: Toggle cursor visibility and lock state (SetCursorEnabled) or set a custom cursor texture (SetCursor).

Object Pooling Integration: Automatically initializes and provides access to the PooledObjectsManager.

2. EventBus System
A powerful, decoupled messaging system for communication between different parts of your game without creating hard dependencies.

Type-Based Events: Events are defined as simple structs, ensuring type safety and minimal overhead.

Static API: Subscribe, Unsubscribe, and Publish events from anywhere in your codebase.

Automatic Integration: The BaseBehaviour class automatically subscribes to core game events like GamePausedEvent, SceneLoadedEvent, and SceneUnloadedEvent.

3. Asynchronous Object Pooling
An efficient, addressable-based object pooling system (PooledObjectsManager) to reduce garbage collection and improve performance by reusing GameObjects.

Addressables Integration: Asynchronously loads prefabs using Unity's Addressable Asset System.

Simple API:

Prewarm: Pre-instantiate a number of objects to avoid hitches during gameplay.

InstantiatePooledObject: Retrieve an object from the pool. If none are available, a new one is created.

ReleaseObject: Return an object to the pool for later reuse.

IPoolableObject Interface: Ensures pooled objects follow a consistent lifecycle with OnInitialize() and OnRelease() methods.

4. BaseBehaviour
This class is intended to replace MonoBehaviour in your scripts. It extends Unity's default behavior with built-in framework integration and utility functions.

Automatic Event Handling: Automatically subscribes to and unsubscribes from core framework events in OnEnable() and OnDisable().

Component Utilities:

GetAllSubcomponents: Recursively finds all components of a type in an object's children.

GetSubcomponent: Finds a specific component in a named child.

Delayed Invokes: A coroutine-based Invoke method that accepts an Action for cleaner, parameter-less delayed calls.

Physics Utilities: GetOverlapContactPoints calculates contact points for an object's collider against overlapping colliders within a specified radius.

GameObject Creation: Simplified methods like CreateWithComponent to instantiate a new GameObject and add a component in a single call.

🛠️ Utilities & Tools
MagmaUtils
A static utility class (MagmaUtils) filled with helpful functions to solve common problems.

Time Formatting: GetTimeFormatted converts seconds into a formatted hh:mm:ss string.

Data Conversion:

StringToVector3: Parses a string into a Vector3.

HexaToColor: Converts a hex color code into a Color object.

Editor Console: ClearConsole programmatically clears the Unity editor console.

LayerMask Helpers:

LayerMaskToLayer: Converts a single-layer LayerMask to its integer layer index.

CanvasScaleTool
A component to automatically manage CanvasScaler settings for consistent UI scaling across different screen resolutions.

Automatic Scaling: Adjusts the canvas reference resolution based on the screen's aspect ratio.

Customizable: Set the desired draw order and width/height match property directly from the inspector.

Event-Driven: Automatically responds to SetScreenResolutionEvent to rescale the UI when the resolution changes.

MagmaFpsCounter
A lightweight, drag-and-drop FPS counter optimized for minimal performance impact.

Performance Metrics: Displays average FPS, lowest FPS, and average frame time in milliseconds.

System Info: Optionally displays static system information like GPU, CPU, VRAM, and OS.

Customizable Formatting: Both dynamic (FPS) and static (system info) text formats can be fully customized in the inspector.

MagmaExtensions
A collection of C# extension methods to enhance built-in Unity types.

Transform:

ResetTransformation: Resets a transform's position, rotation, and scale.

ClearChildren: Destroys all child objects of a transform.

CanvasGroup: SetVisible quickly toggles the alpha, interactable, and blocksRaycasts properties.

GameObject:

IsPartOfLayerMask: Checks if a GameObject belongs to a given LayerMask.

LayerToLayerMask: Converts a GameObject's layer into a LayerMask.

🎮 How to Use
Add the MagmaFramework Prefab: Place the MagmaFrameworkCore prefab (or a GameObject with the MagmaFramework.cs script) into your initial scene.

Inherit from BaseBehaviour: Change your scripts to inherit from BaseBehaviour instead of MonoBehaviour to gain access to the framework's features.

Use the Systems:

Access the framework instance via MagmaFramework.Instance or the MagmaFramework property in BaseBehaviour.

Publish and subscribe to events using EventBus.Publish<MyEvent>() and EventBus.Subscribe<MyEvent>(MyCallback).

Use the object pooler via MagmaFramework.Instance.PooledObjectsManager.

Happy developing!