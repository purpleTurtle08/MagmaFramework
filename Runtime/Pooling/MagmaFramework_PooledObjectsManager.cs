using MagmaFlow.Framework.Events;
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
		/// <summary>
		/// Access to the MonoBehaviour attached to this object
		/// </summary>
		public MonoBehaviour MonoBehaviour => this as MonoBehaviour;
		/// <summary>
		/// Called after a pooled object is instantiated.
		/// </summary>
		public void OnInitialize();
		/// <summary>
		///	Called when a pooled object is released back into the pool.
		/// The object is also set inactive by the pooled manager.
		/// </summary>
		public void OnRelease();
	}

	/// <summary>
	/// A singleton managing pooled object creation.
	/// <para>Can log the pool state via a context menu 'Log Pool State'</para>
	/// </summary>
	[DefaultExecutionOrder(-100)]
	public sealed class MagmaFramework_PooledObjectsManager : MonoBehaviour
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
		[SerializeField][Tooltip("Keeps all the active pooled instances alive (if they are alive) upon scene changing.\nRECOMMENDED VALUE:: FALSE")] private bool keepInstancesAliveOnSceneChange = false;
		[SerializeField][Tooltip("Clears / Keeps all the pooled objects.\nIf you never use the same assets in different scenes, you might want to consider disabling this.")] private bool keepPoolsOnSceneChange = true;

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
			if (Instance != null && Instance != this)
			{
				//Debug.LogWarning($"Removed {name}, as it is a duplicate. Ensure you only have 1 {name} per scene.");
				Destroy(gameObject);
				return false;
			}

			Instance = this;
			DontDestroyOnLoad(gameObject);
