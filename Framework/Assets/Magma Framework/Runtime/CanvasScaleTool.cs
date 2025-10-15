using MagmaFlow.Framework.Events;
using UnityEngine;
using UnityEngine.UI;

namespace MagmaFlow.Framework.Utils
{
	public enum CanvasDrawOrder
	{
		Layer_1_Base,
		Layer_1_Top,
		Layer_2_Base,
		Layer_2_Top,
		Layer_3_Base,
		Layer_3_Top,
		Layer_4_Base,
		Layer_4_Top
	}

	public enum MatchResolutionProperty
	{
		Width = 0,
		Height = 1
	}

	/// <summary>
	/// This tool ensures correct and consistent canvas scaling for your canvases
	/// </summary>
	[RequireComponent(typeof(Canvas))]
	public class CanvasScaleTool : BaseBehaviour
	{
		[SerializeField] private CanvasDrawOrder canvasDrawOrder;
		[SerializeField] private MatchResolutionProperty matchResolutionProperty;

		private void Awake()
		{
			float ratio = Screen.width < Screen.height ? (float)Screen.width / (float)Screen.height : (float)Screen.height / (float)Screen.width;
			var scaledScreen = new Vector2(1920, ratio * 1920);
			GetComponent<CanvasScaler>().referenceResolution = scaledScreen;
		}

		protected override void OnEnable()
		{	
			base.OnEnable();
			EventBus.Subscribe<SetScreenResolutionEvent>(ScaleCanvas);
		}

		protected override void OnDisable()
		{	
			base.OnDisable();
			EventBus.Unsubscribe<SetScreenResolutionEvent>(ScaleCanvas);
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			EventBus.Unsubscribe<SetScreenResolutionEvent>(ScaleCanvas);
		}

		private void ScaleCanvas(SetScreenResolutionEvent eventData)
		{
			float ratio = Screen.width < Screen.height ? (float)Screen.width / (float)Screen.height : (float)Screen.height / (float)Screen.width;
			var scaledScreen = new Vector2(1920, ratio * 1920);
			GetComponent<CanvasScaler>().referenceResolution = scaledScreen;
		}

		private void OnValidate()
		{
			var canvas = GetComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			var canvasScaler = GetComponent<CanvasScaler>();
			canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
			canvasScaler.matchWidthOrHeight = (int) matchResolutionProperty;
			canvasScaler.referencePixelsPerUnit = 100;
			canvas.sortingOrder = (int)canvasDrawOrder;
		}
	}
}
