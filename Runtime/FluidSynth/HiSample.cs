namespace FluidSynth {

	/// <summary>
	/// Sample from fluid_sample_t
	/// </summary>
	public class HiSample {
		/// <summary>
		/// unique item id (see int note above)
		/// </summary>
		public int ItemId;

		public string Name;
		public uint Start;
		public uint End; /* Note: Index of last valid sample point (contrary to SF spec) */
		public uint LoopStart;
		public uint LoopEnd; /* Note: first point following the loop (superimposed on loopstart) */
		public int Correctedloopstart; // defined in instrument, used only when ave are extracted
		public int Correctedloopend; // defined in instrument, used only when ave are extracted
		public int Correctedcoarseloopstart; // defined in instrument, used only when ave are extracted
		public int Correctedcoarseloopend; // defined in instrument, used only when ave are extracted
		public uint SampleRate;

		/// <summary>
		/// Contains the MIDI key number of the recorded pitch of the sample.
		/// For example, a recording of an instrument playing middle C(261.62 Hz) should receive a value of 60.
		/// This value is used as the default “root key” for the sample, so that in the example, a MIDI key-on command for note number 60 would reproduce the sound at its original pitch.
		/// For unpitched sounds, a conventional value of 255 should be used.Values between 128 and 254 are illegal.
		/// Whenever an illegal value or a value of 255 is encountered, the value 60 should be used.
		/// </summary>
		public int OrigPitch;

		/// origpitch sets MIDI root note while pitchadj is a fine tuning amount  which offsets the original rate.This means that the fine tuning is inverted
		/// with respect to the root note(so subtract it, not add).
		public int PitchAdj;

		public int SampleType;

		private static readonly float[] NO_DATA = new float[0];
		public float[] Data = NO_DATA;
	}

}
