using MagmaFlow.Framework.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;

namespace MagmaFlow.Framework.Core
{	
	/// <summary>
	/// This should replace Unity's MonoBehaviour for an array of extended functionality provided by the MagmaFlow Framework
	/// </summary>
	public class BaseBehaviour : MonoBehaviour
	{	
		protected MagmaFramework MagmaFramework => MagmaFramework.Instance;

		/// <summary>
		/// Using ComputePenetration(), returns a list of all the contact points around a sphere collider of radius 'radius' for this object's collider
		/// </summary>
		/// <param name="sourcePoint"></param>
		/// <param name="radius"></param>
		/// <param name="collisionLayerMask"></param>
		/// <returns></returns>
		protected List<Vector3> GetOverlapContactPoints(Vector3 sourcePoint, float radius, LayerMask collisionLayerMask)
		{	
			if(!TryGetComponent<Collider>(out var thisCollider))
			{
				Debug.LogError("Cannot get overlap points because this object does not have a collider");
				return null;
			}

			var contactPoints = new List<Vector3>();
			// Get all colliders overlapping the sphere
			Collider[] colliders = Physics.OverlapSphere(sourcePoint, radius, collisionLayerMask, QueryTriggerInteraction.Ignore);

			foreach (var hitCollider in colliders)
			{
				if (Physics.ComputePenetration(
					thisCollider, transform.position, transform.rotation,
					hitCollider, hitCollider.transform.position, hitCollider.transform.rotation,
					out Vector3 direction, out float distance))
				{
					// Approximate contact point
					Vector3 contactPoint = hitCollider.ClosestPoint(sourcePoint) - direction * distance;
					contactPoints.Add(contactPoint);
				}
			}

			return contactPoints;
		}

		#region Events
		protected virtual void OnGamePaused(GamePausedEvent eventData) { }
		protected virtual void OnSceneLoaded(SceneLoadedEvent eventData) { }
		protected virtual void OnSceneUnloaded(SceneUnloadedEvent eventData) { }
		protected virtual void OnDestroy()
		{
			EventBus.Unsubscribe<GamePausedEvent>(OnGamePaused);
			EventBus.Unsubscribe<SceneLoadedEvent>(OnSceneLoaded);
			EventBus.Unsubscribe<SceneUnloadedEvent>(OnSceneUnloaded);
		}
		protected virtual void OnEnable()
		{
			EventBus.Subscribe<GamePausedEvent>(OnGamePaused);
			EventBus.Subscribe<SceneLoadedEvent>(OnSceneLoaded);
			EventBus.Subscribe<SceneUnloadedEvent>(OnSceneUnloaded);
		}
		protected virtual void OnDisable()
		{
			EventBus.Unsubscribe<GamePausedEvent>(OnGamePaused);
			EventBus.Unsubscribe<SceneLoadedEvent>(OnSceneLoaded);
			EventBus.Unsubscribe<SceneUnloadedEvent>(OnSceneUnloaded);
		}
		#endregion Events

		#region Get subcomponents

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

		#endregion Get subcomponents

		#region Invoke

		/// <summary>
		/// This is a coroutine-based wrapper and should not be confused with Unity's built-in string-based Invoke()
		/// </summary>
		/// <param name="task"></param>
		/// <param name="delay"></param>
		protected void Invoke(UnityAction task, float delay = 0)
		{
			if (task != null)
			{
				StartCoroutine(InvokeInternal(task, delay));
			}
			else
			{
				Debug.LogError($"Can't invoke a NULL action on {gameObject.name}!");
				return;
			}
		}

		private IEnumerator InvokeInternal(UnityAction task, float delay)
		{
			yield return new WaitForSeconds(delay);

			task?.Invoke();
		}

		#endregion Invoke

		#region Custom GameObject Creation

