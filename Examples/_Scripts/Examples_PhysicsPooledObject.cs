using MagmaFlow.Framework.Core;
using MagmaFlow.Framework.Pooling;
using UnityEngine;

namespace MagmaFlow.Framework.Examples
{
	public class Examples_PhysicsPooledObject : BaseBehaviour, IPoolableObject
	{
		private bool isInitialized = false;
		private Rigidbody body;

		private void Initialize()
		{
			body = GetComponent<Rigidbody>();
			isInitialized = true;
		}

		private float lifeSpan = 3;
		private float lifetimeLeft;

		private void FixedUpdate()
		{
			lifetimeLeft -= Time.fixedDeltaTime;

			//Once the object's life is ended we release it back into the pool
			if (lifetimeLeft <= 0)
			{
				MagmaFramework_PooledObjectsManager.ReleaseObject(this);
			}
		}

		public void OnInitialize()
		{
			if (!isInitialized)
				Initialize();

			body.useGravity = true;
			body.isKinematic = false;
			//We set the lifetime of the object to the inspector value
			lifetimeLeft = lifeSpan;
		}

		public void OnRelease()
		{
			if (!isInitialized)
				Initialize();

			body.angularVelocity = Vector3.zero;
			body.linearVelocity = Vector3.zero;
		}

		public void Launch()
		{
			transform.localScale = new Vector3(.2f, .4f, .2f);
			body.AddForce(Vector3.up * Random.Range(8, 20), ForceMode.Impulse);
		}
	}
}
