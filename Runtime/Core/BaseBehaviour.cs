using MagmaFlow.Framework.Events;
using MagmaFlow.Framework.Pooling;
using MagmaFlow.Framework.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MagmaFlow.Framework.Core
{	
	/// <summary>
	/// This should replace Unity's MonoBehaviour for an array of extended functionality provided by the MagmaFlow Framework
	/// </summary>
	public class BaseBehaviour : MonoBehaviour
	{
		// Shared by all BaseBehaviours. Safe because Physics queries are main-thread only and these methods don't re-enter.
		private static Collider[] s_overlapBuffer = new Collider[16];
		private static readonly RaycastHit[] s_lineOfSightHits = new RaycastHit[16];

		protected MagmaFramework_Core MagmaFramework_Core => MagmaFramework_Core.Instance;
		protected MagmaFramework_PooledObjectsManager MagmaFramework_PooledObjectsManager => MagmaFramework_PooledObjectsManager.Instance;
		protected MagmaFramework_MusicManager MagmaFramework_MusicManager => MagmaFramework_MusicManager.Instance;

		private Collider[] _overlapResult;
		private readonly RaycastHit[] _lineOfSightHits = new RaycastHit[16];

		#region Get overlap points

		/// <summary>
		/// Finds the closest points (to the sourcePoint) on all colliders that are within the overlap sphere of this sourcePoint.
		/// <para>Colliders on this object and its children are always ignored.</para>
		/// </summary>
		/// <param name="results">Points are appended to this list.</param>
		/// <param name="sourcePoint">The center of the overlap sphere.</param>
		/// <param name="radius">The radius of the overlap sphere.</param>
		/// <param name="numberOfPoints">Maximum number of colliders to check.</param>
		/// <param name="collisionLayerMask">The layers to check against.</param>
		/// <param name="ignoreTriggers">How to handle trigger colliders.</param>
		/// <param name="passThrough">If false, points hidden behind other colliders are discarded.</param>
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
			if (s_overlapBuffer.Length < numberOfPoints) s_overlapBuffer = new Collider[numberOfPoints]; // grows once, never per call

			int actualMask = collisionLayerMask ?? Physics.AllLayers;
			int noOfOverlappingColliders = Physics.OverlapSphereNonAlloc(sourcePoint, radius, s_overlapBuffer, mask, ignoreTriggers);

			if (noOfOverlappingColliders >= _overlapResult.Length)
			{
				MagmaUtils.LogWarning($"{gameObject.name} ::: Found {noOfOverlappingColliders} overlapping colliders, " +
								 $"which fills the buffer of {_overlapResult.Length}. Some colliders may have been missed. " +
								 $"Increase numberOfPoints OR decrease the radius when calling GetOverlapContactPoints().");
			}

			if (results == null) results = new List<Vector3>(numberOfPoints);
			for (int i = 0; i < noOfOverlappingColliders; i++)
			{
				Collider target = _overlapResult[i];
				if (IsOwnCollider(target)) continue;

				Vector3 pointToAdd = target.ClosestPoint(sourcePoint);
				if (sourcePoint == pointToAdd) continue;

				if (passThrough || HasLineOfSight(pointToAdd, sourcePoint, target, actualMask, ignoreTriggers))
				{
					results.Add(pointToAdd);
				}
			}
		}

		/// <summary>
		/// True if nothing except this object's own colliders and the target itself lies between the two points.
		/// </summary>
		private bool HasLineOfSight(Vector3 from, Vector3 to, Collider target, int mask, QueryTriggerInteraction triggers)
		{
			Vector3 delta = to - from;
			float distance = delta.magnitude;
			if (distance <= Mathf.Epsilon) return true;

			int hits = Physics.RaycastNonAlloc(from, delta / distance, _lineOfSightHits, distance, mask, triggers);
			for (int h = 0; h < hits; h++)
			{
				Collider hit = _lineOfSightHits[h].collider;
				if (hit == target || IsOwnCollider(hit)) continue;
				return false; // Something else is in the way.
			}
			return true;
		}

		private bool IsOwnCollider(Collider c) => c.transform.IsChildOf(transform);

		#endregion Get overlap points

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
				newObject.transform.SetParent(parent, false);
			}
			return newObject.AddComponent<T>();
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

		protected async Task<T> InstantiateAddressable<T>
		(
			AssetReference assetReference,
			Vector3 position,
			Quaternion rotation,
			Transform parent = null,
			bool useWorldSpace = true
		) where T : UnityEngine.Object
		{
			if (assetReference == null || !assetReference.RuntimeKeyIsValid())
			{
				MagmaUtils.LogError($"{name} ::: InstantiateAddressable called with a null or invalid AssetReference.");
				return null;
			}

			try
			{
				ToWorldPose(ref position, ref rotation, parent, useWorldSpace);
				var handle = Addressables.InstantiateAsync(assetReference, position, rotation, parent);
				await handle.Task;
				return FinalizeInstance<T>(handle, assetReference);
			}
			catch (Exception e)
			{
				MagmaUtils.LogException(e);
				return null;
			}
		}

		protected T InstantiateAddressableSync<T>
		(
			AssetReference assetReference,
			Vector3 position,
			Quaternion rotation,
			Transform parent = null,
			bool useWorldSpace = true
		) where T : UnityEngine.Object
		{
			if (assetReference == null || !assetReference.RuntimeKeyIsValid())
			{
				MagmaUtils.LogError($"{name} ::: InstantiateAddressableSync called with a null or invalid AssetReference.");
				return null;
			}

			try
			{
				ToWorldPose(ref position, ref rotation, parent, useWorldSpace);
				var handle = Addressables.InstantiateAsync(assetReference, position, rotation, parent);
				handle.WaitForCompletion();
				return FinalizeInstance<T>(handle, assetReference);
			}
			catch (Exception e)
			{
				MagmaUtils.LogException(e);
				return null;
			}
		}

		#endregion Custom GameObject Creation

		/// <summary>
		/// Shared post-instantiation step for InstantiateAddressable / InstantiateAddressableSync.
		/// Validates the operation, attaches the release-on-destroy component and resolves the requested type.
		/// <para>On any failure the instance (if one was created) is released and null is returned.</para>
		/// </summary>
		private T FinalizeInstance<T>(AsyncOperationHandle<GameObject> handle, AssetReference assetReference) where T : UnityEngine.Object
		{
			if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
			{
				MagmaUtils.LogError($"{name} ::: Failed to instantiate Addressable '{assetReference.AssetGUID}'. {handle.OperationException}");
				if (handle.IsValid()) Addressables.Release(handle);
				return null;
			}

			// The caller was destroyed while the asset was loading; nobody will own this instance.
			if (this == null)
			{
				Addressables.ReleaseInstance(handle);
				return null;
			}

			GameObject instance = handle.Result;

			// Must be added before any Destroy() below, so destroying the instance also releases it.
			instance.AddComponent<InstantiatedAddressableCleanup>();

			if (typeof(T) == typeof(GameObject))
			{
				return instance as T;
			}

			if (typeof(Component).IsAssignableFrom(typeof(T)) && instance.TryGetComponent(typeof(T), out var component))
			{
				return component as T;
			}

			MagmaUtils.LogError($"{name} ::: Prefab '{instance.name}' does not contain a {typeof(T).Name}. Destroying the instance.");
			Destroy(instance);
			return null;
		}

		/// <summary>
		/// Converts a pose to world space when it was given relative to the parent.
		/// </summary>
		private static void ToWorldPose(ref Vector3 position, ref Quaternion rotation, Transform parent, bool useWorldSpace)
		{
			if (useWorldSpace || parent == null) return;
			position = parent.TransformPoint(position);
			rotation = parent.rotation * rotation;
		}
	}
}
