using MagmaFlow.Framework.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace MagmaFlow.Framework.Events
{
	/// <summary>
	/// A fast, strictly managed Event Bus.
	/// <para>MAIN THREAD ONLY: Subscribe / Unsubscribe / Publish must be called from Unity's main thread,
	/// like most Unity APIs (subscribers run on the publishing thread, and usually touch Unity objects).
	/// From a background thread, hand the event over to the main thread first. Editor / development builds log an error otherwise.</para>
	/// GOLDEN RULE: You MUST unsubscribe from events when your object is destroyed.
	/// </summary>
	public static class MagmaFramework_EventBus
	{
		/// <summary>
		/// One static channel per event type: the runtime creates a separate copy of this class for each T,
		/// so finding the subscribers of an event is a single field read (no dictionary lookup, no cast).
		/// </summary>
		private static class Channel<T> where T : struct
		{
			public static Action<T> Handlers;

			// Runs once per event type, on first use: lets ClearAllEvents() reach every channel
			static Channel()
			{
				s_channelResets.Add(() => Handlers = null);
			}
		}

		// One reset action per event type that has been used so far
		private static readonly List<Action> s_channelResets = new List<Action>();

		// Captured on startup, used by the development-only thread check
		private static int s_mainThreadId;

		/// <summary>
		/// Runs on the main thread at the start of every play session, before any scene object's Awake.
		/// <para>With 'Enter Play Mode Options' and domain reload disabled, statics survive between play sessions,
		/// so subscribers that were never unsubscribed would otherwise carry over (and run on destroyed objects).</para>
		/// </summary>
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void OnRuntimeInitialize()
		{
			s_mainThreadId = Thread.CurrentThread.ManagedThreadId;
			ClearAllEvents();
		}

		/// <summary>
		/// Removes every subscriber of every event.
		/// </summary>
		public static void ClearAllEvents()
		{
			AssertMainThread(nameof(ClearAllEvents));
			for (int i = 0; i < s_channelResets.Count; i++)
			{
				s_channelResets[i]();
			}
		}

		/// <summary>
		/// Subscribes the callback to events of type T. Subscribing the same callback twice makes it run twice.
		/// <para>Subscribing during a Publish() of the same event takes effect from the next Publish().</para>
		/// </summary>
		public static void Subscribe<T>(Action<T> callback) where T : struct
		{
			AssertMainThread(nameof(Subscribe));
			Channel<T>.Handlers += callback;
		}

		/// <summary>
		/// Unsubscribes the callback from events of type T (one subscription, if it was subscribed more than once).
		/// <para>Unsubscribing during a Publish() of the same event takes effect from the next Publish().</para>
		/// </summary>
		public static void Unsubscribe<T>(Action<T> callback) where T : struct
		{
			AssertMainThread(nameof(Unsubscribe));
			Channel<T>.Handlers -= callback;
		}

		/// <summary>
		/// Invokes every subscriber of events of type T. Generates no garbage.
		/// </summary>
		public static void Publish<T>(T eventData) where T : struct
		{
			AssertMainThread(nameof(Publish));

			// Delegates are immutable: subscribing / unsubscribing inside a subscriber doesn't affect this invocation
			var handlers = Channel<T>.Handlers;
			if (handlers == null) return;

			try
			{
				handlers.Invoke(eventData);
			}
			catch (Exception e)
			{
				MagmaUtils.LogError($"Error in EventBus subscriber for event: -{typeof(T).Name}-");
				MagmaUtils.LogException(e);
			}
		}

		/// <summary>
		/// Logs an error when called off the main thread. Stripped from release builds.
		/// </summary>
		[Conditional("UNITY_EDITOR")]
		[Conditional("DEVELOPMENT_BUILD")]
		private static void AssertMainThread(string method)
		{
			// 0 = not captured yet (e.g. called in Edit Mode before entering Play Mode); skip the check
			if (s_mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId != s_mainThreadId)
			{
				MagmaUtils.LogError($"MagmaFramework_EventBus.{method}() called from a background thread. The event bus is main-thread only.");
			}
		}
	}
}
