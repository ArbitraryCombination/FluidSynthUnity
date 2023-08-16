using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidiPlayerTK {

	public class SoundFont {

		public readonly SFData HiSf;

		public readonly Dictionary<(int bank, int num), HiPreset> Instruments =
			new Dictionary<(int bank, int num), HiPreset>();

		public readonly List<(int bank, int num)> InstrumentsByName = new List<(int, int)>();

		public SoundFont(byte[] binary, string debugName) {
			// Create sf from binary data
			SFLoad load = new SFLoad(binary, SFFile.SfSource.MPTK);
			if (load.SfData == null) {
				Debug.LogWarningFormat("Error when decoding SoundFont {0}", debugName);
				return;
			}

			HiSf = load.SfData;

			foreach (HiPreset p in HiSf.preset) {
				if (p == null) continue;

				Instruments.Add((p.Bank, p.Num), p);
				InstrumentsByName.Add((p.Bank, p.Num));
			}
			InstrumentsByName.Sort(((int, int) first, (int, int) second) =>
				string.Compare(Instruments[first].Name, Instruments[second].Name, StringComparison.Ordinal));
		}
	}

}
