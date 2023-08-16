namespace NVorbis {
	internal static class Crc {
		private const uint CRC32_POLY = 0x04c11db7;
		private static readonly uint[] s_crcTable;

		static Crc() {
			s_crcTable = new uint[256];
			for (uint i = 0; i < 256; i++) {
				var s = i << 24;
				for (var j = 0; j < 8; ++j) s = (s << 1) ^ (s >= 1U << 31 ? CRC32_POLY : 0);

				s_crcTable[i] = s;
			}
		}
		
		public const uint EMPTY_CRC = 0U;

		public static void Update(ref uint crc, byte nextVal) {
			crc = (crc << 8) ^ s_crcTable[nextVal ^ (crc >> 24)];
		}
	}
}