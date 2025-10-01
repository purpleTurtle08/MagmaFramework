using MagmaFlow.Framework.Publishing;
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

	[RequireComponent(typeof(Canvas))]
	public class CanvasScaleTool : BaseBehaviour
	{
		[SerializeField] private CanvasDrawOrder canvasDrawOrder;
		[SerializeField] private MatchResolutionProperty matchResolutionProperty;

		private Publisher publisher;

		private void Awake()
		{	
			publisher = ApplicationCore.Publisher;

			publisher.Unsubscribe(PublisherTopics.SYSTEM_RESERVED_SCREEN_RESOLUTION_SET, ScaleCanvas);
			ScaleCanvas();
			publisher.Subscribe(PublisherTopics.SYSTEM_RESERVED_SCREEN_RESOLUTION_SET, ScaleCanvas);
		}

		private void ScaleCanvas()
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

		private void OnApplicationQuit()
		{
			publisher.Unsubscribe(PublisherTopics.SYSTEM_RESERVED_SCREEN_RESOLUTION_SET, ScaleCanvas);
		}

		private void OnDestroy()
		{
			publisher.Unsubscribe(PublisherTopics.SYSTEM_RESERVED_SCREEN_RESOLUTION_SET, ScaleCanvas);
		}
	}
}
