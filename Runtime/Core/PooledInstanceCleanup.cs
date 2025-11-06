using UnityEngine;

namespace MagmaFlow.Framework.Core
{	
	/// <summary>
	/// This will clean-up the reference in the manager, in case that the object is destroyed
	/// </summary>
	public class PooledInstanceCleanup : MonoBehaviour
	{
		private PooledObjectsManager manager;
		private IPoolableObject pooledObject;

		public void Initialize(PooledObjectsManager manager, IPoolableObject pooledObject)
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
