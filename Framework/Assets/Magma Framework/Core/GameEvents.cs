using UnityEngine;
using UnityEngine.SceneManagement;

namespace MagmaFlow.Framework.Events
{	
	/// <summary>
	/// This event is already created by the MagmaFramwork component
	/// </summary>
    public struct SceneLoadedEvent
    {
		public Scene Scene;
		public LoadSceneMode LoadSceneMode;

		public SceneLoadedEvent(Scene scene, LoadSceneMode loadSceneMode)
		{
			Scene = scene;
			LoadSceneMode = loadSceneMode;
		}
	}

	/// <summary>
	/// This event is already created by the MagmaFramwork component
	/// </summary>
	public struct SceneUnloadedEvent
	{
		public Scene Scene;

		public SceneUnloadedEvent(Scene scene)
		{
			Scene = scene;
		}
	}

	/// <summary>
	/// This event is already created by the MagmaFramework component
	/// </summary>
	public struct SetScreenResolutionEvent
	{	
		/// <summary>
		/// Contains information such as height, width and refresh rate
		/// </summary>
		public Resolution ScreenResolution;
		public FullScreenMode FullScreenMode;

		public SetScreenResolutionEvent(Resolution screenResolution, FullScreenMode fullScreenMode)
		{
			ScreenResolution = screenResolution;
			FullScreenMode = fullScreenMode;
		}
	}

	/// <summary>
	/// This event is already created by the MagmaFramework component
	/// </summary>
	public struct GamePausedEvent
	{
		public bool Status;

		public GamePausedEvent(bool status)
		{
			Status = status;
		}
	}
}
