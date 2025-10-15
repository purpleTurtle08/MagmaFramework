using System;
using System.Collections.Generic;
using UnityEngine.Events;

namespace MagmaFlow.Framework.Events
{
	/// <summary>
	/// As a golden rule, each object should manage its subscribe / unsubscribe to events. This can be generally made through 
	/// the OnEnable() / OnDisable methods
	/// </summary>
	public static class EventBus
	{
		// The core of the system: a dictionary that maps an event's Type to its corresponding delegate.
		private static Dictionary<Type, Delegate> s_eventRegistry = new Dictionary<Type, Delegate>();

		/// <summary>
		/// Subscribes a callback to an event of type T.
		/// </summary>
		/// <typeparam name="T">The type of event to subscribe to (must be a struct).</typeparam>
		/// <param name="callback">The method to be called when the event is published.</param>
		public static void Subscribe<T>(UnityAction<T> callback) where T : struct
		{
			Type eventType = typeof(T);
			if (s_eventRegistry.TryGetValue(eventType, out Delegate existingDelegate))
			{
				// If there are already subscribers, add this new one to the invocation list.
				s_eventRegistry[eventType] = (UnityAction<T>)existingDelegate + callback;
			}
			else
			{
				// If this is the first subscriber for this event type, create a new entry.
				s_eventRegistry[eventType] = callback;
			}
		}

		/// <summary>
		/// Unsubscribes a callback from an event of type T.
		/// </summary>
		/// <typeparam name="T">The type of event to unsubscribe from.</typeparam>
		/// <param name="callback">The method to remove from the subscription.</param>
		public static void Unsubscribe<T>(UnityAction<T> callback) where T : struct
		{
			Type eventType = typeof(T);
			if (s_eventRegistry.TryGetValue(eventType, out Delegate existingDelegate))
			{
				// Remove the callback from the invocation list.
				Delegate newDelegate = (UnityAction<T>)existingDelegate - callback;

				if (newDelegate == null)
				{
					// If no subscribers remain, remove the event type from the dictionary to save memory.
					s_eventRegistry.Remove(eventType);
				}
				else
				{
					s_eventRegistry[eventType] = newDelegate;
				}
			}
		}

		/// <summary>
		/// Publishes an event, notifying all subscribers.
		/// </summary>
		/// <typeparam name="T">The type of event to publish.</typeparam>
		/// <param name="eventData">The data associated with the event.</param>
		public static void Publish<T>(T eventData) where T : struct
		{
			Type eventType = typeof(T);
			if (s_eventRegistry.TryGetValue(eventType, out Delegate existingDelegate))
			{
				// Invoke all the subscribed callbacks, passing in the event data.
				(existingDelegate as UnityAction<T>)?.Invoke(eventData);
			}
		}
	}
}