using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MagmaFlow.Framework
{
	public class BaseBehaviour : MonoBehaviour
	{	
		protected ApplicationCore ApplicationCore => ApplicationCore.Instance;

		private Transform subComponent;

		/// <summary>
		/// Using ComputePenetration(), returns a list of all the contact points around a sphere collider of radius 'radius'
		/// </summary>
		/// <param name="spherePosition"></param>
		/// <param name="radius"></param>
		/// <param name="wallLayer"></param>
		/// <returns></returns>
		protected List<Vector3> GetWallOverlapContactPoints(Vector3 spherePosition, float radius, LayerMask wallLayer)
		{
			var contactPoints = new List<Vector3>();

			// Get all colliders overlapping the sphere
			Collider[] colliders = Physics.OverlapSphere(spherePosition, radius, wallLayer, QueryTriggerInteraction.Ignore);

			// Create a temporary sphere collider (used for penetration testing)
			GameObject tempGO = new GameObject("TempSphere");
			SphereCollider tempCollider = tempGO.AddComponent<SphereCollider>();
			tempCollider.radius = radius;
			tempGO.transform.position = spherePosition;

			foreach (var hitCollider in colliders)
			{
				if (Physics.ComputePenetration(
					tempCollider, spherePosition, Quaternion.identity,
					hitCollider, hitCollider.transform.position, hitCollider.transform.rotation,
					out Vector3 direction, out float distance))
				{
					// Approximate contact point
					Vector3 contactPoint = hitCollider.ClosestPoint(spherePosition) - direction * distance;
					contactPoints.Add(contactPoint);
				}
			}

			// Cleanup
			Destroy(tempGO);

			return contactPoints;
		}

		/// <summary>
		/// This only works for a layer mask that contains a single layer
		/// </summary>
		/// <param name="mask"></param>
		/// <returns></returns>
		protected int LayerMaskToLayer(LayerMask mask)
		{
			int bitmask = mask.value;

			if (bitmask == 0 || (bitmask & (bitmask - 1)) != 0)
			{
				Debug.LogError("LayerMask must have exactly one bit set.");
				return -1; // or throw an exception
			}

			return (int)Mathf.Log(bitmask, 2);
		}

		/// <summary>
		/// Converts a 'int' layer to a layer mask
		/// </summary>
		/// <param name="gameObjectLayer"></param>
		/// <returns></returns>
		protected LayerMask LayerToLayerMask(GameObject gameObjectLayer)
		{
			return 1 << gameObjectLayer.layer;
		}

		#region Get Children
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
		/// <param name="searchOrigin"></param>
		/// <param name="childName"></param>
		/// <returns></returns>
		protected T GetSubcomponent<T>(Transform searchOrigin, string childName)
		{

			if (searchOrigin.GetComponent<T>() != null && searchOrigin.gameObject.name == childName)
			{
				subComponent = searchOrigin;
			}
			
			foreach (Transform child in searchOrigin)
			{
				GetSubcomponent<T>(child, childName);
			}

			if (subComponent != null)
			{
				return subComponent.GetComponent<T>();
			}
			else
			{
				return default;
			}

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

		#endregion Get Children

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
