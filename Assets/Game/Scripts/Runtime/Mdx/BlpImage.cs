using System;
using UnityEngine;

namespace Game.Mdx
{
    /// <summary>
    /// BLP1 image decoder ported from mdx-m3-viewer's <c>parsers/blp/image.ts</c>.
    /// Supports both content types: JPG (decoded via Unity's native JPEG decoder,
    /// then R/B swapped because WC3 stores BGR) and indexed palette with packed alpha bits.
    /// </summary>
    public sealed class BlpImage
    {
        const int Blp1Magic = 0x31504c42;
        const int ContentJpg = 0;

        public int Content;
        public int AlphaBits;
        public int Width;
        public int Height;
        public int Type;
        public bool HasMipmaps;
        public int[] MipmapOffsets = new int[16];
        public int[] MipmapSizes = new int[16];
        public byte[] Bytes;
        public byte[] JpgHeader;
        public byte[] Palette;

        public void Load(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 160)
            {
                throw new ArgumentException("BLP data too small");
            }

            var magic = BitConverter.ToInt32(bytes, 0);
            if (magic != Blp1Magic)
            {
                throw new ArgumentException("WrongMagicNumber");
            }

            Content = BitConverter.ToInt32(bytes, 4);
            AlphaBits = BitConverter.ToInt32(bytes, 8);
            Width = BitConverter.ToInt32(bytes, 12);
            Height = BitConverter.ToInt32(bytes, 16);
            Type = BitConverter.ToInt32(bytes, 20);
            HasMipmaps = BitConverter.ToInt32(bytes, 24) != 0;

            for (var i = 0; i < 16; i++)
            {
                MipmapOffsets[i] = BitConverter.ToInt32(bytes, 28 + i * 4);
                MipmapSizes[i] = BitConverter.ToInt32(bytes, 92 + i * 4);
            }

            Bytes = bytes;

            if (Content == ContentJpg)
            {
                var headerSize = BitConverter.ToInt32(bytes, 156);
                JpgHeader = new byte[headerSize];
                Array.Copy(bytes, 160, JpgHeader, 0, headerSize);
            }
            else
            {
                Palette = new byte[1024];
                Array.Copy(bytes, 156, Palette, 0, 1024);
            }
        }

        /// <summary>
        /// Decode a mipmap level into an RGBA byte array (row-major, stride = width*4).
        /// </summary>
        public byte[] GetMipmap(int level, out int outWidth, out int outHeight)
        {
            var offset = MipmapOffsets[level];
            var size = MipmapSizes[level];

            if (Content == ContentJpg)
            {
                var data = new byte[JpgHeader.Length + size];
                Array.Copy(JpgHeader, 0, data, 0, JpgHeader.Length);
                Array.Copy(Bytes, offset, data, JpgHeader.Length, size);

                var jpeg = new JpegImage();
                jpeg.Parse(data);
                var interleaved = jpeg.GetData();
                outWidth = jpeg.Width;
                outHeight = jpeg.Height;
                var pixelCount = outWidth * outHeight;
                var components = interleaved.Length / pixelCount;
                var result = new byte[pixelCount * 4];
                for (var i = 0; i < pixelCount; i++)
                {
                    var src = i * components;
                    var dst = i * 4;
                    result[dst] = interleaved[src];
                    result[dst + 1] = interleaved[src + 1];
                    result[dst + 2] = interleaved[src + 2];
                    result[dst + 3] = components > 3 ? interleaved[src + 3] : (byte)255;
                }

                return result;
            }

            var width = Math.Max(Width >> level, 1);
            var height = Math.Max(Height >> level, 1);
            var count = width * height;
            var alphaBits = AlphaBits;
            BitStream bitStream = null;
            float bitsToByte = 0f;

            if (alphaBits > 0)
            {
                if (alphaBits > 8)
                {
                    alphaBits = 8;
                }

                bitStream = new BitStream(Bytes, offset + count, (count * alphaBits + 7) / 8);
                bitsToByte = (float)ConvertBitRange(alphaBits, 8);
            }

            var result2 = new byte[count * 4];
            for (var i = 0; i < count; i++)
            {
                var dataIndex = i * 4;
                var paletteIndex = Bytes[offset + i] * 4;

                result2[dataIndex] = Palette[paletteIndex + 2];
                result2[dataIndex + 1] = Palette[paletteIndex + 1];
                result2[dataIndex + 2] = Palette[paletteIndex];

                if (alphaBits > 0)
                {
                    result2[dataIndex + 3] = (byte)(bitStream.ReadBits(alphaBits) * bitsToByte);
                }
                else
                {
                    result2[dataIndex + 3] = 255;
                }
            }

            outWidth = width;
            outHeight = height;
            return result2;
        }

        public int Mipmaps()
        {
            var mipmaps = 0;
            for (var i = 0; i < 16; i++)
            {
                if (MipmapSizes[i] > 0)
                {
                    mipmaps += 1;
                }
            }

            return mipmaps;
        }

        public int FakeMipmaps()
        {
            var mipmaps = 0;
            for (var i = 0; i < 16; i++)
            {
                var offset = MipmapOffsets[i];
                if (offset > 0)
                {
                    for (var j = i + 1; j < 16; j++)
                    {
                        if (offset == MipmapOffsets[j])
                        {
                            mipmaps += 1;
                            break;
                        }
                    }
                }
            }

            return mipmaps;
        }

        static double ConvertBitRange(int fromBits, int toBits)
        {
            return (((1L << toBits) - 1) / (double)((1L << fromBits) - 1));
        }
    }
}
