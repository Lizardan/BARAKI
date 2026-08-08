using System;

namespace Game.Editor
{
    /// <summary>
    /// Minimal DDS (DirectDraw Surface) decoder used by <see cref="MdxPrefabBuilder"/> to import
    /// textures extracted from the WC3 Reforged CASC store (which ships classic textures as DXT
    /// DDS files rather than BLP). Decodes the first mip only.
    /// Supported: BC1/DXT1, DXT3, BC3/DXT5, and uncompressed 24/32-bit RGB(A).
    /// </summary>
    public static class DdsDecoder
    {
        const uint DdsMagic = 0x20534444; // "DDS "

        const uint PfFlags = 0x04;        // DDPF_FOURCC
        const uint PfRgb = 0x40;          // DDPF_RGB

        const uint FccDxt1 = 0x31545844;  // "DXT1"
        const uint FccDxt3 = 0x33545844;  // "DXT3"
        const uint FccDxt5 = 0x35545844;  // "DXT5"
        const uint FccDx10 = 0x30315844;  // "DX10"

        const uint DxgiBc1 = 71;          // BC1_UNORM
        const uint DxgiBc2 = 74;          // BC2_UNORM
        const uint DxgiBc3 = 77;          // BC3_UNORM
        const uint DxgiRgba = 28;         // R8G8B8A8_UNORM
        const uint DxgiRgbaSrgb = 29;     // R8G8B8A8_UNORM_SRGB

        public static bool TryDecode(byte[] data, out int width, out int height, out byte[] rgba)
        {
            width = 0;
            height = 0;
            rgba = null;

            if (data == null || data.Length < 128 || BitConverter.ToUInt32(data, 0) != DdsMagic)
            {
                return false;
            }

            height = BitConverter.ToInt32(data, 12);
            width = BitConverter.ToInt32(data, 16);
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            var pfFlags = BitConverter.ToUInt32(data, 80);
            var fourCc = BitConverter.ToUInt32(data, 84);
            var rgbBits = BitConverter.ToUInt32(data, 88);

            if ((pfFlags & PfFlags) != 0)
            {
                if (fourCc == FccDxt1)
                {
                    return DecodeBc1(data, width, height, 128, out rgba);
                }
                if (fourCc == FccDxt3)
                {
                    return DecodeBc2(data, width, height, 128, out rgba);
                }
                if (fourCc == FccDxt5)
                {
                    return DecodeBc3(data, width, height, 128, out rgba);
                }
                if (fourCc == FccDx10 && data.Length >= 148)
                {
                    var dxgi = BitConverter.ToUInt32(data, 128);
                    if (dxgi == DxgiBc1)
                    {
                        return DecodeBc1(data, width, height, 148, out rgba);
                    }
                    if (dxgi == DxgiBc2)
                    {
                        return DecodeBc2(data, width, height, 148, out rgba);
                    }
                    if (dxgi == DxgiBc3)
                    {
                        return DecodeBc3(data, width, height, 148, out rgba);
                    }
                    if (dxgi == DxgiRgba || dxgi == DxgiRgbaSrgb)
                    {
                        return DecodeRaw(data, width, height, 148, 4, out rgba);
                    }
                }
                return false;
            }

            if ((pfFlags & PfRgb) != 0)
            {
                var bytesPerPixel = (int)(rgbBits / 8);
                if (bytesPerPixel == 3 || bytesPerPixel == 4)
                {
                    return DecodeRaw(data, width, height, 128, bytesPerPixel, out rgba);
                }
            }

            return false;
        }

        static bool DecodeBc1(byte[] data, int width, int height, int offset, out byte[] rgba)
        {
            rgba = new byte[width * height * 4];
            var blocksX = (width + 3) / 4;
            var blocksY = (height + 3) / 4;
            if (data.Length < offset + blocksX * blocksY * 8)
            {
                return false;
            }

            var src = offset;
            for (var by = 0; by < blocksY; by++)
            {
                for (var bx = 0; bx < blocksX; bx++)
                {
                    DecodeColorBlock(data, src, width, height, bx * 4, by * 4, rgba, true);
                    src += 8;
                }
            }
            return true;
        }

