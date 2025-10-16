using System;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace MagmaFlow.Framework.Sound
{
	[RequireComponent(typeof(AudioSource))]
	[DefaultExecutionOrder(-50)]
	public class MusicManager : MonoBehaviour
	{
		[SerializeField]
		[Tooltip("The duration in seconds for a track to fade out and the next track fades in during automatic playback.")]
		[Range(0f, 10f)]
		private float autoPlayCrossfadeDuration = 3f;
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

		public static MusicManager Instance { get; private set; }
		public bool IsMuted { get; private set; }
		/// <summary>
		/// This indicates the state of the music manager, NOT if the actual audio source is playing or not.
		/// </summary>
		public bool IsPlaying { get; private set; } = false;
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
		public float Volume => currentVolumeTarget;

		/// <summary>
		/// Fades the volume to the provided value
		/// </summary>
		/// <param name="value"></param>
		/// <param name="duration">Duration of the fade in seconds</param>
		public void SetReferenceVolume(float value, float duration)
		{
			volumeDifference = Mathf.Abs(value - currentVolumeTarget);
			currentVolumeTarget = IsMuted ? 0 : Mathf.Clamp01(value);
			crossfadeDuration = duration;
			interpolateVolume = true;
		}

		/// <summary>
		/// Fades the volume of the music to 0
		/// </summary>
		/// <param name="crossfadeSpeed">This is the fade duration in seconds</param>
		public void MuteMusic(float crossfadeSpeed)
		{
			Initialize();
			IsMuted = true;
			SetReferenceVolume(0, crossfadeSpeed);
		}

		/// <summary>
		/// Fades the volume of the music to 1
		/// </summary>
		/// <param name="crossfadeSpeed">This is the duration of the fade in seconds</param>
		public void UnmuteMusic(float crossfadeSpeed)
		{
			Initialize();
			IsMuted = false;
			SetReferenceVolume(referenceVolume, crossfadeSpeed);
		}

		/// <summary>
		/// If the music is already playing, it will pick the next clip
		/// <para>If ShuffleTracks is enable and no clip is overriden, it will play a random track next</para>
		/// </summary>
		/// <param name="crossfadeSpeed">Duration of the fade in seconds</param>
		/// <param name="overrideClipIndex"></param>
		public void PlayMusic(float crossfadeSpeed, int overrideClipIndex = -1)
		{
			if (playList == null || playList.Length <= 0) return;
			Initialize();

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
				SetReferenceVolume(referenceVolume, crossfadeSpeed);
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
			Initialize();

			SetReferenceVolume(0, crossfadeSpeed);
			onInterpolationFinished = () => { IsPlaying = false; musicSource.Stop(); onFadeComplete?.Invoke(); };
		}

		private void LerpCurrentVolume()
		{
			if (!interpolateVolume) return;

			if(currentVolume == currentVolumeTarget)
			{
				interpolateVolume = false;
				onInterpolationFinished?.Invoke();
				onInterpolationFinished = null;
			}
			float clampedVolumeDifference = Mathf.Clamp(volumeDifference, .1f, 1f);//We make sure that the distance can't be 0
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
			PlayMusic(autoPlayCrossfadeDuration, nextClipData.Item2);
		}

		private void Awake()
		{
			Singleton();

			if (playOnAwake) PlayMusic(autoPlayCrossfadeDuration, currentClipIndex);
		}

		private void Update()
		{
			if (!isInitialized) return;

			LerpCurrentVolume();

			// Check if we should start a crossfade for the next track
			if (IsPlaying && !isShuffleCrossfading && musicSource.clip != null && musicSource.isPlaying)
			{
				// If the remaining time is less than the fade duration, start the next track
				if (musicSource.clip.length - musicSource.time <= autoPlayCrossfadeDuration)
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
			if (shuffle)
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

		private void Singleton()
		{
			if (Instance != null && Instance != this) 
			{
				Debug.LogWarning($"Removed {gameObject.name}, as it is a duplicate MusicManager. Ensure you only have 1 MusicManager per scene");
				DestroyImmediate(gameObject);
			}
			else
			{
				Instance = this;
				Initialize();
			}
		}

		/// <summary>
		/// The volume must always be one for music and can be adjusted from the mixer
		/// </summary>
		private void OnValidate()
		{
			var audioComponent = GetComponent<AudioSource>();
			audioComponent.volume = referenceVolume;
			audioComponent.loop = false;
			audioComponent.spatialBlend = 0;
			audioComponent.playOnAwake = false;
			audioComponent.bypassEffects = true;
			audioComponent.bypassReverbZones = true;
		}

	}
}

