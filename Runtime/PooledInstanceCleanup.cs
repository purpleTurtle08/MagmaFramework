using UnityEngine;

namespace MagmaFlow.Framework.Core
{
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
