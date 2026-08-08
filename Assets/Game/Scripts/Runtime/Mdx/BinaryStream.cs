using System;
using System.Buffers.Binary;
using System.Text;

namespace Game.Mdx
{
    /// <summary>
    /// Little-endian binary reader ported from mdx-m3-viewer's <c>common/binarystream.ts</c>.
    /// All offsets are relative to the window (substream) it was created over, matching the
    /// TypeScript <c>index</c> semantics used by the MDX chunk readers.
    /// </summary>
    public sealed class BinaryStream
    {
        readonly byte[] _bytes;
        readonly int _offset;
        readonly int _length;
        int _index;

        public BinaryStream(byte[] buffer, int byteOffset = 0, int byteLength = -1)
        {
            _bytes = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _offset = byteOffset;
            _length = byteLength < 0 ? buffer.Length - byteOffset : byteLength;
        }

        public int Index => _index;
        public int ByteLength => _length;
        public int Remaining => _length - _index;

        /// <summary>
        /// Create a subreader of this reader, at its position, with the given byte length.
        /// </summary>
        public BinaryStream Substream(int byteLength)
        {
            if (Remaining < byteLength)
            {
                throw new InvalidOperationException($"ByteStream: substream: want {byteLength} bytes but have {Remaining}");
            }

            var index = _index;
            _index += byteLength;
            return new BinaryStream(_bytes, _offset + index, byteLength);
        }

        /// <summary>Skip a number of bytes.</summary>
        public void Skip(int bytes)
        {
            if (Remaining < bytes)
            {
                throw new InvalidOperationException($"ByteStream: skip: premature end - want {bytes} bytes but have {Remaining}");
            }

            _index += bytes;
        }

        /// <summary>Set the reader's index.</summary>
        public void Seek(int index)
        {
            if (index < 0 || index > _length)
            {
                throw new InvalidOperationException($"ByteStream: seek: index {index} out of range 0..{_length}");
            }

            _index = index;
        }

        /// <summary>
        /// Read a UTF8 string with the given number of bytes.
        /// The entire block is consumed, but the returned string is NULL terminated in its memory block.
        /// </summary>
        public string Read(int bytes)
        {
            if (Remaining < bytes)
            {
                throw new InvalidOperationException($"ByteStream: read: premature end - want {bytes} bytes but have {Remaining}");
            }

            var start = _offset + _index;
            var end = start + bytes;
            var nullIndex = Array.IndexOf(_bytes, (byte)0, start, bytes);
            var length = nullIndex < 0 ? bytes : nullIndex - start;
            var s = Encoding.UTF8.GetString(_bytes, start, length);

            _index += bytes;
            return s;
        }

        /// <summary>Read a UTF8 NULL terminated string (consumes the terminator).</summary>
        public string ReadNull()
        {
            if (Remaining < 1)
            {
                throw new InvalidOperationException("ByteStream: readNull: premature end - want at least 1 byte but have 0");
            }

            var start = _offset + _index;
            var end = Array.IndexOf(_bytes, (byte)0, start);
            if (end < 0)
            {
                end = _offset + _length - 1;
            }

            var bytes = end - start + 1;
            var s = Encoding.UTF8.GetString(_bytes, start, end - start);

            _index += bytes;
            return s;
        }

        /// <summary>Read a binary string with the given number of bytes (raw Latin1-ish chars).</summary>
        public string ReadBinary(int bytes)
        {
            if (Remaining < bytes)
            {
                throw new InvalidOperationException($"ByteStream: readBinary: premature end - want {bytes} bytes but have {Remaining}");
            }

            var start = _offset + _index;
            var sb = new StringBuilder(bytes);
            for (var i = 0; i < bytes; i++)
            {
                sb.Append((char)_bytes[start + i]);
            }

            _index += bytes;
            return sb.ToString();
        }

        public sbyte ReadInt8() => unchecked((sbyte)ReadUint8());

        public short ReadInt16()
        {
            if (Remaining < 2)
            {
                throw Premature(2);
            }

            var value = BinaryPrimitives.ReadInt16LittleEndian(_bytes.AsSpan(_offset + _index, 2));
            _index += 2;
            return value;
        }

