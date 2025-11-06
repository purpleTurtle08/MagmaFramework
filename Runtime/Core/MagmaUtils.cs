using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MagmaFlow.Framework.Utils
{
	public static partial class MagmaUtils
	{
		private static readonly Encoding DefaultEncoding = new UTF8Encoding(false);
		
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

		#region I/O logic
		/// <summary>
		/// Reads a file and returns a strings
		/// </summary>
		/// <param name="filePath"></param>
		/// <returns></returns>
		public static async Task<string> ReadFromFile(string filePath)
		{
			if (string.IsNullOrEmpty(filePath))
				throw new ArgumentNullException(nameof(filePath));

			if (!File.Exists(filePath))
				return string.Empty;

			try
			{
				var result = await File.ReadAllTextAsync(filePath, DefaultEncoding);
				return result;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[FileUtil] Failed to read file '{filePath}': {ex}");
				return null;
			}
		}

		/// <summary>
		/// Reads a file and returns its raw bytes
		/// </summary>
		public static async Task<byte[]> ReadFromFileAsByteArray(string filePath)
		{
			if (string.IsNullOrEmpty(filePath))
				throw new ArgumentNullException(nameof(filePath));

			if (!File.Exists(filePath))
				return Array.Empty<byte>();

			try
			{
				var readTask = await File.ReadAllBytesAsync(filePath);
				return readTask;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[FileUtil] Failed to read bytes from file '{filePath}': {ex}");
				return null;
			}
		}

		/// <summary>
		/// Creates a temporary file to write data to, ensuring that the file that is written will not be corrupted in case the task fails
		/// </summary>
		/// <param name="filePath"></param>
		/// <param name="data"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentNullException"></exception>
		public static async Task<bool> WriteBytesToFile(string filePath, byte[] data)
		{
			if (string.IsNullOrEmpty(filePath))
				throw new ArgumentNullException(nameof(filePath));

			if (data == null)
				throw new ArgumentNullException(nameof(data));

			string directory = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				Directory.CreateDirectory(directory);

			string tempFile = $"{filePath}.tmp";

			try
			{
				// Write to temp file
				await File.WriteAllBytesAsync(tempFile, data);

				// Replace existing file atomically (or create if it doesn't exist)
				File.Copy(tempFile, filePath, overwrite: true);

				// Cleanup temp file
				File.Delete(tempFile);

				return true;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[FileUtil] Failed to safely write file '{filePath}': {ex}");

				// Try cleaning up the temp file on failure
				try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }

				return false;
			}
		}

		/// <summary>
		/// Creates a temporary file to write data to, ensuring that the file that is written will not be corrupted in case the task fails
		/// </summary>
		/// <param name="filePath"></param>
		/// <param name="text"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentNullException"></exception>
		public static async Task<bool> WriteToFile(string filePath, string text)
		{
			if (string.IsNullOrEmpty(filePath))
				throw new ArgumentNullException(nameof(filePath));

			string directory = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				Directory.CreateDirectory(directory);

			string tempFile = $"{filePath}.tmp";

			try
			{
				// Write to temp file
				await File.WriteAllTextAsync(tempFile, text);

				// Replace existing file atomically (or create if it doesn't exist)
				File.Copy(tempFile, filePath, overwrite: true);

				// Cleanup temp file
				File.Delete(tempFile);

				return true;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[FileUtil] Failed to safely write file '{filePath}': {ex}");

				// Try cleaning up the temp file on failure
				try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }

				return false;
			}
		}

		/// <summary>
		/// Checks if the file exists and deletes it
		/// </summary>
		/// <param name="path"></param>
		public static void DeleteFile(string path)
		{ 
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FileUtil] Failed to delete file '{path}': {ex}");
            }
		}
		#endregion

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
