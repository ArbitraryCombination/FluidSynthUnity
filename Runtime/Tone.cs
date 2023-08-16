using UnityEngine;

namespace FluidSynthUnity {
	public enum Tone : byte {
		A_0 = 21,
		AIS_0 = 22,
		B_0 = 23,
		C_1 = 24,
		CIS_1 = 25,
		D_1 = 26,
		DIS_1 = 27,
		E_1 = 28,
		F_1 = 29,
		FIS_1 = 30,
		G_1 = 31,
		GIS_1 = 32,
		A_1 = 33,
		AIS_1 = 34,
		B_1 = 35,
		C_2 = 36,
		CIS_2 = 37,
		D_2 = 38,
		DIS_2 = 39,
		E_2 = 40,
		F_2 = 41,
		FIS_2 = 42,
		G_2 = 43,
		GIS_2 = 44,
		A_2 = 45,
		AIS_2 = 46,
		B_2 = 47,
		C_3 = 48,
		CIS_3 = 49,
		D_3 = 50,
		DIS_3 = 51,
		E_3 = 52,
		F_3 = 53,
		FIS_3 = 54,
		G_3 = 55,
		GIS_3 = 56,
		A_3 = 57,
		AIS_3 = 58,
		B_3 = 59,
		C_4 = 60,
		CIS_4 = 61,
		D_4 = 62,
		DIS_4 = 63,
		E_4 = 64,
		F_4 = 65,
		FIS_4 = 66,
		G_4 = 67,
		GIS_4 = 68,
		A_4 = 69,
		AIS_4 = 70,
		B_4 = 71,
		C_5 = 72,
		CIS_5 = 73,
		D_5 = 74,
		DIS_5 = 75,
		E_5 = 76,
		F_5 = 77,
		FIS_5 = 78,
		G_5 = 79,
		GIS_5 = 80,
		A_5 = 81,
		AIS_5 = 82,
		B_5 = 83,
		C_6 = 84,
		CIS_6 = 85,
		D_6 = 86,
		DIS_6 = 87,
		E_6 = 88,
		F_6 = 89,
		FIS_6 = 90,
		G_6 = 91,
		GIS_6 = 92,
		A_6 = 93,
		AIS_6 = 94,
		B_6 = 95,
		C_7 = 96,
		CIS_7 = 97,
		D_7 = 98,
		DIS_7 = 99,
		E_7 = 100,
		F_7 = 101,
		FIS_7 = 102,
		G_7 = 103,
		GIS_7 = 104,
		A_7 = 105,
		AIS_7 = 106,
		B_7 = 107,
		C_8 = 108,
	}

	public static class ToneUtil {
		/// I think. Shouldn't include C_5, but eh.
		public static readonly Tone[] C_MAJOR_SCALE = {
			Tone.C_4,
			Tone.D_4,
			Tone.E_4,
			Tone.F_4,
			Tone.G_4,
			Tone.A_4,
			Tone.B_4,
			Tone.C_5,
		};

		public static readonly Tone[] C_MAJOR_SCALE_2_OCTAVES = {
			Tone.B_3,
			Tone.C_4,
			Tone.D_4,
			Tone.E_4,
			Tone.F_4,
			Tone.G_4,
			Tone.A_4,
			Tone.B_4,
			Tone.C_5,
			Tone.D_5,
			Tone.E_5,
			Tone.F_5,
			Tone.G_5,
			Tone.A_5,
			Tone.B_5,
			Tone.C_6,
			Tone.D_6,
		};

		public static readonly Tone[] CUSTOM = {
			Tone.C_4,
			Tone.D_4,
			Tone.E_4,
			Tone.F_4,
			Tone.G_4,
			Tone.A_4,
			Tone.B_4,
			Tone.C_5,
			Tone.FIS_4,
			Tone.GIS_4,
			Tone.D_5,
			Tone.E_5,
			Tone.F_5,
			Tone.FIS_5,
			Tone.G_5,
			Tone.GIS_5,
			Tone.A_5,
			Tone.B_5
		};

		public static readonly Tone[] PENTATONIC = {
			Tone.C_4,
			Tone.D_4,
			Tone.F_4,
			Tone.G_4,
			Tone.AIS_4,
			Tone.C_5,
		};

		public static readonly Tone[] PENTATONIC_2_OCTAVES = {
			Tone.C_4,
			Tone.D_4,
			Tone.F_4,
			Tone.G_4,
			Tone.AIS_4,
			Tone.C_5,
			Tone.D_5,
			Tone.F_5,
			Tone.G_5,
			Tone.AIS_5,
			Tone.C_6,
		};

		public static Tone Get01(this Tone[] scale, float value01) {
			var i = Mathf.RoundToInt(value01 * (scale.Length - 1));
			if (i < 0) {
				i = 0;
			} else if (i >= scale.Length) {
				i = scale.Length - 1;
			}

			return scale[i];
		}

		public static Tone GetRandom(this Tone[] scale) {
			return scale[Random.Range(0, scale.Length)];
		}
	}

}