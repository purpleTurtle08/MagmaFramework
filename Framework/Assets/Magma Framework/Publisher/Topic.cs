using UnityEngine.Events;

namespace MagmaFlow.Framework.Publishing
{	
	public class Topic : ITopic
	{
		public string Name { get; set; }
		public UnityAction Callback { get; set; }
		public bool ClearOnSceneChange { get; set; }
		public void InvokeTopic()
		{
			Callback?.Invoke();
		}
		public void Subscribe(UnityAction callback)
		{
			Callback += callback;
		}
		public void Unsubscribe(UnityAction callback)
		{
			if (Callback != null)
				Callback -= callback;
		}
	}
}
