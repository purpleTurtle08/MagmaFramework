using MagmaFlow.Framework.Core;
using MagmaFlow.Framework.Pooling;
using UnityEngine;

namespace MagmaFlow.Framework.Examples
{
	/// <summary>
	/// This is a simple behaviour, exemplifying the implementation of IPoolableObject
	/// </summary>
	public class Examples_PooledObjectImplementation : BaseBehaviour, IPoolableObject
	{
		[SerializeField] private float lifeSpan;
		private float lifetimeLeft;

		private void Update()
		{
			lifetimeLeft -= Time.deltaTime;

			//Once the object's life is ended we release it back into the pool
			if (lifetimeLeft <= 0)
			{
				MagmaFramework_PooledObjectsManager.ReleaseObject(this);
			}
		}

		public void OnInitialize()
		{
			//We set the lifetime of the object to the inspector value
			lifetimeLeft = lifeSpan;
		}

		public void OnRelease() { }
	}
}
