# Magma Framework

Magma Framework is a **Unity framework** built to provide essential, reusable functionality and streamline game development. It fills in the gaps left by Unity out of the box, enabling faster and more maintainable development.

## 🚀 Features

- **Memory-efficient object pooling** (leverages Unity Addressables)  
- **Global event bus** for decoupled, cross-system communication  
- A set of **useful extensions** (helper methods, syntactic sugar, etc.)  
- **Utility tools** (common tasks, helpers)  
- **Real-time minimal profiling**, rendered in a UI canvas for in-game diagnostics  

## 📖 Documentation

Full documentation is available [here](https://docs.google.com/document/d/1_v2yFBbT9sble8bZTTgPfpZOcjiiAbKannSIF90Vvc4/edit?usp=sharing).

## 💡 Why Use Magma Framework

Many Unity projects end up re-implementing common systems (pooling, event dispatch, profiling). Magma Framework offers battle-tested implementations that are:

- **Efficient** — Especially for memory and performance  
- **Modular** — Pick and choose which parts you want to use  
- **Easy to integrate** — Designed to work alongside your existing workflows  

## 🧪 Getting Started

1. Clone or include the framework in your Unity project  
2. Add the prefab for the service that you want to use (each service is a singleton); 
   - MagmaFramework_Core
   - MagmaFramework_PoolingManager
3. All game objects should inherit from BaseBehaviour
3. Configure Addressables (if using the pooling system)  
4. Use the event bus by subscribing/publishing events  
5. (Optional) Add the profiler UI to your scene to start measuring  

> For more detailed setup, check the [documentation](https://docs.google.com/document/d/1_v2yFBbT9sble8bZTTgPfpZOcjiiAbKannSIF90Vvc4/edit?usp=sharing).
