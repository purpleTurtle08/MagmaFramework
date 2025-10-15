using UnityEngine;
using UnityEngine.UI;

namespace MagmaFlow.Framework.Utils
{
	/// <summary>
	/// Utility class that outputs FPS to text. In addition it can also show system info.
	/// It has been optimized to make as few frame allocations as possible, as well
	/// as fastest rendering of text possible.
	/// </summary>
	[SelectionBase]
	[DisallowMultipleComponent]
#if UNITY_EDITOR
	[ExecuteInEditMode]
#endif
	public class MagmaFpsCounter : MonoBehaviour
	{
		[Header("Formatting")]

		[SerializeField]
		[Tooltip("{0} = Average FPS, {1} = Lowest FPS, {2} = Average milliseconds")]
		[TextArea(3, 10)]
		private string dynamicText = DEFAULT_DYNAMIC_TEXT;

		[SerializeField]
		[Tooltip("{0} = GPU, {1} = GFX API, {2} = VRAM, {3} = {CPU}, {4} = {RAM}, {5} = OS")]
		[TextArea(3, 10)]
		private string staticText = DEFAULT_STATIC_TEXT;

		[Header("Settings")]

		[Tooltip("Default = 0.25")]
		[SerializeField]
		private float updateInterval = 0.25f;
		
		[Tooltip("Shows information about the system.")]
		[SerializeField]
		private bool showStaticInfo = true;

		private Text _dynamicTextMesh;
		private Text _staticTextMesh;

		// Use 'mspace' to keep mono space on the text so that it doesn't move around.
		private const string DEFAULT_DYNAMIC_TEXT =
			"<mspace=0.8em>{0:0.0} AV\n{1:0.0} LO\n{2:00.0}ms</mspace>";

		private const string DEFAULT_STATIC_TEXT =
			"{0}[{1}]\n" +
			"{2} MB VRAM\n" +
			"{3}\n" +
			"{4}MB RAM\n" +
			"{5}";

		private double _accumulatedTime;
		private double _sampleStartTime;
		private int _frameCount;
		private float _lowFps = float.MaxValue;

		private void Bind()
		{
			// This component is meant to be as lightweight as possible,
			// so we are not using framework binding.
			_dynamicTextMesh = transform.Find("DynamicText")?.GetComponent<Text>();
			_staticTextMesh = transform.Find("StaticText")?.GetComponent<Text>();
		}

		private void Awake()
		{
			Bind();

			// Try to use whatever is set on the text itself.
			// If not, use defaults.
			//dynamicText = dynamicText.Replace("\\n", "\n");
			if (string.IsNullOrEmpty(dynamicText.Trim()))
			{
				dynamicText = DEFAULT_DYNAMIC_TEXT;
			}

			// The static system nfo text is optional.
			if (_staticTextMesh != null)
			{
				var gpu = $"{SystemInfo.graphicsDeviceName}";            // {0}
				var graphicsAPI = $"{SystemInfo.graphicsDeviceType}";    // {1}
				var vram = $"{SystemInfo.graphicsMemorySize}";           // {2}
				var cpu = $"{SystemInfo.processorType}";                 // {3}
				var ram = $"{SystemInfo.systemMemorySize}";              // {4}
				var os = $"{SystemInfo.operatingSystem}";                // {5}

				// Try to use whatever is set on the text itself.
				// If not, use defaults.
				if (string.IsNullOrEmpty(staticText.Trim()))
				{
					staticText = DEFAULT_STATIC_TEXT;
				}

				_staticTextMesh.text = string.Format(staticText,
					gpu,
					graphicsAPI,
					vram,
					cpu,
					ram,
					os);

				_staticTextMesh.gameObject.SetActive(showStaticInfo);
			}
		}

#if UNITY_EDITOR
		private void OnValidate()
		{
			if (Application.isPlaying)
				return;

			if (_dynamicTextMesh != null)
			{
				//_dynamicTextMesh.text = "";
			}

			if (_staticTextMesh != null)
			{
				_staticTextMesh.gameObject.SetActive(showStaticInfo);
				_staticTextMesh.text = string.Format(staticText,
						"NVIDIA GeForce GTX 1060 3GB",
						"Direct3D11",
						"2978",
						"Intel(R) Core(TM) i7-8700K CPU @ 3.70GHz",
						"32713",
						"Windows 10  (10.0.19045) 64bit");

			}
		}
#endif

		private void Update()
		{

#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				return;
			}
#endif
			_accumulatedTime = Time.unscaledTimeAsDouble - _sampleStartTime;
			_frameCount++;

			var fps = 1f / Time.unscaledDeltaTime;
			_lowFps = Mathf.Min(fps, _lowFps);

			// Only display and do further calculations at a set interval.
			if (_accumulatedTime < updateInterval)
				return;

			var avgFps = _frameCount / _accumulatedTime;
			var ms = _accumulatedTime * 1000 / _frameCount;

			// Use SetText with parameters. TextMeshPro will know 
			// not to update the static part of the string.
			// Furthermore, because the string is not actually concatenated,
			// this makes almost no allocations.
			_dynamicTextMesh.text = string.Format(dynamicText, (float)avgFps,_lowFps,(float)ms);

			// Reset counters.
			_frameCount = 0;
			_accumulatedTime = 0;
			_sampleStartTime = Time.unscaledTimeAsDouble;
			_lowFps = float.MaxValue;
		}
	}
}
