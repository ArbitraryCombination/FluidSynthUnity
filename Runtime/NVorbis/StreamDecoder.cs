using System;
using System.IO;

namespace NVorbis {
	///     Describes a stream decoder instance for Vorbis data.
	public sealed class StreamDecoder {
		private int _block0Size;
		private int _block1Size;

		private byte _channels;

		private long _currentPosition = 0L;
		private bool _eosFound = false;
		private bool _hasPosition = false;
		private int _modeFieldBits;
		private Mode[] _modes;

		private float[][] _nextPacketBuf;

		private readonly PacketProvider _packetProvider;
		private float[][] _prevPacketBuf = null;
		private int _prevPacketEnd = 0;
		private int _prevPacketStart = 0;
		private int _prevPacketStop = 0;

		internal StreamDecoder(PacketProvider packetProvider) {
			_packetProvider = packetProvider;
			
			if (!ProcessHeaderPacket(LoadStreamHeader)
			    || !ProcessHeaderPacket(LoadComments)
			    || !ProcessHeaderPacket(LoadBooks)) {
				throw new ArgumentException("Could not find Vorbis data to decode.");
			}
		}

		#region Init

		private bool ProcessHeaderPacket(Func<Packet, bool> processAction) {
			var packet = _packetProvider.GetNextPacket();
			return packet != null && processAction(packet);
		}

		private static readonly byte[] PacketSignatureStream = {0x01, 0x76, 0x6f, 0x72, 0x62, 0x69, 0x73, 0x00, 0x00, 0x00, 0x00};
		private static readonly byte[] PacketSignatureComments = {0x03, 0x76, 0x6f, 0x72, 0x62, 0x69, 0x73};
		private static readonly byte[] PacketSignatureBooks = {0x05, 0x76, 0x6f, 0x72, 0x62, 0x69, 0x73};

		private static bool ValidateHeader(Packet packet, byte[] expected) {
			foreach (var e in expected)
				if (e != packet.ReadBits(8))
					return false;

			return true;
		}

		private bool LoadStreamHeader(Packet packet) {
			if (!ValidateHeader(packet, PacketSignatureStream)) return false;

			_channels = (byte) packet.ReadBits(8);
			SampleRate = (int) packet.ReadBits(32);
			UpperBitrate = (int) packet.ReadBits(32);
			NominalBitrate = (int) packet.ReadBits(32);
			LowerBitrate = (int) packet.ReadBits(32);

			_block0Size = 1 << (int) packet.ReadBits(4);
			_block1Size = 1 << (int) packet.ReadBits(4);

			if (NominalBitrate == 0 && UpperBitrate > 0 && LowerBitrate > 0) NominalBitrate = (UpperBitrate + LowerBitrate) / 2;

			return true;
		}

		private bool LoadComments(Packet packet) {
			return ValidateHeader(packet, PacketSignatureComments);
		}

		private bool LoadBooks(Packet packet) {
			if (!ValidateHeader(packet, PacketSignatureBooks)) return false;

			// read the books
			var books = new Codebook[packet.ReadBits(8) + 1];
			for (var i = 0; i < books.Length; i++) {
				books[i] = new Codebook();
				books[i].Init(packet);
			}

			// Vorbis never used this feature, so we just skip the appropriate number of bits
			var times = (int) packet.ReadBits(6) + 1;
			packet.SkipBits(16 * times);

			// read the floors
			var floors = new IFloor[packet.ReadBits(6) + 1];
			for (var i = 0; i < floors.Length; i++) {
				var type = (int) packet.ReadBits(16);
				IFloor floor;
				switch (type) {
					case 0: 
						floor = new Floor0();
						break;
					case 1:
						floor = new Floor1();
						break;
					default: throw new InvalidDataException("Invalid floor type!");
				}
				
				floors[i] = floor;
				floors[i].Init(packet, _channels, _block0Size, _block1Size, books);
			}

			// read the residues
			var residues = new IResidue[packet.ReadBits(6) + 1];
			for (var i = 0; i < residues.Length; i++) {
				var type = (int) packet.ReadBits(16);
				IResidue residue;
				switch (type) {
					case 0: 
						residue = new Residue0(); 
						break;
					case 1: 
						residue = new Residue1(); 
						break;
					case 2: 
						residue = new Residue2(); 
						break;
					default: throw new InvalidDataException("Invalid residue type!");
				}
				
				residues[i] = residue;
				residues[i].Init(packet, _channels, books);
			}

			// read the mappings
			var mappings = new Mapping[packet.ReadBits(6) + 1];
			for (var i = 0; i < mappings.Length; i++) {
				if (packet.ReadBits(16) != 0) {
					throw new InvalidDataException("Invalid mapping type!");
				}

				mappings[i] = new Mapping();
				mappings[i].Init(packet, _channels, floors, residues);
			}

			// read the modes
			_modes = new Mode[packet.ReadBits(6) + 1];
			for (var i = 0; i < _modes.Length; i++) {
				_modes[i] = new Mode();
				_modes[i].Init(packet, _channels, _block0Size, _block1Size, mappings);
			}

			// verify the closing bit
			if (!packet.ReadBit()) throw new InvalidDataException("Book packet did not end on correct bit!");

			// save off the number of bits to read to determine packet mode
			_modeFieldBits = Utils.ilog(_modes.Length - 1);

			return true;
		}

