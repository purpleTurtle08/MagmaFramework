using MagmaFlow.Framework.Core;
using MagmaFlow.Framework.Events;
using MagmaFlow.Framework.Pooling;
using UnityEngine;

namespace MagmaFlow.Framework.Examples
{
	public class Examples_BaseBehaviourImplementation : BaseBehaviour
	{
		private Collider[] overlappingCollidersRef = new Collider[15];

		[Header("Get Overlap Contact Points Parameters")]
		[Range(1, 100)] public float Range = 3;
		public bool PassThrough = false;

		[SerializeField] private AssetReferenceDictionaryBuilder assets;

		private void Start()
		{
			//You can store a an Invoke() as a coroutine (maybe for situational stopping: StopCoroutine(greetingsRoutine);
			Coroutine greetingsRoutine = Invoke(GreetingOnStart, 3);
			//Or if you don't want to manage a coroutine, you can simply not store it
			Invoke(GreetingOnStart, 3);
			//Or Invoking without any delay (not sure why you'd do that)
			Invoke(GreetingOnStart);

			//This will attempt to get a subcomponent from a child with any depth, based on its NAME
			var subComponent = GetSubcomponent<Transform>(transform, "nameOfTheChild");

			//This will create a new empty game object with any component (Transform in this case), parented to a target (this object in this case)
			var newChild = CreateWithComponent<Transform>("testChild", transform);
		}
		private void Update()
		{
			DisplayOverlapContactPoints();
		}
		/// <summary>
		/// You can override this subscriber (if you are inheriting from BaseBehaviour), and do your custom logic inside here
		/// </summary>
		/// <param name="eventData"></param>
		protected override void OnGamePaused(GamePausedEvent eventData)
		{
			base.OnGamePaused(eventData);
			//TO DO: 
			//My logic here
		}
		/// <summary>
		/// You can override this subscriber (if you are inheriting from BaseBehaviour), and do your custom logic inside here
		/// </summary>
		/// <param name="eventData"></param>
		protected override void OnSceneLoaded(SceneLoadedEvent eventData)
		{
			base.OnSceneLoaded(eventData);
			//TO DO: 
			//My logic here
		}
		/// <summary>
		/// You can override this subscriber (if you are inheriting from BaseBehaviour), and do your custom logic inside here
		/// </summary>
		/// <param name="eventData"></param>
		protected override void OnSceneUnloaded(SceneUnloadedEvent eventData)
		{
			base.OnSceneUnloaded(eventData);
			//TO DO: 
			//My logic here
		}
		/// <summary>
		/// This method exemplifies the use of GetOverlapContactPoints(), as well as instantiating a pooled object or an addressable
		/// </summary>
		private async void DisplayOverlapContactPoints()
		{
			//We get the list of points on the colliders in range
			var contactPoints = GetOverlapContactPoints(ref overlappingCollidersRef, transform.position, Range, passThrough: PassThrough);
			//We instantiate a prefab at each of those points, for a visual feedback
			foreach (var contactPoint in contactPoints)
			{
				//awaited pooled object instantiation
				await MagmaFramework_PooledObjectsManager.InstantiatePooledObject<Examples_PooledObjectImplementation>(assets.GetAssetReference("ContactPoint"), contactPoint, Quaternion.identity);
				// If you don't need to await the instantiation, use the discard operator
				// _ = MagmaFramework_PooledObjectsManager.InstantiatePooledObject<Exampled_PooledObject>(assets.GetAssetReference("ContactPoint"), contactPoint, Quaternion.identity);
				// If you want to instantiate an addressable (not pooled)
				// await InstantiateAddressable<Exampled_PooledObject>(assets.GetAssetReference("ContactPoint"), contactPoint, Quaternion.identity);

			}
		}
		private void GreetingOnStart()
		{
			Debug.Log($"Greetings from {gameObject.name}");
		}
	}
}
