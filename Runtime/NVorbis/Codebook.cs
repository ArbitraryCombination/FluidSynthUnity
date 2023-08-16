using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;

namespace NVorbis {
	internal class Codebook {

		private int[] _lengths;
		private float[] _lookupTable;
		private int _maxBits;
		[CanBeNull] private List<HuffmanListNode> _overflowList;
		private int _prefixBitLength;
		private HuffmanListNode[] _prefixList;

		public void Init(Packet packet) {
			// first, check the sync pattern
			var chkVal = packet.ReadBits(24);
			if (chkVal != 0x564342UL) throw new InvalidDataException("Book header had invalid signature!");

			// get the counts
			Dimensions = (int) packet.ReadBits(16);
			Entries = (int) packet.ReadBits(24);

			// init the storage
			_lengths = new int[Entries];

			InitTree(packet);
			InitLookupTable(packet);
		}

		public int DecodeScalar(Packet packet) {
			var data = (int) packet.TryPeekBits(_prefixBitLength, out var bitsRead);
			if (bitsRead == 0) return -1;

			// try to get the value from the prefix list...
			var node = _prefixList[data];
			if (node.Length != 0) {
				packet.SkipBits(node.Length);
				return node.Value;
			}

			// nope, not possible... run through the overflow nodes
			data = (int) packet.TryPeekBits(_maxBits, out _);

			for (var i = 0; i < _overflowList.Count; i++) {
				node = _overflowList[i];
				if (node.Bits == (data & node.Mask)) {
					packet.SkipBits(node.Length);
					return node.Value;
				}
			}

			return -1;
		}

		public float this[int entry, int dim] => _lookupTable[entry * Dimensions + dim];

		public int Dimensions { get; private set; }

		public int Entries { get; private set; }

		public int MapType { get; private set; }

		private void InitTree(Packet packet) {
			bool sparse;
			var total = 0;

			int maxLen;
			if (packet.ReadBit()) {
				// ordered
				var len = (int) packet.ReadBits(5) + 1;
				for (var i = 0; i < Entries;) {
					var cnt = (int) packet.ReadBits(Utils.ilog(Entries - i));

					while (--cnt >= 0) _lengths[i++] = len;

					++len;
				}

				total = 0;
				sparse = false;
				maxLen = len;
			} else {
				// unordered
				maxLen = -1;
				sparse = packet.ReadBit();
				for (var i = 0; i < Entries; i++) {
					if (!sparse || packet.ReadBit()) {
						var length = (int) packet.ReadBits(5) + 1;
						_lengths[i] = length;
						++total;
						maxLen = Math.Max(maxLen, length);
					} else {
						// mark the entry as unused
						_lengths[i] = -1;
					}
				}
			}

			// figure out the maximum bit size; if all are unused, don't do anything else
			if ((_maxBits = maxLen) > -1) {
				int[] codewordLengths = null;
				if (sparse && total >= Entries >> 2) {
					codewordLengths = new int[Entries];
					Array.Copy(_lengths, codewordLengths, Entries);

					sparse = false;
				}

				int sortedCount;
				// compute size of sorted tables
				if (sparse)
					sortedCount = total;
				else
					sortedCount = 0;

				int[] values = null;
				int[] codewords = null;
				if (!sparse) {
					codewords = new int[Entries];
				} else if (sortedCount != 0) {
					codewordLengths = new int[sortedCount];
					codewords = new int[sortedCount];
					values = new int[sortedCount];
				}

				if (!ComputeCodewords(sparse, codewords, codewordLengths, _lengths, Entries, values)) throw new InvalidDataException();

				Huffman.GenerateTable(values, codewordLengths ?? _lengths, codewords, out _prefixBitLength, out _prefixList, out _overflowList);
			}
		}

		private bool ComputeCodewords(bool sparse, int[] codewords, int[] codewordLengths, int[] len, int n, int[] values) {
			int i, k, m = 0;
			var available = new uint[32];

			for (k = 0; k < n; ++k)
				if (len[k] > 0)
					break;
			if (k == n) return true;

			AddEntry(sparse, codewords, codewordLengths, 0, k, m++, len[k], values);

			for (i = 1; i <= len[k]; ++i) available[i] = 1U << (32 - i);

			for (i = k + 1; i < n; ++i) {
				uint res;
				int z = len[i], y;
				if (z <= 0) continue;

				while (z > 0 && available[z] == 0) --z;
				if (z == 0) return false;
				res = available[z];
				available[z] = 0;
				AddEntry(sparse, codewords, codewordLengths, Utils.BitReverse(res), i, m++, len[i], values);

				if (z != len[i])
					for (y = len[i]; y > z; --y)
						available[y] = res + (1U << (32 - y));
			}

			return true;
		}

		private void AddEntry(bool sparse, int[] codewords, int[] codewordLengths, uint huffCode, int symbol, int count, int len, int[] values) {
			if (sparse) {
				codewords[count] = (int) huffCode;
				codewordLengths[count] = len;
				values[count] = symbol;
			} else {
				codewords[symbol] = (int) huffCode;
			}
		}

		private void InitLookupTable(Packet packet) {
			MapType = (int) packet.ReadBits(4);
			if (MapType == 0) return;

			var minValue = Utils.ConvertFromVorbisFloat32((uint) packet.ReadBits(32));
			var deltaValue = Utils.ConvertFromVorbisFloat32((uint) packet.ReadBits(32));
			var valueBits = (int) packet.ReadBits(4) + 1;
			var sequence_p = packet.ReadBit();

			var lookupValueCount = Entries * Dimensions;
			var lookupTable = new float[lookupValueCount];
			if (MapType == 1) lookupValueCount = lookup1_values();

			var multiplicands = new uint[lookupValueCount];
			for (var i = 0; i < lookupValueCount; i++) multiplicands[i] = (uint) packet.ReadBits(valueBits);

			// now that we have the initial data read in, calculate the entry tree
			if (MapType == 1)
				for (var idx = 0; idx < Entries; idx++) {
					var last = 0.0;
					var idxDiv = 1;
					for (var i = 0; i < Dimensions; i++) {
						var moff = idx / idxDiv % lookupValueCount;
						var value = multiplicands[moff] * deltaValue + minValue + last;
						lookupTable[idx * Dimensions + i] = (float) value;

						if (sequence_p) last = value;

						idxDiv *= lookupValueCount;
					}
				}
			else
				for (var idx = 0; idx < Entries; idx++) {
					var last = 0.0;
					var moff = idx * Dimensions;
					for (var i = 0; i < Dimensions; i++) {
						var value = multiplicands[moff] * deltaValue + minValue + last;
						lookupTable[idx * Dimensions + i] = (float) value;

						if (sequence_p) last = value;

						++moff;
					}
				}

			_lookupTable = lookupTable;
		}

		private int lookup1_values() {
			var r = (int) Math.Floor(Math.Exp(Math.Log(Entries) / Dimensions));

			if (Math.Floor(Math.Pow(r + 1, Dimensions)) <= Entries) ++r;

			return r;
		}
	}
}