using MagmaFlow.Framework.Events;
using MagmaFlow.Framework.Pooling;
using MagmaFlow.Framework.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
		// Created on first use, so a BaseBehaviour that never calls GetOverlapContactPoints() costs nothing.
		private static Collider[] s_overlapBuffer;
		private static RaycastHit[] s_lineOfSightHits;

		protected MagmaFramework_Core MagmaFramework_Core => MagmaFramework_Core.Instance;
		protected MagmaFramework_PooledObjectsManager MagmaFramework_PooledObjectsManager => MagmaFramework_PooledObjectsManager.Instance;
		protected MagmaFramework_MusicManager MagmaFramework_MusicManager => MagmaFramework_MusicManager.Instance;

		private Collider[] _overlapResult;

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
			// Grows only when a caller asks for more than any previous one, never per call
			if (s_overlapBuffer == null || s_overlapBuffer.Length < numberOfPoints) s_overlapBuffer = new Collider[numberOfPoints];

			int actualMask = collisionLayerMask ?? Physics.AllLayers;
			int noOfOverlappingColliders = Physics.OverlapSphereNonAlloc(sourcePoint, radius, s_overlapBuffer, actualMask, ignoreTriggers);

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

			s_lineOfSightHits ??= new RaycastHit[16];
			int hits = Physics.RaycastNonAlloc(from, delta / distance, s_lineOfSightHits, distance, mask, triggers);
			for (int h = 0; h < hits; h++)
			{
				Collider hit = s_lineOfSightHits[h].collider;
				if (hit == target || IsOwnCollider(hit)) continue;
				return false; // Something else is in the way.
			}
			return true;
		}

		private bool IsOwnCollider(Collider c) => c.transform.IsChildOf(transform);

		#endregion Get overlap points

		#region Events

		// Per concrete type: does it override OnGamePaused()? Resolved once per type, then cached.
		private static readonly Dictionary<Type, bool> s_overridesOnGamePaused = new();
		private static readonly Type[] s_onGamePausedSignature = { typeof(GamePausedEvent) };

		private bool isSubscribedToGamePaused;

		/// <summary>
		/// Fired when MagmaFramework_Core.PauseGame() is called.
		/// <para>Only objects whose class overrides this method are subscribed, so the rest cost nothing.</para>
		/// <para>If you override Awake() / OnDestroy(), call base.Awake() / base.OnDestroy(), or this won't be received / unsubscribed.</para>
		/// </summary>
		/// <param name="eventData"></param>
		protected virtual void OnGamePaused(GamePausedEvent eventData) { }
		protected virtual void OnDestroy()
		{
			if (isSubscribedToGamePaused)
			{
				MagmaFramework_EventBus.Unsubscribe<GamePausedEvent>(OnGamePaused);
				isSubscribedToGamePaused = false;
			}
		}
		protected virtual void Awake()
		{
			if (OverridesOnGamePaused(GetType()))
			{
				MagmaFramework_EventBus.Subscribe<GamePausedEvent>(OnGamePaused);
				isSubscribedToGamePaused = true;
			}
		}
		private static bool OverridesOnGamePaused(Type type)
		{
			if (!s_overridesOnGamePaused.TryGetValue(type, out bool overrides))
			{
				// Returns the most derived override, so an override in an intermediate base class counts too
				var method = type.GetMethod(nameof(OnGamePaused), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, s_onGamePausedSignature, null);
				overrides = method != null && method.DeclaringType != typeof(BaseBehaviour);
				s_overridesOnGamePaused[type] = overrides;
			}
			return overrides;
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
