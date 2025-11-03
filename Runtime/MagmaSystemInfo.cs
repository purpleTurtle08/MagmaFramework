using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace MagmaFlow.Framework.Utils
{
	/// <summary>
	/// Utility class that outputs FPS to text. In addition it can also show system info.
	/// It has been optimized to make as few frame allocations as possible, as well
	/// as fastest rendering of text possible by using TextMeshPro.
	/// </summary>
	[SelectionBase]
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Canvas))]
	[RequireComponent(typeof(CanvasScaler))]
#if UNITY_EDITOR
	[ExecuteInEditMode]
#endif
	public class MagmaSystemInfo : MonoBehaviour
	{
		[Header("Formatting")]
		[SerializeField] [Tooltip("{0} = Average FPS, {1} = Lowest FPS (from peak ms), {2} = Average milliseconds")] [TextArea(3, 10)] private string dynamicText = DEFAULT_DYNAMIC_TEXT;
		[SerializeField] [Tooltip("{0} = GPU, {1} = GFX API, {2} = VRAM, {3} = {CPU}, {4} = {RAM}, {5} = OS")] [TextArea(3, 10)] private string staticText = DEFAULT_STATIC_TEXT;

		[Header("Settings")]
		[Tooltip("Default = 0.25")] [SerializeField] private float updateInterval = 0.25f;
		[Tooltip("Shows information about the system.")] [SerializeField] private bool showStaticInfo = true;
		[Tooltip("Shows information about the system memory usage.")] [SerializeField] private bool profileMemory = true;

		// Use TextMeshProUGUI for performance. It avoids GC allocations on text update.
		private TextMeshProUGUI _dynamicTextMesh;
		private TextMeshProUGUI _staticTextMesh;

		// Use 'mspace' to keep mono space on the text so that it doesn't move around.
		private const string DEFAULT_DYNAMIC_TEXT =
			"<mspace=0.8em>{0} AV\n{1} LO\n{2:00.0}ms</mspace>";

		private const string DEFAULT_STATIC_TEXT =
			"{0}[{1}]\n" +
			"{2} MB VRAM\n" +
			"{3}\n" +
			"{4}MB RAM\n" +
			"{5}";

		private double _accumulatedTime;
		private double _sampleStartTime;
		private int _frameCount;
		private double _lastMemoryReport;
		private string _memoryReport;
		private double _lastLowFpsUpdateTime;
		private readonly double _memoryReportInterval = 2;
		private readonly float _lowFpsUpdateInterval = 1f;
		private readonly StringBuilder _stringBuilder = new StringBuilder(256);
		// --- Improvement 2: Track peak frame time for a more stable "Low FPS" ---
		// Track the highest frame time (ms) in the interval instead of the instantaneous lowest FPS.
		private float _highMs;
		private float _lowestFps = float.MaxValue;

		private void Bind()
		{
			_dynamicTextMesh = transform.Find("DynamicText")?.GetComponent<TextMeshProUGUI>();
			_staticTextMesh = transform.Find("StaticText")?.GetComponent<TextMeshProUGUI>();

			if (_dynamicTextMesh == null)
			{
				Debug.LogWarning("MagmaFpsCounter: Could not find 'DynamicText' child with TextMeshProUGUI component.", this);
			}
			if (_staticTextMesh == null)
			{
				Debug.LogWarning("MagmaFpsCounter: Could not find 'StaticText' child with TextMeshProUGUI component.", this);
			}
		}

		private void Awake()
		{
			Bind();

			// Try to use whatever is set on the text itself.
			// If not, use defaults.
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

				// Use TextMeshPro's SetText for efficient formatting
				_staticTextMesh.text = string.Format(staticText,
					gpu,
					graphicsAPI,
					vram,
					cpu,
					ram,
					os);

				_staticTextMesh.gameObject.SetActive(showStaticInfo);
			}

			// Initialize sample time
			_sampleStartTime = Time.unscaledTimeAsDouble;
		}

		/// <summary>
		/// Returns a snapshot of current memory usage in bytes.
		/// </summary>
		private string GetMemoryReport()
		{
			long managedMemory = System.GC.GetTotalMemory(false);
			long monoUsed = Profiler.GetMonoUsedSizeLong();
			long monoHeap = Profiler.GetMonoHeapSizeLong();
			long totalAllocated = Profiler.GetTotalAllocatedMemoryLong();
			long totalReserved = Profiler.GetTotalReservedMemoryLong();
			long totalUnused = Profiler.GetTotalUnusedReservedMemoryLong();

			return
				$"Managed GC: {BytesToMB(managedMemory)} MB\n" +
				$"Mono Used: {BytesToMB(monoUsed)} MB / Mono Heap: {BytesToMB(monoHeap)} MB\n" +
				$"Total Allocated: {BytesToMB(totalAllocated)} MB\n" +
				$"Reserved: {BytesToMB(totalReserved)} MB (Unused: {BytesToMB(totalUnused)} MB)";
		}

		private static int BytesToMB(long bytes) => Mathf.CeilToInt(bytes / (1024f * 1024f));

