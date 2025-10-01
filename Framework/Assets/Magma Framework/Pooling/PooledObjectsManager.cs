using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
		/// The pools of inactive objects
		/// </summary>
		private readonly Dictionary<string, Queue<IPoolableObject>> pool = new();
		/// <summary>
		/// The massive collection of all the instantiated objects
		/// </summary>
		private readonly Dictionary<IPoolableObject, string> lookUp = new();
		/// <summary>
		/// This is so that our scene inspector doesn't get filled with pooled objects
		/// </summary>
		private Transform genericPooledObjectsParent;

		private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
		{
			if (genericPooledObjectsParent == null)
			{
				genericPooledObjectsParent = new GameObject("Pooled Objects Container").transform;
			}
		}

		private void Awake()
		{
			if (genericPooledObjectsParent == null)
			{
				genericPooledObjectsParent = new GameObject("Pooled Objects Container").transform;
			}
			SceneManager.sceneLoaded += OnSceneLoaded;
		}

		private void OnDestroy()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
		}

		/// <summary>
		/// Internal helper to create and register a new pooled instance.
		/// </summary>
		private IPoolableObject CreateNewInstance(string prefabPath)
		{
			GameObject loadedPrefab = Resources.Load<GameObject>(prefabPath);
			if (loadedPrefab == null)
			{
				Debug.LogError($"Prefab not found at path: {prefabPath}");
				return null;
			}

			var instance = Instantiate(loadedPrefab, genericPooledObjectsParent);
			var pooledObject = instance.GetComponent<IPoolableObject>();

			if (pooledObject == null)
			{
				Debug.LogError($"Prefab {prefabPath} does not implement IPoolableObject\nObject cleaned up");
				Destroy(instance);
				return null;
			}

			lookUp[pooledObject] = prefabPath;
			return pooledObject;
		}

		/// <summary>
		/// Prewarm the pool with a number of instances.
		/// </summary>
		public void Prewarm(string prefabPath, int count)
		{
			if (!pool.ContainsKey(prefabPath))
				pool[prefabPath] = new Queue<IPoolableObject>();

			for (int i = 0; i < count; i++)
			{
				var instance = CreateNewInstance(prefabPath);
				ReleaseObject(instance);
			}
		}

		/// <summary>
		/// Instantiates or retrieves a pooled object and returns the component of type T.
		/// </summary>
		public T InstantiatePooledObject<T>(
			string path,
			Transform parent,
			Vector3 position,
			Quaternion rotation,
			bool useWorldSpace) where T : Component
		{
			if (!pool.TryGetValue(path, out var queue))
			{
				queue = new Queue<IPoolableObject>();
				pool[path] = queue;
			}

			IPoolableObject pooledObject = null;

			// Try to reuse from queue
			if (queue.Count > 0)
				pooledObject = queue.Dequeue();

			// Otherwise create new
			if (pooledObject == null || pooledObject.Behaviour == null)
				pooledObject = CreateNewInstance(path);

			// Reparent + position
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
