using UnityEngine;
using System.Diagnostics;

namespace MagmaFlow.Framework.Utils
{
	/// <summary>
	/// A collection of utility methods for the MagmaFlow Framework.
	/// This partial class handles garbage-free logging that is completely stripped from standard release builds.
	/// </summary>
	public static partial class MagmaUtils
	{
		/// <summary>
		/// Logs a message to the Unity Console.
		/// This method is conditionally compiled and will be stripped from non-development release builds, generating zero garbage.
		/// </summary>
		/// <param name="message">String or object to be converted to string representation for display.</param>
		/// <param name="context">Optional object to which the message applies. Clicking the log in the console will highlight this object.</param>
		[Conditional("UNITY_EDITOR")]
		[Conditional("DEVELOPMENT_BUILD")]
		public static void Log(object message, Object context = null)
		{
			UnityEngine.Debug.Log(message, context);
		}

		/// <summary>
		/// Logs a warning message to the Unity Console.
		/// This method is conditionally compiled and will be stripped from non-development release builds.
		/// </summary>
		/// <param name="message">String or object to be converted to string representation for display.</param>
		/// <param name="context">Optional object to which the message applies. Clicking the log in the console will highlight this object.</param>
		[Conditional("UNITY_EDITOR")]
		[Conditional("DEVELOPMENT_BUILD")]
		public static void LogWarning(object message, Object context = null)
		{
			UnityEngine.Debug.LogWarning(message, context);
		}

		/// <summary>
		/// Logs an error message to the Unity Console.
		/// This method is conditionally compiled and will be stripped from non-development release builds.
		/// </summary>
		/// <param name="message">String or object to be converted to string representation for display.</param>
		/// <param name="context">Optional object to which the message applies. Clicking the log in the console will highlight this object.</param>
		[Conditional("UNITY_EDITOR")]
		[Conditional("DEVELOPMENT_BUILD")]
		public static void LogError(object message, Object context = null)
		{
			UnityEngine.Debug.LogError(message, context);
		}

		/// <summary>
		/// Logs a framework-specific formatted message to the Unity Console.
		/// Includes a rich text [MagmaFlow] prefix for easy filtering and visibility.
		/// This method is conditionally compiled and will be stripped from non-development release builds.
		/// </summary>
		/// <param name="message">The framework message to display.</param>
		/// <param name="context">Optional object to which the message applies. Clicking the log in the console will highlight this object.</param>
		[Conditional("UNITY_EDITOR")]
		[Conditional("DEVELOPMENT_BUILD")]
		public static void LogFramework(object message, Object context = null)
		{
			UnityEngine.Debug.Log($"<b><color=#FF5733>[MagmaFlow]</color></b> {message}", context);
		}
	}
}