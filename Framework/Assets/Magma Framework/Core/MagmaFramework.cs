using MagmaFlow.Framework.Publishing;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MagmaFlow.Framework.Core
{
	/// <summary>
	/// This script is the core of the framework.
	/// Place it in the scene on an empty object or drag & drop the MagmaFrameworkCore prefab into the scene.
	/// 
	/// RECOMMENDED: Create your own game core script and inherit the MagmaFramework core behaviour or simply make a separate singleton
	/// </summary>
	[DefaultExecutionOrder(-100)]
	public class MagmaFramework : BaseBehaviour
	{	
		/// <summary>
		/// Static reference to this singleton. A property of BaseBehaviour as well
		/// </summary>
		public static MagmaFramework Instance { get; private set; }
		/// <summary>
		/// Handles event subscription and publishing
		/// </summary>
		public Publisher Publisher { get; private set; }
		/// <summary>
		/// This handles pooled objects
		/// </summary>
		public PooledObjectsManager PooledObjectsManager { get; private set; }

		[Tooltip("Check this if you want to clear all publisher events at a scene load/unload event")]
		[SerializeField] private bool automaticallyClearPublisherEvents = false;
		[Tooltip("Check this if you want to clear all pooled objects at a scene load/unload event")]
		[SerializeField] private bool automaticallyClearPooledObjects = false;

		protected virtual void Awake()
		{
			if (!Singleton())
			{
				return;
			}

			SetupFrameworkCore();
		}

		/// <summary>
		/// This event is subscribed to the Unity change scene event at game core startup
		/// </summary>
		/// <param name="scene"></param>
		/// <param name="mode"></param>
		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			Publisher.Publish(PublisherTopics.SYSTEM_RESERVED_ON_SCENE_LOADED, scene, mode);
		}

		/// <summary>
		/// This event is subscribed to the Unity change scene event at game core startup
		/// </summary>
		/// <param name="scene"></param>
		/// <param name="mode"></param>
		private void OnSceneUnloaded(Scene scene)
		{
			Publisher.Publish(PublisherTopics.SYSTEM_RESERVED_ON_SCENE_UNLOADED, scene);

			if (automaticallyClearPublisherEvents)
				Publisher.ClearAllEvents();

			if(automaticallyClearPooledObjects)
				PooledObjectsManager.ClearObjectPools();
		}

		/// <summary>
		/// Initializes the game core component
		/// </summary>
		private void SetupFrameworkCore()
		{	
			Publisher = CreateWithComponent<Publisher>("Publisher", transform);
			PooledObjectsManager = CreateWithComponent<PooledObjectsManager>("PooledInstancesManager", transform);

			UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
			UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
		}

		/// <summary>
		/// This ensures that there can only be one object of type GameCore per scene
		/// </summary>
		private bool Singleton()
		{
			Debug.Log("Magma Framework core initializing...");

			if (Instance == null)
			{
				Instance = this;
				DontDestroyOnLoad(gameObject);
				return true;
			}
			else
			{
				Destroy(this.gameObject);
				Debug.LogWarning("Other Magma Framework core found ...keeping the old instance...");
				return false;
			}
		}
	}
}
