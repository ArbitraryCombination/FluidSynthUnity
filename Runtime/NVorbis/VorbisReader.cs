using System.Collections.Generic;

namespace NVorbis {
	/// Implements an easy to use wrapper around <see cref="PageReader" />
	public sealed class VorbisReader {
		
		public readonly PageReader reader;
		public readonly List<StreamDecoder> decoders = new List<StreamDecoder>();

		/// Creates a new instance of <see cref="VorbisReader" /> reading from the specified array.
		public VorbisReader(byte[] oggData) {
			reader = new PageReader(oggData, ProcessNewStream);

			while (reader.ReadNextPage(out _) && decoders.Count == 0) {
				// Read until first stream is found
			}
		}

		private bool ProcessNewStream(PacketProvider packetProvider) {
			var decoder = new StreamDecoder(packetProvider);
			decoders.Add(decoder);
			return true;
		}
	}
}