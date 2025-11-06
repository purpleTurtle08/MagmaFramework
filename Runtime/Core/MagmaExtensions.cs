using UnityEngine;

namespace MagmaFlow.Framework.Utils
{
	public static class MagmaExtensions
	{
		/// <summary>
		/// Resets the scale, position and rotation to origin values
		/// </summary>
		/// <param name="transform"></param>
		/// <param name="localSpace"></param>
		public static void ResetTransformation(this Transform transform, bool localSpace = false)
		{
			if (localSpace) transform.localRotation = Quaternion.identity; else transform.rotation = Quaternion.identity;
			transform.localScale = new Vector3(1, 1, 1);
			if (localSpace) transform.localPosition = new Vector3(0, 0, 0); else transform.position = new Vector3(0, 0, 0);
		}

		/// <summary>
		/// Sets the canvas group to displayed / hidden
		/// </summary>
		/// <param name="canvasGroup"></param>
		/// <param name="value"></param>
		public static void SetVisible(this CanvasGroup canvasGroup, bool value)
		{
			canvasGroup.alpha = value ? 1 : 0;
			canvasGroup.blocksRaycasts = value;
			canvasGroup.interactable = value;
		}

		/// <summary>
		/// Destroys all children of this transform.
		/// Works in both Play Mode (Destroy) and Edit Mode (DestroyImmediate).
		/// </summary>
		public static void ClearChildren(this Transform transform, bool immediate = false)
		{
			for (int i = transform.childCount - 1; i >= 0; i--)
			{
				var child = transform.GetChild(i);
#if UNITY_EDITOR
				if (!Application.isPlaying || immediate)
					Object.DestroyImmediate(child.gameObject);
				else
					Object.Destroy(child.gameObject);
#else
            Object.Destroy(child.gameObject);
#endif
			}
		}

		/// <summary>
		/// Returns true if a game object is part (included) of/in a layer mask, false if not
		/// </summary>
		/// <param name="gameObject"></param>
		/// <param name="layerMask"></param>
		/// <returns></returns>
		public static bool IsPartOfLayerMask(this GameObject gameObject, LayerMask layerMask)
		{
			return (layerMask.value & (1 << gameObject.layer)) != 0;
		}

		/// <summary>
		/// Retrieves the layer of this object as a layer mask that only includes the object's layer
		/// </summary>
		/// <param name="gameObject"></param>
		/// <returns></returns>
		public static LayerMask LayerToLayerMask(this GameObject gameObject)
		{
			return 1 << gameObject.layer;
		}

		/// <summary>
		/// This only works for a layer mask that contains a single layer
		/// </summary>
		/// <param name="mask">This mask must only contain a single layer!</param>
		/// <returns></returns>
		public static int LayerMaskToLayer(this LayerMask mask)
		{
			int bitmask = mask.value;

			if (bitmask == 0 || (bitmask & (bitmask - 1)) != 0)
			{
				Debug.LogError("LayerMask must have exactly one bit set.");
				return -1; // or throw an exception
			}

			return (int)Mathf.Log(bitmask, 2);
		}
	}
}
