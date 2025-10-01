using MagmaFlow.Framework.Publishing;
using UnityEditor;
using UnityEngine;
using MagmaFlow.Framework.Core;

/// <summary>
/// This script is the capsule of all the major application components
/// <para>Can be accessed directly from the BaseBehaviour</para>
/// </summary>
[DefaultExecutionOrder(-90)]
public class ApplicationCore : MagmaFramework
{
	/// <summary>
	/// Static reference to this singleton. A property of BaseBehaviour as well
	/// </summary>
	public new static ApplicationCore Instance { get; private set; }
	public bool IsGamePaused { get; private set; } = false;

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

	/// < summary >
	/// Pause/unpause the game
	/// </summary>
	/// <param name="value">True is game pause, false is unpaused</param>
	/// <param name="force">This will ignore any conditional return and force that pause state</param>
	/// <param name="affectTimeScale">If set to true, this will set the time scale as well</param>
	public void PauseGame(bool value, bool affectTimeScale = false)
	{
		if(affectTimeScale) Time.timeScale = value ? 1 : 0.0000001f;
		IsGamePaused = value;
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
	/// Sets the game's display resolution
	/// </summary>
	/// <param name="resolution"></param>
	/// <param name="mode"></param>
	public void SetScreen(Resolution resolution, FullScreenMode mode)
	{
		if (Application.isEditor)
		{
			return;
		}

		Screen.SetResolution(resolution.width, resolution.height, mode, resolution.refreshRateRatio);
		Publisher.Publish(PublisherTopics.SYSTEM_RESERVED_SCREEN_RESOLUTION_SET, resolution, mode);
	}

	protected override void Awake()
	{
		base.Awake();

		if (!Singleton())
		{
			return;
		}
	}

	/// <summary>
	/// This ensures that there can only be one object of type ApplicationCore per scene
	/// </summary>
	private bool Singleton()
	{
		Debug.Log("Application core initializing...");

		if (Instance == null)
		{
			Instance = this;
			DontDestroyOnLoad(gameObject);
			return true;
		}
		else
		{
			Destroy(this.gameObject);
			Debug.LogWarning("Other Application core found ...keeping the old instance...");
			return false;
		}
	}
}
