using UnityEngine;
using UnityEngine.SceneManagement;

namespace MagmaFlow.Framework.Events
{	
	/// <summary>
	/// This event is fired inside MagmaFramework_Core component
	/// </summary>
    public struct SceneLoadedEvent
    {
		public Scene Scene;
		public LoadSceneMode LoadSceneMode;
		/// <summary>
		/// This event is fired inside MagmaFramework_Core component
		/// </summary>
		/// <param name="scene"></param>
		/// <param name="loadSceneMode"></param>
		public SceneLoadedEvent(Scene scene, LoadSceneMode loadSceneMode)
		{	
			Scene = scene;
			LoadSceneMode = loadSceneMode;
		}
	}

	/// <summary>
	/// This event is fired inside MagmaFramework_Core component
	/// </summary>
	public struct SceneUnloadedEvent
	{
		public Scene Scene;
		/// <summary>
		/// This event is fired inside MagmaFramework_Core component
		/// </summary>
		/// <param name="scene"></param>
		public SceneUnloadedEvent(Scene scene)
		{
			Scene = scene;
		}
	}

	/// <summary>
	/// This event is fired inside MagmaFramework_Core component
	/// </summary>
	public struct SetScreenResolutionEvent
	{
		/// <summary>
		/// Contains information such as height, width and refresh rate
		/// </summary>
		public Resolution ScreenResolution;
		public FullScreenMode FullScreenMode;
		/// <summary>
		/// This event is fired inside MagmaFramework_Core component
		/// </summary>
		/// <param name="screenResolution"></param>
		/// <param name="fullScreenMode"></param>
		public SetScreenResolutionEvent(Resolution screenResolution, FullScreenMode fullScreenMode)
		{
			ScreenResolution = screenResolution;
			FullScreenMode = fullScreenMode;
		}
	}

	/// <summary>
	/// This event is fired inside MagmaFramework_Core component
	/// </summary>
	public struct GamePausedEvent
	{
		public bool Status;
		public bool WasTimeScaleAffected;
		/// <summary>
		/// This event is fired inside MagmaFramework_Core component
		/// </summary>
		/// <param name="status">The pause state of the game</param>
		/// <param name="wasTimeScaleAffected">Indicates if the time scale has been affected</param>
		public GamePausedEvent(bool status, bool wasTimeScaleAffected)
		{
			Status = status;
			WasTimeScaleAffected = wasTimeScaleAffected;
		}
	}
}
