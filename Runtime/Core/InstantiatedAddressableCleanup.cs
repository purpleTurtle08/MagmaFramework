using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MagmaFlow.Framework.Core
{
	/// <summary>
	/// This component is added whenever an object is instantiated via baseBehaviour.InstantiateAddressable()
	/// </summary>
	internal sealed class InstantiatedAddressableCleanup : MonoBehaviour
    {
		private void OnDestroy()
		{
			Addressables.ReleaseInstance(gameObject);
		}
	}
}
