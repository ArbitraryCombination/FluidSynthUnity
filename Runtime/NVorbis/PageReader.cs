using System;
using System.Collections.Generic;

namespace NVorbis {
	
	[Flags]
	public enum PageFlags {
		ContinuesPacket = 1,
		BeginningOfStream = 2,
		EndOfStream = 4,
		
		OggDefinedMask = ContinuesPacket | BeginningOfStream | EndOfStream,
		
		InternalIsResync = 8,
		InternalIsContinued = 16
	}
	
	public class PageReader {

		private readonly byte[] oggData;

		private readonly Func<PacketProvider, bool> _newStreamCallback;

		private readonly Dictionary<int, StreamPageReader> _streamReaders = new Dictionary<int, StreamPageReader>();

		private int _nextPageOffset;

		public PageReader(byte[] oggData, Func<PacketProvider, bool> newStreamCallback) {
			this.oggData = oggData;
			_newStreamCallback = newStreamCallback;
		}

		public bool ReadNextPage(out PageInfo page) {
			var isResync = false;
			page = default;

			var headerStart = _nextPageOffset;
			while (headerStart < oggData.Length - 27) {
				if (!CheckHeaderStartPoint(headerStart, out var header)
				    || !VerifyPage(header, out var pageBuf, isResync)) {
					isResync = true;
					headerStart++;
					continue;
				}

				// save state for next time
				headerStart += pageBuf.Count;
				_nextPageOffset = headerStart;

				var streamSerial = pageBuf.GetInt(14);

				page = ParsePageHeader(pageBuf, isResync);
				Page = page;

				// if the page doesn't have any packets, we can't use it
				if (page.packetCount == 0) {
					// Don't ignore the stream though
					continue;
				}

				if (_streamReaders.TryGetValue(streamSerial, out var spr)) {
					spr.AddPage(page);

					// if we've read the last page, remove from our list so cleanup can happen.
					// this is safe because the instance still has access to us for reading.
					if (page.isEndOfStream) {
						_streamReaders.Remove(streamSerial);
					}
				} else {
					var streamReader = new StreamPageReader(this);
					streamReader.AddPage(page);
					_streamReaders.Add(streamSerial, streamReader);
					_newStreamCallback(streamReader.PacketProvider);
				}

				return true;
			}
			
			SetEndOfStreams();
			return false;
		}
		
		public bool ReadPageAt(int offset, out PageInfo page) {
			// this should be safe; we've already checked the page by now

			if (Page.pageOffset == offset && Page.pageLength != 0) {// short circuit for when we've already loaded the page
				page = Page;
				return true;
			}

			if (CheckHeaderStartPoint(offset, out var header)) {
				// don't read the whole page yet; if our caller is seeking, they won't need packets anyway
				Page = page = ParsePageHeader(header, false);
				return true;
			}

			page = default;
			return false;
		}

		private bool VerifyPage(ArraySegment<byte> header, out ArraySegment<byte> page, bool checkCrc) {
			var dataLen = 0;
			for (int i = 27; i < header.Count; i++) {
				dataLen += header.Get(i);
			}

			page = ReadFile(header.Offset, header.Count + dataLen);
			if (page.Count != header.Count + dataLen) {
				// Not enough space
				return false;
			}

			if (!checkCrc) {
				return true;
			}
			
			// Check CRC
			var crc = Crc.EMPTY_CRC;
			for (int i = 0; i < 22; i++) {
				Crc.Update(ref crc, page.Get(i));
			}
			
			// Crc goes here and is computed as 0
			Crc.Update(ref crc, 0);
			Crc.Update(ref crc, 0);
			Crc.Update(ref crc, 0);
			Crc.Update(ref crc, 0);

			for (int i = 26; i < page.Count; i++) {
				Crc.Update(ref crc, page.Get(i));
			}

			return crc == page.GetUint(22);
		}

