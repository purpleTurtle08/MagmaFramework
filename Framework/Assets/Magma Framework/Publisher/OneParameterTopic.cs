using UnityEngine.Events;

namespace MagmaFlow.Framework.Publishing
{
	public class OneParameterTopic<T> : ITopic
	{
		public string Name { get; set; }
		public UnityAction<T> Callback { get; set; }
		public bool ClearOnSceneChange { get; set; }
		public void InvokeTopic(T param)
		{
			Callback?.Invoke(param);
		}
		public void Subscribe(UnityAction<T> callback)
		{
			Callback += callback;
		}
		public void Unsubscribe(UnityAction<T> callback)
		{
			if (Callback != null)
				Callback -= callback;
		}
	}
}
