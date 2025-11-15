using MagmaFlow.Framework.Core;
using MagmaFlow.Framework.Events;
using MagmaFlow.Framework.Utils;
using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MagmaFlow.Framework.Examples
{

	public struct ExampleEvent
	{
		/// <summary>
		/// Random event data
		/// </summary>
		public string Message;
		/// <summary>
		/// Random event data
		/// </summary>
		public string TimeSinceRuntimeStart;

		public ExampleEvent(string message, string timeSinceRuntimeStart)
		{
			Message = message;
			TimeSinceRuntimeStart = timeSinceRuntimeStart;
		}

		// You could have anything / any data inside an event.
		// Such as a PlayerController or GameObject reference
	}

	/// <summary>
	/// This class demonstrates the use of the global event handler, called MagmaFramework_EventBus.
	/// 
	/// </summary>
	public class Examples_GlobalEventHandling : BaseBehaviour
	{
		[SerializeField] private AssetReference fakeParticleAsset;

		private Coroutine launchParticlesRoutine;

		private void Update()
		{
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
			// ───────────────────────────────
			// NEW INPUT SYSTEM ONLY
			// ───────────────────────────────
			if (UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
				Publish_ExampleEvent();
#elif ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
    // ───────────────────────────────
    // BOTH INPUT SYSTEMS ENABLED
    // Prefer new Input System, but you could add fallback below
    // ───────────────────────────────
    var keyboard = UnityEngine.InputSystem.Keyboard.current;
    if (keyboard != null)
    {
	    if (keyboard.eKey.wasPressedThisFrame)
			Publish_ExampleEvent();
	}
	    else
    {
        // Fallback to old system if Keyboard.current is null
		if (Input.GetKeyDown(KeyCode.E))
			Publish_ExampleEvent();
	}
#elif !ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
    // ───────────────────────────────
    // LEGACY INPUT SYSTEM ONLY
    // ───────────────────────────────
    if (Input.GetKeyDown(KeyCode.E))
        Publish_ExampleEvent();
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
		/// We want to subscribe to the event whenever this component is enabled.
		/// This is the best practice implementation to avoid leaks.
		/// </summary>
		private void OnEnable()
		{
			MagmaFramework_EventBus.Subscribe<ExampleEvent>(Subscriber_OnExampleEvent);
		}
		/// <summary>
		/// We want to un-subscribe from the event when this component is disabled.
		/// This is the best practice implementation to avoid leaks.
		/// </summary>
		private void OnDisable()
		{
			//We want to make sure that we properly stop the coroutine, even though Unity should handle this.
			StopLaunchingParticles();

			MagmaFramework_EventBus.Unsubscribe<ExampleEvent>(Subscriber_OnExampleEvent);
		}

		/// <summary>
		/// This method will act as our 'Subscriber' meaning it will be called when the event of type ExampleEvent is fired
		/// </summary>
		/// <param name="eventData"></param>
		private void Subscriber_OnExampleEvent(ExampleEvent eventData)
		{
			if (launchParticlesRoutine != null)
			{
				StopLaunchingParticles();
				return;
			}
			launchParticlesRoutine = StartCoroutine(LaunchParticlesRoutine());
			Debug.Log($"Event fired at {eventData.TimeSinceRuntimeStart} with message {eventData.Message}");
		}

		private void Publish_ExampleEvent()
		{
			string timeSinceRuntimeStart = MagmaUtils.GetTimeFormatted((int)Time.time, true);
			MagmaFramework_EventBus.Publish(new ExampleEvent("Firing some particles!", timeSinceRuntimeStart));
		}

		private void StopLaunchingParticles()
		{
			if (launchParticlesRoutine != null)
			{
				StopCoroutine(launchParticlesRoutine);
				Debug.Log("Firing particles stopped!");
				launchParticlesRoutine = null;
			}
		}

		/// <summary>
		/// Launch particles example, as an IEnumerator.
		/// You could also 
		/// </summary>
		private IEnumerator LaunchParticlesRoutine()
		{
			for (int i = 0; i < 200; i++)
			{
				// Start async task
				var task = MagmaFramework_PooledObjectsManager.InstantiatePooledObject<Examples_PhysicsPooledObject>(
					fakeParticleAsset, transform, false);

				// Wait until task completes
				yield return new WaitUntil(() => task.IsCompleted);

				// Now safely access the result
				var instantiatedObject = task.Result;
				instantiatedObject.Launch();

				yield return new WaitForSeconds(.05f);
			}

			launchParticlesRoutine = null;
		}
	}
}