#if UNITY_EDITOR
			Debug.Log($"\u23E9 {name} service registered.");
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

		/// <summary>
		/// Due to memory constraints you can set a maximum pool size, or leave it -1
		/// </summary>
		/// <param name="maximumPoolSize"></param>
		private void Initialize(int maximumPoolSize = -1)
		{
			MaximumPoolSize = maximumPoolSize >= 0 ? maximumPoolSize : 9999999;
			var poolRoot = new GameObject("Pooled Objects Container");
			poolRoot.transform.SetParent(transform);
			genericPooledObjectsParent = poolRoot.transform;
			MagmaFramework_EventBus.Subscribe<SceneUnloadedEvent>(OnSceneUnload);
		}

		private void OnDestroy()
		{
			MagmaFramework_EventBus.Unsubscribe<SceneUnloadedEvent>(OnSceneUnload);
			CancelAllPrewarmOperations();
			CancelAllInstantiateOperation();
			ClearObjectPools(true);
		}

		private void OnSceneUnload(SceneUnloadedEvent eventData)
		{
			CancelAllPrewarmOperations();
			CancelAllInstantiateOperation();

			if(!keepPoolsOnSceneChange)
				ClearObjectPools(true);

			if(!keepInstancesAliveOnSceneChange)
				ReleaseAllObjects();
		}

		/// <summary>
		/// Internal logic for releasing the pooled instance
		/// </summary>
		/// <param name="pooledObject"></param>
		private void ReleaseInstanceInternal(IPoolableObject pooledObject)
		{
			if (pooledObject == null) return;
			var _monoBehaviour = pooledObject.MonoBehaviour;

			pooledObject.OnRelease();
			_monoBehaviour.gameObject.SetActive(false);
			_monoBehaviour.transform.SetParent(genericPooledObjectsParent);
			var assetReference = lookUp[pooledObject];
			if (pool[assetReference].Count >= MaximumPoolSize)
				Destroy(_monoBehaviour.gameObject);
			else
				pool[assetReference].Enqueue(pooledObject);
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
#if UNITY_EDITOR
					Debug.LogError(
						$"The prefab '{instance.name}' does not implement IPoolableObject. Cleaning up."
					);
#endif
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
		/// Cancelling is handled in poolingObjectsManager.OnDestroy() as well.
		/// </summary>
		public void CancelAllPrewarmOperations()
		{
			foreach (var cts in prewarmTokens.Values)
			{
				cts?.Cancel();
			}
		}

		/// <summary>
		/// Cancel a specific prewarm operation.
		/// Cancelling is handled in poolingObjectsManager.OnDestroy() as well.
		/// </summary>
		/// <param name="forAsset">The asset refference used to start the prewarm</param>
		public void CancelPrewarmOperation(AssetReference forAsset)
		{
			if(!prewarmTokens.TryGetValue(forAsset.RuntimeKey, out var cancellationTokenSource))
			{
#if UNITY_EDITOR
				Debug.LogWarning($"No token present for prewarming {forAsset.editorAsset.name}");
#endif
				return;
			}

			cancellationTokenSource?.Cancel();
		}

		/// <summary>
		/// Stops all instantiating operations
		/// </summary>
		public void CancelAllInstantiateOperation()
		{
			foreach (var cts in activeInstantiations)
			{
				cts?.Cancel();
			}
		}

		/// <summary>
		/// Pre-warm the pool to a number of instances.
		/// This does not 'add' or 'remove' items to/from the pool, it simply populates a pool to the desired size.
		/// <para>Example; calling Prewarm(assetRef, 100) 2 times will not result in having a pool of 200 objects. The second call is redundant.</para>
		/// </summary>
		/// <param name="assetReference"></param>
		/// <param name="count">If this is higher than the current pool count, the pool will increase in size to match the new count. If it is smaller, then nothing happens.</param>
		public async Task PrewarmPool(AssetReference assetReference, int count)
		{
			var key = assetReference.RuntimeKey;
			if (!currentlyPrewarming.Add(key))
				return;
			if (!pool.TryGetValue(key, out var currentPool))
			{
				currentPool = new Queue<IPoolableObject>();
				pool[key] = currentPool;
			}
			if (currentPool.Count >= count)
			{
#if UNITY_EDITOR
				Debug.Log($"Pre-warm pool {assetReference.editorAsset.name} request ignored, because the pool is already this size or larger!");
#endif
				return;
			}
			int difference = count - currentPool.Count;

			CancellationTokenSource cts = new CancellationTokenSource();
			prewarmTokens[key] = cts;

			try
			{
#if UNITY_EDITOR
				Debug.Log($"Prewarming {difference} {assetReference.editorAsset.name}...");
#endif
				for (int i = 0; i < difference; i++)
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
		/// Can only be called from the main thread.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="assetReference"></param>
		/// <param name="parent"></param>
		/// <param name="position"></param>
		/// <param name="rotation"></param>
		/// <param name="useWorldSpace"></param>
		/// <returns></returns>
		public async Task<T> InstantiatePooledObject<T>
		(
			AssetReference assetReference,
			Vector3 position,
			Quaternion rotation,
			Transform parent = null,
			bool useWorldSpace = true
		) where T : Component
		{
			if (assetReference == null)
			{
#if UNITY_EDITOR
				Debug.LogError("The asset reference that you want to instantiate is null.");
#endif
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

			if (!useWorldSpace && parent != null)
			{	
				objTransform.SetLocalPositionAndRotation(position, rotation);
			}
			else
			{	
				objTransform.SetPositionAndRotation(position, rotation);
			}

			objTransform.gameObject.SetActive(true);
			pooledObject.OnInitialize();

			return objTransform.GetComponent<T>();
		}

		/// <summary>
		/// Instantiates or retrieves a pooled object and returns the component of type T.
		/// Can only be called from the main thread.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="assetReference"></param>
		/// <param name="parent"></param>
		/// <param name="useWorldSpace"></param>
		/// <returns></returns>
		public async Task<T> InstantiatePooledObject<T>
		(
			AssetReference assetReference,
			Transform parent = null,
			bool useWorldSpace = true
		) where T : Component
		{
			return await InstantiatePooledObject<T>(assetReference, new Vector3(0, 0, 0), Quaternion.identity, parent, useWorldSpace);
		}

		/// <summary>
		/// Releases the active pooled object instances, returning them to the pool and disabling the gameObject.
		/// </summary>
		public void ReleaseAllObjects()
		{
			foreach(var activeInstance in lookUp.Keys)
			{	
				ReleaseInstanceInternal(activeInstance);
			}
		}

		/// <summary>
		/// Releases the object back into the pool.
		/// Disables the game object.
		/// </summary>
		/// <param name="pooledObject"></param>
		public void ReleaseObject(IPoolableObject pooledObject)
		{
			if(!lookUp.ContainsKey(pooledObject))
			{
				Debug.LogError($"The object {pooledObject.MonoBehaviour.name}, that you want to release is not pooled.");
				return;
			}

			ReleaseInstanceInternal(pooledObject);
		}

		/// <summary>
		/// Destroys ALL objects managed by this pool (both active and inactive).
		/// Can release all loaded Addressable assets.
		/// </summary>
		/// <param name="releaseAllLoadedAssets">If TRUE: Releases all loaded Addressable assets</param>
		public void ClearObjectPools(bool releaseAllLoadedAssets = false)
		{
			isClearing = true;

			// Destroy all INACTIVE objects (in the queues)
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

			if (releaseAllLoadedAssets)
			{
				// 3. Now that all GameObjects are destroyed, release the assets.
				foreach (var entry in loadedAssets.Values)
				{
					Addressables.Release(entry);
				}
				loadedAssets.Clear();
				assetNames.Clear();
			}

			isClearing = false;
		}
	}
}
