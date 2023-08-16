using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MidiPlayerTK {
	
	public abstract class AudioGeneratorBehavior : MonoBehaviour {
		
		public AudioSource CoreAudioSource;
		
		protected int OutputRate;
		private bool playing = false;
		
		private static AudioClip lastUnitAudioClip = null;
		
		protected void Awake() {
			OutputRate = AudioSettings.GetConfiguration().sampleRate;
			AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;

			CoreAudioSource.clip = getOrCreateUnitSilence();
			InitializeAudioGenerator();
			
			// Reset voices statistics
			audioProcessingLoad = 0f;
			audioProcessingLoadMax = 0.2f;

			playing = true;
			CoreAudioSource.Play();
		}
		
		/** For measuring OnAudioFilterRead latency (time between invocations) and
		 * load (what percentage of total time is the function running). */
		private long lastOnAudioFilterReadStartTime = 0;

		private float audioProcessingLoad = 0f;
		private float audioProcessingLoadMax = 0.2f;
		
		private void OnAudioFilterRead(float[] data, int channels) {
			long startTime = Stopwatch.GetTimestamp();
			long latency = lastOnAudioFilterReadStartTime == 0 ? Stopwatch.Frequency/* just big number */ : startTime - lastOnAudioFilterReadStartTime;
			lastOnAudioFilterReadStartTime = startTime;

			if (!playing) {
				Array.Clear(data, 0, data.Length);
				audioProcessingLoad = 0f;
				return;
			}
			
			long ticks = DateTime.UtcNow.Ticks;
			GenerateAudio(data, channels, ticks);

			long duration = Stopwatch.GetTimestamp() - startTime;
			float newAudioProcessingLoad = duration / (float) latency;
			// Use low-pass filter to prevent random spikes throwing the measurement off
			audioProcessingLoad = audioProcessingLoad * 0.5f + newAudioProcessingLoad * 0.5f;
			if (audioProcessingLoad > audioProcessingLoadMax) {
				audioProcessingLoadMax = audioProcessingLoad;
				Debug.Log("Audio processing load: "+audioProcessingLoad);
			}
		}

		/// Get current audio configuration
		private void OnAudioConfigurationChanged(bool deviceWasChanged) {
			AudioConfiguration GetConfiguration = AudioSettings.GetConfiguration();
			int newSampleRate = GetConfiguration.sampleRate;
			if (OutputRate == newSampleRate) {
				// No meaningful change
				return;
			}

			Debug.Log("Sample rate changed to "+newSampleRate);
			OutputRate = newSampleRate;
			var source = CoreAudioSource;
			if (source != null) {
				source.Stop();
				source.clip = getOrCreateUnitSilence();
			}

			OnSampleRateChanged(newSampleRate);

			if (source != null) {
				source.Play();
			}
		}
		
		private void OnApplicationQuit() {
			playing = false;
		}

		protected float AudioProcessingLoad => audioProcessingLoad;

		protected abstract void InitializeAudioGenerator();
		
		protected abstract void GenerateAudio(float[] data, int channels, long ticksNow);
		
		protected abstract void OnSampleRateChanged(int newSampleRate);

		/**
		 * Obtain an AudioClip full of ones at the current sampling frequency.
		 * This is done so that Unity does not have to resample it and we hopefully gain some perf.
		 * This whole garbage is required, because generating audio on the fly with custom AudioClip
		 * is not possible, since that method has abysmal latency (~400ms).
		 * We have to use OnAudioFilterRead instead (latency ~5ms), but that in turn is invoked AFTER panning,
		 * which is too late. Hence this mess.
		 */
		public static AudioClip getOrCreateUnitSilence() {
			if (lastUnitAudioClip == null || lastUnitAudioClip.frequency != AudioSettings.outputSampleRate) {
				lastUnitAudioClip = AudioClip.Create("unit_silence", 8192, 1, AudioSettings.outputSampleRate, false, GenerateOnes);
			}
			return lastUnitAudioClip;
		}

		private static void GenerateOnes(float[] data) {
			int l = data.Length;
			for (int i = 0; i < l; i++) {
				data[i] = 1f;
			}
		}
	}
}