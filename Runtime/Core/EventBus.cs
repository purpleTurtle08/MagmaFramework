using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagmaFlow.Framework.Events
{
	/// <summary>
	/// As a GOLDEN RULE, each object should manage its subscribe / unsubscribe to events. This can be generally made through the OnEnable() / OnDisable methods.
	/// <para>The EventBus will handle cleaning 'dead' subscribers that are instanced as MonoBehaviour only.</para>
	/// <para>For non-MonoBehaviours, please make sure that you handle unsubscribing properly to avoid memory leaks.</para>
	/// </summary>
	public static class EventBus
	{
		/// <summary>
		/// Internal class to hold a reference to the delegate (the callback) and the subscribing entity.
		/// <para>If this is a MonoBehaviour instance, the EventBus will manage unsubscribing this dead entity</para>
		/// </summary>
		sealed class EventEntity
		{
			public EventEntity(Delegate @delegate, UnityEngine.Object @entity)
			{
				Delegate = @delegate;
				IsMonoBehaviour = @entity != null;
				Entity = IsMonoBehaviour ? @entity : null;
				ObjectNameReference = IsMonoBehaviour ? @entity.name : null;
			}
			/// <summary>
			/// The name of the Unity GameObject
			/// </summary>
			public string ObjectNameReference { get; }
			/// <summary>
			/// If this is a non-MonoBehaviour 'entity' this will be false
			/// </summary>
			public bool IsMonoBehaviour { get; }
			public Delegate Delegate { get; }
			public UnityEngine.Object Entity { get; }
		}

		// The core of the system: a dictionary that maps an event's Type to its corresponding delegate.
		private static readonly Dictionary<Type, List<EventEntity>> s_eventRegistry = new Dictionary<Type, List<EventEntity>>();

		/// <summary>
		/// Clears the event registry entirely.
		/// <para>Can only be called on the main thread.</para>
		/// </summary>
		public static void ClearAllEvents()
		{
			lock (s_eventRegistry)
			{
				s_eventRegistry.Clear();
			}
		}

		/// <summary>
		/// Subscribes a callback to an event of type T.
		/// <para>Can only be called on the main thread.</para>
		/// </summary>
		/// <typeparam name="T">The event type</typeparam>
		/// <param name="subscriberEntity">The GameObject that is subscribing (used for cleanup)</param>
		/// <param name="callback">The method to be called</param>
		public static void Subscribe<T>(Action<T> callback) where T : struct
		{
			Type eventType = typeof(T);
			var gameObjectRef = callback.Target as UnityEngine.Object;
			var newEventEntity = new EventEntity(callback, gameObjectRef);

			lock (s_eventRegistry)
			{
				if (!s_eventRegistry.TryGetValue(eventType, out var subscribers))
				{
					// This is the first subscriber for this event type.
					// Create a new list and add it to the registry.
					subscribers = new List<EventEntity>();
					s_eventRegistry[eventType] = subscribers;
				}

				if (subscribers.Exists(p =>
					p.ObjectNameReference == newEventEntity.ObjectNameReference &&
					p.Delegate.Method == newEventEntity.Delegate.Method &&
					p.IsMonoBehaviour == newEventEntity.IsMonoBehaviour &&
					p.Entity == newEventEntity.Entity
				))
				{
#if UNITY_EDITOR
					Debug.LogWarning($"\u2714 Entity with method {newEventEntity.Delegate.Method.Name} is already subscribed to {eventType.Name}");
#endif
					return;
				}

				subscribers.Add(newEventEntity);
			}
		}

		/// <summary>
		/// Unsubscribes a callback from an event of type T.
		/// <para>Can only be called on the main thread.</para>
		/// </summary>
		/// <typeparam name="T">The type of event to unsubscribe from.</typeparam>
		/// <param name="callback">The method to remove from the subscription.</param>
		public static void Unsubscribe<T>(Action<T> callback) where T : struct
		{
			Type eventType = typeof(T);

			lock (s_eventRegistry)
			{
				// Try to get the list of subscribers for this event type
				if (!s_eventRegistry.TryGetValue(eventType, out var subscribers))
				{
					return; // No subscribers for this event type, nothing to do.
				}

				var targetGameObject = callback.Target as UnityEngine.Object;
				var methodToRemove = callback.Method;

				// Iterate backwards to safely remove from the list
				for (int i = subscribers.Count - 1; i >= 0; i--)
				{
					var entity = subscribers[i];

					// Check for a match
					if (entity.Entity == targetGameObject && entity.Delegate.Method == methodToRemove)
					{
						// Found the exact subscriber. Remove it.
						subscribers.RemoveAt(i);
						break; // Stop after removing one (this matches delegate subtraction)
					}
				}

				// If the list is now empty, remove the event type from the dictionary
				if (subscribers.Count == 0)
				{
					s_eventRegistry.Remove(eventType);
				}
			}
		}

		/// <summary>
		/// Publishes an event, notifying all subscribers.
		/// Performs a cleanup for 'dead' game objects, in order to avoid memory leaks
		/// <para>Can only be called on the main thread.</para>
		/// </summary>
		/// <typeparam name="T">The type of event to publish.</typeparam>
		/// <param name="eventData">The data associated with the event.</param>
		public static void Publish<T>(T eventData) where T : struct
		{
			Type eventType = typeof(T);

			lock (s_eventRegistry)
			{
				// Try to get the list of subscribers
				if (!s_eventRegistry.TryGetValue(eventType, out var subscribers))
				{
				// No one is subscribed to this event.
				return;
				}

				// Iterate backwards to safely remove "dead" subscribers
				for (int i = subscribers.Count - 1; i >= 0; i--)
				{
					var entity = subscribers[i];

					// Check if the subscriber's GameObject has been destroyed
					// Unity overloads the == null operator for UnityEngine.Object
					if (entity.IsMonoBehaviour && entity.Entity == null)
					{
#if UNITY_EDITOR
						Debug.LogWarning($"Cleared a 'dead' subscriber -object name:{entity.ObjectNameReference}- on event {eventType.Name}. \u26A0 Make sure that you properly unsubscribe when destroying or disabling a game object!");
#endif
						// This GameObject was destroyed. Remove it from the list.
						subscribers.RemoveAt(i);
						continue;
					}

					// If the entity is alive, cast and invoke its delegate
					try
					{
						(entity.Delegate as Action<T>)?.Invoke(eventData);
					}
					catch (Exception e)
					{
#if UNITY_EDITOR
						// Catch errors from individual subscribers so one bad callback
						// doesn't stop all other subscribers from receiving the event.
						Debug.LogError($"Error in EventBus subscriber for event: -{eventType.Name}-");
						Debug.LogException(e);
#endif
					}
				}

				// If the list is now empty, remove the event type from the dictionary
				if (subscribers.Count == 0)
				{
					s_eventRegistry.Remove(eventType);
				}
			}
		}
	}
}