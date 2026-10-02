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
		private WaitForSeconds _delayedInvokeSlep;

		protected MagmaFramework_Core MagmaFramework_Core => MagmaFramework_Core.Instance;
		protected MagmaFramework_PooledObjectsManager MagmaFramework_PooledObjectsManager => MagmaFramework_PooledObjectsManager.Instance;
		protected MagmaFramework_MusicManager MagmaFramework_MusicManager => MagmaFramework_MusicManager.Instance;

		/// <summary>
		/// Finds the closest points (to the sourcePoint) on all colliders that are within the overlap sphere of this sourcePoint.
		/// </summary>
		/// <param name="numberOfPoints">How many points to return.</param>
		/// <param name="results">A pre-allocated collection to store overlap results.</param>
		/// <param name="sourcePoint">The center of the overlap sphere.</param>
		/// <param name="radius">The radius of the overlap sphere.</param>
		/// <param name="collisionLayerMask">The layers to check against.</param>
		/// <param name="ignoreTriggers">How to handle trigger colliders.</param>
		/// <param name="passThrough">Points on colliders that are considered 'backfaces' will be included.</param>
		/// <returns>Returns a list of approximate contact points on those colliders.</returns>
		protected void GetOverlapContactPoints
		(	
			ref List<Vector3> results, 
			Vector3 sourcePoint, 
			float radius,
			int numberOfPoints = 4,
			LayerMask? collisionLayerMask = null, 
			QueryTriggerInteraction ignoreTriggers = QueryTriggerInteraction.Ignore, 
			bool passThrough = true
		)
		{
			LayerMask actualMask = collisionLayerMask ?? Physics.AllLayers;
			Collider[] overlapResult = new Collider[numberOfPoints];
			int noOfOverlappingColliders = Physics.OverlapSphereNonAlloc(sourcePoint, radius, overlapResult, actualMask, ignoreTriggers);

			if (noOfOverlappingColliders >= overlapResult.Length)
			{
#if UNITY_EDITOR
				Debug.LogWarning($"{gameObject.name} ::: Found {noOfOverlappingColliders} overlapping colliders, " +
								 $"which matches or exceeds the 'results' array size of {overlapResult.Length}. " +
								 $"Some colliders may have been missed. Increase the Collider[] results size OR decrease the range, when calling GetOverlapContactPoints().");
#endif
			}

			if(results == null) results = new List<Vector3>(numberOfPoints);
			for (int i = 0; i < noOfOverlappingColliders; i++)
			{
				if (overlapResult[i].transform.IsChildOf(transform) || overlapResult[i].transform == transform) continue;

				var pointToAdd = overlapResult[i].ClosestPoint(sourcePoint);
				if (sourcePoint != pointToAdd)
				{
					if (passThrough) 
					{
						results.Add(pointToAdd);
					}
					else
					{
						if(!Physics.Linecast(pointToAdd, sourcePoint, actualMask))
						{
							results.Add(pointToAdd);
						}
					}
				}
			}
		}

		#region Events
		/// <summary>
		/// Fired when MagmaFramework_Core.PauseGame() is called.
		/// </summary>
		/// <param name="eventData"></param>
		protected virtual void OnGamePaused(GamePausedEvent eventData) { }
		protected virtual void OnDestroy()
		{
			MagmaFramework_EventBus.Unsubscribe<GamePausedEvent>(OnGamePaused);
		}
		protected virtual void Awake()
		{
			MagmaFramework_EventBus.Subscribe<GamePausedEvent>(OnGamePaused);
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
			for(int i = 0; i < origin.childCount; i++)
			{
				if (origin.GetChild(i).name == childName && origin.GetChild(i).TryGetComponent(out T comp))
					return comp;

				var found = GetSubcomponent<T>(origin.GetChild(i), childName);
				if (found != null) return found;
			}
			return default;
		}

		/// <summary>
		/// Invokes an action after with a delay.
		/// It uses scaled time and callback won't fire if the object dies.
		/// </summary>
		/// <param name="action"></param>
		/// <param name="delay"></param>
		/// <returns>Returns the coroutine that handles the invoking.</returns>
		protected Coroutine InvokeDelayed(Action action, float delay = 0)
		{	
			if(delay <= 0)
			{
				action?.Invoke();
				return null;
			}

			_delayedInvokeSlep = new WaitForSeconds(delay);
			return StartCoroutine(InvokeInternal(action, delay));
		}

		private IEnumerator InvokeInternal(Action action, float delay)
		{
			yield return _delayedInvokeSlep;
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
				var instantiatedObject = await Addressables.InstantiateAsync(assetReference, position, rotation, parent).Task;
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

		/// <summary>
		/// Synchronously instantiates an object using an asset reference.
		/// <para>! WARNING ! This will block the main thread until the asset is loaded and instantiated.</para>
		/// </summary>
		protected T InstantiateAddressableSync<T>
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
				// Start the async operation but immediately force it to complete synchronously
				var handle = Addressables.InstantiateAsync(assetReference, position, rotation, parent);
				var instantiatedObject = handle.WaitForCompletion();

				if (instantiatedObject == null)
				{
#if UNITY_EDITOR
					Debug.LogError($"Failed to synchronously instantiate Addressable: {assetReference.editorAsset.name}");
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

				// User asked for a Component
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
				Debug.LogException(e);
				return null;
			}
		}

		/// <summary>
		/// Synchronously instantiates an object using an asset reference at the origin.
		/// </summary>
		protected T InstantiateAddressableSync<T>
		(
			AssetReference assetReference,
			Transform parent = null,
			bool useWorldSpace = true
		) where T : UnityEngine.Object
		{
			return InstantiateAddressableSync<T>(assetReference, Vector3.zero, Quaternion.identity, parent, useWorldSpace);
		}

		#endregion Custom GameObject Creation
	}
}