        static bool DecodeBc2(byte[] data, int width, int height, int offset, out byte[] rgba)
        {
            rgba = new byte[width * height * 4];
            var blocksX = (width + 3) / 4;
            var blocksY = (height + 3) / 4;
            if (data.Length < offset + blocksX * blocksY * 16)
            {
                return false;
            }

            var src = offset;
            for (var by = 0; by < blocksY; by++)
            {
                for (var bx = 0; bx < blocksX; bx++)
                {
                    DecodeAlphaBlockDxt3(data, src, width, height, bx * 4, by * 4, rgba);
                    DecodeColorBlock(data, src + 8, width, height, bx * 4, by * 4, rgba, false);
                    src += 16;
                }
            }
            return true;
        }

        static bool DecodeBc3(byte[] data, int width, int height, int offset, out byte[] rgba)
        {
            rgba = new byte[width * height * 4];
            var blocksX = (width + 3) / 4;
            var blocksY = (height + 3) / 4;
            if (data.Length < offset + blocksX * blocksY * 16)
            {
                return false;
            }

            var src = offset;
            for (var by = 0; by < blocksY; by++)
            {
                for (var bx = 0; bx < blocksX; bx++)
                {
                    DecodeAlphaBlockDxt5(data, src, width, height, bx * 4, by * 4, rgba);
                    DecodeColorBlock(data, src + 8, width, height, bx * 4, by * 4, rgba, false);
                    src += 16;
                }
            }
            return true;
        }

        static void DecodeColorBlock(byte[] data, int src, int width, int height, int px, int py, byte[] rgba, bool dxt1)
        {
            var c0 = data[src] | (data[src + 1] << 8);
            var c1 = data[src + 2] | (data[src + 3] << 8);

            var r0 = Expand5((c0 >> 11) & 0x1f);
            var g0 = Expand6((c0 >> 5) & 0x3f);
            var b0 = Expand5(c0 & 0x1f);
            var r1 = Expand5((c1 >> 11) & 0x1f);
            var g1 = Expand6((c1 >> 5) & 0x3f);
            var b1 = Expand5(c1 & 0x1f);

            // Palette[4].
            var pal = new byte[16];
            if (c0 > c1 || !dxt1)
            {
                pal[0] = r0; pal[1] = g0; pal[2] = b0; pal[3] = 255;
                pal[4] = r1; pal[5] = g1; pal[6] = b1; pal[7] = 255;
                pal[8] = (byte)((2 * r0 + r1) / 3);
                pal[9] = (byte)((2 * g0 + g1) / 3);
                pal[10] = (byte)((2 * b0 + b1) / 3);
                pal[11] = 255;
                pal[12] = (byte)((r0 + 2 * r1) / 3);
                pal[13] = (byte)((g0 + 2 * g1) / 3);
                pal[14] = (byte)((b0 + 2 * b1) / 3);
                pal[15] = 255;
            }
            else
            {
                pal[0] = r0; pal[1] = g0; pal[2] = b0; pal[3] = 255;
                pal[4] = r1; pal[5] = g1; pal[6] = b1; pal[7] = 255;
                pal[8] = (byte)((r0 + r1) / 2);
                pal[9] = (byte)((g0 + g1) / 2);
                pal[10] = (byte)((b0 + b1) / 2);
                pal[11] = 255;
                pal[12] = 0; pal[13] = 0; pal[14] = 0; pal[15] = 0;
            }

            var indices = src + 4;
            var block = 0u;
            for (var i = 0; i < 4; i++)
            {
                block |= (uint)data[indices + i] << (i * 8);
            }

            for (var row = 0; row < 4; row++)
            {
                for (var col = 0; col < 4; col++)
                {
                    var x = px + col;
                    var y = py + row;
                    if (x >= width || y >= height)
                    {
                        continue;
                    }

                    var index = (int)((block >> ((row * 4 + col) * 2)) & 3) * 4;
                    var dst = (y * width + x) * 4;
                    rgba[dst] = pal[index];
                    rgba[dst + 1] = pal[index + 1];
                    rgba[dst + 2] = pal[index + 2];
                    // For BC1 the palette carries the alpha (index 3 may be transparent).
                    // For BC2/BC3 alpha lives in its own block (DecodeAlphaBlockDxt3/Dxt5),
                    // which must NOT be overwritten here.
                    if (dxt1)
                    {
                        rgba[dst + 3] = pal[index + 3];
                    }
                }
            }
        }

