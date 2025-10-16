using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MagmaFlow.Framework.Core
{
	public interface IPoolableObject
	{
		public bool IsActive { get; }
		public MonoBehaviour MonoBehaviour => this as MonoBehaviour;
		/// <summary>
		/// Called AFTER an object is retrieved from the pool
		/// </summary>
		public void OnInitialize();
		/// <summary>
		/// Releases the object back into the pool by setting the game object as inactive
		/// </summary>
		public void OnRelease();
	}

	[DefaultExecutionOrder(-10)]
	public class PooledObjectsManager : MonoBehaviour
	{
#if UNITY_EDITOR
		[ContextMenu("Log Pool State")]
		private void LogPoolState()
		{
			foreach (var keyValue in pool)
			{
				var key = keyValue.Key;
				var count = keyValue.Value.Count;
				var activeCount = lookUp.Count(kv => Equals(kv.Value, key) && kv.Key.MonoBehaviour != null && kv.Key.MonoBehaviour.gameObject.activeSelf);
				string assetName = "UnknownEntry";
				assetNames.TryGetValue(key, out assetName);
				Debug.Log($"[POOL] {assetName} -> {count} -- In Pool ||| {activeCount} -- Active");
			}
		}
#endif

		/// <summary>
		/// Due to memory efficiency this should be kept fairly around 50.
		/// No upper bound on the pool means it can grow indefinitely if unused objects accumulate.
		/// </summary>
		public int MaximumPoolSize { get; private set; } = 99999999;

		/// <summary>
		/// The pools of inactive objects.
		/// </summary>
		private readonly Dictionary<object, Queue<IPoolableObject>> pool = new();
		/// <summary>
		/// The massive collection of all the instantiated objects.
		/// </summary>
		private readonly Dictionary<IPoolableObject, object> lookUp = new();
		/// <summary>
		/// This holds all the asset names for their respective asset keys (object)
		/// </summary>
		private readonly Dictionary<object, string> assetNames = new();
		/// <summary>
		/// This helps handling possible simultaneous prewarm operations
		/// </summary>
		private readonly HashSet<object> currentlyPrewarming = new();
		/// <summary>
		/// This is so that our scene inspector doesn't get filled with pooled objects
		/// </summary>
		private Transform genericPooledObjectsParent;
		private CancellationTokenSource prewarmCTS;
		private CancellationTokenSource instantiateCTS;
		internal void RemoveLookup(IPoolableObject obj) => lookUp.Remove(obj);

		private void OnDestroy()
		{	
			prewarmCTS?.Cancel();
			instantiateCTS?.Cancel();
			ClearObjectPools();
		}

		/// <summary>
		/// Internal helper to create and register a new pooled instance.
		/// </summary>
		/// <param name="assetReference"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		private async Task<IPoolableObject> CreateNewInstance(AssetReference assetReference, CancellationToken cancellationToken = default)
		{
			if (assetReference == null)
			{
				Debug.LogWarning("CreateNewInstance called with null AssetReference.");
				return null;
			}

			var assetKey = assetReference.RuntimeKey;
			AsyncOperationHandle<GameObject> handle = assetReference.InstantiateAsync(genericPooledObjectsParent);
			try
			{
				// Await completion (will throw if Task is canceled)
				await handle.Task;

				// Handle early cancellation before continuing
				if (cancellationToken.IsCancellationRequested)
				{
					if (handle.IsValid() && handle.Result != null)
						Addressables.ReleaseInstance(handle.Result);

					Debug.Log($"Instantiation of {assetReference.editorAsset.name} was canceled.");
					return null;
				}

				// Check handle result
				if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
				{
					if (handle.IsValid() && handle.Result != null)
						Addressables.ReleaseInstance(handle.Result);

					Debug.LogError($"Failed to instantiate addressable: {assetReference.editorAsset.name}");
					return null;
				}

				GameObject instance = handle.Result;

				// Verify it implements IPoolableObject
				if (!instance.TryGetComponent<IPoolableObject>(out var pooledObject))
				{
					Debug.LogError(
						$"The prefab '{instance.name}' does not implement IPoolableObject. Cleaning up."
					);
					Addressables.ReleaseInstance(instance);
					return null;
				}

				//We add a cleanup component that handles removing the entry OnDestroy()
				instance.AddComponent<PooledInstanceCleanup>().Initialize(this, pooledObject);
				// Register in the appropriate dictionaries
				lookUp[pooledObject] = assetKey;
				if (!assetNames.ContainsKey(assetKey))
				{
					assetNames[assetKey] = assetReference.editorAsset.name;
				}
				return pooledObject;
			}
			catch (Exception e)
			{
				// If something goes wrong during await
				if (handle.IsValid() && handle.Result != null)
					Addressables.ReleaseInstance(handle.Result);

				Debug.LogException(e);
				return null;
			}
		}

		/// <summary>
		/// Due to memory constraints you can set a maximum pool size, or leave it -1
		/// </summary>
		/// <param name="maximumPoolSize"></param>
		public void Initialize(int maximumPoolSize = -1)
		{
			MaximumPoolSize = maximumPoolSize >= 0 ? maximumPoolSize : 99999999;
			var poolRoot = new GameObject("Pooled Objects Container");
			poolRoot.transform.SetParent(transform);
			genericPooledObjectsParent = poolRoot.transform;
		}

		/// <summary>
		/// Prewarm the pool with a number of instances
		/// </summary>
		/// <param name="assetReference"></param>
		/// <param name="count"></param>
		public async Task Prewarm(AssetReference assetReference, int count)
		{
			var key = assetReference.RuntimeKey;
			if (!currentlyPrewarming.Add(key)) return;

			try
			{
				prewarmCTS = new CancellationTokenSource();
				if (!pool.ContainsKey(key))
					pool[key] = new Queue<IPoolableObject>();

				Debug.Log($"Prewarming {count} {assetReference.editorAsset.name}...");
				for (int i = 0; i < count; i++)
				{
					var instance = await CreateNewInstance(assetReference, prewarmCTS.Token);
					if (instance == null) { return; }
					ReleaseObject(instance);
				}
			}
			finally
			{
				currentlyPrewarming.Remove(key);
			}
		}

		/// <summary>
		/// Instantiates or retrieves a pooled object and returns the component of type T.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="assetReference"></param>
		/// <param name="parent"></param>
		/// <param name="position"></param>
		/// <param name="rotation"></param>
		/// <param name="useWorldSpace"></param>
		/// <param name="setActive"></param>
		/// <returns></returns>
		public async Task<T> InstantiatePooledObject<T>(
			AssetReference assetReference,
			Transform parent,
			Vector3 position,
			Quaternion rotation,
			bool useWorldSpace) where T : Component
		{
			if (assetReference == null)
			{
				Debug.LogError("The asset reference that you want to instantiate is null.");
				return null; 
			}

			if (!pool.TryGetValue(assetReference.RuntimeKey, out var queue))
			{
				queue = new Queue<IPoolableObject>();
				pool[assetReference.RuntimeKey] = queue;
			}

			IPoolableObject pooledObject = null;
			if (queue.Count > 0)
			{
				pooledObject = queue.Dequeue();
			}

			if (pooledObject == null || pooledObject.MonoBehaviour == null)
			{
				instantiateCTS = new CancellationTokenSource();
				pooledObject = await CreateNewInstance(assetReference, instantiateCTS.Token);
			}

			if (pooledObject == null) return null;

			var objTransform = pooledObject.MonoBehaviour.transform;
			objTransform.SetParent(parent ?? genericPooledObjectsParent);
			if (!useWorldSpace)
			{
				objTransform.localPosition = position;
				objTransform.localRotation = rotation;
			}
			else
			{
				objTransform.position = position;
				objTransform.rotation = rotation;
			}

			objTransform.gameObject.SetActive(true);
			pooledObject.OnInitialize();

			return objTransform.GetComponent<T>();
		}

		/// <summary>
		/// Releases an object back into the pool.
		/// </summary>
		/// <param name="pooledObject"></param>
		public void ReleaseObject(IPoolableObject pooledObject)
		{
			if (pooledObject == null || !lookUp.ContainsKey(pooledObject))
			{
				//Debug.LogError("The object you want to release is not pooled.");
				return;
			}

			pooledObject.OnRelease();
			pooledObject.MonoBehaviour.gameObject.SetActive(false);
			pooledObject.MonoBehaviour.transform.SetParent(genericPooledObjectsParent);

			var assetReference = lookUp[pooledObject];

			if (pool[assetReference].Count >= MaximumPoolSize)
				Addressables.ReleaseInstance(pooledObject.MonoBehaviour.gameObject);
			else
				pool[assetReference].Enqueue(pooledObject);
		}

		/// <summary>
		/// Destroys all pooled objects.
		/// </summary>
		public void ClearObjectPools()
		{
			prewarmCTS?.Cancel();
			instantiateCTS?.Cancel();

			foreach (var entry in lookUp.Keys)
			{
				if (entry?.MonoBehaviour != null)
					Addressables.ReleaseInstance(entry.MonoBehaviour.gameObject);
			}

			pool.Clear();
			lookUp.Clear();
		}
	}
}
