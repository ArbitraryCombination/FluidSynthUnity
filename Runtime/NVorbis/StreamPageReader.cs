using System;
using System.Collections.Generic;
using System.IO;

namespace NVorbis {
	internal class StreamPageReader {
		private readonly List<int> _pageOffsets = new List<int>();

		private readonly PageReader _reader;

		private ArraySegment<byte>[] _cachedPagePackets;
		private int? _firstDataPageIndex;

		private int _lastPageIndex = -1;
		private PageReader.PageInfo lastPage;

		private int _lastSeqNbr;
		private long _maxGranulePos;

		public StreamPageReader(PageReader pageReader) {
			_reader = pageReader;

			// The packet provider has a reference to us, and we have a reference to it.
			// The page reader has a reference to us.
			// The container reader has a _weak_ reference to the packet provider.
			// The user has a reference to the packet provider.
			// So long as the user doesn't drop their reference and the page reader doesn't drop us,
			//  the packet provider will stay alive.
			// This is important since the container reader only holds a week reference to it.
			PacketProvider = new PacketProvider(this);
		}

		public PacketProvider PacketProvider { get; }

		public void AddPage(PageReader.PageInfo page) {
			// verify we haven't read all pages
			if (HasAllPages) return;
			
			// if the page's granule position is -1 that mean's it doesn't have any samples
			if (page.granulePosition != -1) {
				if (_maxGranulePos > page.granulePosition && _maxGranulePos > 0) // uuuuh, what?!
					throw new InvalidDataException("Granule Position regressed?!");

				_maxGranulePos = page.granulePosition;
			}
			// granule position == -1, so this page doesn't complete any packets
			// we don't really care if it's a continuation itself, only that it is continued and has a single packet
			else if (!page.isContinued || page.packetCount != 1) {
				throw new InvalidDataException("Granule Position was -1 but page does not have exactly 1 continued packet.");
			}

			if (page.isEndOfStream) HasAllPages = true;

			if (_firstDataPageIndex == null && page.granulePosition > 0) _firstDataPageIndex = _pageOffsets.Count;

			if (_lastSeqNbr != 0 && _lastSeqNbr + 1 != page.sequenceNumber) // as a practical matter, if the sequence numbers are "wrong", our logical stream is now out of sync
				// so whether the page header sync was lost or we just got an out of order page / sequence jump, we're counting it as a resync
				_pageOffsets.Add(-page.pageOffset);
			else
				_pageOffsets.Add(page.pageOffset);

			_lastSeqNbr = page.sequenceNumber;
		}

		public ArraySegment<byte>[] GetPagePackets(int pageIndex) {
			if (_cachedPagePackets != null && _lastPageIndex == pageIndex) return _cachedPagePackets;

			var pageOffset = _pageOffsets[pageIndex];
			if (pageOffset < 0) pageOffset = -pageOffset;

			_reader.ReadPageAt(pageOffset, out var page);
			var packets = _reader.GetPackets(page);
			if (pageIndex == _lastPageIndex) _cachedPagePackets = packets;

			return packets;
		}

		public bool GetPage(int pageIndex, out PageReader.PageInfo pageInfo) {
			if (_lastPageIndex == pageIndex) {
				pageInfo = lastPage;
				return true;
			}

			while (pageIndex >= _pageOffsets.Count && !HasAllPages) {
				if (_reader.ReadNextPage(out pageInfo)) {
					// if we found our page, return it from here so we don't have to do further processing
					if (pageIndex < _pageOffsets.Count) {
						lastPage = pageInfo;
						_lastPageIndex = pageIndex;
						_cachedPagePackets = null;
						return true;
					}
				} else {
					break;
				}
			}

			if (pageIndex < _pageOffsets.Count) {
				var offset = _pageOffsets[pageIndex];
				bool isResync;
				if (offset < 0) {
					isResync = true;
					offset = -offset;
				} else {
					isResync = false;
				}

				if (_reader.ReadPageAt(offset, out pageInfo)) {
					if (isResync) {
						// Add the resync flag
						pageInfo = new PageReader.PageInfo(pageInfo.pageOffset, pageInfo.pageLength, pageInfo.sequenceNumber, 
							pageInfo.flags | PageFlags.InternalIsResync, 
							pageInfo.granulePosition, pageInfo.packetCount);
					}
					
					lastPage = pageInfo;
					_lastPageIndex = pageIndex;
					_cachedPagePackets = null;
					return true;
				}
			}

			pageInfo = default;
			return false;
		}

		public void SetEndOfStream() {
			HasAllPages = true;
		}

		public int PageCount => _pageOffsets.Count;

		public bool HasAllPages { get; private set; }

		public long? MaxGranulePosition => HasAllPages ? (long?) _maxGranulePos : null;

	}
}