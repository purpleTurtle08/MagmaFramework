using System;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace MagmaFlow.Framework.Utils
{
	[RequireComponent(typeof(AudioSource))]
	[DefaultExecutionOrder(-100)]
	public sealed class MagmaFramework_MusicManager : MonoBehaviour
	{
		[SerializeField]
		[Tooltip("The duration in seconds for a track to fade out and the next track fades in during automatic playback. This is not a cross-fade")]
		[Range(0f, 10f)]
		private float autoPlayFadeDuration = 3f;
		[SerializeField] [Range(0f, 1f)] private float referenceVolume = 1;
		[Tooltip("The index of the first clip that should be played")] public int StartingClipIndex = 0;
		[SerializeField] private AudioClip[] playList;
		[SerializeField] [Tooltip("Plays next track as a random track from the collection")] private bool shuffleTracks = true;
		[SerializeField] private bool playOnAwake = false;

		private AudioSource musicSource;
		private UnityAction onInterpolationFinished;

		/// <summary>
		/// Prevent this from being re-initialized throughout gameplay
		/// </summary>
		private bool isInitialized = false;
		private int currentClipIndex = 0;
		private bool interpolateVolume = true;
		private bool isShuffleCrossfading = false;
		private float currentVolume = 1;
		private float currentVolumeTarget = 1;
		//Used to compute volume fade speed
		private float volumeDifference = 0;
		private float crossfadeDuration = 1.5f;
		
		/// <summary>
		/// 
		/// </summary>
		public static MagmaFramework_MusicManager Instance { get; private set; }
		/// <summary>
		/// This uses the audio source's current volume.
		/// If there's a case where you fade the volume to 0 and then immediately call IsMuted, it will return 'false'.
		/// Use 'Volume' for such cases.
		/// </summary>
		public bool IsMuted => musicSource?.volume <= 0.05f ?  true : false;
		/// <summary>
		/// This indicates the state of the music manager, NOT if the actual audio source is playing or not.
		/// </summary>
		public bool IsPlaying { get; private set; } = false;
		/// <summary>
		/// 
		/// </summary>
		public bool ShuffleTracks 
		{ 
			get 
			{
				return shuffleTracks;
			}
			set 
			{ 
				shuffleTracks = value;
			}
		}
		/// <summary>
		/// 
		/// </summary>
		public float Volume => currentVolumeTarget;

		/// <summary>
		/// Sets the reference volume for the music (this is the volume that will be used as a reference for all music volume operations)
		/// </summary>
		/// <param name="value"></param>
		/// <param name="alsoSetVolume"></param>
		/// <param name="duration"></param>
		public void SetReferenceVolume(float value, bool alsoSetVolume = false, float duration = 0)
		{
			if (!isInitialized) return;

			referenceVolume = Mathf.Clamp01(value);
			if (alsoSetVolume)
			{
				SetVolume(value, duration);
			}
		}

		/// <summary>
		/// Fades the volume to the provided value
		/// </summary>
		/// <param name="value"></param>
		/// <param name="duration">Duration of the fade in seconds</param>
		public void SetVolume(float value, float duration)
		{
			if (!isInitialized) return;

			volumeDifference = Mathf.Abs(value - currentVolumeTarget);
			currentVolumeTarget = Mathf.Clamp01(value);
			crossfadeDuration = duration;
			interpolateVolume = true;
		}

		/// <summary>
		/// If the music is already playing, it will pick the next clip
		/// <para>If ShuffleTracks is enable and no clip is overriden, it will play a random track next</para>
		/// </summary>
		/// <param name="crossfadeSpeed">Duration of the fade in seconds</param>
		/// <param name="overrideClipIndex"></param>
		public void PlayMusic(float crossfadeSpeed, int overrideClipIndex = -1)
		{
			if (!isInitialized) return;
			if (playList == null || playList.Length <= 0) return;

			StopMusic(crossfadeSpeed, () => 
			{
				if (overrideClipIndex >= 0)
				{
					overrideClipIndex = Mathf.Clamp(overrideClipIndex, 0, playList.Length - 1);
					musicSource.clip = playList[overrideClipIndex];
					currentClipIndex = overrideClipIndex;
				}
				else
				{	
					var nextClipData = GetNextClip(shuffleTracks);
					musicSource.clip = nextClipData.Item1;
					currentClipIndex = nextClipData.Item2;
				}
				musicSource.volume = 0;
				SetVolume(referenceVolume, crossfadeSpeed);
				musicSource.Play();
				IsPlaying = true;
				isShuffleCrossfading = false;
			});
		}

		/// <summary>
		/// Stops the playback completely
		/// </summary>
		/// <param name="crossfadeSpeed">Duration of the fade in seconds</param>
		/// <param name="onFadeComplete"></param>
		public void StopMusic(float crossfadeSpeed, UnityAction onFadeComplete = null)
		{
			if (!isInitialized) return;

			SetVolume(0, crossfadeSpeed);
			onInterpolationFinished = () => { IsPlaying = false; musicSource.Stop(); onFadeComplete?.Invoke(); };
		}

		private void LerpCurrentVolume()
		{
			if(currentVolume == currentVolumeTarget)
			{
				interpolateVolume = false;
				onInterpolationFinished?.Invoke();
				onInterpolationFinished = null;
			}
			float clampedVolumeDifference = Mathf.Clamp(volumeDifference, .05f, 1f);//We make sure that the distance can't be 0
			float lerpSpeed = (clampedVolumeDifference / (float)crossfadeDuration) * Time.unscaledDeltaTime;
			currentVolume = Mathf.MoveTowards(currentVolume, currentVolumeTarget, lerpSpeed);
			musicSource.volume = currentVolume;
		}

		/// <summary>
		/// This is a shuffling helper, ensuring that the music is always playing
		/// </summary>
		private void ShuffleMusic()
		{
			if (playList == null || playList.Length <= 0)
			{
				StopMusic(0);
				return;
			}

			var nextClipData = GetNextClip(shuffleTracks);
			PlayMusic(autoPlayFadeDuration, nextClipData.Item2);
		}

		private void Awake()
		{
			if (!Singleton())
			{
				return;
			}

			Initialize();
			if (playOnAwake) PlayMusic(autoPlayFadeDuration, currentClipIndex);
		}

		private void Update()
		{
			if (!isInitialized) return;

			if (!interpolateVolume) LerpCurrentVolume();

			// Check if we should start a crossfade for the next track
			if (IsPlaying && !isShuffleCrossfading && musicSource.clip != null && musicSource.isPlaying)
			{
				// If the remaining time is less than the fade duration, start the next track
				if (musicSource.clip.length - musicSource.time <= autoPlayFadeDuration)
				{
					isShuffleCrossfading = true;
					ShuffleMusic();   
				}
			}
		}

		private Tuple<AudioClip, int> GetNextClip(bool shuffle)
		{
			if (playList == null || playList.Length <= 0) return null;

			int nextClipIndex = -1;
			///We also check if the playlist has more than 1 track, as that will lead to an infinite loop
			if (shuffle && playList.Length > 1)
			{
				nextClipIndex = Random.Range(0, playList.Length - 1);
				while (nextClipIndex == currentClipIndex)
					nextClipIndex = Random.Range(0, playList.Length - 1);
			}
			else
			{
				nextClipIndex = currentClipIndex + 1 >= playList.Length ? 0 : currentClipIndex + 1;
			}

			return new Tuple<AudioClip, int>(playList[nextClipIndex], nextClipIndex);
		}

		private void Initialize()
		{
			if (isInitialized) return;

			musicSource = GetComponent<AudioSource>();
			currentClipIndex = StartingClipIndex;
			currentVolume = Volume;
			isInitialized = true;
		}

		/// <summary>
		/// This ensures that there can only be one object of this type per scene
		/// </summary>
		private bool Singleton()
		{
			var gameObjectName = gameObject.name;
			if (Instance != null && Instance != this)
			{
				Debug.LogWarning($"Removed {gameObjectName}, as it is a duplicate. Ensure you only have 1 {gameObjectName} per scene.");
				Destroy(gameObject);
				return false;
			}

			Instance = this;
			DontDestroyOnLoad(gameObject);
#if UNITY_EDITOR
			Debug.Log($"MagmaFramework::: {gameObjectName} service present.");
#endif
			return true;
		}

#if UNITY_EDITOR
		/// <summary>
		/// The volume must always be one for music and can be adjusted from the mixer
		/// </summary>
		private void OnValidate()
		{
			if (Application.isPlaying) return;

			var audioComponent = GetComponent<AudioSource>();
			audioComponent.volume = referenceVolume;
			audioComponent.loop = false;
			audioComponent.spatialBlend = 0;
			audioComponent.playOnAwake = false;
			audioComponent.bypassEffects = true;
			audioComponent.bypassReverbZones = true;
		}
#endif
	}
}

