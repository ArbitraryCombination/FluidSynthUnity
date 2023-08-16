using System;
using UnityEngine;

namespace NVorbis {
	internal static class Utils {

		public static readonly byte[] EMPTY_BYTE_ARRAY = new byte[0];

		internal static int ilog(int x) {
			var cnt = 0;
			while (x > 0) {
				++cnt;
				x >>= 1; // this is safe because we'll never get here if the sign bit is set
			}

			return cnt;
		}

		internal static uint BitReverse(uint n, int bits = 32) {
			n = ((n & 0xAAAAAAAA) >> 1) | ((n & 0x55555555) << 1);
			n = ((n & 0xCCCCCCCC) >> 2) | ((n & 0x33333333) << 2);
			n = ((n & 0xF0F0F0F0) >> 4) | ((n & 0x0F0F0F0F) << 4);
			n = ((n & 0xFF00FF00) >> 8) | ((n & 0x00FF00FF) << 8);
			return ((n >> 16) | (n << 16)) >> (32 - bits);
		}

		internal static float ConvertFromVorbisFloat32(uint bits) {
			// do as much as possible with bit tricks in integer math
			var sign = (int) bits >> 31; // sign-extend to the full 32-bits
			var exponent = (double) ((int) ((bits & 0x7fe00000) >> 21) - 788); // grab the exponent, remove the bias, store as double (for the call to System.Math.Pow(...))
			var mantissa = (float) (((bits & 0x1fffff) ^ sign) + (sign & 1)); // grab the mantissa and apply the sign bit.  store as float

			// NB: We could use bit tricks to calc the exponent, but it can't be more than 63 in either direction.
			//     This creates an issue, since the exponent field allows for a *lot* more than that.
			//     On the flip side, larger exponent values don't seem to be used by the Vorbis codebooks...
			//     Either way, we'll play it safe and let the BCL calculate it.

			// now switch to single-precision and calc the return value
			return mantissa * (float) Math.Pow(2.0, exponent);
		}

		public static T Get<T>(this ArraySegment<T> segment, int index) {
			var array = segment.Array;
			Debug.Assert(array != null);
			Debug.Assert(index >= 0 && index < segment.Count);
			return array[segment.Offset + index];
		}
		
		public static uint GetUint(this ArraySegment<byte> segment, int index) {
			var array = segment.Array;
			Debug.Assert(array != null);
			Debug.Assert(index >= 0 && index + 4 <= segment.Count);
			return BitConverter.ToUInt32(array, segment.Offset + index);
		}
		
		public static int GetInt(this ArraySegment<byte> segment, int index) {
			var array = segment.Array;
			Debug.Assert(array != null);
			Debug.Assert(index >= 0 && index + 4 <= segment.Count);
			return BitConverter.ToInt32(array, segment.Offset + index);
		}
		
		public static long GetLong(this ArraySegment<byte> segment, int index) {
			var array = segment.Array;
			Debug.Assert(array != null);
			Debug.Assert(index >= 0 && index + 8 <= segment.Count);
			return BitConverter.ToInt64(array, segment.Offset + index);
		}

		public static ArraySegment<T> SubSegment<T>(this ArraySegment<T> segment, int offset, int length) {
			Debug.Assert(offset >= 0 && offset <= segment.Count);
			Debug.Assert(length >= 0 && offset + length <= segment.Count);
			var segmentArray = segment.Array;
			Debug.Assert(segmentArray != null);
			return new ArraySegment<T>(segmentArray, segment.Offset + offset, length);
		}

	}
}