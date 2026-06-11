namespace MagmaFlow.Framework.Events
{	
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
