using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace MagmaFlow.Framework.Utils
{
	public static class MagmaUtils
	{
		/// <summary>
		/// Returns the time(in seconds) formatted. Checks if hours <= 0
		/// <para>hh"h":mm"m":ss"s" if addLetters set TRUE</para>
		/// <para>hh:mm:ss if addLetters set FALSE</para>
		/// </summary>
		/// <param name="time"></param>
		/// <returns></returns>
		public static string GetTimeFormatted(int time, bool addLetters = false)
		{
			string formattedResult = "";

			TimeSpan t = TimeSpan.FromSeconds(time);

			if (t.Hours <= 0)
			{
				formattedResult = addLetters ? string.Format("{0:D2}m:{1:D2}s", t.Minutes, t.Seconds) : string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
			}
			else
			{
				formattedResult = addLetters ? string.Format("{0:D2}h:{1:D2}m:{2:D2}s", t.Hours, t.Minutes, t.Seconds) : string.Format("{0:D2}:{1:D2}:{2:D2}", t.Hours, t.Minutes, t.Seconds);
			}
			return formattedResult;
		}

		/// <summary>
		/// Format a date time string to universal time, en-US culture standard
		/// </summary>
		/// <param name="dateTime"></param>
		/// <returns></returns>
		public static DateTime FormatDateTimeToUTC(string dateTime)
		{
			DateTime dateToReturn;
			var dateTimeStyle = DateTimeStyles.AssumeLocal;
			IFormatProvider format = CultureInfo.InvariantCulture;

			DateTime.TryParse(dateTime, format, dateTimeStyle, out dateToReturn);

			var convertedDateToUTC = dateToReturn.ToUniversalTime();

			return convertedDateToUTC;
		}

		/// <summary>
		/// Returns a Vector3 from a string
		/// </summary>
		/// <param name="vector3"></param>
		/// <returns></returns>
		public static UnityEngine.Vector3 StringToVector3(string vector3)
		{
			// Remove the parentheses
			if (vector3.StartsWith("(") && vector3.EndsWith(")"))
			{
				vector3 = vector3.Substring(1, vector3.Length - 2);
			}

			// split the items
			string[] sArray = vector3.Split(',');

			// store as a Vector3
			UnityEngine.Vector3 result = new UnityEngine.Vector3(
				float.Parse(sArray[0]),
				float.Parse(sArray[1]),
				float.Parse(sArray[2]));

			return result;
		}

		/// <summary>
		/// Converts the hexadecimal color code to color
		/// </summary>
		/// <param name="hexa"></param>
		/// <param name="color"></param>
		/// <returns></returns>
		public static Color HexaToColor(string hexa, out Color color)
		{
			if (ColorUtility.TryParseHtmlString(hexa, out color))
			{
				return color;
			}

			color = Color.white; // fallback
			return color;
		}

#if UNITY_EDITOR
		/// <summary>
		/// Clears the console
		/// NOT RECOMMENDED TO CALL EVERY FRAME
		/// </summary>
		public static void ClearConsole()
		{
			var logEntries = System.Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
			var clearMethod = logEntries.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);
			clearMethod.Invoke(null, null);
		}
#endif

		/// <summary>
		/// This only works for a layer mask that contains a single layer
		/// </summary>
		/// <param name="mask"></param>
		/// <returns></returns>
		public static int LayerMaskToLayer(LayerMask mask)
		{
			int bitmask = mask.value;

			if (bitmask == 0 || (bitmask & (bitmask - 1)) != 0)
			{
				Debug.LogError("LayerMask must have exactly one bit set.");
				return -1; // or throw an exception
			}

			return (int)Mathf.Log(bitmask, 2);
		}
	}
}
