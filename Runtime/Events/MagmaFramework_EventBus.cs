using MagmaFlow.Framework.Utils;
using System;
using System.Collections.Concurrent;

namespace MagmaFlow.Framework.Events
{
	/// <summary>
	/// A thread-safe, lightning-fast, strictly managed Event Bus.
	/// GOLDEN RULE: You MUST unsubscribe from events when your object is destroyed.
	/// </summary>
	public static class MagmaFramework_EventBus
	{
		// Thread-safe dictionary mapped directly to a Multicast Delegate
		private static readonly ConcurrentDictionary<Type, Delegate> s_eventRegistry = new ConcurrentDictionary<Type, Delegate>();

		public static void ClearAllEvents()
		{
			s_eventRegistry.Clear();
		}

		public static void Subscribe<T>(Action<T> callback) where T : struct
		{
			Type eventType = typeof(T);

			// AddOrUpdate is natively thread-safe. 
			// If the key doesn't exist, it adds 'callback'. 
			// If it DOES exist, it runs Delegate.Combine.
			s_eventRegistry.AddOrUpdate(
				eventType,
				callback,
				(key, existingDelegate) => Delegate.Combine(existingDelegate, callback)
			);
		}

		public static void Unsubscribe<T>(Action<T> callback) where T : struct
		{
			Type eventType = typeof(T);

			if (s_eventRegistry.TryGetValue(eventType, out var existingDelegate))
			{
				var updatedDelegate = Delegate.Remove(existingDelegate, callback);

				if (updatedDelegate == null)
				{
					// Safely attempt to remove the key if the delegate is now empty
					s_eventRegistry.TryRemove(eventType, out _);
				}
				else
				{
					// Update the registry with the remaining subscribers
					s_eventRegistry[eventType] = updatedDelegate;
				}
			}
		}

		public static void Publish<T>(T eventData) where T : struct
		{
			Type eventType = typeof(T);

			if (s_eventRegistry.TryGetValue(eventType, out var existingDelegate))
			{
				try
				{
					// Safely invoke all subscribers
					(existingDelegate as Action<T>)?.Invoke(eventData);
				}
				catch (Exception e)
				{
					MagmaUtils.LogError($"Error in EventBus subscriber for event: -{eventType.Name}-");
					MagmaUtils.LogException(e);
				}
			}
		}
	}
}