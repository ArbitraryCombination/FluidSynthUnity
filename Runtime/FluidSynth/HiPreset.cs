namespace FluidSynth {

	/// <summary>
	/// Preset from a ImSoundFont
	/// </summary>
	public class HiPreset {
		/// <summary>
		/// unique item id (see int note above)
		/// </summary>
		public int ItemId;

		public string Name;

		/// <summary>
		/// the bank number
		/// </summary>
		public int Bank;

		public int Num;
		public uint Libr; /* Not used (preserved) */
		public uint Genre; /* Not used (preserved) */
		public uint Morph; /* Not used (preserved) */

		public HiZone GlobalZone;
		public HiZone[] Zone;
	}

}
