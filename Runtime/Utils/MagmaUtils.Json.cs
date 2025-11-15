#if MAGMA_FRAMEWORK_USE_NEWTONSOFT

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace MagmaFlow.Framework.Utils
{
	/// <summary>
	/// Add 'MAGMA_FRAMEWORK_USE_NEWTONSOFT' if you have Newtonsoft.Json package installed in order to use this utility.
	/// </summary>
	public static partial class MagmaUtils
	{
		/// <summary>
		/// <para>[REQUIRES NEWTONSOFT.JSON]</para>
		/// Serializes an object to a compressed UTF-8 JSON byte array.
		/// </summary>
		public static byte[] ObjectToByteArray(System.Object obj)
		{
			if (obj == null)
				throw new ArgumentNullException(nameof(obj));

			try
			{
				string json = JsonConvert.SerializeObject(obj);
				byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

				using var output = new MemoryStream();
				using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
				{
					gzip.Write(jsonBytes, 0, jsonBytes.Length);
				} // disposing flushes automatically

				return output.ToArray();
			}
			catch (Exception ex)
			{
				Debug.LogError($"[SerializationUtil] Failed to serialize object: {ex}");
				return null;
			}
		}

		/// <summary>
		/// <para>[REQUIRES NEWTONSOFT.JSON]</para>
		/// Decompresses and deserializes an object from a compressed UTF-8 JSON byte array.
		/// </summary>
		public static T ByteArrayToObject<T>(byte[] data) where T : class
		{
			if (data == null || data.Length == 0)
				throw new ArgumentNullException(nameof(data));

			try
			{
				using var input = new MemoryStream(data);
				using var gzip = new GZipStream(input, CompressionMode.Decompress);
				using var output = new MemoryStream();

				gzip.CopyTo(output);

				string json = Encoding.UTF8.GetString(output.ToArray());
				return JsonConvert.DeserializeObject<T>(json);
			}
			catch (Exception ex)
			{
				Debug.LogError($"[SerializationUtil] Failed to deserialize bytes to object: {ex}");
				return null;
			}
		}
	}
}

#endif