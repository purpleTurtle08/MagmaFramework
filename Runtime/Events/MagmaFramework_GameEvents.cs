namespace MagmaFlow.Framework.Events
{
	/// <summary>
	/// Published by MagmaFramework_Core.PauseGame(), every time it's called (pausing or unpausing).
	/// <para>BaseBehaviours receive it through OnGamePaused(); anything else can subscribe via MagmaFramework_EventBus.</para>
	/// </summary>
	public readonly struct GamePausedEvent
	{
		/// <summary>
		/// True if the game was paused, false if it was unpaused.
		/// </summary>
		public readonly bool IsPaused;
		/// <summary>
		/// True if PauseGame() also changed Time.timeScale (0 when paused, 1 when unpaused).
		/// </summary>
		public readonly bool WasTimeScaleAffected;

		///<summary>
		/// <param name="isPaused">True if the game was paused, false if it was unpaused.</param>
		/// <param name="wasTimeScaleAffected">True if Time.timeScale was changed as well.</param>
		/// </summary>
		public GamePausedEvent(bool isPaused, bool wasTimeScaleAffected)
		{
			IsPaused = isPaused;
			WasTimeScaleAffected = wasTimeScaleAffected;
		}
	}
}