		#endregion

		#region Decoding

		/// <summary>
		///     Reads samples into the specified buffer.
		/// </summary>
		/// <param name="buffer">The buffer to read the samples into.</param>
		/// <param name="offset">The index to start reading samples into the buffer.</param>
		/// <param name="count">
		///     The number of samples that should be read into the buffer.  Must be a multiple of
		///     <see cref="Channels" />.
		/// </param>
		/// <returns>The number of samples read into the buffer.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		///     Thrown when the buffer is too small or <paramref name="offset" /> is less
		///     than zero.
		/// </exception>
		/// <remarks>
		///     The data populated into <paramref name="buffer" /> is interleaved by channel in normal PCM fashion: Left,
		///     Right, Left, Right, Left, Right
		/// </remarks>
		public int ReadSamples(float[] buffer, int offset, int count) {
			if (buffer == null) throw new ArgumentNullException(nameof(buffer));
			if (offset < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
			if (count % _channels != 0) throw new ArgumentOutOfRangeException(nameof(count), "Must be a multiple of Channels!");
			if (_packetProvider == null) throw new ObjectDisposedException(nameof(StreamDecoder));

			// if the caller didn't ask for any data, bail early
			if (count <= 0) return 0;

			// save off value to track when we're done with the request
			var idx = offset;
			var tgt = offset + count;

			// try to fill the buffer; drain the last buffer if EOS, resync, bad packet, or parameter change
			while (idx < tgt) {
				// if we don't have any more valid data in the current packet, read in the next packet
				if (_prevPacketStart == _prevPacketEnd) {
					if (_eosFound) {
						_nextPacketBuf = null;
						_prevPacketBuf = null;

						// no more samples, so just return
						break;
					}

					if (!ReadNextPacket((idx - offset) / _channels, out var samplePosition)) // drain the current packet (the windowing will fade it out)
						_prevPacketEnd = _prevPacketStop;

					// if we need to pick up a position, and the packet had one, apply the position now
					if (samplePosition.HasValue && !_hasPosition) {
						_hasPosition = true;
						_currentPosition = samplePosition.Value - (_prevPacketEnd - _prevPacketStart) - (idx - offset) / _channels;
					}
				}

				// we read out the valid samples from the previous packet
				var copyLen = Math.Min((tgt - idx) / _channels, _prevPacketEnd - _prevPacketStart);
				if (copyLen > 0) {
					idx += CopyBuffer(buffer, idx, copyLen);
				}
			}

			// update the count of floats written
			count = idx - offset;

			// update the position
			_currentPosition += count / _channels;

			// return count of floats written
			return count;
		}

		private int CopyBuffer(float[] target, int targetIndex, int count) {
			var idx = targetIndex;
			for (; count > 0; _prevPacketStart++, count--) {
				for (var ch = 0; ch < _channels; ch++)
					target[idx++] = _prevPacketBuf[ch][_prevPacketStart];
			}

			return idx - targetIndex;
		}

		private bool ReadNextPacket(int bufferedSamples, out long? samplePosition) {
			// decode the next packet now so we can start overlapping with it
			var curPacket = DecodeNextPacket(out var startIndex, out var validLen, out var totalLen, out var isEndOfStream, out samplePosition);
			_eosFound |= isEndOfStream;
			if (curPacket == null) {
				return false;
			}

			// if we get a max sample position, back off our valid length to match
			if (samplePosition.HasValue && isEndOfStream) {
				var actualEnd = _currentPosition + bufferedSamples + validLen - startIndex;
				var diff = (int) (samplePosition.Value - actualEnd);
				if (diff < 0) validLen += diff;
			}

			// start overlapping (if we don't have an previous packet data, just loop and the previous packet logic will handle things appropriately)
			if (_prevPacketEnd > 0) {
				// overlap the first samples in the packet with the previous packet, then loop
				OverlapBuffers(_prevPacketBuf, curPacket, _prevPacketStart, _prevPacketStop, startIndex, _channels);
				_prevPacketStart = startIndex;
			} else if (_prevPacketBuf == null) {
				// first packet, so it doesn't have any good data before the valid length
				_prevPacketStart = validLen;
			}

			// keep the old buffer so the GC doesn't have to reallocate every packet
			_nextPacketBuf = _prevPacketBuf;

			// save off our current packet's data for the next pass
			_prevPacketEnd = validLen;
			_prevPacketStop = totalLen;
			_prevPacketBuf = curPacket;
			return true;
		}

		private float[][] DecodeNextPacket(out int packetStartindex, out int packetValidLength, out int packetTotalLength, out bool isEndOfStream, out long? samplePosition) {
			Packet packet = _packetProvider.GetNextPacket();
			if (packet == null) {
				// no packet? we're at the end of the stream
				isEndOfStream = true;
			} else {
				// if the packet is flagged as the end of the stream, we can safely mark _eosFound
				isEndOfStream = packet.IsEndOfStream;

				// resync... that means we've probably lost some data; pick up a new position
				if (packet.IsResync) _hasPosition = false;

				// make sure the packet starts with a 0 bit as per the spec
				if (packet.ReadBit()) {
				} else {
					// if we get here, we should have a good packet; decode it and add it to the buffer
					var mode = _modes[(int) packet.ReadBits(_modeFieldBits)];
					if (_nextPacketBuf == null) {
						_nextPacketBuf = new float[_channels][];
						for (var i = 0; i < _channels; i++) _nextPacketBuf[i] = new float[_block1Size];
					}

					if (mode.Decode(packet, _nextPacketBuf, out packetStartindex, out packetValidLength, out packetTotalLength)) {
						// per the spec, do not decode more samples than the last granulePosition
						samplePosition = packet.GranulePosition;
						return _nextPacketBuf;
					}
				}
			}

			packetStartindex = 0;
			packetValidLength = 0;
			packetTotalLength = 0;
			samplePosition = null;
			return null;
		}

		private static void OverlapBuffers(float[][] previous, float[][] next, int prevStart, int prevLen, int nextStart, int channels) {
			for (; prevStart < prevLen; prevStart++, nextStart++)
			for (var c = 0; c < channels; c++)
				next[c][nextStart] += previous[c][prevStart];
		}

		#endregion

		#region Properties

		/// <summary>
		///     Gets the number of channels in the stream.
		/// </summary>
		public int Channels => _channels;

		/// <summary>
		///     Gets the sample rate of the stream.
		/// </summary>
		public int SampleRate { get; private set; }

		/// <summary>
		///     Gets the upper bitrate limit for the stream, if specified.
		/// </summary>
		public int UpperBitrate { get; private set; }

		/// <summary>
		///     Gets the nominal bitrate of the stream, if specified.  May be calculated from <see cref="LowerBitrate" /> and
		///     <see cref="UpperBitrate" />.
		/// </summary>
		public int NominalBitrate { get; private set; }

		/// <summary>
		///     Gets the lower bitrate limit for the stream, if specified.
		/// </summary>
		public int LowerBitrate { get; private set; }

		/// <summary>
		///     Gets the total number of samples in the decoded stream.
		/// </summary>
		public long TotalSamples => _packetProvider?.GetGranuleCount() ?? throw new ObjectDisposedException(nameof(StreamDecoder));

		/// <summary>
		///     Gets whether the decoder has reached the end of the stream.
		/// </summary>
		public bool IsEndOfStream => _eosFound && _prevPacketBuf == null;

		#endregion

	}
}