using System;

namespace MagmaFlow.Framework.Publishing
{
	public static class PublisherTopics
	{
		#region SYSTEM_RESERVED (DO NOT REMOVE THESE)
		/// <summary>
		/// <para>Scene</para>
		/// <para>LoadSceneMode</para>
		/// </summary>
		public static Tuple<string,bool> SYSTEM_RESERVED_ON_SCENE_LOADED = new Tuple<string, bool>("SYSTEM_RESERVED_SCENELOADED", false);
		/// <summary>
		/// <para>Scene</para>
		/// </summary>
		public static Tuple<string, bool> SYSTEM_RESERVED_ON_SCENE_UNLOADED = new Tuple<string, bool>("SYSTEM_RESERVED_SCENEUNLOADED", false);
		/// <summary>
		/// <para>Resolution</para>
		/// <para>FullScreenMode</para>
		/// </summary>
		public static Tuple<string, bool> SYSTEM_RESERVED_SCREEN_RESOLUTION_SET = new Tuple<string, bool>("SYSTEM_RESERVED_SCREENSETRESOL", true);
		#endregion SYSTEM_RESERVED (DO NOT REMOVE THESE)
	}
}
