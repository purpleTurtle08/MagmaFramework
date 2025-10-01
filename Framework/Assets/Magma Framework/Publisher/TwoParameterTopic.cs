using UnityEngine.Events;

namespace MagmaFlow.Framework.Publishing
{
	public class TwoParameterTopic<T, U> : ITopic
	{
		public string Name { get; set; }
		public UnityAction<T, U> Callback { get; set; }
		public bool ClearOnSceneChange { get; set; }
		public void InvokeTopic(T param, U param1)
		{
			Callback?.Invoke(param, param1);
		}
		public void Subscribe(UnityAction<T, U> callback)
		{
			Callback += callback;
		}
		public void Unsubscribe(UnityAction<T, U> callback)
		{
			if (Callback != null)
				Callback -= callback;
		}
	}
}
