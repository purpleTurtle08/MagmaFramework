using UnityEngine;

namespace MagmaFlow.Framework.Pooling
{
	/// <summary>
	/// Managed by MagmaFramework_PooledObjectsManager.
	/// This will clean-up the reference in the manager, in case that the object is destroyed.
	/// </summary>
	internal sealed class PooledInstanceCleanup : MonoBehaviour
	{
		private MagmaFramework_PooledObjectsManager manager;
		private IPoolableObject pooledObject;
		/// <summary>
		/// Called when a pooled object is created.
		/// </summary>
		/// <param name="manager"></param>
		/// <param name="pooledObject"></param>
		internal void Initialize(MagmaFramework_PooledObjectsManager manager, IPoolableObject pooledObject)
		{
			this.manager = manager;
			this.pooledObject = pooledObject;
		}

		private void OnDestroy()
		{
			manager?.RemoveLookup(pooledObject);
		}
	}
}