		/**
		 * Check whether the given index could contain a valid header
		 * (checks sync point (magic) and that there is enough space)
		 */
		private bool CheckHeaderStartPoint(int index, out ArraySegment<byte> header) {
			var headerData = ReadFile(index, 27 + 255);
			header = default;

			if (headerData.Count < 27) {
				// Not enough bytes left for a full header
				return false;
			}

			// Check that sync point is here (the magic OggS string)
			if (headerData.Get(0) != 0x4f || headerData.Get(1) != 0x67 || headerData.Get(2) != 0x67 || headerData.Get(3) != 0x53) {
				return false;
			}
			var segCnt = headerData.Get(26);
			var headerSize = 27 + segCnt;
			if (headerData.Count >= headerSize) {
				header = ReadFile(index, headerSize);
				return true;
			}

			return false;
		}

		private ArraySegment<byte> ReadFile(int offset, int length) {
			return new ArraySegment<byte>(oggData, offset, Math.Min(oggData.Length - offset, length));
		}

		private PageInfo ParsePageHeader(ArraySegment<byte> pageBuf, bool isResync) {
			var segCnt = pageBuf.Get(26);
			var dataLen = 0;
			var pktCnt = 0;
			var isContinued = false;

			var size = 0;
			for (int i = 0, idx = 27; i < segCnt; i++, idx++) {
				var seg = pageBuf.Get(idx);
				size += seg;
				dataLen += seg;
				if (seg < 255) {
					if (size > 0) ++pktCnt;

					size = 0;
				}
			}

			if (size > 0) {
				isContinued = pageBuf.Get(segCnt + 26) == 255;
				++pktCnt;
			}

			var offset = pageBuf.Offset;
			var length = 27 + segCnt + dataLen;
			var sequenceNumber = pageBuf.GetInt(18);
			var pageFlags = (PageFlags) pageBuf.Get(5) & PageFlags.OggDefinedMask;// Masking so that we can safely use unused bits for own purposes
			var granulePosition = pageBuf.GetLong(6);
			var packetCount = (short) pktCnt;
			if (isResync) {
				pageFlags |= PageFlags.InternalIsResync;
			}
			if (isContinued) {
				pageFlags |= PageFlags.InternalIsContinued;
			}

			return new PageInfo(offset, (ushort) length, sequenceNumber, pageFlags, granulePosition, packetCount);
		}

		private static ArraySegment<byte>[] ReadPackets(int packetCount, ArraySegment<byte> segments, ArraySegment<byte> dataBuffer) {
			var list = new ArraySegment<byte>[packetCount];
			var listIdx = 0;
			var dataIdx = 0;
			var size = 0;

			for (var i = 0; i < segments.Count; i++) {
				var seg = segments.Get(i);
				size += seg;
				if (seg < 255) {
					if (size > 0) {
						list[listIdx++] = dataBuffer.SubSegment(dataIdx, size);
						dataIdx += size;
					}

					size = 0;
				}
			}

			if (size > 0) list[listIdx] = dataBuffer.SubSegment(dataIdx, size);

			return list;
		}

		private void SetEndOfStreams() {
			foreach (var spr in _streamReaders.Values) {
				spr.SetEndOfStream();
			}

			_streamReaders.Clear();
		}

		public readonly struct PageInfo {
			public readonly int pageOffset;
			public readonly ushort pageLength;
			public readonly int sequenceNumber;
			public readonly PageFlags flags;
			public readonly long granulePosition;
			public readonly short packetCount;
			
			public bool isEndOfStream => (flags & PageFlags.EndOfStream) != 0;
			public bool isContinuation => (flags & PageFlags.ContinuesPacket) != 0;
			public bool isResync => (flags & PageFlags.InternalIsResync) != 0;
			public bool isContinued => (flags & PageFlags.InternalIsContinued) != 0;

			public PageInfo(int pageOffset, ushort pageLength, int sequenceNumber, PageFlags flags, long granulePosition, short packetCount) {
				this.pageOffset = pageOffset;
				this.pageLength = pageLength;
				this.sequenceNumber = sequenceNumber;
				this.flags = flags;
				this.granulePosition = granulePosition;
				this.packetCount = packetCount;
			}
		}

		private PageInfo Page { get; set; }

		#region IPacketData

		public ArraySegment<byte>[] GetPackets(PageInfo page) {
			var pageBuf = ReadFile(page.pageOffset, page.pageLength);
			var segments =  pageBuf.Get(26);
			return ReadPackets(page.packetCount, 
				pageBuf.SubSegment(27, segments),
				pageBuf.SubSegment(27 + segments, pageBuf.Count - 27 - segments));
		}

		#endregion

	}
}