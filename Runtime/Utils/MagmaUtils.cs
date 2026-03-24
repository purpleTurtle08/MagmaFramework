using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace MagmaFlow.Framework.Utils
{
	/// <summary>
	/// Add the script symbol MAGMA_FRAMEWORK_USE_NEWTONSOFT if you are using the newtosoft.json package
	/// </summary>
	public static partial class MagmaUtils
	{
		private static readonly Encoding DEFAULT_ENCODING = new UTF8Encoding(false);

		/// <summary>
		/// Divides one vector by another, component by component.
		/// This is the C# extension method equivalent to Vector3.Scale().
		/// </summary>
		/// <param name="a">The dividend.</param>
		/// <param name="b">The divisor.</param>
		/// <returns>A new Vector3 with the component-wise division.</returns>
		public static Vector3 Vector3_Divide(Vector3 a, Vector3 b)
		{
			return new Vector3(
				(b.x == 0) ? a.x : a.x / b.x,
				(b.y == 0) ? a.y : a.y / b.y,
				(b.z == 0) ? a.z : a.z / b.z
			);
		}

		/// <summary>
		/// Divides one vector by another, component by component.
		/// This is the C# extension method equivalent to Vector3.Scale().
		/// </summary>
		/// <param name="a">The dividend.</param>
		/// <param name="b">The divisor.</param>
		/// <returns>A new Vector3 with the component-wise division.</returns>
		public static Vector2 Vector2_Divide(Vector2 a, Vector2 b)
		{
			return new Vector3(
				(b.x == 0) ? a.x : a.x / b.x,
				(b.y == 0) ? a.y : a.y / b.y
			);
		}

		///<summary>
		/// Returns the time (in seconds) formatted.
		/// <para>Format if addLetters set TRUE = xx'H':xx'M':xx's'</para>
		/// <para>Format if addLetters set FALSE = xx:xx:xx</para>
		/// </summary>
		/// <param name="time"></param>
		/// <param name="addLetters"></param>
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

			UnityEngine.Vector3 result = new UnityEngine.Vector3(
				float.Parse(sArray[0], CultureInfo.InvariantCulture),
				float.Parse(sArray[1], CultureInfo.InvariantCulture),
				float.Parse(sArray[2], CultureInfo.InvariantCulture));

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
	}
}
