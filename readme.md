# Magma Framework

Magma Framework is a **Unity framework** built to provide essential, reusable functionality and streamline game development. It fills in the gaps left by Unity out of the box, enabling faster and more maintainable development.

## 🚀 Features

- **Memory-efficient object pooling** (leverages Unity Addressables)  
- **Global event bus** for decoupled, cross-system communication  
- A set of **useful extensions** (helper methods, syntactic sugar, etc.)  
- **Utility tools** (common tasks, helpers)  
- **Real-time minimal profiling**, rendered in a UI canvas for in-game diagnostics  

## 📖 Documentation

Full documentation is available [here](https://docs.google.com/document/d/e/2PACX-1vRjudwI4xmRMmXOKgQH6CA1VQmvLxAO70rtXhQFds0vPjP0iR0zguIednKDQw9OIJy7Nr8BWNEoyUac/pub).

## 💡 Why Use Magma Framework

Many Unity projects end up re-implementing common systems like; pooling, event dispatch, delayed invoke, etc. Magma Framework offers implementations that are:

- **Efficient** — Especially for memory and performance  
- **Modular** — Pick and choose which parts you want to use  
- **Easy to integrate** — Designed to work alongside your existing workflows  

## 🧪 Getting Started

1. Add the framework as a unity package (via the package manager, using a git URL).
2. Add the prefab for the service that you want to use (each service is a singleton); 
   - MagmaFramework_Core
   - MagmaFramework_PoolingManager
   - MagmaFramework_SystemInformation
   - MagmaFramework_MusicManager
3. All game objects should inherit from BaseBehaviour.
4. Configuring addressable assets (if using the pooling system or InstantiateAddressable() from BaseBehaviour)
5. Create a 'AssetReferenceDictionaryBuilder' scriptable object that allows retrieving addressable asset references by prefab name.
6. Open the 'MagmaFramework_Examples' scene, to learn how to use all of the framework's features.
> For more detailed setup, check the [documentation](https://docs.google.com/document/d/e/2PACX-1vRjudwI4xmRMmXOKgQH6CA1VQmvLxAO70rtXhQFds0vPjP0iR0zguIednKDQw9OIJy7Nr8BWNEoyUac/pub).