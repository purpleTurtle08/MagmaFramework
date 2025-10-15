using UnityEngine;
using UnityEngine.Events;

namespace MagmaFlow.Framework.Sound
{
	[RequireComponent(typeof(AudioSource))]
	public class MusicManager : BaseBehaviour
	{
		private const float CROSSFADE_SPEED_MIN = .2f;
		private const float CROSSFADE_SPEED_MAX = 5;

		[SerializeField] [Range(CROSSFADE_SPEED_MIN, CROSSFADE_SPEED_MAX)] public float CrossfadeSpeed = 1.5f;
		[SerializeField] private AudioClip[] soundtrackCollection;
		[SerializeField] [Tooltip("Plays next track as a random track from the collection")] private bool shuffleTracks = true;
		[SerializeField] [Tooltip("If this is checked, the manager will play all clips in order, if not, it will only play the first clip")] private bool playAsPlaylist = true;

		private AudioSource musicSource;
		private UnityAction onInterpolationFinished;

		/// <summary>
		/// Prevent this from being re-initialized throughout gameplay
		/// </summary>
		private bool isInitialized = false;
		private int currentClipIndex = 0;
		private bool interpolateVolume = true;

		private float refStartVolume;
		private float currentVolumeTarget = 1; 
		private float currentVolume = 1;

		private float audioCheckInterval = 1;
		private float lastAudioStopCheck = 0;

		public void MuteMusic(float crossfadeSpeed)
		{
			Initialize();

			currentVolumeTarget = 0;
			CrossfadeSpeed = Mathf.Clamp(crossfadeSpeed, CROSSFADE_SPEED_MIN, CROSSFADE_SPEED_MAX);
			interpolateVolume = true;
		}

		public void UnmuteMusic(float crossfadeSpeed)
		{
			Initialize();

			currentVolumeTarget = refStartVolume;
			CrossfadeSpeed = Mathf.Clamp(crossfadeSpeed, CROSSFADE_SPEED_MIN, CROSSFADE_SPEED_MAX);
			interpolateVolume = true;
		}

		public void PlayMusic(float crossfadeSpeed, int overrideClipIndex = -1)
		{
			Initialize();

			if (musicSource.isPlaying) 
			{
				StopMusic(crossfadeSpeed, () => 
				{
					if (overrideClipIndex >= 0)
					{
						musicSource.clip = soundtrackCollection[overrideClipIndex];
						currentClipIndex = overrideClipIndex;
					}
					else
					{
						musicSource.clip = GetNextClip(shuffleTracks);
					}
					musicSource.volume = 0;
					interpolateVolume = true;
					CrossfadeSpeed = Mathf.Clamp(crossfadeSpeed, CROSSFADE_SPEED_MIN, CROSSFADE_SPEED_MAX);
					currentVolumeTarget = refStartVolume;
					musicSource.Play();
				});
				return;
			}

			if(overrideClipIndex >= 0)
			{
				musicSource.clip = soundtrackCollection[overrideClipIndex];
				currentClipIndex = overrideClipIndex;
			}
			else
			{
				musicSource.clip = GetNextClip(shuffleTracks);
			}
			musicSource.volume = 0;
			interpolateVolume = true;
			CrossfadeSpeed = Mathf.Clamp(crossfadeSpeed, CROSSFADE_SPEED_MIN, CROSSFADE_SPEED_MAX);
			currentVolumeTarget = refStartVolume;
			musicSource.Play();
		}

		public void StopMusic(float crossfadeSpeed, UnityAction onFadeComplete = null)
		{
			Initialize();

			currentVolumeTarget = 0;
			CrossfadeSpeed = Mathf.Clamp(crossfadeSpeed, CROSSFADE_SPEED_MIN, CROSSFADE_SPEED_MAX);
			interpolateVolume = true;
			onInterpolationFinished = () => { musicSource.Stop(); onFadeComplete?.Invoke(); };
		}

		private void Update()
		{
			LerpCurrentVolume();
			if (playAsPlaylist && isInitialized)
				ShuffleMusic();
		}

		private void LerpCurrentVolume()
		{
			if (!interpolateVolume)
			{
				return;
			}

			if(currentVolume <= currentVolumeTarget + .03f && currentVolume >= currentVolumeTarget - .03f)
			{
				interpolateVolume = false;
				onInterpolationFinished?.Invoke();
				onInterpolationFinished = null;
			}
			currentVolume = Mathf.Lerp(currentVolume, currentVolumeTarget, CrossfadeSpeed * Time.deltaTime);
			musicSource.volume = currentVolume;
		}

		private void ShuffleMusic()
		{
			if (soundtrackCollection == null)
			{
				return;
			}

			if (Time.time - lastAudioStopCheck <= audioCheckInterval)
			{
				return;
			}

			lastAudioStopCheck = Time.time;

			if (!musicSource.isPlaying)
			{
				GetNextClip(shuffleTracks);

				musicSource.clip = soundtrackCollection[currentClipIndex];
				musicSource.Play();
			}
		}

		private void Awake()
		{
			Initialize();
		}

		private AudioClip GetNextClip(bool shuffle)
		{
			int nextClipIndex = 0;
			if (shuffle)
			{
				nextClipIndex = Random.Range(0, soundtrackCollection.Length - 1);
				while (nextClipIndex == currentClipIndex)
					nextClipIndex = Random.Range(0, soundtrackCollection.Length - 1);
			}
			else
			{
				currentClipIndex++;
				if (currentClipIndex >= soundtrackCollection.Length)
				{
					currentClipIndex = 0;
				}
				nextClipIndex = currentClipIndex;
			}

			currentClipIndex = nextClipIndex;
			return soundtrackCollection[currentClipIndex];
		}

		private void Initialize()
		{
			if (isInitialized)
			{
				return;
			}

			currentClipIndex = 0;
			musicSource = GetComponent<AudioSource>();
			refStartVolume = musicSource.volume;
			currentVolumeTarget = refStartVolume;
			currentVolume = refStartVolume;
			musicSource.loop = false;
			musicSource.spatialBlend = 0;
			musicSource.playOnAwake = false;
			musicSource.bypassEffects = true;
			musicSource.bypassReverbZones = true;
			musicSource.mute = false;

			isInitialized = true;
		}

		private void OnValidate()
		{
			if (!playAsPlaylist)
			{
				shuffleTracks = false;
			}
		}

	}
}

