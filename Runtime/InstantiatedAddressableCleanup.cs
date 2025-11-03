using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MagmaFlow.Framework.Core
{	
	/// <summary>
	/// This component is added whenever an object is instantiated via BaseBehaviour.InstantiateAddressable<>()
	/// </summary>
    public class InstantiatedAddressableCleanup : MonoBehaviour
    {
		private void OnDestroy()
		{
			Addressables.ReleaseInstance(gameObject);
		}
	}
}
