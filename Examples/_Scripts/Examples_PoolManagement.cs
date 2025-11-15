using MagmaFlow.Framework.Core;
using MagmaFlow.Framework.Pooling;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MagmaFlow.Framework.Examples
{

	public class Examples_PoolManagement : BaseBehaviour
	{
		[SerializeField] private Transform instantiatedAsset1_Parent;
		[SerializeField] private Transform instantiatedAsset2_Parent;


		[SerializeField] private AssetReferenceDictionaryBuilder assets;

		private void Start()
		{
			StartCoroutine(PrewarmAllAssetsRoutine());
		}
		private void Update()
		{
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
			// ───────────────────────────────
			// NEW INPUT SYSTEM ONLY
			// ───────────────────────────────
			if (UnityEngine.InputSystem.Keyboard.current.digit1Key.wasPressedThisFrame)
				PrewarmAsset1();

			if (UnityEngine.InputSystem.Keyboard.current.digit2Key.wasPressedThisFrame)
				PrewarmAsset2();

			if (UnityEngine.InputSystem.Keyboard.current.digit4Key.wasPressedThisFrame)
				ReleaseAll();

			if (UnityEngine.InputSystem.Keyboard.current.wKey.isPressed)
				InstantiatePooledObject_Asset2();

			if (UnityEngine.InputSystem.Keyboard.current.qKey.isPressed)
				InstantiatePooledObject_Asset1();

#elif ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
    // ───────────────────────────────
    // BOTH INPUT SYSTEMS ENABLED
    // Prefer new Input System, but you could add fallback below
    // ───────────────────────────────
    var keyboard = UnityEngine.InputSystem.Keyboard.current;
    if (keyboard != null)
    {
        if (keyboard.digit1Key.wasPressedThisFrame)
            PrewarmAsset1();

        if (keyboard.digit2Key.wasPressedThisFrame)
            PrewarmAsset2();

        if (keyboard.digit4Key.wasPressedThisFrame)
            ReleaseAll();

        if (keyboard.wKey.isPressed)
            InstantiatePooledObject_Asset2();

        if (keyboard.qKey.isPressed)
            InstantiatePooledObject_Asset1();
    }
    else
    {
        // Fallback to old system if Keyboard.current is null
        if (Input.GetKeyDown(KeyCode.Alpha1))
            PrewarmAsset1();

        if (Input.GetKeyDown(KeyCode.Alpha2))
            PrewarmAsset2();

        if (Input.GetKeyDown(KeyCode.Alpha4))
            ReleaseAll();

        if (Input.GetKey(KeyCode.W))
            InstantiatePooledObject_Asset2();

        if (Input.GetKey(KeyCode.Q))
            InstantiatePooledObject_Asset1();
    }

#elif !ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
    // ───────────────────────────────
    // LEGACY INPUT SYSTEM ONLY
    // ───────────────────────────────
    if (Input.GetKeyDown(KeyCode.Alpha1))
        PrewarmAsset1();

    if (Input.GetKeyDown(KeyCode.Alpha2))
        PrewarmAsset2();

    if (Input.GetKeyDown(KeyCode.Alpha4))
        ReleaseAll();

    if (Input.GetKey(KeyCode.W))
        InstantiatePooledObject_Asset2();

    if (Input.GetKey(KeyCode.Q))
        InstantiatePooledObject_Asset1();

#else
		// ───────────────────────────────
		// NO INPUT SYSTEM AVAILABLE
		// (very rare, but safe guard)
		// ───────────────────────────────
		// Do nothing or optionally log once:
		// Debug.LogWarning("No active input system configured in Project Settings > Player > Active Input Handling.");
#endif
		}
		/// <summary>
		/// Press key 1 to pre-warm this prefab
		/// </summary>
		private void PrewarmAsset1()
		{
			//Example of discaded pre-warm call, pre-warming 5000 prefabs
			_ = MagmaFramework_PooledObjectsManager.PrewarmPool(assets.GetAssetReference("Cube"), 5000);
		}
		/// <summary>
		/// Press key 2 to pre-warm this prefab
		/// </summary>
		private async void PrewarmAsset2()
		{
			//Example of awaited pre-warm call, pre-warming 5000 prefabs
			await MagmaFramework_PooledObjectsManager.PrewarmPool(assets.GetAssetReference("Sphere"), 5000);
		}
		/// <summary>
		/// Example of integrating a pre-warm pool call inside an IEnumerator
		/// </summary>
		/// <returns></returns>
		private IEnumerator PrewarmAllAssetsRoutine()
		{
			yield return new WaitForSeconds(1);
			yield return MagmaFramework_PooledObjectsManager.PrewarmPool(assets.GetAssetReference("Cube"), 1000);
			yield return new WaitForSeconds(1);
			yield return MagmaFramework_PooledObjectsManager.PrewarmPool(assets.GetAssetReference("Sphere"), 1000);
			yield return new WaitForEndOfFrame();
		}
		/// <summary>
		/// Press key 4.
		/// Returns all the objects into the pool.
		/// </summary>
		private void ReleaseAll()
		{
			MagmaFramework_PooledObjectsManager.ReleaseAllObjects();

			// If you want to learn how to release a single pooled object.
			// Check Examples_PooledObjectImplementation
		}
		/// <summary>
		/// Press key 'Q'
		/// Example of awaited instantiate pooled object call
		/// </summary>
		private async void InstantiatePooledObject_Asset1()
		{
			var position = Random.insideUnitSphere * Random.Range(1, 3);
			var instantiatedObjectComponent =
				await MagmaFramework_PooledObjectsManager.InstantiatePooledObject<Examples_PooledObjectImplementation>
				(assets.GetAssetReference("Cube"), position, Quaternion.identity, instantiatedAsset1_Parent, false);
			//Do something with the component
		}
		/// <summary>
		/// Press key 'W'
		/// Example of discarded pooled object call
		/// </summary>
		private void InstantiatePooledObject_Asset2()
		{
			var position = Random.insideUnitSphere * Random.Range(1, 3);
			_ = MagmaFramework_PooledObjectsManager.InstantiatePooledObject<Examples_PooledObjectImplementation>
				(assets.GetAssetReference("Sphere"), position, Quaternion.identity, instantiatedAsset2_Parent, false);
		}
	}
}