        static void DecodeAlphaBlockDxt3(byte[] data, int src, int width, int height, int px, int py, byte[] rgba)
        {
            for (var row = 0; row < 4; row++)
            {
                var lo = data[src + row * 2];
                var hi = data[src + row * 2 + 1];
                for (var col = 0; col < 4; col++)
                {
                    var x = px + col;
                    var y = py + row;
                    if (x >= width || y >= height)
                    {
                        continue;
                    }

                    var nibble = col < 2 ? (lo >> (col * 4)) & 0xf : (hi >> ((col - 2) * 4)) & 0xf;
                    rgba[(y * width + x) * 4 + 3] = (byte)(nibble * 17);
                }
            }
        }

        static void DecodeAlphaBlockDxt5(byte[] data, int src, int width, int height, int px, int py, byte[] rgba)
        {
            var a0 = data[src];
            var a1 = data[src + 1];

            var alphas = new byte[8];
            alphas[0] = a0;
            alphas[1] = a1;
            if (a0 > a1)
            {
                alphas[2] = (byte)((6 * a0 + 1 * a1) / 7);
                alphas[3] = (byte)((5 * a0 + 2 * a1) / 7);
                alphas[4] = (byte)((4 * a0 + 3 * a1) / 7);
                alphas[5] = (byte)((3 * a0 + 4 * a1) / 7);
                alphas[6] = (byte)((2 * a0 + 5 * a1) / 7);
                alphas[7] = (byte)((1 * a0 + 6 * a1) / 7);
            }
            else
            {
                alphas[2] = (byte)((4 * a0 + 1 * a1) / 5);
                alphas[3] = (byte)((3 * a0 + 2 * a1) / 5);
                alphas[4] = (byte)((2 * a0 + 3 * a1) / 5);
                alphas[5] = (byte)((1 * a0 + 4 * a1) / 5);
                alphas[6] = 0;
                alphas[7] = 255;
            }

            var bits = 0ul;
            for (var i = 0; i < 6; i++)
            {
                bits |= (ulong)data[src + 2 + i] << (i * 8);
            }

            for (var i = 0; i < 16; i++)
            {
                var row = i / 4;
                var col = i % 4;
                var x = px + col;
                var y = py + row;
                if (x >= width || y >= height)
                {
                    continue;
                }

                var index = (int)((bits >> (i * 3)) & 7);
                rgba[(y * width + x) * 4 + 3] = alphas[index];
            }
        }

        static bool DecodeRaw(byte[] data, int width, int height, int offset, int bytesPerPixel, out byte[] rgba)
        {
            rgba = new byte[width * height * 4];
            var rowBytes = width * bytesPerPixel;
            var pitch = (rowBytes + 3) & ~3;
            if (data.Length < offset + pitch * height)
            {
                return false;
            }

            for (var y = 0; y < height; y++)
            {
                var src = offset + y * pitch;
                for (var x = 0; x < width; x++)
                {
                    var dst = (y * width + x) * 4;
                    if (bytesPerPixel == 4)
                    {
                        rgba[dst] = data[src + x * 4 + 2];
                        rgba[dst + 1] = data[src + x * 4 + 1];
                        rgba[dst + 2] = data[src + x * 4];
                        rgba[dst + 3] = data[src + x * 4 + 3];
                    }
                    else
                    {
                        rgba[dst] = data[src + x * 3 + 2];
                        rgba[dst + 1] = data[src + x * 3 + 1];
                        rgba[dst + 2] = data[src + x * 3];
                        rgba[dst + 3] = 255;
                    }
                }
            }
            return true;
        }

        static byte Expand5(int v)
        {
            return (byte)((v << 3) | (v >> 2));
        }

        static byte Expand6(int v)
        {
            return (byte)((v << 2) | (v >> 4));
        }
    }
}
