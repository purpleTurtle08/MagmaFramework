using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MagmaFlow.Framework.Publishing
{
	public class Publisher : MonoBehaviour
	{	
		private List<ITopic> topics { get; set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="topic"></param>
		public void Publish(Tuple<string, bool> topic)
		{
			if (topics == null) return;

			foreach (var t in topics)
			{
				if (t.Name == topic.Item1)
				{
					var currTopic = (Topic)t;
					currTopic.InvokeTopic();
					return;
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="topic"></param>
		/// <param name="param"></param>
		public void Publish<T>(Tuple<string, bool> topic, T param)
		{
			if (topics == null) return;

			foreach (var t in topics)
			{
				if(t.Name == topic.Item1)
				{
					var currTopic = (OneParameterTopic<T>)t;
					currTopic.InvokeTopic(param);
					return;
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <typeparam name="U"></typeparam>
		/// <param name="topic"></param>
		/// <param name="param"></param>
		/// <param name="param1"></param>
		public void Publish<T, U>(Tuple<string, bool> topic, T param, U param1)
		{
			if (topics == null) return;

			foreach (var t in topics)
			{
				if (t.Name == topic.Item1)
				{
					var currTopic = (TwoParameterTopic<T, U>)t;
					currTopic.InvokeTopic(param, param1);
					return;
					}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="topic"></param>
		/// <param name="callback"></param>
		public void Subscribe(Tuple<string, bool> topic, UnityAction callback)
		{
			if (topics == null)
				topics = new List<ITopic>();

			foreach (var t in topics)
			{
				if (t.Name == topic.Item1)
				{
					var currTopic = (Topic)t;
					currTopic.Subscribe(callback);
					return;
				}
			}

			//In case the topic was non existent
			var newTopic = new Topic()
			{
				Callback = callback,
				Name = topic.Item1,
				ClearOnSceneChange = topic.Item2
			};

			if (topics == null)
				topics = new List<ITopic>();

			topics.Add(newTopic);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="topic"></param>
		/// <param name="callback"></param>
		public void Subscribe<T>(Tuple<string, bool> topic, UnityAction<T> callback)
		{
			if (topics == null)
				topics = new List<ITopic>();

			foreach (var t in topics)
			{
				if(t.Name == topic.Item1)
				{
					OneParameterTopic<T> currTopic = (OneParameterTopic<T>)t;
					currTopic.Subscribe(callback);
					return;
				}
			}

			//In case the topic was non existent
			var newTopic = new OneParameterTopic<T>
			{
				Callback = callback,
				Name = topic.Item1,
				ClearOnSceneChange = topic.Item2
			};

			if (topics == null)
				topics = new List<ITopic>();

			topics.Add(newTopic);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <typeparam name="U"></typeparam>
		/// <param name="topic"></param>
		/// <param name="callback"></param>
		public void Subscribe<T, U>(Tuple<string, bool> topic, UnityAction<T, U> callback)
		{
			if (topics == null)
				topics = new List<ITopic>();

			foreach (var t in topics)
			{
				if (t.Name == topic.Item1)
				{
					TwoParameterTopic<T, U> currTopic = (TwoParameterTopic<T, U>)t;
					currTopic.Subscribe(callback);
					return;
				}
			}

			//In case the topic was non existent
			var newTopic = new TwoParameterTopic<T, U>
			{
				Callback = callback,
				Name = topic.Item1,
				ClearOnSceneChange= topic.Item2
			};

			topics.Add(newTopic);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="topic"></param>
		/// <param name="callback"></param>
		public void Unsubscribe(Tuple<string, bool> topic, UnityAction callback)
		{
			if (topics == null) return;

			foreach (var t in topics)
			{
				if (t.Name == topic.Item1)
				{
					Topic currTopic = (Topic)t;
					currTopic.Unsubscribe(callback);
					return;
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="topic"></param>
		/// <param name="callback"></param>
		public void Unsubscribe<T>(Tuple<string, bool> topic, UnityAction<T> callback)
		{
			if (topics == null) return;

			foreach (var t in topics)
			{
				if (t.Name == topic.Item1)
				{
					OneParameterTopic<T> currTopic = (OneParameterTopic<T>)t;
					currTopic.Unsubscribe(callback);
					return;
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <typeparam name="U"></typeparam>
		/// <param name="topic"></param>
		/// <param name="callback"></param>
		public void Unsubscribe<T, U>(Tuple<string, bool> topic, UnityAction<T, U> callback)
		{
			if (topics == null) return;

			foreach (var t in topics)
			{
				if (t.Name == topic.Item1)
				{
					TwoParameterTopic<T, U> currTopic = (TwoParameterTopic<T, U>)t;
					currTopic.Unsubscribe(callback);
					return;
				}
			}
		}

		/// <summary>
		/// Clears all the tracked events considering which topics should be cleared
		/// </summary>
		public void ClearAllEvents()
		{
			if (topics == null) return;

			for (int i = topics.Count - 1; i >= 0; i--)
			{
				if (topics[i].ClearOnSceneChange)
				{
					topics.Remove(topics[i]);
				}
			}
		}

		public void CleanupErroneousCallbacks()
		{
			foreach(var t in topics)
			{

			}
		}
	}
}
