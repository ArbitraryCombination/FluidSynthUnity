using System;
using System.IO;

namespace NVorbis {
	
	internal interface IResidue {
		void Init(Packet packet, int channels, Codebook[] codebooks);
		void Decode(Packet packet, bool[] doNotDecodeChannel, int blockSize, float[][] buffer);
	}
	
	// each channel gets its own pass, one dimension at a time
	internal class Residue0 : IResidue {
		private int _begin;

		private Codebook[][] _books;

		private int[] _cascade;

		private int _channels;
		private Codebook _classBook;
		private int _classifications;
		private int[][] _decodeMap;
		private int _end;
		private int _maxStages;
		private int _partitionSize;


		public virtual void Init(Packet packet, int channels, Codebook[] codebooks) {
			// this is pretty well stolen directly from libvorbis...  BSD license
			_begin = (int) packet.ReadBits(24);
			_end = (int) packet.ReadBits(24);
			_partitionSize = (int) packet.ReadBits(24) + 1;
			_classifications = (int) packet.ReadBits(6) + 1;
			_classBook = codebooks[(int) packet.ReadBits(8)];

			_cascade = new int[_classifications];
			var acc = 0;
			for (var i = 0; i < _classifications; i++) {
				var low_bits = (int) packet.ReadBits(3);
				if (packet.ReadBit())
					_cascade[i] = ((int) packet.ReadBits(5) << 3) | low_bits;
				else
					_cascade[i] = low_bits;

				acc += icount(_cascade[i]);
			}

			var bookNums = new int[acc];
			for (var i = 0; i < acc; i++) {
				bookNums[i] = (int) packet.ReadBits(8);
				if (codebooks[bookNums[i]].MapType == 0) throw new InvalidDataException();
			}

			var entries = _classBook.Entries;
			var dim = _classBook.Dimensions;
			var partvals = 1;
			while (dim > 0) {
				partvals *= _classifications;
				if (partvals > entries) throw new InvalidDataException();
				--dim;
			}

			// now the lookups
			_books = new Codebook[_classifications][];

			acc = 0;
			var maxstage = 0;
			int stages;
			for (var j = 0; j < _classifications; j++) {
				stages = Utils.ilog(_cascade[j]);
				_books[j] = new Codebook[stages];
				if (stages > 0) {
					maxstage = Math.Max(maxstage, stages);
					for (var k = 0; k < stages; k++)
						if ((_cascade[j] & (1 << k)) > 0)
							_books[j][k] = codebooks[bookNums[acc++]];
				}
			}

			_maxStages = maxstage;

			_decodeMap = new int[partvals][];
			for (var j = 0; j < partvals; j++) {
				var val = j;
				var mult = partvals / _classifications;
				_decodeMap[j] = new int[_classBook.Dimensions];
				for (var k = 0; k < _classBook.Dimensions; k++) {
					var deco = val / mult;
					val -= deco * mult;
					mult /= _classifications;
					_decodeMap[j][k] = deco;
				}
			}

			_channels = channels;
		}

		public virtual void Decode(Packet packet, bool[] doNotDecodeChannel, int blockSize, float[][] buffer) {
			// this is pretty well stolen directly from libvorbis...  BSD license
			var end = _end < blockSize / 2 ? _end : blockSize / 2;
			var n = end - _begin;

			if (n > 0 && Array.IndexOf(doNotDecodeChannel, false) != -1) {
				var partitionCount = n / _partitionSize;

				var partitionWords = (partitionCount + _classBook.Dimensions - 1) / _classBook.Dimensions;
				var partWordCache = new int[_channels, partitionWords][];

				for (var stage = 0; stage < _maxStages; stage++)
				for (int partitionIdx = 0, entryIdx = 0; partitionIdx < partitionCount; entryIdx++) {
					if (stage == 0)
						for (var ch = 0; ch < _channels; ch++) {
							var idx = _classBook.DecodeScalar(packet);
							if (idx >= 0 && idx < _decodeMap.Length) {
								partWordCache[ch, entryIdx] = _decodeMap[idx];
							} else {
								partitionIdx = partitionCount;
								stage = _maxStages;
								break;
							}
						}

					for (var dimensionIdx = 0; partitionIdx < partitionCount && dimensionIdx < _classBook.Dimensions; dimensionIdx++, partitionIdx++) {
						var offset = _begin + partitionIdx * _partitionSize;
						for (var ch = 0; ch < _channels; ch++) {
							var idx = partWordCache[ch, entryIdx][dimensionIdx];
							if ((_cascade[idx] & (1 << stage)) != 0) {
								var book = _books[idx][stage];
								if (book != null)
									if (WriteVectors(book, packet, buffer, ch, offset, _partitionSize)) {
										// bad packet...  exit now and try to use what we already have
										partitionIdx = partitionCount;
										stage = _maxStages;
										break;
									}
							}
						}
					}
				}
			}
		}

		private static int icount(int v) {
			var ret = 0;
			while (v != 0) {
				ret += v & 1;
				v >>= 1;
			}

			return ret;
		}

		protected virtual bool WriteVectors(Codebook codebook, Packet packet, float[][] residue, int channel, int offset, int partitionSize) {
			var res = residue[channel];
			var steps = partitionSize / codebook.Dimensions;
			var entryCache = new int[steps];

			for (var i = 0; i < steps; i++)
				if ((entryCache[i] = codebook.DecodeScalar(packet)) == -1)
					return true;

			for (var dim = 0; dim < codebook.Dimensions; dim++)
			for (var step = 0; step < steps; step++, offset++)
				res[offset] += codebook[entryCache[step], dim];

			return false;
		}
	}
	
	// each channel gets its own pass, with the dimensions interleaved
    internal class Residue1 : Residue0 {
    	protected override bool WriteVectors(Codebook codebook, Packet packet, float[][] residue, int channel, int offset, int partitionSize) {
    		var res = residue[channel];

    		for (var i = 0; i < partitionSize;) {
    			var entry = codebook.DecodeScalar(packet);
    			if (entry == -1) return true;

    			for (var j = 0; j < codebook.Dimensions; i++, j++) res[offset + i] += codebook[entry, j];
    		}

    		return false;
    	}
    }
    
    // all channels in one pass, interleaved
    internal class Residue2 : Residue0 {
	    private int _channels;

	    public override void Init(Packet packet, int channels, Codebook[] codebooks) {
		    _channels = channels;
		    base.Init(packet, 1, codebooks);
	    }

	    public override void Decode(Packet packet, bool[] doNotDecodeChannel, int blockSize, float[][] buffer) {
		    // since we're doing all channels in a single pass, the block size has to be multiplied.
		    // otherwise this is just a pass-through call
		    base.Decode(packet, doNotDecodeChannel, blockSize * _channels, buffer);
	    }

	    protected override bool WriteVectors(Codebook codebook, Packet packet, float[][] residue, int channel, int offset, int partitionSize) {
		    var chPtr = 0;

		    offset /= _channels;
		    for (var c = 0; c < partitionSize;) {
			    var entry = codebook.DecodeScalar(packet);
			    if (entry == -1) return true;

			    for (var d = 0; d < codebook.Dimensions; d++, c++) {
				    residue[chPtr][offset] += codebook[entry, d];
				    if (++chPtr == _channels) {
					    chPtr = 0;
					    offset++;
				    }
			    }
		    }

		    return false;
	    }
    }
}