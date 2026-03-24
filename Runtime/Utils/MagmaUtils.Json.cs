#if MAGMA_FRAMEWORK_USE_NEWTONSOFT

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;

namespace MagmaFlow.Framework.Utils
{
	public static partial class MagmaUtils
	{
		/// <summary>
		/// <para>[REQUIRES NEWTONSOFT.JSON]</para>
		/// Serializes an object to a compressed UTF-8 JSON byte array with zero string allocations.
		/// </summary>
		public static byte[] ObjectToByteArray(object obj)
		{
			if (obj == null)
				throw new ArgumentNullException(nameof(obj));

			try
			{
				using var output = new MemoryStream();

				// Chain the streams together. 
				// The leaveOpen flag is crucial so GZipStream doesn't close our MemoryStream before we read it!
				using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
				using (var streamWriter = new StreamWriter(gzip, Encoding.UTF8))
				using (var jsonWriter = new JsonTextWriter(streamWriter))
				{
					var serializer = new JsonSerializer();
					serializer.Serialize(jsonWriter, obj);
				} // Disposing these flushes the compressor automatically

				return output.ToArray();
			}
			catch (Exception ex)
			{
				LogError($"[SerializationUtil] Failed to serialize object: {ex}");
				return null;
			}
		}

		/// <summary>
		/// <para>[REQUIRES NEWTONSOFT.JSON]</para>
		/// Decompresses and deserializes an object from a compressed UTF-8 JSON byte array seamlessly.
		/// </summary>
		public static T ByteArrayToObject<T>(byte[] data) where T : class
		{
			if (data == null || data.Length == 0)
				throw new ArgumentNullException(nameof(data));

			try
			{
				// Read directly from the byte array, through the decompressor, straight into Newtonsoft
				using var input = new MemoryStream(data);
				using var gzip = new GZipStream(input, CompressionMode.Decompress);
				using var streamReader = new StreamReader(gzip, Encoding.UTF8);
				using var jsonReader = new JsonTextReader(streamReader);

				var serializer = new JsonSerializer();
				return serializer.Deserialize<T>(jsonReader);
			}
			catch (Exception ex)
			{
				LogError($"[SerializationUtil] Failed to deserialize bytes to object: {ex}");
				return null;
			}
		}
	}
}

#endif