using MagmaFlow.Framework.Events;
using MagmaFlow.Framework.Pooling;
using MagmaFlow.Framework.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MagmaFlow.Framework.Core
{	
	/// <summary>
	/// This should replace Unity's MonoBehaviour for an array of extended functionality provided by the MagmaFlow Framework
	/// </summary>
	public class BaseBehaviour : MonoBehaviour
	{
		protected MagmaFramework_Core MagmaFramework_Core => MagmaFramework_Core.Instance;
		protected MagmaFramework_PooledObjectsManager MagmaFramework_PooledObjectsManager => MagmaFramework_PooledObjectsManager.Instance;
		protected MagmaFramework_MusicManager MagmaFramework_MusicManager => MagmaFramework_MusicManager.Instance;

		/// <summary>
		/// Finds the closest points (to the sourcePoint) on all colliders that are within the overlap sphere of this sourcePoint.
		/// <para>! WARNING ! Make sure to exclude this object from the layermask used, when using passThrough = false</para>
		/// </summary>
		/// <param name="results">A pre-allocated array to store overlap results (prevents garbage).</param>
		/// <param name="sourcePoint">The center of the overlap sphere.</param>
		/// <param name="radius">The radius of the overlap sphere.</param>
		/// <param name="collisionLayerMask">The layers to check against.</param>
		/// <param name="ignoreTriggers">How to handle trigger colliders.</param>
		/// <param name="passThrough">Points on colliders that are considered 'backfaces will be ignored'.</param>
		/// <returns>Returns a list of approximate contact points on those colliders.</returns>
		protected List<Vector3> GetOverlapContactPoints
		(
			ref Collider[] results, 
			Vector3 sourcePoint, 
			float radius, 
			LayerMask? collisionLayerMask = null, 
			QueryTriggerInteraction ignoreTriggers = QueryTriggerInteraction.Ignore, 
			bool passThrough = true
		)
		{
			if (TryGetComponent<Collider>(out var thisCollider))
			{
				thisCollider.enabled = false;
			}

			LayerMask actualMask = collisionLayerMask ?? Physics.AllLayers;
			int noOfOverlappingColliders = Physics.OverlapSphereNonAlloc(sourcePoint, radius, results, actualMask, ignoreTriggers);

			if (noOfOverlappingColliders >= results.Length)
			{
#if UNITY_EDITOR
				Debug.LogWarning($"{gameObject.name} ::: Found {noOfOverlappingColliders} overlapping colliders, " +
								 $"which matches or exceeds the 'results' array size of {results.Length}. " +
								 $"Some colliders may have been missed. Increase the Collider[] results size OR decrease the range, when calling GetOverlapContactPoints().");
#endif
			}

			List<Vector3> contactPointsFound = new List<Vector3>();
			for (int i = 0; i < noOfOverlappingColliders; i++)
			{
				var pointToAdd = results[i].ClosestPoint(sourcePoint);
				if (sourcePoint != pointToAdd)
				{
					if (passThrough) 
					{
						contactPointsFound.Add(pointToAdd);
					}
					else
					{
						if(!Physics.Linecast(pointToAdd, sourcePoint, actualMask))
						{
							contactPointsFound.Add(pointToAdd);
						}
					}
				}
			}

			if(thisCollider != null) thisCollider.enabled = true;
			return contactPointsFound;
		}

		#region Events
		/// <summary>
		/// Fired when MagmaFramework_Core.PauseGame() is called.
		/// </summary>
		/// <param name="eventData"></param>
		protected virtual void OnGamePaused(GamePausedEvent eventData) { }
		/// <summary>
		/// Fired when SceneManager.onSceneLoaded is fired.
		/// </summary>
		/// <param name="eventData"></param>
		protected virtual void OnSceneLoaded(SceneLoadedEvent eventData) { }
		/// <summary>
		/// Fired when SceneManager.onSceneUnloaded is fired.
		/// </summary>
		/// <param name="eventData"></param>
		protected virtual void OnSceneUnloaded(SceneUnloadedEvent eventData) { }
		private void OnDestroy()
		{
			MagmaFramework_EventBus.Unsubscribe<GamePausedEvent>(OnGamePaused);
			MagmaFramework_EventBus.Unsubscribe<SceneLoadedEvent>(OnSceneLoaded);
			MagmaFramework_EventBus.Unsubscribe<SceneUnloadedEvent>(OnSceneUnloaded);
		}
		private void OnEnable()
		{
			MagmaFramework_EventBus.Subscribe<GamePausedEvent>(OnGamePaused);
			MagmaFramework_EventBus.Subscribe<SceneLoadedEvent>(OnSceneLoaded);
			MagmaFramework_EventBus.Subscribe<SceneUnloadedEvent>(OnSceneUnloaded);
		}
		private void OnDisable()
		{
			MagmaFramework_EventBus.Unsubscribe<GamePausedEvent>(OnGamePaused);
			MagmaFramework_EventBus.Unsubscribe<SceneLoadedEvent>(OnSceneLoaded);
			MagmaFramework_EventBus.Unsubscribe<SceneUnloadedEvent>(OnSceneUnloaded);
		}
		#endregion Events

		#region Essentials

		/// <summary>
		/// Returns the instance of the given type in the child with the provided name of the provided parent
		/// <para>The search excludes the parent!</para>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="origin"></param>
		/// <param name="childName"></param>
		/// <returns></returns>
		protected T GetSubcomponent<T>(Transform origin, string childName) where T : Component
		{
			foreach (Transform child in origin)
			{
				if (child.name == childName && child.TryGetComponent(out T comp))
					return comp;

				var found = GetSubcomponent<T>(child, childName);
				if (found != null) return found;
			}
			return default;
		}

		/// <summary>
		/// Invokes an action after with a delay.
		/// </summary>
		/// <param name="action"></param>
		/// <param name="delay"></param>
		/// <returns>Returns the coroutine that handles the invoking.</returns>
		protected Coroutine Invoke(Action action, float delay = 0)
		{
			return StartCoroutine(InvokeInternal(action, delay));
		}

		private IEnumerator InvokeInternal(Action action, float delay)
		{
			yield return new WaitForSeconds(delay);
			action?.Invoke();
		}

		#endregion Essentials

		#region Custom GameObject Creation

		/// <summary>
		/// Creates an object with the given name and required component, and sets its parent.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name"></param>
		/// <param name="parent"></param>
		/// <returns></returns>
		protected T CreateWithComponent<T>(string name, Transform parent = null) where T : Component
		{
			GameObject newObject = new GameObject(name);
			if (parent != null)
			{
				newObject.transform.SetParent(parent);
			}
			return newObject.AddComponent<T>();
		}

		/// <summary>
		/// Instantiates an object using an asset refference.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="assetReference"></param>
		/// <param name="position"></param>
		/// <param name="rotation"></param>
		/// <param name="parent"></param>
		/// <param name="useWorldSpace"></param>
		/// <returns></returns>
		protected async Task<T> InstantiateAddressable<T>
		(
			AssetReference assetReference,
			Vector3 position,
			Quaternion rotation,
			Transform parent = null,
			bool useWorldSpace = true
		) where T : UnityEngine.Object
		{
			if (assetReference == null || assetReference.RuntimeKeyIsValid() == false)
			{
#if UNITY_EDITOR
				Debug.LogError("Instantiate addressable called with a null or invalid AssetReference.");
#endif
				return null;
			}

			try
			{
				var instantiatedObject = await Addressables.InstantiateAsync(assetReference, parent).Task;
				if (instantiatedObject == null)
				{
#if UNITY_EDITOR
					// This can happen if the operation is cancelled or fails
					Debug.LogError($"Failed to instantiate Addressable: {assetReference.editorAsset.name}");
#endif
					return null;
				}

				instantiatedObject.AddComponent<InstantiatedAddressableCleanup>();

				if (!useWorldSpace && parent != null)
				{
					instantiatedObject.transform.SetLocalPositionAndRotation(position, rotation);
				}
				else
				{
					instantiatedObject.transform.SetPositionAndRotation(position, rotation);
				}

				// User asked for GameObject
				if (typeof(T) == typeof(GameObject))
				{
					return instantiatedObject as T;
				}

				//User asked for a Component
				if (typeof(Component).IsAssignableFrom(typeof(T)) &&
					instantiatedObject.TryGetComponent(typeof(T), out var component))
				{
					return component as T;
				}
				else
				{
#if UNITY_EDITOR
					// No matching component found
					Debug.LogError($"Prefab '{instantiatedObject.name}' does not contain component {typeof(T)}. Performing cleanup");
#endif	
					Destroy(instantiatedObject);
					return null;
				}
			}
			catch (Exception e)
			{
				// Handle exceptions during the async operation
				Debug.LogException(e);
				return null;
			}
		}

		/// <summary>
		/// Instantiates an object using an asset refference.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="assetReference"></param>
		/// <param name="parent"></param>
		/// <param name="useWorldSpace"></param>
		/// <returns></returns>
		protected async Task<T> InstantiateAddressable<T>
		(
			AssetReference assetReference,
			Transform parent = null,
			bool useWorldSpace = true
		) where T : UnityEngine.Object
		{
			return await InstantiateAddressable<T>(assetReference, new Vector3(0, 0, 0), Quaternion.identity, parent, useWorldSpace);
		}

		#endregion Custom GameObject Creation
	}
}
