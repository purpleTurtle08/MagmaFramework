using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MagmaFlow.Framework.Core
{
	public interface IPoolableObject
	{
		public bool IsActive { get; }
		public BaseBehaviour Behaviour => this as BaseBehaviour;

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
		/// <summary>
		/// The pools of inactive objects.
		/// </summary>
		private readonly Dictionary<string, Queue<IPoolableObject>> pool = new();
		/// <summary>
		/// The massive collection of all the instantiated objects.
		/// </summary>
		private readonly Dictionary<IPoolableObject, string> lookUp = new();
		/// <summary>
		/// Stored loaded prefabs, so we don't load them more than once.
		/// </summary>
		private readonly Dictionary<string, GameObject> loadedPrefabs = new();
		/// <summary>
		/// This is so that our scene inspector doesn't get filled with pooled objects
		/// </summary>
		private Transform genericPooledObjectsParent;

		private void Awake()
		{
			genericPooledObjectsParent = new GameObject("Pooled Objects Container").transform;
		}

		/// <summary>
		/// Asynchronously loads a prefab from Addressables if it's not already loaded.
		/// </summary>
		private async Task<GameObject> GetPrefab(string address)
		{
			if (loadedPrefabs.TryGetValue(address, out var prefab))
			{
				return prefab;
			}

			// Asynchronously load the asset by its address.
			AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(address);
			await handle.Task; // Wait for the loading to complete.

			if (handle.Status == AsyncOperationStatus.Succeeded)
			{
				loadedPrefabs[address] = handle.Result;
				return handle.Result;
			}
			else
			{
				Debug.LogError($"Prefab not found at address: {address}, make sure that it is marked as adressable");
				Addressables.Release(handle); // Clean up the failed handle.
				return null;
			}
		}

		/// <summary>
		/// Internal helper to create and register a new pooled instance.
		/// </summary>
		private IPoolableObject CreateNewInstance(GameObject prefab, string prefabAddress)
		{
			if (prefab == null) return null;

			var instance = Instantiate(prefab, genericPooledObjectsParent);
			if (instance.TryGetComponent<IPoolableObject>(out var pooledObject))
			{
				lookUp[pooledObject] = prefabAddress;
				return pooledObject;
			}
			else
			{
				Debug.LogError($"Prefab {prefab.name} does not implement IPoolableObject\nObject cleaned up");
				Destroy(instance);
				return null;
			}
		}

		/// <summary>
		/// Prewarm the pool with a number of instances.
		/// </summary>
		public async void Prewarm(string prefabAddress, int count)
		{
			if (!pool.ContainsKey(prefabAddress))
				pool[prefabAddress] = new Queue<IPoolableObject>();

			var objectToPrewarm = await GetPrefab(prefabAddress);

			if (objectToPrewarm != null)
			{
				Debug.Log($"Prewarming {count} {objectToPrewarm.name} from {prefabAddress}");
				for (int i = 0; i < count; i++)
				{
					var instance = CreateNewInstance(objectToPrewarm, prefabAddress);
					ReleaseObject(instance);
				}
			}
		}

		/// <summary>
		/// Instantiates or retrieves a pooled object and returns the component of type T.
		/// </summary>
		public async Task<T> InstantiatePooledObject<T>(
			string address,
			Transform parent,
			Vector3 position,
			Quaternion rotation,
			bool useWorldSpace) where T : Component
		{
			if (!pool.TryGetValue(address, out var queue))
			{
				queue = new Queue<IPoolableObject>();
				pool[address] = queue;
			}

			IPoolableObject pooledObject = null;
			if (queue.Count > 0)
			{
				pooledObject = queue.Dequeue();
			}

			if (pooledObject == null || pooledObject.Behaviour == null)
			{
				// Await the prefab loading before creating a new instance.
				GameObject prefab = await GetPrefab(address);
				pooledObject = CreateNewInstance(prefab, address);
			}

			if (pooledObject == null) return null;

			var objTransform = pooledObject.Behaviour.transform;
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

			pooledObject.Behaviour.gameObject.SetActive(true);
			pooledObject.OnInitialize();

			return pooledObject.Behaviour.GetComponent<T>();
		}

		/// <summary>
		/// Releases an object back into the pool.
		/// </summary>
		public void ReleaseObject(IPoolableObject pooledObject)
		{
			if (pooledObject == null || !lookUp.ContainsKey(pooledObject))
			{
				Debug.LogError("The object you want to release is not pooled.");
				return;
			}

			pooledObject.OnRelease();
			pooledObject.Behaviour.gameObject.SetActive(false);
			pooledObject.Behaviour.transform.SetParent(genericPooledObjectsParent);

			var prefabPath = lookUp[pooledObject];
			pool[prefabPath].Enqueue(pooledObject);
		}

		/// <summary>
		/// Destroys all pooled objects and clears the pool.
		/// </summary>
		public void ClearObjectPools()
		{
			foreach (var entry in lookUp.Keys)
			{
				if (entry?.Behaviour != null)
					Destroy(entry.Behaviour.gameObject);
			}

			pool.Clear();
			lookUp.Clear();
			Destroy(genericPooledObjectsParent);
		}
	}
}