        public int ReadInt32()
        {
            if (Remaining < 4)
            {
                throw Premature(4);
            }

            var value = BinaryPrimitives.ReadInt32LittleEndian(_bytes.AsSpan(_offset + _index, 4));
            _index += 4;
            return value;
        }

        public byte ReadUint8()
        {
            if (Remaining < 1)
            {
                throw Premature(1);
            }

            return _bytes[_offset + _index++];
        }

        public ushort ReadUint16()
        {
            if (Remaining < 2)
            {
                throw Premature(2);
            }

            var value = BinaryPrimitives.ReadUInt16LittleEndian(_bytes.AsSpan(_offset + _index, 2));
            _index += 2;
            return value;
        }

        public uint ReadUint32()
        {
            if (Remaining < 4)
            {
                throw Premature(4);
            }

            var value = BinaryPrimitives.ReadUInt32LittleEndian(_bytes.AsSpan(_offset + _index, 4));
            _index += 4;
            return value;
        }

        public float ReadFloat32()
        {
            if (Remaining < 4)
            {
                throw Premature(4);
            }

            var value = BitConverter.ToSingle(_bytes, _offset + _index);
            _index += 4;
            return value;
        }

        public double ReadFloat64()
        {
            if (Remaining < 8)
            {
                throw Premature(8);
            }

            var value = BitConverter.ToDouble(_bytes, _offset + _index);
            _index += 8;
            return value;
        }

        public int[] ReadInt32Array(int count)
        {
            if (Remaining < count * 4)
            {
                throw Premature(count * 4);
            }

            var result = new int[count];
            var start = _offset + _index;
            for (var i = 0; i < count; i++)
            {
                result[i] = BinaryPrimitives.ReadInt32LittleEndian(_bytes.AsSpan(start + i * 4, 4));
            }

            _index += count * 4;
            return result;
        }

        public ushort[] ReadUint16Array(int count)
        {
            if (Remaining < count * 2)
            {
                throw Premature(count * 2);
            }

            var result = new ushort[count];
            var start = _offset + _index;
            for (var i = 0; i < count; i++)
            {
                result[i] = BinaryPrimitives.ReadUInt16LittleEndian(_bytes.AsSpan(start + i * 2, 2));
            }

            _index += count * 2;
            return result;
        }

        public uint[] ReadUint32Array(int count)
        {
            if (Remaining < count * 4)
            {
                throw Premature(count * 4);
            }

            var result = new uint[count];
            var start = _offset + _index;
            for (var i = 0; i < count; i++)
            {
                result[i] = BinaryPrimitives.ReadUInt32LittleEndian(_bytes.AsSpan(start + i * 4, 4));
            }

            _index += count * 4;
            return result;
        }

        public byte[] ReadUint8Array(int count)
        {
            if (Remaining < count)
            {
                throw Premature(count);
            }

            var result = new byte[count];
            Array.Copy(_bytes, _offset + _index, result, 0, count);
            _index += count;
            return result;
        }

        public float[] ReadFloat32Array(int count)
        {
            if (Remaining < count * 4)
            {
                throw Premature(count * 4);
            }

            var result = new float[count];
            var start = _offset + _index;
            for (var i = 0; i < count; i++)
            {
                result[i] = BitConverter.ToSingle(_bytes, start + i * 4);
            }

            _index += count * 4;
            return result;
        }

        public double[] ReadFloat64Array(int count)
        {
            if (Remaining < count * 8)
            {
                throw Premature(count * 8);
            }

            var result = new double[count];
            var start = _offset + _index;
            for (var i = 0; i < count; i++)
            {
                result[i] = BitConverter.ToDouble(_bytes, start + i * 8);
            }

            _index += count * 8;
            return result;
        }

        InvalidOperationException Premature(int bytes)
        {
            return new InvalidOperationException($"ByteStream: premature end - want {bytes} bytes but have {Remaining}");
        }
    }
}