#if UNITY_EDITOR
		private void OnValidate()
		{
			// Ensure components are bound in editor
			if (_dynamicTextMesh == null || _staticTextMesh == null)
			{
				Bind();
			}

			if (Application.isPlaying)
				return;

			var canvasScaler = GetComponent<CanvasScaler>();
			var canvas = GetComponent<Canvas>();

			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.pixelPerfect = false;
			canvas.sortingOrder = 9999;
			canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1;
			canvasScaler.referenceResolution = new Vector2(1920, 1080);
			canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
			canvasScaler.matchWidthOrHeight = 0;
			if (TryGetComponent<GraphicRaycaster>(out var raycaster))
			{
				DestroyImmediate(raycaster);
			}
			// Show placeholder in editor
			if (_dynamicTextMesh != null)
			{
				_dynamicTextMesh.SetText(DEFAULT_DYNAMIC_TEXT, 60, 30, 16.7f);
			}

			if (_staticTextMesh != null)
			{
				_staticTextMesh.gameObject.SetActive(showStaticInfo);
				// Use SetText for formatting
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
				return;
#endif

			if (_dynamicTextMesh == null)
				return; // Nothing to update

			var timeNow = Time.unscaledTimeAsDouble;

			// Increment frame count and track peak frame time BEFORE interval check
			_frameCount++;
			_highMs = Mathf.Max(_highMs, Time.unscaledDeltaTime * 1000f);

			// Accumulated time since last sample
			_accumulatedTime = timeNow - _sampleStartTime;

			// Only update display if enough time has passed
			if (_accumulatedTime >= updateInterval)
			{
				var avgFps = _frameCount / _accumulatedTime;
				var avgMs = _accumulatedTime * 1000 / _frameCount;

				// Update low FPS every _lowFpsUpdateInterval
				if (timeNow - _lastLowFpsUpdateTime >= _lowFpsUpdateInterval)
				{
					_lastLowFpsUpdateTime = timeNow;
					_lowestFps = _highMs > 0 ? (1000f / _highMs) : 0f;
				}

				if (profileMemory)
				{
					// Update memory report if interval passed
					if (timeNow - _lastMemoryReport >= _memoryReportInterval)
					{
						_lastMemoryReport = timeNow;
						_memoryReport = GetMemoryReport();
					}
				}
				else
				{
					_memoryReport = "";
				}

				_stringBuilder.Clear();
				_stringBuilder.AppendFormat("<mspace=0.8em>{0} AVG", Mathf.FloorToInt((float)avgFps));
				_stringBuilder.AppendFormat("\n{0} LOW", Mathf.FloorToInt(_lowestFps));
				_stringBuilder.AppendFormat("\n{0:F} FMS</mspace>", (float)avgMs);
				_stringBuilder.Append($"\n{_memoryReport}");
				_dynamicTextMesh.SetText(_stringBuilder);

				// Reset counters for next interval
				_frameCount = 0;
				_accumulatedTime = 0;
				_sampleStartTime = timeNow;
				_highMs = 0f; // reset peak frame time
			}
		}

	}
}
