using System;
using System.Collections.Generic;

namespace NVorbis {

	public class PacketProvider {
		private Packet _lastPacket;
		private int _lastPacketPacketIndex;

		private int _lastPacketPageIndex;
		private int _nextPacketPacketIndex;
		private int _nextPacketPageIndex;
		private int _packetIndex;

		private int _pageIndex;
		private readonly StreamPageReader _reader;

		internal PacketProvider(StreamPageReader reader) {
			_reader = reader ?? throw new ArgumentNullException(nameof(reader));
		}

		/// <summary>
		///     Gets the total number of granule available in the stream.
		/// </summary>
		public long GetGranuleCount() {
			if (_reader == null) throw new ObjectDisposedException(nameof(PacketProvider));

			if (!_reader.HasAllPages) // this will force the reader to attempt to read all pages
				_reader.GetPage(int.MaxValue, out _);

			return _reader.MaxGranulePosition.Value;
		}

		/// <summary>
		///     Gets the next packet in the stream and advances to the next packet position.
		/// </summary>
		/// <returns>The <see cref="Packet" /> instance for the next packet if available, otherwise <see langword="null" />.</returns>
		public Packet GetNextPacket() {
			return GetNextPacket(ref _pageIndex, ref _packetIndex);
		}

		/// <summary>
		///     Gets the next packet in the stream without advancing to the next packet position.
		/// </summary>
		/// <returns>The <see cref="Packet" /> instance for the next packet if available, otherwise <see langword="null" />.</returns>
		public Packet PeekNextPacket() {
			var pageIndex = _pageIndex;
			var packetIndex = _packetIndex;
			return GetNextPacket(ref pageIndex, ref packetIndex);
		}

		public ArraySegment<byte> GetPacketData(int pagePacketIndex) {
			var pageIndex = (pagePacketIndex >> 8) & 0xFFFFFF;
			var packetIndex = pagePacketIndex & 0xFF;

			var packets = _reader.GetPagePackets(pageIndex);
			if (packetIndex < packets.Length) return packets[packetIndex];

			return new ArraySegment<byte>(Utils.EMPTY_BYTE_ARRAY);
		}

		// this method calc's the appropriate page and packet prior to the one specified, honoring continuations and handling negative packetIndex values
		// if packet index is larger than the current page allows, we just return it as-is

		private Packet GetNextPacket(ref int pageIndex, ref int packetIndex) {
			if (_reader == null) throw new ObjectDisposedException(nameof(PacketProvider));

			if (_lastPacketPacketIndex != packetIndex || _lastPacketPageIndex != pageIndex || _lastPacket == null) {
				if (!_reader.GetPage(pageIndex, out var page)) {
					return _lastPacket = null;
				}
				_lastPacketPageIndex = pageIndex;
				_lastPacketPacketIndex = packetIndex;
				_lastPacket = CreatePacket(ref pageIndex, ref packetIndex, true, page.granulePosition, page.isResync, page.isContinued, page.packetCount);
				_nextPacketPageIndex = pageIndex;
				_nextPacketPacketIndex = packetIndex;
			} else {
				pageIndex = _nextPacketPageIndex;
				packetIndex = _nextPacketPacketIndex;
			}

			return _lastPacket;
		}

		private Packet CreatePacket(ref int pageIndex, ref int packetIndex, bool advance, long granulePos, bool isResync, bool isContinued, int packetCount) {
			// save off the packet data for the initial packet
			var firstPacketData = _reader.GetPagePackets(pageIndex)[packetIndex];

			// create the packet list and add the item to it
			var pktList = new List<int>(2) {(pageIndex << 8) | packetIndex};

			// make sure we handle continuations
			bool isLastPacket;
			var finalPage = pageIndex;
			if (isContinued && packetIndex == packetCount - 1) {
				// by definition, it's the first packet in the page it ends on

				// go read the next page(s) that include this packet
				var contPageIdx = pageIndex;
				while (isContinued) {
					if (!_reader.GetPage(++contPageIdx, out var page)) // no more pages?  In any case, we can't satify the request
						return null;
					
					granulePos = page.granulePosition;
					isResync = page.isResync;
					var isContinuation = page.isContinuation;
					isContinued = page.isContinued;
					packetCount = page.packetCount;

					// if the next page isn't a continuation or is a resync, the stream is broken so we'll just return what we could get
					if (!isContinuation || isResync) break;

					// if the next page is continued, only keep reading if there are no more packets in the page
					if (isContinued && packetCount > 1) isContinued = false;

					// add the packet to the list
					pktList.Add(contPageIdx << 8);
				}

				// we're now the first packet in the final page, so we'll act like it...
				isLastPacket = packetCount == 1;

				// track the final page read
				finalPage = contPageIdx;
			} else {
				isLastPacket = packetIndex == packetCount - 1;
			}

			// create the packet instance and populate it with the appropriate initial data
			var packet = new Packet(pktList, this, firstPacketData) {
				IsResync = isResync
			};

			// if we're the last packet completed in the page, set the .GranulePosition
			if (isLastPacket) {
				packet.GranulePosition = granulePos;

				// if we're the last packet completed in the page, no more pages are available, and _hasAllPages is set, set .IsEndOfStream
				if (_reader.HasAllPages && finalPage == _reader.PageCount - 1) packet.IsEndOfStream = true;
			}

			if (advance) {
				// if we've advanced a page, we continued a packet and should pick up with the next page
				if (finalPage != pageIndex) {
					// we're on the final page now
					pageIndex = finalPage;

					// the packet index will be modified below, so set it to the end of the continued packet
					packetIndex = 0;
				}

				// if we're on the last packet in the page, move to the next page
				// we can't use isLast here because the logic is different; last in page granule vs. last in physical page
				if (packetIndex == packetCount - 1) {
					++pageIndex;
					packetIndex = 0;
				}
				// otherwise, just move to the next packet
				else {
					++packetIndex;
				}
			}

			// done!
			return packet;
		}
	}
}