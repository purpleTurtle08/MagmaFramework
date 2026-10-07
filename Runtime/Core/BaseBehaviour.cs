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
		protected MagmaFramework_Core MagmaFramework_Core => MagmaFramework_Core.Instance;
		protected MagmaFramework_PooledObjectsManager MagmaFramework_PooledObjectsManager => MagmaFramework_PooledObjectsManager.Instance;
		protected MagmaFramework_MusicManager MagmaFramework_MusicManager => MagmaFramework_MusicManager.Instance;

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
		/// Invokes an action after a delay, in scaled time.
		/// <para>The callback is cancelled if the object is destroyed OR deactivated (coroutines stop on SetActive(false)).
		/// For pooled objects this means releasing them cancels any pending call.</para>
		/// <para>With delay &lt;= 0 the action runs immediately (in this call, not next frame) and null is returned.</para>
		/// <para>Allocates a coroutine, a WaitForSeconds and (for lambdas / method groups) a delegate per call. Avoid it in per-frame code.</para>
		/// </summary>
		/// <param name="action"></param>
		/// <param name="delay">Seconds, in scaled time (paused when Time.timeScale is 0).</param>
		/// <returns>Returns the coroutine that handles the invoking (can be stopped with StopCoroutine), or null if it ran immediately.</returns>
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
		/// Asynchronously instantiates an Addressable at the origin (or at the parent's origin when useWorldSpace is false).
		/// <para>See the overload with position / rotation for details.</para>
		/// </summary>
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
		/// Synchronously instantiates an Addressable at the origin (or at the parent's origin when useWorldSpace is false).
		/// <para>See the overload with position / rotation for details.</para>
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

		/// <summary>
		/// Asynchronously instantiates an Addressable, optionally under a parent.
		/// <para>The instance is released from Addressables automatically when it's destroyed.</para>
		/// <para>Returns null (and releases the instance) if loading fails, if the prefab has no T, or if this object was destroyed while loading.</para>
		/// </summary>
		/// <typeparam name="T">GameObject, or a component on the prefab's root.</typeparam>
		/// <param name="position">World position, or local to the parent when useWorldSpace is false.</param>
		/// <param name="rotation">World rotation, or local to the parent when useWorldSpace is false.</param>
		/// <param name="parent">Optional parent.</param>
		/// <param name="useWorldSpace">False: position / rotation are relative to the parent, and stay correct even if the parent moves while loading.</param>
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
				Vector3 worldPosition = position;
				Quaternion worldRotation = rotation;
				ToWorldPose(ref worldPosition, ref worldRotation, parent, useWorldSpace);
				var handle = Addressables.InstantiateAsync(assetReference, worldPosition, worldRotation, parent);
				await handle.Task;

				// The world pose above was computed when the request was made; if the parent moved while loading,
				// re-apply the requested local pose so the instance ends up where it was asked to be, relative to the parent.
				if (!useWorldSpace && parent != null && handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
				{
					handle.Result.transform.SetLocalPositionAndRotation(position, rotation);
				}

				return FinalizeInstance<T>(handle, assetReference);
			}
			catch (Exception e)
			{
				MagmaUtils.LogException(e);
				return null;
			}
		}

		/// <summary>
		/// Synchronously instantiates an Addressable, optionally under a parent. Blocks until the asset is loaded.
		/// <para>NOT supported on WebGL (relies on WaitForCompletion()); use InstantiateAddressable() there.</para>
		/// <para>Blocking on an asset that isn't loaded yet (especially a remote one) can freeze the frame; prefer it for already loaded or local assets.</para>
		/// <para>Same release / null-return rules as InstantiateAddressable().</para>
		/// </summary>
		/// <typeparam name="T">GameObject, or a component on the prefab's root.</typeparam>
		/// <param name="position">World position, or local to the parent when useWorldSpace is false.</param>
		/// <param name="rotation">World rotation, or local to the parent when useWorldSpace is false.</param>
		/// <param name="parent">Optional parent.</param>
		/// <param name="useWorldSpace">False: position / rotation are relative to the parent.</param>
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
		/// Validates the operation, resolves the requested type and attaches the release-on-destroy component.
		/// <para>On any failure the instance (if one was created) is released and null is returned.</para>
		/// </summary>
		private T FinalizeInstance<T>(AsyncOperationHandle<GameObject> handle, AssetReference assetReference) where T : UnityEngine.Object
		{
			if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
			{
				// 'this' may have been destroyed while loading, and reading 'name' on it would throw
				string caller = this != null ? name : "<destroyed caller>";
				MagmaUtils.LogError($"{caller} ::: Failed to instantiate Addressable '{assetReference.AssetGUID}'. {handle.OperationException}");
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
			T result = null;

			if (typeof(T) == typeof(GameObject))
			{
				result = instance as T;
			}
			else if (typeof(Component).IsAssignableFrom(typeof(T)) && instance.TryGetComponent(typeof(T), out var component))
			{
				result = component as T;
			}

			if (result == null)
			{
				// Released through the handle rather than Destroy(): OnDestroy (and so the cleanup component)
				// never runs on a prefab whose root is saved inactive, which would leak the Addressables reference.
				MagmaUtils.LogError($"{name} ::: Prefab '{instance.name}' does not contain a {typeof(T).Name}. Releasing the instance.");
				Addressables.ReleaseInstance(handle);
				return null;
			}

			// Releases the instance when it's destroyed.
			// Note: like any OnDestroy, it only runs if the instance was active at some point.
			instance.AddComponent<InstantiatedAddressableCleanup>();
			return result;
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
