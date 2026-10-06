using MagmaFlow.Framework.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace MagmaFlow.Framework.Pooling
{
	public interface IPoolableObject
	{
		/// <summary>
		/// Implement this as IPoolableObject.IsAvailable so that the property becomes invisible
		/// </summary>
		bool IsAvailable { get; set; }
		/// <summary>
		/// Access to the MonoBehaviour attached to this object
		/// </summary>
		public MonoBehaviour MonoBehaviour => this as MonoBehaviour;
		/// <summary>
		/// Called every time the object is taken from the pool, after it's positioned, parented and activated.
		/// <para>Awake / OnEnable / Start run on the first activation, so they run at the spawn pose too.</para>
		/// </summary>
		public void OnInitialize();
		/// <summary>
		///	Called when an object that was handed out by InstantiatePooledObject() is released back into the pool.
		/// <para>Called exactly once per OnInitialize(). Never called for prewarmed objects that were never spawned.</para>
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
				if (!assetNames.TryGetValue(key, out var assetName))
				{
					assetName = "UnknownEntry";
				}
				MagmaUtils.Log($"{LOG_PREFIX}{assetName} -> {count} -- In Pool ||| {activeCount} -- Active");
			}
		}
#endif

		/// <summary>
		/// Prepended to every log from this manager. Same style as MagmaUtils.LogFramework().
		/// </summary>
		private const string LOG_PREFIX = "<b><color=#FF5733>[PoolManager]</color></b> ";

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
				Destroy(gameObject);
				return false;
			}

			Instance = this;
			DontDestroyOnLoad(gameObject);

			MagmaUtils.Log($"{LOG_PREFIX}\u23E9 {name} service registered.");
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
		/// The prefab's local scale per asset key, restored on every spawn.
		/// </summary>
		private readonly Dictionary<object, Vector3> prefabScales = new();
		/// <summary>
		/// This helps handling possible simultaneous prewarm operations
		/// </summary>
		private readonly HashSet<object> currentlyPrewarming = new();
		/// <summary>
		/// This is so that our scene inspector doesn't get filled with pooled objects
		/// </summary>
		private Transform genericPooledObjectsParent;
		/// <summary>
		/// Inactive container that new instances are created under, so their Awake / OnEnable don't run until they're spawned.
		/// </summary>
		private Transform inactiveStagingParent;
		/// <summary>
		/// Reused by ReleaseAllObjects() so it can safely iterate while the lookup changes.
		/// </summary>
		private readonly List<IPoolableObject> releaseBuffer = new();
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
		/// Returns a readable name for an AssetReference that is safe to call in both the Editor and builds.
		/// <para>Editor: the editor asset's name.</para>
		/// <para>Builds: the cached prefab name if the asset has been loaded by this manager,
		/// otherwise the loaded asset's name, otherwise the asset GUID.</para>
		/// </summary>
		/// <param name="assetReference"></param>
		/// <returns></returns>
		public string GetAssetName(AssetReference assetReference)
		{
			if (assetReference == null)
				return "NULL AssetReference";

#if UNITY_EDITOR
			var editorAsset = assetReference.editorAsset;
			if (editorAsset != null)
				return editorAsset.name;
#endif

			var key = assetReference.RuntimeKey;
			if (key != null)
			{
				// Name cached when the prefab was first loaded through this manager
				if (assetNames.TryGetValue(key, out var cachedName) && !string.IsNullOrEmpty(cachedName))
					return cachedName;

				// Prefab loaded through this manager, but no name cached yet
				if (loadedAssets.TryGetValue(key, out var handle)
					&& handle.IsValid()
					&& handle.Status == AsyncOperationStatus.Succeeded
					&& handle.Result != null)
					return handle.Result.name;
			}

			// Asset loaded directly through the AssetReference elsewhere
			if (assetReference.Asset != null)
				return assetReference.Asset.name;

			return string.IsNullOrEmpty(assetReference.AssetGUID)
				? "UnknownAsset"
				: $"Asset[{assetReference.AssetGUID}]";
		}

		/// <summary>
		/// Due to memory constraints you can set a maximum pool size, or leave it -1
		/// </summary>
		/// <param name="maximumPoolSize"></param>
		private void Initialize(int maximumPoolSize = -1)
		{
			MaximumPoolSize = maximumPoolSize >= 0 ? maximumPoolSize : int.MaxValue;
			var poolRoot = new GameObject("Pooled Objects Container");
			poolRoot.transform.SetParent(transform);
			poolRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
			genericPooledObjectsParent = poolRoot.transform;

			var stagingRoot = new GameObject("Pooled Objects Staging (inactive)");
			stagingRoot.SetActive(false);
			stagingRoot.transform.SetParent(transform);
			inactiveStagingParent = stagingRoot.transform;

			SceneManager.sceneUnloaded += OnSceneUnload;
		}

		private void OnDestroy()
		{
			SceneManager.sceneUnloaded -= OnSceneUnload;
			CancelAllPrewarmOperations();
			CancelAllInstantiateOperation();
			ClearObjectPools(true);
		}

		private void OnSceneUnload(Scene eventData)
		{
			CancelAllPrewarmOperations();
			CancelAllInstantiateOperation();

			if (!keepPoolsOnSceneChange)
				ClearObjectPools(true);

			if (!keepInstancesAliveOnSceneChange)
				ReleaseAllObjects();
		}

		/// <summary>
		/// Internal logic for releasing the pooled instance
		/// </summary>
		/// <param name="pooledObject"></param>
		private void ReleaseInstanceInternal(IPoolableObject pooledObject)
		{
			// Already in the pool: releasing again would call OnRelease() twice and could destroy a queued instance.
			if (pooledObject == null || pooledObject.IsAvailable) return;

			if (pooledObject.MonoBehaviour == null)
			{
				lookUp.Remove(pooledObject);
				return;
			}

			pooledObject.OnRelease();
			ReturnToPool(pooledObject);
		}

		/// <summary>
		/// Deactivates the instance and queues it, or destroys it if the pool is full.
		/// <para>Does NOT call OnRelease(); used directly for instances that were never handed out (prewarm, aborted spawns).</para>
		/// </summary>
		/// <param name="pooledObject"></param>
		private void ReturnToPool(IPoolableObject pooledObject)
		{
			if (!lookUp.TryGetValue(pooledObject, out var assetKey))
			{
				MagmaUtils.LogError($"{LOG_PREFIX}The object {pooledObject.MonoBehaviour.name} is not managed by the pool.");
				return;
			}

			var _monoBehaviour = pooledObject.MonoBehaviour;
			_monoBehaviour.gameObject.SetActive(false);
			// The pose is reset on the next spawn, so there's no need to preserve it
			_monoBehaviour.transform.SetParent(genericPooledObjectsParent, false);

			var queue = GetOrCreatePool(assetKey);
			if (queue.Count >= MaximumPoolSize)
			{
				// Removed explicitly, since OnDestroy (and so PooledInstanceCleanup) doesn't run on never-activated objects.
				lookUp.Remove(pooledObject);
				Destroy(_monoBehaviour.gameObject);
				return;
			}

			pooledObject.IsAvailable = true;
			queue.Enqueue(pooledObject);
		}

		private Queue<IPoolableObject> GetOrCreatePool(object assetKey)
		{
			if (!pool.TryGetValue(assetKey, out var queue))
			{
				queue = new Queue<IPoolableObject>();
				pool[assetKey] = queue;
			}
			return queue;
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
				MagmaUtils.LogError($"{LOG_PREFIX}CreateNewInstance() called with a NULL asset reference.");
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
					MagmaUtils.LogWarning($"{LOG_PREFIX}Instantiation of {GetAssetName(assetReference)} was cancelled.");
					return null;
				}

				if (loadedAsset == null)
				{
					MagmaUtils.LogWarning($"{LOG_PREFIX}There was an issue loading {GetAssetName(assetReference)} asset.");
					return null;
				}

				// Cache the prefab name so GetAssetName() returns something readable in builds
				if (!assetNames.ContainsKey(assetKey))
				{
					assetNames[assetKey] = loadedAsset.name;
				}
				prefabScales[assetKey] = loadedAsset.transform.localScale;

				// Created under an inactive parent, so Awake / OnEnable wait until the object is actually spawned
				var instance = Instantiate(loadedAsset, inactiveStagingParent);

				// Verify it implements IPoolableObject
				if (!instance.TryGetComponent<IPoolableObject>(out var pooledObject))
				{
					MagmaUtils.LogError(
						$"{LOG_PREFIX}The prefab '{instance.name}' does not implement IPoolableObject. Cleaning up."
					);
					Destroy(instance);

					return null;
				}

				//We add a cleanup component that handles removing the entry OnDestroy() from the lookup table
				instance.AddComponent<PooledInstanceCleanup>().Initialize(this, pooledObject);

				// Register in the lookup table
				lookUp[pooledObject] = assetKey;

				return pooledObject;
			}
			catch (Exception e)
			{

				MagmaUtils.LogException(e);

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
				// The handle may have been released meanwhile (failed load, or ClearObjectPools)
				return handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null;
			}

			// 2. If not, load it for the first time
			var loadHandle = Addressables.LoadAssetAsync<GameObject>(assetReference);

			// 3. Store the *handle* in the dictionary immediately
			loadedAssets[key] = loadHandle;

			// 4. Await the new handle and return the prefab
			await loadHandle.Task;

			// Released by ClearObjectPools() while loading
			if (!loadHandle.IsValid()) return null;

			if (loadHandle.Status == AsyncOperationStatus.Succeeded) return loadHandle.Result;

			// Don't cache failures, so the next request retries the load
			MagmaUtils.LogError($"{LOG_PREFIX}Failed to load {GetAssetName(assetReference)}. {loadHandle.OperationException}");
			if (loadedAssets.TryGetValue(key, out var current) && current.Equals(loadHandle))
			{
				loadedAssets.Remove(key);
			}
			Addressables.Release(loadHandle);
			return null;
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
			if (!prewarmTokens.TryGetValue(forAsset.RuntimeKey, out var cancellationTokenSource))
			{
				MagmaUtils.LogWarning($"{LOG_PREFIX}No token present for prewarming {GetAssetName(forAsset)}");
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
		/// <para>Prewarmed instances stay inactive: their Awake / OnEnable, OnInitialize() and OnRelease() don't run until they're spawned.</para>
		/// <para>The count is capped by MaximumPoolSize.</para>
		/// </summary>
		/// <param name="assetReference"></param>
		/// <param name="count">If this is higher than the current pool count, the pool will increase in size to match the new count. If it is smaller, then nothing happens.</param>
		public async Task PrewarmPool(AssetReference assetReference, int count)
		{
			var key = assetReference.RuntimeKey;

			var currentPool = GetOrCreatePool(key);
			count = Math.Min(count, MaximumPoolSize);
			if (currentPool.Count >= count)
			{
				MagmaUtils.Log($"{LOG_PREFIX}Pre-warm pool {GetAssetName(assetReference)} request ignored, because the pool is already this size or larger!");
				return;
			}

			if (!currentlyPrewarming.Add(key))
				return;

			int difference = count - currentPool.Count;

			CancellationTokenSource cts = new CancellationTokenSource();
			prewarmTokens[key] = cts;

			try
			{
				MagmaUtils.Log($"{LOG_PREFIX}Prewarming {difference} {GetAssetName(assetReference)}...");
				for (int i = 0; i < difference; i++)
				{
					if (cts.Token.IsCancellationRequested)
						break;

					var instance = await CreateNewInstance(assetReference, cts.Token);
					if (instance == null)
						break;

					// Never handed out, so it skips OnRelease()
					ReturnToPool(instance);
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
		/// <para>Scale behaves like Instantiate(prefab, parent): the prefab's scale is restored on every spawn and the parent's scale is inherited.</para>
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
				MagmaUtils.LogError($"{LOG_PREFIX}The asset reference that you want to instantiate is null.");
				return null;
			}

			var queue = GetOrCreatePool(assetReference.RuntimeKey);

			IPoolableObject pooledObject = null;
			if (queue.Count > 0)
			{
				pooledObject = queue.Dequeue();
				pooledObject.IsAvailable = false;
			}

			if (pooledObject == null || pooledObject.MonoBehaviour == null)
			{
				var cts = new CancellationTokenSource();

				try
				{
					activeInstantiations.Add(cts);
					pooledObject = await CreateNewInstance(assetReference, cts.Token);
				}
				catch (Exception e)
				{

					MagmaUtils.LogException(e);

				}
				finally
				{
					activeInstantiations.Remove(cts);
					cts.Dispose();
				}
			}

			if (pooledObject == null)
				return null;

			// A parent was given but destroyed while the asset was loading ('??' would skip Unity's destroyed check)
			if (parent is not null && parent == null)
			{
				MagmaUtils.LogWarning($"{LOG_PREFIX}The parent for {GetAssetName(assetReference)} was destroyed before it could spawn. Returning it to the pool.");
				ReturnToPool(pooledObject);
				return null;
			}

			var objTransform = pooledObject.MonoBehaviour.transform;

			if (!objTransform.TryGetComponent<T>(out var component))
			{
				MagmaUtils.LogError($"{LOG_PREFIX}{GetAssetName(assetReference)} does not have a component of type {typeof(T).Name} assigned.");
				ReturnToPool(pooledObject);
				return null;
			}

			// Like Instantiate(prefab, parent): start from the prefab's scale and inherit the parent's,
			// so scale changes made during a previous use don't carry over.
			if (prefabScales.TryGetValue(assetReference.RuntimeKey, out var prefabScale))
			{
				objTransform.localScale = prefabScale;
			}
			objTransform.SetParent(parent != null ? parent : genericPooledObjectsParent, false);
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

			return component;
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
			// Iterate a copy: releasing can remove entries from the lookup (full pool, destroyed instances)
			releaseBuffer.Clear();
			releaseBuffer.AddRange(lookUp.Keys);
			foreach (var instance in releaseBuffer)
			{
				ReleaseInstanceInternal(instance);
			}
			releaseBuffer.Clear();
		}

		/// <summary>
		/// Releases the object back into the pool.
		/// Disables the game object.
		/// </summary>
		/// <param name="pooledObject"></param>
		public void ReleaseObject(IPoolableObject pooledObject)
		{
			if (pooledObject is null)
			{
				MagmaUtils.LogError($"{LOG_PREFIX}ReleaseObject() called with a NULL object.");
				return;
			}

			// Destroyed elsewhere (e.g. with its parent's scene): nothing to return, just drop any stale entry
			if (pooledObject.MonoBehaviour == null)
			{
				lookUp.Remove(pooledObject);
				MagmaUtils.LogWarning($"{LOG_PREFIX}ReleaseObject() called on a destroyed object. Ignoring the release.");
				return;
			}

			if (!lookUp.ContainsKey(pooledObject))
			{
				MagmaUtils.LogError($"{LOG_PREFIX}The object {pooledObject.MonoBehaviour.name}, that you want to release is not pooled.");
				return;
			}

			if (pooledObject.IsAvailable)
			{
				MagmaUtils.LogWarning($"{LOG_PREFIX}The object {pooledObject.MonoBehaviour.name} is already in the pool. Ignoring the release.");
				return;
			}

			ReleaseInstanceInternal(pooledObject);
		}

		/// <summary>
		/// Destroys ALL objects managed by this pool (both active and inactive).
		/// Can release all loaded Addressable assets.
		/// <para>Also cancels pending prewarm / instantiate operations; their awaiting callers get null.</para>
		/// </summary>
		/// <param name="releaseAllLoadedAssets">If TRUE: Releases all loaded Addressable assets</param>
		public void ClearObjectPools(bool releaseAllLoadedAssets = false)
		{
			// Otherwise an in-flight load would register an instance after its pool is gone
			CancelAllPrewarmOperations();
			CancelAllInstantiateOperation();

			// Blocks RemoveLookup(), since we clear the lookup ourselves below.
			// (Destroy is deferred to the end of the frame anyway, so PooledInstanceCleanup's OnDestroy runs after this method.)
			isClearing = true;

			// Destroy all INACTIVE objects (in the queues)
			foreach (var queue in pool.Values)
			{
				while (queue.Count > 0)
				{
					var pooledObject = queue.Dequeue();
					if (pooledObject != null && pooledObject.MonoBehaviour != null)
					{
						Destroy(pooledObject.MonoBehaviour.gameObject);
					}
				}
			}
			pool.Clear();

			// The lookup holds every instance, so this covers the ACTIVE ones.
			// Queued objects are hit a second time, which is harmless (Destroy on an already destroyed object is ignored).
			foreach (var pooledObject in lookUp.Keys)
			{
				if (pooledObject != null && pooledObject.MonoBehaviour != null)
				{
					Destroy(pooledObject.MonoBehaviour.gameObject);
				}
			}

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
				prefabScales.Clear();
			}

			isClearing = false;
		}
	}
}