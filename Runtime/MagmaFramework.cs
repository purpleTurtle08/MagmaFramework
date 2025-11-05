using MagmaFlow.Framework.Events;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MagmaFlow.Framework.Core
{
	/// <summary>
	/// Controls how often vertical synchronization (VSync) occurs.
	/// </summary>
	public enum VerticalSyncType
	{
		/// <summary>
		/// VSync is disabled. The game renders as fast as possible, which may cause screen tearing.
		/// </summary>
		Off = 0,

		/// <summary>
		/// Syncs with every vertical blank (VBlank). Caps frame rate to the monitor's refresh rate (e.g., 60 FPS on a 60 Hz monitor).
		/// </summary>
		EveryVBlank = 1,

		/// <summary>
		/// Syncs with every second vertical blank. Caps frame rate to half the refresh rate (e.g., 30 FPS on a 60 Hz monitor).
		/// </summary>
		EverySecondVBlank = 2,

		/// <summary>
		/// Syncs with every third vertical blank. Caps frame rate to one-third of the refresh rate (e.g., 20 FPS on a 60 Hz monitor).
		/// </summary>
		EveryThirdVBlank = 3,

		/// <summary>
		/// Syncs with every fourth vertical blank. Caps frame rate to one-quarter of the refresh rate (e.g., 15 FPS on a 60 Hz monitor).
		/// </summary>
		EveryFourthVBlank = 4
	}

	/// <summary>
	/// This script is the core of the framework.
	/// Place it in the scene on an empty object or drag & drop the MagmaFrameworkCore prefab into the scene.
	/// 
	/// RECOMMENDED: Create your own game core script and inherit the MagmaFramework core behaviour or simply make a separate singleton
	/// </summary>
	[DefaultExecutionOrder(-200)]
	public class MagmaFramework : MonoBehaviour
	{	
		/// <summary>
		/// Static reference to this singleton. A property of BaseBehaviour as well
		/// </summary>
		public static MagmaFramework Instance { get; private set; }
		/// <summary>
		/// This handles pooled objects
		/// </summary>
		public PooledObjectsManager PooledObjectsManager { get; private set; }
		public bool IsGamePaused { get; private set; } = false;

		[SerializeField] [Tooltip("This will be used when initializing the PooledObjectsManager.\nA value of 256 is recommended to avoid bloating up the memory with too many pooled instances.\n-1 for no limit")] private int maximumPoolSize = -1;
		private Resolution currentResolution;
		private FullScreenMode currentScreenMode;

		private void Awake()
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
			EventBus.Publish<SceneLoadedEvent>(new SceneLoadedEvent(scene, mode));
		}

		/// <summary>
		/// This event is subscribed to the Unity change scene event at game core startup
		/// </summary>
		/// <param name="scene"></param>
		/// <param name="mode"></param>
		private void OnSceneUnloaded(Scene scene)
		{
			EventBus.Publish<SceneUnloadedEvent>(new SceneUnloadedEvent(scene));
		}

		/// <summary>
		/// Initializes the game core component.
		/// Creates a PooledObjectsManager that will persist through scene changes
		/// </summary>
		private void SetupFrameworkCore()
		{	
			//currentResolution = Screen.currentResolution;
			var pooledObjManager = new GameObject("PooledObjectsManager");
			PooledObjectsManager = pooledObjManager.AddComponent<PooledObjectsManager>();
			PooledObjectsManager.transform.SetParent(transform);
			PooledObjectsManager.Initialize(maximumPoolSize);
			UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
			UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
		}

		/// <summary>
		/// This ensures that there can only be one object of this type per scene
		/// </summary>
		private bool Singleton()
		{
			if (Instance != null && Instance != this)
			{
				Debug.LogWarning($"Removed {gameObject.name}, as it is a duplicate MagmaFramework. Ensure you only have 1 MagmaFramework per scene.");
				Destroy(gameObject);
				return false;
			}

			Instance = this;
			DontDestroyOnLoad(gameObject);
			Debug.Log("Magma Framework initialized.");
			return true;
		}

		/// <summary>
		/// VSync is a graphics technology that synchronizes a game's frame rate with the refresh rate of the monitor.
		/// The recommended value is EveryVBlank (1) to ensure the smoothest gameplay
		/// </summary>
		/// <param name="vSyncType"></param>
		public void SetVSync(VerticalSyncType vSyncType)
		{
			QualitySettings.vSyncCount = (int) vSyncType;
		}

		/// <summary>
		/// Sets the game's display resolution.
		/// Only works at runtime.
		/// </summary>
		/// <param name="resolution"></param>
		/// <param name="mode"></param>
		public void SetScreenResolution(Resolution resolution, FullScreenMode mode)
		{
#if UNITY_EDITOR
			// If we are inside the editor, we don't do anything
#else
			currentResolution = resolution;
			currentScreenMode = mode;
			Screen.SetResolution(resolution.width, resolution.height, mode, resolution.refreshRateRatio);
			Debug.Log($"Screen size changed to :: {resolution.width}x{resolution.height} {resolution.refreshRateRatio.value}");
#endif
			EventBus.Publish(new SetScreenResolutionEvent(resolution, mode));
		}

		/// <summary>
		/// Enable / disable cursor
		/// </summary>
		/// <param name="value"></param>
		public void SetCursorEnabled(bool value)
		{	
			Cursor.visible = value;
			Cursor.lockState = value ? CursorLockMode.Confined : CursorLockMode.Locked;
		}

		/// <summary>
		/// Sets the cursor's image and position
		/// </summary>
		/// <param name="texture"></param>
		/// <param name="position"></param>
		public void SetCursor(Texture2D texture, Vector2 position)
		{
			Cursor.SetCursor(texture, position, CursorMode.Auto);
		}

		/// <summary>
		/// Exits the application. Treats editor as well
		/// </summary>
		public void Quit()
		{
#if UNITY_EDITOR
			// If in the editor, stop playing
			EditorApplication.isPlaying = false;
#else
			// If in a build, quit the application
			Application.Quit();
#endif
		}

		/// <summary>
		/// Modify the global time scale
		/// </summary>
		/// <param name="value"></param>
		public void SetTimeScale(float value)
		{
			Time.timeScale = value;
		}

		/// <summary>
		/// !!WARNING!! If you affect time scale, you might want to make sure that your Animator / Other components run on unscaledDeltaTime (where it is the case)
		/// Pause/unpause the game
		/// </summary>
		/// <param name="value">True is game pause, false is unpaused</param>
		/// <param name="affectTimeScale">If set to true, this will set the time scale as well</param>
		public void PauseGame(bool value, bool affectTimeScale = false)
		{
			if (affectTimeScale) Time.timeScale = value ? 0 : 1;
			IsGamePaused = value;
			EventBus.Publish(new GamePausedEvent(IsGamePaused));
		}
	}
}
