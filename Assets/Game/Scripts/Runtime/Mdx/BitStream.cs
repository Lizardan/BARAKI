using System;

namespace Game.Mdx
{
    /// <summary>
    /// Bit reader ported from mdx-m3-viewer's <c>common/bitstream.ts</c>.
    /// Used by the BLP1 indexed-image alpha channel decoding.
    /// </summary>
    public sealed class BitStream
    {
        readonly byte[] _bytes;
        readonly int _offset;
        readonly int _length;
        int _index;
        long _bitBuffer;
        int _bits;

        public BitStream(byte[] buffer, int byteOffset, int byteLength)
        {
            _bytes = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _offset = byteOffset;
            _length = byteLength;
        }

        /// <summary>Peek a number of bits.</summary>
        public int PeekBits(int bits)
        {
            LoadBits(bits);
            return (int)(_bitBuffer & ((1L << bits) - 1));
        }

        /// <summary>Read a number of bits.</summary>
        public int ReadBits(int bits)
        {
            var data = PeekBits(bits);
            _bitBuffer >>= bits;
            _bits -= bits;
            return data;
        }

        /// <summary>Skip a number of bits.</summary>
        public void SkipBits(int bits)
        {
            LoadBits(bits);
            _bitBuffer >>= bits;
            _bits -= bits;
        }

        void LoadBits(int bits)
        {
            while (_bits < bits)
            {
                _bitBuffer += (long)_bytes[_offset + _index] << _bits;
                _bits += 8;
                _index += 1;
            }
        }
    }
}
