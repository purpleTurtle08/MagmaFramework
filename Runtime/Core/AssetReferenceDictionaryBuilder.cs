using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MagmaFlow.Framework.Core
{
	/// <summary>
	/// ScriptableObject that maps prefab names (keys) to Addressables AssetReference objects (values).
	/// Use the inspector to populate AssetReferences; the inspector will derive and persist the keys (names)
	/// so the dictionary can be reconstructed at runtime in builds.
	/// </summary>
	[CreateAssetMenu(fileName = "AssetReferenceDictionaryBuilder", menuName = "Scriptable Objects/MagmaFramework/Asset Reference Dictionary Builder")]
	public class AssetReferenceDictionaryBuilder : ScriptableObject
	{
		[SerializeField, Tooltip("Drag & drop all the AssetReference entries you want in this dictionary.")]
		private AssetReference[] assetReferences = Array.Empty<AssetReference>();

		// Persisted keys (prefab / asset names) that correspond by index to assetReferences.
		// This allows rebuilding the dictionary at runtime without editor-only APIs.
		[SerializeField, Tooltip("Automatically populated keys (asset names). Do not edit manually.")]
		private string[] generatedKeys = Array.Empty<string>();

		// Runtime dictionary (not serialized)
		private readonly Dictionary<string, AssetReference> assetReferencesDictionary = new(StringComparer.Ordinal);

		/// <summary>
		/// Read-only view of the mapping from prefab/asset name to AssetReference.
		/// </summary>
		public IReadOnlyDictionary<string, AssetReference> AssetReferences => assetReferencesDictionary;

		/// <summary>
		/// Called in editor when values change. Validates and populates the persistent keys array.
		/// This runs only in the editor.
		/// </summary>
#if UNITY_EDITOR
		private void OnValidate()
		{
			if (Application.isPlaying) return;

			if (assetReferences == null || assetReferences.Length == 0)
			{
				generatedKeys = Array.Empty<string>();
				assetReferencesDictionary.Clear();
				return;
			}

			var seenNames = new HashSet<string>(StringComparer.Ordinal);
			var seenGuids = new HashSet<string>(StringComparer.Ordinal);
			var newKeys = new List<string>(assetReferences.Length);

			bool hasError = false;

			for (int i = 0; i < assetReferences.Length; i++)
			{
				var _assetReference = assetReferences[i];

				if (_assetReference == null)
				{
					Debug.LogError($"[{name}] AssetReference at index {i} is null. Remove or replace it.", this);
					hasError = true;
					newKeys.Add(string.Empty);
					continue;
				}

				string guid = _assetReference.AssetGUID ?? string.Empty;
				string editorName = string.Empty;

				// editorAsset is editor-only; still access it safely in editor
				var editorAsset = _assetReference.editorAsset;
				if (editorAsset != null)
					editorName = editorAsset.name ?? string.Empty;

				// Prefer editor name (if available) otherwise fallback to the runtime key or guid
				string key = !string.IsNullOrEmpty(editorName) ? editorName : _assetReference.SubObjectName ?? guid;

				if (string.IsNullOrEmpty(key))
				{
					Debug.LogError($"[{name}] Could not determine a key/name for AssetReference at index {i}. AssetGUID: {guid}", this);
					hasError = true;
					newKeys.Add(string.Empty);
					continue;
				}

				// Duplicate GUID check
				if (!string.IsNullOrEmpty(guid) && !seenGuids.Add(guid))
				{
					Debug.LogError($"[{name}] Duplicate AssetGUID detected for asset '{key}' (index {i}). GUID: {guid}", this);
					hasError = true;
				}

				// Duplicate name check
				if (!seenNames.Add(key))
				{
					Debug.LogError($"[{name}] Duplicate asset name detected: '{key}'. Consider renaming one of the assets or using unique keys.", this);
					hasError = true;
				}

				newKeys.Add(key);
			}

			if (hasError)
			{
				Debug.LogError($"[{name}] Failed to build asset reference mapping due to errors above. Fix the inspector entries.", this);
				// Do not persist invalid keys
				return;
			}

			generatedKeys = newKeys.ToArray();

			// Build ephemeral runtime dictionary for editor convenience
			assetReferencesDictionary.Clear();
			for (int i = 0; i < assetReferences.Length; i++)
			{
				var k = generatedKeys[i];
				var v = assetReferences[i];
				if (!string.IsNullOrEmpty(k) && v != null)
				{
					assetReferencesDictionary[k] = v;
				}
			}
#if UNITY_EDITOR
			Debug.Log($"\u2705 Asset reference dictionary '{name}' validated! Keys persisted - {generatedKeys.Length} entries.", this);
#endif
		}
#endif

		/// <summary>
		/// Ensure dictionary is re-built at runtime from the serialized keys + assetReferences.
		/// OnEnable runs in editor and runtime; guard editor-only behavior where needed.
		/// </summary>
		private void OnEnable()
		{
			RebuildRuntimeDictionary();
		}

		/// <summary>
		/// Because we can't access the asset name at runtime, we use the stored keys to rebuild the dictionary.
		/// </summary>
		private void RebuildRuntimeDictionary()
		{
			assetReferencesDictionary.Clear();

			if (assetReferences == null || generatedKeys == null) return;

			int count = Math.Min(assetReferences.Length, generatedKeys.Length);
			for (int i = 0; i < count; i++)
			{
				var _key = generatedKeys[i];
				var _assetReference = assetReferences[i];
				if (_assetReference == null || string.IsNullOrEmpty(_key)) continue;

				// If duplicate key somehow exists, prefer the first and warn.
				if (assetReferencesDictionary.ContainsKey(_key))
				{
#if UNITY_EDITOR
					Debug.LogWarning($"[{name}] Duplicate key '{_key}' found while rebuilding runtime dictionary. Skipping index {i}.", this);
#endif
					continue;
				}

				assetReferencesDictionary[_key] = _assetReference;
			}
		}

		/// <summary>
		/// Try to get an AssetReference by prefab/asset name.
		/// </summary>
		/// <param name="assetName"></param>
		/// <param name="assetReference"></param>
		/// <returns>Returns true if asset found.</returns>
		public bool TryGetAssetReference(string assetName, out AssetReference assetReference)
		{
			if (string.IsNullOrEmpty(assetName))
			{
				assetReference = null;
				return false;
			}

			return assetReferencesDictionary.TryGetValue(assetName, out assetReference);
		}

		/// <summary>
		/// Get an AssetReference by name or null if not found. Optionally log an error.
		/// </summary>
		/// <param name="assetName"></param>
		/// <param name="logIfMissing"></param>
		/// <returns></returns>
		public AssetReference GetAssetReference(string assetName, bool logIfMissing = true)
		{
			if (TryGetAssetReference(assetName, out var ar))
				return ar;

#if UNITY_EDITOR
			Debug.LogError($"[{name}] Asset '{assetName}' not present in the references dictionary.", this);
#endif
			return null;
		}
	}
}
