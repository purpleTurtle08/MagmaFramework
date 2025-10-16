using MagmaFlow.Framework.Core;
using MagmaFlow.Framework.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
					thisCollider, transform.position, Quaternion.identity,
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
			OnDisable();
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
		/// Returns a list of all instances of a type in all the children including grandchildren.
		/// </summary>
		/// <returns></returns>
		protected T[] GetAllSubcomponents<T>() where T : Component
		{
			List<T> typeList = new List<T>();
			GetChildrenInternal(transform, typeList);
			return typeList.ToArray();
		}

		/// <summary>
		/// Returns the instance of the given type in the child with the provided name of the provided parent
		/// <para>The search excludes the parent!</para>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="origin"></param>
		/// <param name="childName"></param>
		/// <returns></returns>
		protected T GetSubcomponent<T>(Transform origin, string childName)
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

		private void GetChildrenInternal<T>(Transform parent, List<T> result) where T : Component
		{
			if (parent != transform)
			{
				T comp = parent.GetComponent<T>();
				if (comp != null)
				{
					result.Add(comp);
				}
			}

			foreach (Transform child in parent)
			{
				GetChildrenInternal(child, result);
			}
		}

		#endregion Get subcomponents

		#region Invoke
		/// <summary>
		/// Calls the requested action with the desired delay
		/// </summary>
		/// <param name="task"></param>
		/// <param name="delay"></param>
		protected void Invoke(Action task, float delay = 0)
		{
			if (task != null)
			{
				StartCoroutine(InvokeInternal(task, delay));
			}
			else
			{
				return;
			}
		}

		private IEnumerator InvokeInternal(Action task, float delay)
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

		#endregion Custom GameObject Creation
	}
}
