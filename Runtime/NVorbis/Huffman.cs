using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace NVorbis {
	
	internal readonly struct HuffmanListNode : IComparable<HuffmanListNode> {
		internal readonly int Bits;

		internal readonly int Length;
		internal readonly int Mask;
		internal readonly int Value;
		
		public HuffmanListNode(int bits, int length, int mask, int value) {
			Bits = bits;
			Length = length;
			Mask = mask;
			Value = value;
		}

		public int CompareTo(HuffmanListNode other) {
			var len = Length - other.Length;
			if (len != 0) return len;
			return Bits - other.Bits;
		}
	}
	
	internal static class Huffman {
		private const int MAX_TABLE_BITS = 10;

		public static void GenerateTable(
			[CanBeNull] int[] values, int[] lengthList, int[] codeList,
			out int tableBits, out HuffmanListNode[] prefixTree, out List<HuffmanListNode> overflowList) {
			var list = new HuffmanListNode[lengthList.Length];

			var maxLen = 0;
			for (var i = 0; i < list.Length; i++) {
				list[i] = new HuffmanListNode(
					codeList[i],
					lengthList[i] <= 0 ? 99999 : lengthList[i],
					(1 << lengthList[i]) - 1,
					values?[i] ?? i
				);
				if (lengthList[i] > 0 && maxLen < lengthList[i]) maxLen = lengthList[i];
			}

			Array.Sort(list, 0, list.Length);

			tableBits = maxLen > MAX_TABLE_BITS ? MAX_TABLE_BITS : maxLen;

			prefixTree = new HuffmanListNode[1 << tableBits];
			overflowList = null;
			for (var i = 0; i < list.Length && list[i].Length < 99999; i++) {
				var itemBits = list[i].Length;
				if (itemBits > tableBits) {
					overflowList = new List<HuffmanListNode>(list.Length - i);
					for (; i < list.Length && list[i].Length < 99999; i++) overflowList.Add(list[i]);
				} else {
					var maxVal = 1 << (tableBits - itemBits);
					var item = list[i];
					for (var j = 0; j < maxVal; j++) {
						var idx = (j << itemBits) | item.Bits;

						prefixTree[idx] = item;
					}
				}
			}
		}
	}
}