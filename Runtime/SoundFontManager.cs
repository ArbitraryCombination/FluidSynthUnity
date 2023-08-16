using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using FluidSynth;
using NVorbis;
using Debug = UnityEngine.Debug;

namespace FluidSynthUnity {

	/// <summary>
	/// Singleton class to load and manage the sound font.
	/// </summary>
	public static class SoundFontManager {

		/// Current SoundFont loaded
		public static SoundFont soundFont;

		public static bool soundFontInitialized = false;

		public static readonly Dictionary<string, HiSample> soundFontSamples = new Dictionary<string, HiSample>();

		public static void LoadSoundFont(SoundFontAsset soundFontAsset) {
			var extracted = new ExtractedSoundFontAsset(soundFontAsset);
#if UNITY_WEBGL
			initAndLoadSoundFont(extracted);
#else
            System.Threading.Tasks.Task.Run(() => {
				try {
                    initAndLoadSoundFont(extracted);
				} catch (Exception e) {
                    Debug.LogException(e);
				}
			});
#endif
		}

		private static void initAndLoadSoundFont(ExtractedSoundFontAsset soundFontAsset) {
			//NOTE(jp): Lookup table initialization calls lifted out of MidiSynth
			fluid_conv.fluid_conversion_config();
			fluid_dsp_float.fluid_dsp_float_config();
			
			byte[] sfBytes = soundFontAsset.soundFont.bytes;
			var soundFont =  new SoundFont(sfBytes, soundFontAsset.soundFont.name);
			SoundFontManager.soundFont = soundFont;
			Debug.Log("SoundFont loaded");
			
			var sampleData = new Dictionary<string, float[]>();
			var soundLoadingStartTime = Stopwatch.GetTimestamp();
			
			float[] DecodeSamples((string name, byte[] bytes) soundAsset) {
				var vorbisData = soundAsset.bytes;
				
				// Note(jp): I was being lazy with optimization here,
				// VorbisReader is still way too slow (10ms average per sample),
				// and we probably don't have to load every sound at once, but whatever.
				// Parallel.For on non-web platforms helps a lot.
				// If anybody wants to continue optimizing, check this blog:
				// https://fgiesen.wordpress.com/2018/02/19/reading-bits-in-far-too-many-ways-part-1/
				// I suspect that there is a lot to be gained in Packet bit reading and in Codebook parsing and usage.
				
				var reader = new VorbisReader(vorbisData);
				var readerDecoders = reader.decoders;
				if (readerDecoders.Count != 1) {
					Debug.Log("Failed to load sample " + soundAsset.name + ": Bad amount of decoders - " + readerDecoders.Count);
					return null;
				}

				var decoder = readerDecoders[0];
				var sampleCount = decoder.TotalSamples;
				var channelCount = decoder.Channels;
				var data = new float[sampleCount * channelCount];
				int read = decoder.ReadSamples(data, 0, data.Length);
				if (read != data.Length) {
					Debug.LogWarning("Read different amount of floats than expected: " + read + ", expected: " + data.Length);
				}

				return data;
			}

#if UNITY_WEBGL
			foreach (var soundAsset in soundFontAsset.samples) {
				var data = DecodeSamples(soundAsset);
				if (data != null) {
					sampleData.Add(soundAsset.name, data);
				}
			}
#else
			Parallel.For(0, soundFontAsset.samples.Length, new ParallelOptions(), i => {
				var soundAsset = soundFontAsset.samples[i];
				var data = DecodeSamples(soundAsset);
				if (data != null) {
					lock (sampleData) {
						sampleData.Add(soundAsset.name, data);
					}
				}
			});
#endif
			
			foreach (HiSample sample in soundFont.HiSf.Samples) {
				if (sampleData.TryGetValue(sample.Name, out var data)) {
					sample.Data = data;
				} else {
					Debug.LogWarning("Sample " + sample.Name+" has no sample data");
				}
				soundFontSamples.Add(sample.Name, sample);
			}
			
			var soundLoadingDuration = Stopwatch.GetTimestamp() - soundLoadingStartTime;
			Debug.Log("Loaded " + sampleData.Count + " samples in " + (soundLoadingDuration / (float) Stopwatch.Frequency) + " s");

			soundFontInitialized = true;
		}
	}
}