		/// <summary>
		/// Creates an object with the given name and required component
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name"></param>
		/// <returns></returns>
		protected T CreateWithComponent<T>(string name) where T : Component
		{	
			var temp = new GameObject(name);
			var tempT = temp?.AddComponent(typeof(T));
			return (T)tempT;
		}

		/// <summary>
		/// Creates an object with the given name and required component, and sets its parent
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="Name"></param>
		protected T CreateWithComponent<T>(string name, Transform parent) where T : Component
		{
			var temp = CreateWithComponent<T>(name);
			temp?.transform.SetParent(parent);
			return temp;
		}

		/// <summary>
		/// Instantiates an object using an asset refference
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="assetReference"></param>
		/// <param name="parent"></param>
		/// <param name="position"></param>
		/// <param name="rotation"></param>
		/// <returns></returns>
		protected async Task<T> InstantiateAddressableAsync<T>
		(
			AssetReference assetReference,
			Transform parent,
			Vector3 position,
			Quaternion rotation
		) where T : UnityEngine.Object
		{
			if (assetReference == null || assetReference.RuntimeKeyIsValid() == false)
			{
				Debug.LogError("Instantiate addressable called with a null or invalid AssetReference.");
				return null;
			}

			try
			{
				GameObject instantiatedObject = await Addressables.InstantiateAsync(
					assetReference,
					position,
					rotation,
					parent
				).Task;

				// 2. Check if the *instantiatedObject* has the component.
				if (instantiatedObject == null)
				{
#if UNITY_EDITOR
					// This can happen if the operation is cancelled or fails
					Debug.LogError($"Failed to instantiate Addressable: {assetReference.editorAsset.name}");
#endif
					return null;
				}

				// Case 1: user asked for GameObject
				if (typeof(T) == typeof(GameObject))
					return instantiatedObject as T;

				// Case 2: user asked for a Component
				if (typeof(Component).IsAssignableFrom(typeof(T)) &&
					instantiatedObject.TryGetComponent(typeof(T), out var component))
				{
					instantiatedObject.AddComponent<InstantiatedAddressableCleanup>();
					return component as T;
				}

				// No matching component found
				Debug.LogError(
					$"Prefab '{instantiatedObject.name}' does not contain component {typeof(T)}"
				);
				Addressables.ReleaseInstance(instantiatedObject);
				return null;
			}
			catch (Exception e)
			{
				// Handle exceptions during the async operation
				Debug.LogException(e);
				return null;
			}
		}

		/// <summary>
		/// Wrapper over InstantiateAddressableAsync() for better usability
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="assetReference"></param>
		/// <param name="parent"></param>
		/// <param name="position"></param>
		/// <param name="rotation"></param>
		/// <param name="onComplete"></param>
		public void InstantiateAddressable<T>(
			AssetReference assetReference,
			Transform parent,
			Vector3 position,
			Quaternion rotation,
			UnityAction<T> onComplete = null
		) where T : UnityEngine.Object
		{
			// Start a coroutine to handle the async operation
			// and its callback on the main thread.
			StartCoroutine(InstantiateAddressableRoutine(
				assetReference,
				parent,
				position,
				rotation,
				onComplete
			));
		}

		private IEnumerator InstantiateAddressableRoutine<T>(
			AssetReference assetReference,
			Transform parent,
			Vector3 position,
			Quaternion rotation,
			UnityAction<T> onComplete
		) where T : UnityEngine.Object
		{
			// Await the async task. StartCoroutine will pause here
			// and resume on the main thread when the task is done.
			var task = InstantiateAddressableAsync<T>(assetReference, parent, position, rotation);
			yield return new WaitUntil(() => task.IsCompleted);

			if (task.IsCompletedSuccessfully)
			{
				onComplete?.Invoke(task.Result);
			}
			else if (task.IsFaulted)
			{
#if UNITY_EDITOR
				Debug.LogException(new Exception($"An error occurred during {assetReference.editorAsset.name} instantiation.", task.Exception));
#endif
			}
		}

		#endregion Custom GameObject Creation
	}
}
