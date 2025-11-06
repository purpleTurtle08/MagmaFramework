using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MagmaFlow.Framework.Pooling
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

	[DefaultExecutionOrder(-50)]
	public class MagmaFramework_PooledObjectsManager : MonoBehaviour
	{
#if UNITY_EDITOR
		/// <summary>
		/// Editor only method. Logs the state of the pools
		/// </summary>
		[ContextMenu("Log Pool State")]
		public void LogPoolState()
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

		public static MagmaFramework_PooledObjectsManager Instance { get; private set; }
		[SerializeField][Tooltip("This will be used when initializing the PooledObjectsManager.\nA value of 256 is recommended to avoid bloating up the memory with too many pooled instances.\n-1 for no limit")] private int maximumPoolSize = -1;

		private void Awake()
		{
			if (!Singleton())
			{
				return;
			}

			Initialize(maximumPoolSize);
		}

		/// <summary>
		/// This ensures that there can only be one object of this type per scene
		/// </summary>
		private bool Singleton()
		{
			var gameObjectName = gameObject.name;
			if (Instance != null && Instance != this)
			{
				Debug.LogWarning($"Removed {gameObjectName}, as it is a duplicate. Ensure you only have 1 {gameObjectName} per scene.");
				Destroy(gameObject);
				return false;
			}

			Instance = this;
			DontDestroyOnLoad(gameObject);
#if UNITY_EDITOR
			Debug.Log($"MagmaFramework::: {gameObjectName} service present.");
#endif
			return true;
		}

		/// <summary>
		/// Due to memory efficiency this should be kept fairly around 500.
		/// No upper bound on the pool means it can grow indefinitely if unused objects accumulate.
		/// </summary>
		public int MaximumPoolSize { get; private set; } = int.MaxValue;

		/// <summary>
		/// This dictionary contains the assets loaded into memory.
		/// <para>Used in preventing loading the same asset twice.</para>
		/// </summary>
		private Dictionary<object, AsyncOperationHandle<GameObject>> loadedAssets = new();
		/// <summary>
		/// One CTS per prewarm request (key = asset runtime key), avoid passing CTS logic to the caller
		/// </summary>
		private readonly Dictionary<object, CancellationTokenSource> prewarmTokens = new();
		/// <summary>
		/// Track all ongoing instantiations (for global cancel on destroy and avoid passing CTS logic to the caller)
		/// </summary>
		private readonly HashSet<CancellationTokenSource> activeInstantiations = new();
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
		/// <summary>
		/// A flag that indicates if ClearObjectPools() is in progress
		/// </summary>
		private bool isClearing = false;

		internal void RemoveLookup(IPoolableObject obj)
		{
			if (isClearing) return;
			lookUp.Remove(obj);
		}

		private void OnDestroy()
		{
			ClearObjectPools(true, true);
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
				Debug.LogError("CreateNewInstance() called with a NULL asset reference.");
				return null;
			}

			var assetKey = assetReference.RuntimeKey;

			try
			{
				// Await completion, there's no way of stopping/cancelling this handle
				var loadedAsset = await GetPrefabAsync(assetReference);

				// Handle early cancellation before continuing
				if (cancellationToken.IsCancellationRequested)
				{
#if UNITY_EDITOR
					Debug.LogWarning($"Instantiation of {assetReference.editorAsset.name} was cancelled.");
#endif
					return null;
				}

				if(loadedAsset == null)
				{
#if UNITY_EDITOR
					Debug.LogWarning($"There was an issue loading {assetReference.editorAsset.name} asset.");
#endif
					return null;
				}

				var instance = Instantiate(loadedAsset);

				// Verify it implements IPoolableObject
				if (!instance.TryGetComponent<IPoolableObject>(out var pooledObject))
				{
					Debug.LogError(
						$"The prefab '{instance.name}' does not implement IPoolableObject. Cleaning up."
					);
					Destroy(instance);

					return null;
				}

				//We add a cleanup component that handles removing the entry OnDestroy() from the lookup table
				instance.AddComponent<PooledInstanceCleanup>().Initialize(this, pooledObject);

				// Register in the appropriate dictionaries
				lookUp[pooledObject] = assetKey;
				if (!assetNames.ContainsKey(assetKey))
				{
					assetNames[assetKey] = instance.name;
				}
				return pooledObject;
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				return null;
			}
		}

		private async Task<GameObject> GetPrefabAsync(AssetReference assetReference)
		{
			object key = assetReference.RuntimeKey;

			// 1. Check if we already have it loaded (or are loading it)
			if (loadedAssets.TryGetValue(key, out var handle))
			{
				// We have a handle, so just wait for it to finish if it's not already
				await handle.Task;
				return handle.Result; // This is the GameObject prefab
			}

			// 2. If not, load it for the first time
			var loadHandle = Addressables.LoadAssetAsync<GameObject>(assetReference);

			// 3. Store the *handle* in the dictionary immediately
			loadedAssets[key] = loadHandle;

			// 4. Await the new handle and return the prefab
			await loadHandle.Task;
			return loadHandle.Result;
		}

		/// <summary>
		/// Stops all the prewarm operations
		/// </summary>
		public void CancelPrewarmOperations()
		{
			foreach (var cts in prewarmTokens.Values)
			{
				cts.Cancel();
			}
		}

		/// <summary>
		/// Stops all instantiating operations
		/// </summary>
		public void CancelInstantiateOperation()
		{
			foreach (var cts in activeInstantiations)
			{
				cts.Cancel();
			}
		}

		/// <summary>
		/// Due to memory constraints you can set a maximum pool size, or leave it -1
		/// </summary>
		/// <param name="maximumPoolSize"></param>
		public void Initialize(int maximumPoolSize = -1)
		{
			MaximumPoolSize = maximumPoolSize >= 0 ? maximumPoolSize : 9999999;
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
			if (!currentlyPrewarming.Add(key))
				return;

			CancellationTokenSource cts = new CancellationTokenSource();
			prewarmTokens[key] = cts;

			try
			{
				if (!pool.ContainsKey(key))
					pool[key] = new Queue<IPoolableObject>();
#if UNITY_EDITOR
				Debug.Log($"Prewarming {count} {assetReference.editorAsset.name}...");
#endif
				for (int i = 0; i < count; i++)
				{
					if (cts.Token.IsCancellationRequested)
						break;

					var instance = await CreateNewInstance(assetReference, cts.Token);
					if (instance == null)
						break;

					ReleaseObject(instance);
				}
			}
			finally
			{
				currentlyPrewarming.Remove(key);

				// Remove and dispose CTS
				if (prewarmTokens.Remove(key))
				{
					cts.Dispose();
				}
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

			IPoolableObject pooledObject = queue.Count > 0 ? queue.Dequeue() : null;

			if (pooledObject == null || pooledObject.MonoBehaviour == null)
			{
				var cts = new CancellationTokenSource();

				try
				{
					activeInstantiations.Add(cts);
					pooledObject = await CreateNewInstance(assetReference, cts.Token);
				}
				catch(Exception e)
				{
					Debug.LogException(e);
				}
				finally
				{
					activeInstantiations.Remove(cts);
					cts.Dispose();
				}
			}
			
			if (pooledObject == null)
				return null;

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
		/// Disables the game object.
		/// Releases the object back into the pool.
		/// </summary>
		/// <param name="pooledObject"></param>
		public void ReleaseObject(IPoolableObject pooledObject)
		{
			if (pooledObject == null) return;

			if(!lookUp.ContainsKey(pooledObject))
			{
				Debug.LogError($"The object {pooledObject.MonoBehaviour.name}, that you want to release is not pooled.");
				return;
			}

			pooledObject.OnRelease();
			pooledObject.MonoBehaviour.gameObject.SetActive(false);
			pooledObject.MonoBehaviour.transform.SetParent(genericPooledObjectsParent);

			var assetReference = lookUp[pooledObject];

			if (pool[assetReference].Count >= MaximumPoolSize)
				Destroy(pooledObject.MonoBehaviour.gameObject);
			else
				pool[assetReference].Enqueue(pooledObject);
		}

		/// <summary>
		/// Destroys ALL objects managed by this pool (both active and inactive),
		/// Cancels all operations, and releases all loaded Addressable assets.
		/// </summary>
		/// <param name="cancelPrewarmOperations">If this is set to true, the active prewarm operations will be cancelled</param>
		/// <param name="cancelInstantiateOperations">If set to true, all active instantiation operations will be cancelled</param>
		public void ClearObjectPools(bool cancelPrewarmOperations = false, bool cancelInstantiateOperations = false)
		{
			isClearing = true;

			if (cancelInstantiateOperations)
				CancelInstantiateOperation();

			if (cancelPrewarmOperations)
				CancelPrewarmOperations();

			// 1. Destroy all INACTIVE objects (in the queues)
			foreach (var queue in pool.Values)
			{
				while (queue.Count > 0)
				{
					var pooledObject = queue.Dequeue();
					if (pooledObject != null && pooledObject.MonoBehaviour != null)
					{
						// This will NOT trigger PooledInstanceCleanup because
						// the object is not in the 'lookUp' dictionary.
						Destroy(pooledObject.MonoBehaviour.gameObject);
					}
				}
			}
			pool.Clear();

			// 2. Destroy all ACTIVE objects (in the lookup)
			// We MUST copy to a new List, because PooledInstanceCleanup.OnDestroy()
			// will modify the 'lookUp' dictionary as we iterate.
			var activeObjects = new List<IPoolableObject>(lookUp.Keys);
			foreach (var pooledObject in lookUp.Keys)
			{
				if (pooledObject != null && pooledObject.MonoBehaviour != null)
				{
					Destroy(pooledObject.MonoBehaviour.gameObject);
				}
			}

			// By now, PooledInstanceCleanup should have cleared the lookup.
			// We clear it just in case of any stragglers.
			lookUp.Clear();

			// 3. Now that all GameObjects are destroyed, release the assets.
			foreach (var entry in loadedAssets.Values)
			{
				Addressables.Release(entry);
			}
			loadedAssets.Clear();
			assetNames.Clear();

			isClearing = false;
		}
	}
}
