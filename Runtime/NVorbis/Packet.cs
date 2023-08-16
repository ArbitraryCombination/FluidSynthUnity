using System;
using System.Collections.Generic;

namespace NVorbis {
	/// Describes a packet of data from a data stream.
	public sealed class Packet {

		private ulong _bitBucket;
		private int _bitCount;
		private byte _overflowBits;
		private PacketFlags _packetFlags;
		
		private ArraySegment<byte> _data;
		private int _dataIndex; // 4

		private int _dataOfs; // 4
		// size with 1-2 packet segments (> 2 packet segments should be very uncommon):
		//   x86:  68 bytes
		//   x64: 104 bytes

		// this is the list of pages & packets in packed 24:8 format
		// in theory, this is good for up to 1016 GiB of Ogg file
		// in practice, probably closer to 300 days @ 160k bps
		private readonly IReadOnlyList<int> _dataParts;
		private readonly PacketProvider _packetReader;

		public Packet(IReadOnlyList<int> dataParts, PacketProvider packetReader, ArraySegment<byte> initialData) {
			_dataParts = dataParts;
			_packetReader = packetReader;
			_data = initialData;
		}

		/// <summary>
		///     Gets the granule position of the packet, if known.
		/// </summary>
		public long? GranulePosition { get; set; }

		/// <summary>
		///     Gets whether this packet occurs immediately following a loss of sync in the stream.
		/// </summary>
		public bool IsResync {
			get => GetFlag(PacketFlags.IsResync);
			set => SetFlag(PacketFlags.IsResync, value);
		}

		/// <summary>
		///     Gets whether this packet did not read its full data.
		/// </summary>
		public bool IsShort {
			get => GetFlag(PacketFlags.IsShort);
			private set => SetFlag(PacketFlags.IsShort, value);
		}

		/// <summary>
		///     Gets whether the packet is the last packet of the stream.
		/// </summary>
		public bool IsEndOfStream {
			get => GetFlag(PacketFlags.IsEndOfStream);
			set => SetFlag(PacketFlags.IsEndOfStream, value);
		}

		/// <summary>
		///     Gets the number of bits read from the packet.
		/// </summary>
		private int BitsRead { get; set; }

		/// <summary>
		///     Reads the specified number of bits from the packet and advances the read position.
		/// </summary>
		/// <param name="count">The number of bits to read.</param>
		/// <returns>The value read.  If not enough bits remained, this will be a truncated value.</returns>
		public ulong ReadBits(int count) {
			var value = TryPeekBits(count, out _);
			SkipBits(count);
			return value;
		}

		/// <summary>
		///     Attempts to read the specified number of bits from the packet.  Does not advance the read position.
		/// </summary>
		/// <param name="count">The number of bits to read.</param>
		/// <param name="bitsRead">Outputs the actual number of bits read.</param>
		/// <returns>The value of the bits read.</returns>
		public ulong TryPeekBits(int count, out int bitsRead) {
			if (count < 0 || count > 64) throw new ArgumentOutOfRangeException(nameof(count));
			if (count == 0) {
				bitsRead = 0;
				return 0UL;
			}

			ulong value;
			while (_bitCount < count) {
				var val = ReadNextByte();
				if (val == -1) {
					bitsRead = _bitCount;
					value = _bitBucket;
					return value;
				}

				_bitBucket = ((ulong) (val & 0xFF) << _bitCount) | _bitBucket;
				_bitCount += 8;

				if (_bitCount > 64) _overflowBits = (byte) (val >> (72 - _bitCount));
			}

			value = _bitBucket;

			if (count < 64) value &= (1UL << count) - 1;

			bitsRead = count;
			return value;
		}

		/// <summary>
		///     Advances the read position by the the specified number of bits.
		/// </summary>
		/// <param name="count">The number of bits to skip reading.</param>
		public void SkipBits(int count) {
			if (count <= 0) return;
			if (_bitCount > count) {
				// we still have bits left over...
				if (count > 63)
					_bitBucket = 0;
				else
					_bitBucket >>= count;

				if (_bitCount > 64) {
					var overflowCount = _bitCount - 64;
					_bitBucket |= (ulong) _overflowBits << (_bitCount - count - overflowCount);

					if (overflowCount > count) // ugh, we have to keep bits in overflow
						_overflowBits >>= count;
				}

				_bitCount -= count;
				BitsRead += count;
			} else if (_bitCount == count) {
				_bitBucket = 0UL;
				_bitCount = 0;
				BitsRead += count;
			} else { //  _bitCount < count
				// we have to move more bits than we have available...
				count -= _bitCount;
				BitsRead += _bitCount;
				_bitCount = 0;
				_bitBucket = 0;

				while (count > 8) {
					if (ReadNextByte() == -1) {
						count = 0;
						IsShort = true;
						break;
					}

					count -= 8;
					BitsRead += 8;
				}

				if (count > 0) {
					var temp = ReadNextByte();
					if (temp == -1) {
						IsShort = true;
					} else {
						_bitBucket = (ulong) (temp >> count);
						_bitCount = 8 - count;
						BitsRead += count;
					}
				}
			}
		}

		private bool GetFlag(PacketFlags flag) {
			return _packetFlags.HasFlag(flag);
		}

		private void SetFlag(PacketFlags flag, bool value) {
			if (value)
				_packetFlags |= flag;
			else
				_packetFlags &= ~flag;
		}

		/// <summary>
		///     Reads the next byte in the packet.
		/// </summary>
		/// <returns>The next byte in the packet, or <c>-1</c> if no more data is available.</returns>
		private int ReadNextByte() {
			if (_dataIndex == _dataParts.Count) return -1;

			var b = _data.Get(_dataOfs);

			if (++_dataOfs == _data.Count) {
				_dataOfs = 0;
				if (++_dataIndex < _dataParts.Count)
					_data = _packetReader.GetPacketData(_dataParts[_dataIndex]);
				else
					_data = new ArraySegment<byte>(Utils.EMPTY_BYTE_ARRAY);
			}

			return b;
		}

		/// <summary>
		///     Defines flags to apply to the current packet
		/// </summary>
		[Flags]
		// for now, let's use a byte... if we find we need more space, we can always expand it...
		private enum PacketFlags : byte {
			/// <summary>
			///     Packet is first since reader had to resync with stream.
			/// </summary>
			IsResync = 0x01,

			/// <summary>
			///     Packet is the last in the logical stream.
			/// </summary>
			IsEndOfStream = 0x02,

			/// <summary>
			///     Packet does not have all its data available.
			/// </summary>
			IsShort = 0x04,
		}
	}
}