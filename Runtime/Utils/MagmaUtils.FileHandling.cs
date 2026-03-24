using System;
using System.IO;
using System.Threading.Tasks;

namespace MagmaFlow.Framework.Utils
{
    public partial class MagmaUtils
    {
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
				var result = await File.ReadAllTextAsync(filePath, DEFAULT_ENCODING);
				return result;
			}
			catch (Exception ex)
			{
				LogError($"[FileUtil] Failed to read file '{filePath}': {ex}");
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
				LogError($"[FileUtil] Failed to read bytes from file '{filePath}': {ex}");
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
				LogError($"[FileUtil] Failed to safely write file '{filePath}': {ex}");

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
				LogError($"[FileUtil] Failed to safely write file '{filePath}': {ex}");

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
				LogError($"[FileUtil] Failed to delete file '{path}': {ex}");
			}
		}
		#endregion

		#region Synchronous I/O logic

		/// <summary>
		/// Synchronously reads a file and returns a string
		/// </summary>
		public static string ReadFromFileSync(string filePath)
		{
			if (string.IsNullOrEmpty(filePath))
				throw new ArgumentNullException(nameof(filePath));

			if (!File.Exists(filePath))
				return string.Empty;

			try
			{
				return File.ReadAllText(filePath, DEFAULT_ENCODING);
			}
			catch (Exception ex)
			{
				LogError($"[FileUtil] Failed to read file '{filePath}': {ex}");
				return null;
			}
		}

		/// <summary>
		/// Synchronously reads a file and returns its raw bytes
		/// </summary>
		public static byte[] ReadFromFileAsByteArraySync(string filePath)
		{
			if (string.IsNullOrEmpty(filePath))
				throw new ArgumentNullException(nameof(filePath));

			if (!File.Exists(filePath))
				return Array.Empty<byte>();

			try
			{
				return File.ReadAllBytes(filePath);
			}
			catch (Exception ex)
			{
				LogError($"[FileUtil] Failed to read bytes from file '{filePath}': {ex}");
				return null;
			}
		}

		/// <summary>
		/// Synchronously creates a temporary file to safely write bytes, preventing corruption on crash.
		/// </summary>
		public static bool WriteBytesToFileSync(string filePath, byte[] data)
		{
			if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
			if (data == null) throw new ArgumentNullException(nameof(data));

			string directory = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				Directory.CreateDirectory(directory);

			string tempFile = $"{filePath}.tmp";

			try
			{
				File.WriteAllBytes(tempFile, data);
				File.Copy(tempFile, filePath, overwrite: true);
				File.Delete(tempFile);
				return true;
			}
			catch (Exception ex)
			{
				LogError($"[FileUtil] Failed to safely write file '{filePath}': {ex}");
				try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
				return false;
			}
		}

		/// <summary>
		/// Synchronously creates a temporary file to safely write text, preventing corruption on crash.
		/// </summary>
		public static bool WriteToFileSync(string filePath, string text)
		{
			if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));

			string directory = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				Directory.CreateDirectory(directory);

			string tempFile = $"{filePath}.tmp";

			try
			{
				File.WriteAllText(tempFile, text, DEFAULT_ENCODING);
				File.Copy(tempFile, filePath, overwrite: true);
				File.Delete(tempFile);
				return true;
			}
			catch (Exception ex)
			{
				LogError($"[FileUtil] Failed to safely write file '{filePath}': {ex}");
				try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
				return false;
			}
		}

		#endregion
	}
}
