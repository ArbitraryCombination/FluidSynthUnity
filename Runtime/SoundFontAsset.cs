using System;
using UnityEngine;

namespace FluidSynthUnity {
	
	/**
	 * Unity assets required to load a sound font.
	 */
	[Serializable]
	public class SoundFontAsset {
		public TextAsset soundFont;
		public TextAsset[] samples;
	}

	public readonly struct ExtractedSoundFontAsset {
		public readonly (string name, byte[] bytes) soundFont;
		public readonly (string name, byte[] bytes)[] samples;
		
		public ExtractedSoundFontAsset(SoundFontAsset sf) {
			this.soundFont = (sf.soundFont.name, sf.soundFont.bytes);
			var sfSamples = sf.samples;
			samples = new (string, byte[])[sfSamples.Length];
			for (var i = 0; i < sfSamples.Length; i++) {
				var sample = sfSamples[i];
				samples[i] = (sample.name, sample.bytes);
			}
		}
	}
}