using System;
using System.Collections.Generic;

namespace Game.Mdx
{
    /// <summary>
    /// Baseline/progressive JPEG decoder ported from Mozilla's jpg.js as bundled in
    /// mdx-m3-viewer (<c>parsers/blp/jpg.js</c>). Needed because WC3 BLP1 JPG textures
    /// are non-standard: 4 components that hold BGR(A) data directly (no YCbCr conversion),
    /// which Unity's native libjpeg refuses to decode.
    /// All integer arithmetic replicates JS 32-bit semantics exactly (double products
    /// truncated to int32 via <see cref="ToInt32"/> before shifts).
    /// </summary>
    public sealed class JpegImage
    {
        static readonly int[] DctZigZag =
        {
            0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5, 12, 19, 26, 33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21, 28, 35, 42,
            49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51, 58, 59, 52, 45, 38, 31, 39, 46, 53, 60, 61, 54, 47, 55, 62, 63,
        };
        const int DctCos1 = 4017;
        const int DctSin1 = 799;
        const int DctCos3 = 3406;
        const int DctSin3 = 2276;
        const int DctCos6 = 1567;
        const int DctSin6 = 3784;
        const int DctSqrt2 = 5793;
        const int DctSqrt1d2 = 2896;

        public int Width;
        public int Height;

        List<Component> _components;

        sealed class HuffNode
        {
            public object[] Children = new object[2];
            public int Index;
        }

        sealed class Component
        {
            public int H;
            public int V;
            public int QuantizationId;
            public int[] QuantizationTable;
            public short[] BlockData;
            public short[] Output;
            public int BlocksPerLine;
            public int BlocksPerColumn;
            public int Pred;
            public object[] HuffmanTableDC;
            public object[] HuffmanTableAC;
        }

        sealed class Frame
        {
            public bool Extended;
            public bool Progressive;
            public int Precision;
            public int ScanLines;
            public int SamplesPerLine;
            public List<Component> Components = new List<Component>();
            public Dictionary<int, int> ComponentIds = new Dictionary<int, int>();
            public int MaxH;
            public int MaxV;
            public int McusPerLine;
            public int McusPerColumn;
        }

        sealed class FileMarker
        {
            public string Invalid;
            public int Marker;
            public int Offset;
        }

        static int ToInt32(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d) || d == 0)
            {
                return 0;
            }

            d = Math.Truncate(d) % 4294967296.0;
            if (d < 0)
            {
                d += 4294967296.0;
            }

            return unchecked((int)(uint)d);
        }

        static int GetBlockBufferOffset(Component component, int row, int col)
        {
            return 64 * ((component.BlocksPerLine + 1) * row + col);
        }

        static object[] BuildHuffmanTable(int[] codeLengths, int[] values)
        {
            var k = 0;
            var code = new List<HuffNode>();
            var length = 16;
            while (length > 0 && codeLengths[length - 1] == 0)
            {
                length--;
            }

            var root = new HuffNode();
            code.Add(root);
            var p = root;

            for (var i = 0; i < length; i++)
            {
                for (var j = 0; j < codeLengths[i]; j++)
                {
                    p = code[code.Count - 1];
                    code.RemoveAt(code.Count - 1);
                    p.Children[p.Index] = values[k];
                    while (p.Index > 0)
                    {
                        p = code[code.Count - 1];
                        code.RemoveAt(code.Count - 1);
                    }

                    p.Index++;
                    code.Add(p);
                    while (code.Count <= i)
                    {
                        var q = new HuffNode();
                        code.Add(q);
                        p.Children[p.Index] = q.Children;
                        p = q;
                    }

                    k++;
                }

                if (i + 1 < length)
                {
                    var q = new HuffNode();
                    code.Add(q);
                    p.Children[p.Index] = q.Children;
                    p = q;
                }
            }

            return root.Children;
        }

        static FileMarker FindNextFileMarker(byte[] data, int currentPos, int startPos)
        {
            int PeekUint16(int pos)
            {
                return (data[pos] << 8) | data[pos + 1];
            }

            var maxPos = data.Length - 1;
            var newPos = startPos < currentPos ? startPos : currentPos;
            if (currentPos >= maxPos)
            {
                return null;
            }

            var currentMarker = PeekUint16(currentPos);
            if (currentMarker >= 0xFFC0 && currentMarker <= 0xFFFE)
            {
                return new FileMarker { Invalid = null, Marker = currentMarker, Offset = currentPos };
            }

            var newMarker = PeekUint16(newPos);
            while (!(newMarker >= 0xFFC0 && newMarker <= 0xFFFE))
            {
                if (++newPos >= maxPos)
                {
                    return null;
                }

                newMarker = PeekUint16(newPos);
            }

            return new FileMarker { Invalid = currentMarker.ToString("x2"), Marker = newMarker, Offset = newPos };
        }

        static int DecodeScan(byte[] data, int startOffset, Frame frame, List<Component> components, int resetInterval, int spectralStart, int spectralEnd, int successivePrev, int successive)
        {
            var mcusPerLine = frame.McusPerLine;
            var progressive = frame.Progressive;
            var offset = startOffset;
            var bitsData = 0;
            var bitsCount = 0;
            var eobrun = 0;
            var successiveACState = 0;
            var successiveACNextValue = 0;

            int ReadBit()
            {
                if (bitsCount > 0)
                {
                    bitsCount--;
                    return (bitsData >> bitsCount) & 1;
                }

                bitsData = data[offset++];
                if (bitsData == 0xFF)
                {
                    var nextByte = data[offset++];
                    if (nextByte != 0)
                    {
                        throw new InvalidOperationException("unexpected marker " + ((bitsData << 8) | nextByte).ToString("x2"));
                    }
                }

                bitsCount = 7;
                return (int)((uint)bitsData >> 7);
            }

            int DecodeHuffman(object[] tree)
            {
                object node = tree;
                while (true)
                {
                    node = ((object[])node)[ReadBit()];
                    if (node is int leaf)
                    {
                        return leaf;
                    }

                    if (!(node is object[]))
                    {
                        throw new InvalidOperationException("invalid huffman sequence");
                    }
                }
            }

            int Receive(int length)
            {
                var n = 0;
                while (length > 0)
                {
                    n = (n << 1) | ReadBit();
                    length--;
                }

                return n;
            }

            int ReceiveAndExtend(int length)
            {
                if (length == 1)
                {
                    return ReadBit() == 1 ? 1 : -1;
                }

                var n = Receive(length);
                if (n >= 1 << (length - 1))
                {
                    return n;
                }

                return n + (-1 << length) + 1;
            }

            void DecodeBaseline(Component component, int bOffset)
            {
                var t = DecodeHuffman(component.HuffmanTableDC);
                var diff = t == 0 ? 0 : ReceiveAndExtend(t);
                component.Pred += diff;
                component.BlockData[bOffset] = (short)component.Pred;
                var k = 1;
                while (k < 64)
                {
                    var rs = DecodeHuffman(component.HuffmanTableAC);
                    var s = rs & 15;
                    var r = rs >> 4;
                    if (s == 0)
                    {
                        if (r < 15)
                        {
                            break;
                        }

                        k += 16;
                        continue;
                    }

                    k += r;
                    var z = DctZigZag[k];
                    component.BlockData[bOffset + z] = (short)ReceiveAndExtend(s);
                    k++;
                }
            }

            void DecodeDCFirst(Component component, int bOffset)
            {
                var t = DecodeHuffman(component.HuffmanTableDC);
                var diff = t == 0 ? 0 : ReceiveAndExtend(t) << successive;
                component.Pred += diff;
                component.BlockData[bOffset] = (short)component.Pred;
            }

            void DecodeDCSuccessive(Component component, int bOffset)
            {
                component.BlockData[bOffset] = (short)((ushort)component.BlockData[bOffset] | (uint)(ReadBit() << successive));
            }

            void DecodeACFirst(Component component, int bOffset)
            {
                if (eobrun > 0)
                {
                    eobrun--;
                    return;
                }

                var k = spectralStart;
                var e = spectralEnd;
                while (k <= e)
                {
                    var rs = DecodeHuffman(component.HuffmanTableAC);
                    var s = rs & 15;
                    var r = rs >> 4;
                    if (s == 0)
                    {
                        if (r < 15)
                        {
                            eobrun = Receive(r) + (1 << r) - 1;
                            break;
                        }

                        k += 16;
                        continue;
                    }

                    k += r;
                    var z = DctZigZag[k];
                    component.BlockData[bOffset + z] = (short)(ReceiveAndExtend(s) * (1 << successive));
                    k++;
                }
            }

            void DecodeACSuccessive(Component component, int bOffset)
            {
                var k = spectralStart;
                var e = spectralEnd;
                var r = 0;
                int s;
                int rs;
                while (k <= e)
                {
                    var z = DctZigZag[k];
                    switch (successiveACState)
                    {
                        case 0:
                            rs = DecodeHuffman(component.HuffmanTableAC);
                            s = rs & 15;
                            r = rs >> 4;
                            if (s == 0)
                            {
                                if (r < 15)
                                {
                                    eobrun = Receive(r) + (1 << r);
                                    successiveACState = 4;
                                }
                                else
                                {
                                    r = 16;
                                    successiveACState = 1;
                                }
                            }
                            else
                            {
                                if (s != 1)
                                {
                                    throw new InvalidOperationException("invalid ACn encoding");
                                }

                                successiveACNextValue = ReceiveAndExtend(s);
                                successiveACState = r != 0 ? 2 : 3;
                            }

                            continue;
                        case 1:
                        case 2:
                            if (component.BlockData[bOffset + z] != 0)
                            {
                                component.BlockData[bOffset + z] = (short)(component.BlockData[bOffset + z] + (ReadBit() << successive));
                            }
                            else
                            {
                                r--;
                                if (r == 0)
                                {
                                    successiveACState = successiveACState == 2 ? 3 : 0;
                                }
                            }

                            break;
                        case 3:
                            if (component.BlockData[bOffset + z] != 0)
                            {
                                component.BlockData[bOffset + z] = (short)(component.BlockData[bOffset + z] + (ReadBit() << successive));
                            }
                            else
                            {
                                component.BlockData[bOffset + z] = (short)(successiveACNextValue << successive);
                                successiveACState = 0;
                            }

                            break;
                        case 4:
                            if (component.BlockData[bOffset + z] != 0)
                            {
                                component.BlockData[bOffset + z] = (short)(component.BlockData[bOffset + z] + (ReadBit() << successive));
                            }

                            break;
                    }

                    k++;
                }

                if (successiveACState == 4)
                {
                    eobrun--;
                    if (eobrun == 0)
                    {
                        successiveACState = 0;
                    }
                }
            }

            void DecodeMcu(Component component, Action<Component, int> decode, int mcu, int row, int col)
            {
                var mcuRow = mcu / mcusPerLine;
                var mcuCol = mcu % mcusPerLine;
                var blockRow = mcuRow * component.V + row;
                var blockCol = mcuCol * component.H + col;
                var bOffset = GetBlockBufferOffset(component, blockRow, blockCol);
                decode(component, bOffset);
            }

            void DecodeBlock(Component component, Action<Component, int> decode, int mcu)
            {
                var blockRow = mcu / component.BlocksPerLine;
                var blockCol = mcu % component.BlocksPerLine;
                var bOffset = GetBlockBufferOffset(component, blockRow, blockCol);
                decode(component, bOffset);
            }

            var componentsLength = components.Count;
            Action<Component, int> decodeFn;
            if (progressive)
            {
                decodeFn = spectralStart == 0
                    ? (successivePrev == 0 ? (Action<Component, int>)DecodeDCFirst : DecodeDCSuccessive)
                    : (successivePrev == 0 ? (Action<Component, int>)DecodeACFirst : DecodeACSuccessive);
            }
            else
            {
                decodeFn = DecodeBaseline;
            }

            var mcu = 0;
            FileMarker fileMarker;
            int mcuExpected;
            if (componentsLength == 1)
            {
                mcuExpected = components[0].BlocksPerLine * components[0].BlocksPerColumn;
            }
            else
            {
                mcuExpected = mcusPerLine * frame.McusPerColumn;
            }

            while (mcu < mcuExpected)
            {
                var mcuToRead = resetInterval != 0 ? Math.Min(mcuExpected - mcu, resetInterval) : mcuExpected;
                for (var i = 0; i < componentsLength; i++)
                {
                    components[i].Pred = 0;
                }

                eobrun = 0;
                if (componentsLength == 1)
                {
                    var component = components[0];
                    for (var n = 0; n < mcuToRead; n++)
                    {
                        DecodeBlock(component, decodeFn, mcu);
                        mcu++;
                    }
                }
                else
                {
                    for (var n = 0; n < mcuToRead; n++)
                    {
                        for (var i = 0; i < componentsLength; i++)
                        {
                            var component = components[i];
                            var h = component.H;
                            var v = component.V;
                            for (var j = 0; j < v; j++)
                            {
                                for (var k2 = 0; k2 < h; k2++)
                                {
                                    DecodeMcu(component, decodeFn, mcu, j, k2);
                                }
                            }
                        }

                        mcu++;
                    }
                }

                bitsCount = 0;
                fileMarker = FindNextFileMarker(data, offset, int.MaxValue);
                if (fileMarker != null && fileMarker.Invalid != null)
                {
                    offset = fileMarker.Offset;
                }

                var marker = fileMarker != null ? fileMarker.Marker : 0;
                if (marker == 0 || marker <= 0xFF00)
                {
                    throw new InvalidOperationException("marker was not found");
                }

                if (marker >= 0xFFD0 && marker <= 0xFFD7)
                {
                    offset += 2;
                }
                else
                {
                    break;
                }
            }

            fileMarker = FindNextFileMarker(data, offset, int.MaxValue);
            if (fileMarker != null && fileMarker.Invalid != null)
            {
                offset = fileMarker.Offset;
            }

            return offset - startOffset;
        }

        static void QuantizeAndInverse(Component component, int blockBufferOffset, short[] p)
        {
            var qt = component.QuantizationTable;
            var blockData = component.BlockData;
            int v0, v1, v2, v3, v4, v5, v6, v7;
            int p0, p1, p2, p3, p4, p5, p6, p7;
            int t;

            if (qt == null)
            {
                throw new InvalidOperationException("missing required Quantization Table.");
            }

            for (var row = 0; row < 64; row += 8)
            {
                p0 = blockData[blockBufferOffset + row];
                p1 = blockData[blockBufferOffset + row + 1];
                p2 = blockData[blockBufferOffset + row + 2];
                p3 = blockData[blockBufferOffset + row + 3];
                p4 = blockData[blockBufferOffset + row + 4];
                p5 = blockData[blockBufferOffset + row + 5];
                p6 = blockData[blockBufferOffset + row + 6];
                p7 = blockData[blockBufferOffset + row + 7];
                p0 *= qt[row];
                if ((p1 | p2 | p3 | p4 | p5 | p6 | p7) == 0)
                {
                    t = ToInt32(DctSqrt2 * (double)p0 + 512) >> 10;
                    p[row] = (short)t;
                    p[row + 1] = (short)t;
                    p[row + 2] = (short)t;
                    p[row + 3] = (short)t;
                    p[row + 4] = (short)t;
                    p[row + 5] = (short)t;
                    p[row + 6] = (short)t;
                    p[row + 7] = (short)t;
                    continue;
                }

                p1 *= qt[row + 1];
                p2 *= qt[row + 2];
                p3 *= qt[row + 3];
                p4 *= qt[row + 4];
                p5 *= qt[row + 5];
                p6 *= qt[row + 6];
                p7 *= qt[row + 7];
                v0 = ToInt32(DctSqrt2 * (double)p0 + 128) >> 8;
                v1 = ToInt32(DctSqrt2 * (double)p4 + 128) >> 8;
                v2 = p2;
                v3 = p6;
                v4 = ToInt32(DctSqrt1d2 * (double)(p1 - p7) + 128) >> 8;
                v7 = ToInt32(DctSqrt1d2 * (double)(p1 + p7) + 128) >> 8;
                v5 = p3 << 4;
                v6 = p5 << 4;
                v0 = (v0 + v1 + 1) >> 1;
                v1 = v0 - v1;
                t = ToInt32(v2 * (double)DctSin6 + v3 * DctCos6 + 128) >> 8;
                v2 = ToInt32(v2 * (double)DctCos6 - v3 * DctSin6 + 128) >> 8;
                v3 = t;
                v4 = (v4 + v6 + 1) >> 1;
                v6 = v4 - v6;
                v7 = (v7 + v5 + 1) >> 1;
                v5 = v7 - v5;
                v0 = (v0 + v3 + 1) >> 1;
                v3 = v0 - v3;
                v1 = (v1 + v2 + 1) >> 1;
                v2 = v1 - v2;
                t = ToInt32(v4 * (double)DctSin3 + v7 * DctCos3 + 2048) >> 12;
                v4 = ToInt32(v4 * (double)DctCos3 - v7 * DctSin3 + 2048) >> 12;
                v7 = t;
                t = ToInt32(v5 * (double)DctSin1 + v6 * DctCos1 + 2048) >> 12;
                v5 = ToInt32(v5 * (double)DctCos1 - v6 * DctSin1 + 2048) >> 12;
                v6 = t;
                p[row] = (short)(v0 + v7);
                p[row + 7] = (short)(v0 - v7);
                p[row + 1] = (short)(v1 + v6);
                p[row + 6] = (short)(v1 - v6);
                p[row + 2] = (short)(v2 + v5);
                p[row + 5] = (short)(v2 - v5);
                p[row + 3] = (short)(v3 + v4);
                p[row + 4] = (short)(v3 - v4);
            }

            for (var col = 0; col < 8; ++col)
            {
                p0 = p[col];
                p1 = p[col + 8];
                p2 = p[col + 16];
                p3 = p[col + 24];
                p4 = p[col + 32];
                p5 = p[col + 40];
                p6 = p[col + 48];
                p7 = p[col + 56];
                if ((p1 | p2 | p3 | p4 | p5 | p6 | p7) == 0)
                {
                    t = ToInt32(DctSqrt2 * (double)p0 + 8192) >> 14;
                    t = t < -2040 ? 0 : t >= 2024 ? 255 : (t + 2056) >> 4;
                    blockData[blockBufferOffset + col] = (short)t;
                    blockData[blockBufferOffset + col + 8] = (short)t;
                    blockData[blockBufferOffset + col + 16] = (short)t;
                    blockData[blockBufferOffset + col + 24] = (short)t;
                    blockData[blockBufferOffset + col + 32] = (short)t;
                    blockData[blockBufferOffset + col + 40] = (short)t;
                    blockData[blockBufferOffset + col + 48] = (short)t;
                    blockData[blockBufferOffset + col + 56] = (short)t;
                    continue;
                }

                v0 = ToInt32(DctSqrt2 * (double)p0 + 2048) >> 12;
                v1 = ToInt32(DctSqrt2 * (double)p4 + 2048) >> 12;
                v2 = p2;
                v3 = p6;
                v4 = ToInt32(DctSqrt1d2 * (double)(p1 - p7) + 2048) >> 12;
                v7 = ToInt32(DctSqrt1d2 * (double)(p1 + p7) + 2048) >> 12;
                v5 = p3;
                v6 = p5;
                v0 = ((v0 + v1 + 1) >> 1) + 4112;
                v1 = v0 - v1;
                t = ToInt32(v2 * (double)DctSin6 + v3 * DctCos6 + 2048) >> 12;
                v2 = ToInt32(v2 * (double)DctCos6 - v3 * DctSin6 + 2048) >> 12;
                v3 = t;
                v4 = (v4 + v6 + 1) >> 1;
                v6 = v4 - v6;
                v7 = (v7 + v5 + 1) >> 1;
                v5 = v7 - v5;
                v0 = (v0 + v3 + 1) >> 1;
                v3 = v0 - v3;
                v1 = (v1 + v2 + 1) >> 1;
                v2 = v1 - v2;
                t = ToInt32(v4 * (double)DctSin3 + v7 * DctCos3 + 2048) >> 12;
                v4 = ToInt32(v4 * (double)DctCos3 - v7 * DctSin3 + 2048) >> 12;
                v7 = t;
                t = ToInt32(v5 * (double)DctSin1 + v6 * DctCos1 + 2048) >> 12;
                v5 = ToInt32(v5 * (double)DctCos1 - v6 * DctSin1 + 2048) >> 12;
                v6 = t;
                p0 = v0 + v7;
                p7 = v0 - v7;
                p1 = v1 + v6;
                p6 = v1 - v6;
                p2 = v2 + v5;
                p5 = v2 - v5;
                p3 = v3 + v4;
                p4 = v3 - v4;
                p0 = p0 < 16 ? 0 : p0 >= 4080 ? 255 : p0 >> 4;
                p1 = p1 < 16 ? 0 : p1 >= 4080 ? 255 : p1 >> 4;
                p2 = p2 < 16 ? 0 : p2 >= 4080 ? 255 : p2 >> 4;
                p3 = p3 < 16 ? 0 : p3 >= 4080 ? 255 : p3 >> 4;
                p4 = p4 < 16 ? 0 : p4 >= 4080 ? 255 : p4 >> 4;
                p5 = p5 < 16 ? 0 : p5 >= 4080 ? 255 : p5 >> 4;
                p6 = p6 < 16 ? 0 : p6 >= 4080 ? 255 : p6 >> 4;
                p7 = p7 < 16 ? 0 : p7 >= 4080 ? 255 : p7 >> 4;
                blockData[blockBufferOffset + col] = (short)p0;
                blockData[blockBufferOffset + col + 8] = (short)p1;
                blockData[blockBufferOffset + col + 16] = (short)p2;
                blockData[blockBufferOffset + col + 24] = (short)p3;
                blockData[blockBufferOffset + col + 32] = (short)p4;
                blockData[blockBufferOffset + col + 40] = (short)p5;
                blockData[blockBufferOffset + col + 48] = (short)p6;
                blockData[blockBufferOffset + col + 56] = (short)p7;
            }
        }

        static void BuildComponentData(Frame frame, Component component)
        {
            var blocksPerLine = component.BlocksPerLine;
            var blocksPerColumn = component.BlocksPerColumn;
            var computationBuffer = new short[64];
            for (var blockRow = 0; blockRow < blocksPerColumn; blockRow++)
            {
                for (var blockCol = 0; blockCol < blocksPerLine; blockCol++)
                {
                    var offset = GetBlockBufferOffset(component, blockRow, blockCol);
                    QuantizeAndInverse(component, offset, computationBuffer);
                }
            }

            component.Output = component.BlockData;
        }

        public void Parse(byte[] data)
        {
            var offset = 0;

            int ReadUint16()
            {
                var value = (data[offset] << 8) | data[offset + 1];
                offset += 2;
                return value;
            }

            byte[] ReadDataBlock()
            {
                var length = ReadUint16();
                var endOffset = offset + length - 2;
                var fileMarker = FindNextFileMarker(data, endOffset, offset);
                if (fileMarker != null && fileMarker.Invalid != null)
                {
                    endOffset = fileMarker.Offset;
                }

                var result = new byte[endOffset - offset];
                Array.Copy(data, offset, result, 0, result.Length);
                offset += result.Length;
                return result;
            }

            void PrepareComponents(Frame frame)
            {
                var mcusPerLine = (int)Math.Ceiling(frame.SamplesPerLine / 8.0 / frame.MaxH);
                var mcusPerColumn = (int)Math.Ceiling(frame.ScanLines / 8.0 / frame.MaxV);
                for (var i = 0; i < frame.Components.Count; i++)
                {
                    var component = frame.Components[i];
                    var blocksPerLine = (int)Math.Ceiling(Math.Ceiling(frame.SamplesPerLine / 8.0) * component.H / frame.MaxH);
                    var blocksPerColumn = (int)Math.Ceiling(Math.Ceiling(frame.ScanLines / 8.0) * component.V / frame.MaxV);
                    var blocksPerLineForMcu = mcusPerLine * component.H;
                    var blocksPerColumnForMcu = mcusPerColumn * component.V;
                    var blocksBufferSize = 64 * blocksPerColumnForMcu * (blocksPerLineForMcu + 1);
                    component.BlockData = new short[blocksBufferSize];
                    component.BlocksPerLine = blocksPerLine;
                    component.BlocksPerColumn = blocksPerColumn;
                }

                frame.McusPerLine = mcusPerLine;
                frame.McusPerColumn = mcusPerColumn;
            }

            Frame frame = null;
            var resetInterval = 0;
            var quantizationTables = new List<int[]> { null, null, null, null };
            var huffmanTablesAC = new List<object[]> { null, null, null, null };
            var huffmanTablesDC = new List<object[]> { null, null, null, null };

            var fileMarker = ReadUint16();
            if (fileMarker != 0xFFD8)
            {
                throw new InvalidOperationException("SOI not found");
            }

            fileMarker = ReadUint16();
            while (fileMarker != 0xFFD9)
            {
                int i;
                int j;
                switch (fileMarker)
                {
                    case 0xFFE0:
                    case 0xFFE1:
                    case 0xFFE2:
                    case 0xFFE3:
                    case 0xFFE4:
                    case 0xFFE5:
                    case 0xFFE6:
                    case 0xFFE7:
                    case 0xFFE8:
                    case 0xFFE9:
                    case 0xFFEA:
                    case 0xFFEB:
                    case 0xFFEC:
                    case 0xFFED:
                    case 0xFFEE:
                    case 0xFFEF:
                    case 0xFFFE:
                        ReadDataBlock();
                        break;
                    case 0xFFDB:
                        var quantizationTablesLength = ReadUint16();
                        var quantizationTablesEnd = quantizationTablesLength + offset - 2;
                        int z;
                        while (offset < quantizationTablesEnd)
                        {
                            var quantizationTableSpec = data[offset++];
                            var tableData = new int[64];
                            if (quantizationTableSpec >> 4 == 0)
                            {
                                for (j = 0; j < 64; j++)
                                {
                                    z = DctZigZag[j];
                                    tableData[z] = data[offset++];
                                }
                            }
                            else if (quantizationTableSpec >> 4 == 1)
                            {
                                for (j = 0; j < 64; j++)
                                {
                                    z = DctZigZag[j];
                                    tableData[z] = ReadUint16();
                                }
                            }
                            else
                            {
                                throw new InvalidOperationException("DQT - invalid table spec");
                            }

                            if (quantizationTables.Count <= (quantizationTableSpec & 15))
                            {
                                while (quantizationTables.Count <= (quantizationTableSpec & 15))
                                {
                                    quantizationTables.Add(null);
                                }
                            }

                            quantizationTables[quantizationTableSpec & 15] = tableData;
                        }

                        break;
                    case 0xFFC0:
                    case 0xFFC1:
                    case 0xFFC2:
                        if (frame != null)
                        {
                            throw new InvalidOperationException("Only single frame JPEGs supported");
                        }

                        ReadUint16();
                        frame = new Frame();
                        frame.Extended = fileMarker == 0xFFC1;
                        frame.Progressive = fileMarker == 0xFFC2;
                        frame.Precision = data[offset++];
                        frame.ScanLines = ReadUint16();
                        frame.SamplesPerLine = ReadUint16();
                        var componentsCount = data[offset++];
                        int componentId;
                        var maxH = 0;
                        var maxV = 0;
                        for (i = 0; i < componentsCount; i++)
                        {
                            componentId = data[offset];
                            var h = data[offset + 1] >> 4;
                            var v = data[offset + 1] & 15;
                            if (maxH < h)
                            {
                                maxH = h;
                            }

                            if (maxV < v)
                            {
                                maxV = v;
                            }

                            var qId = data[offset + 2];
                            var component = new Component { H = h, V = v, QuantizationId = qId, QuantizationTable = null };
                            var index = frame.Components.Count;
                            frame.Components.Add(component);
                            frame.ComponentIds[componentId] = index;
                            offset += 3;
                        }

                        frame.MaxH = maxH;
                        frame.MaxV = maxV;
                        PrepareComponents(frame);
                        break;
                    case 0xFFC4:
                        var huffmanLength = ReadUint16();
                        for (i = 2; i < huffmanLength;)
                        {
                            var huffmanTableSpec = data[offset++];
                            var codeLengths = new int[16];
                            var codeLengthSum = 0;
                            for (j = 0; j < 16; j++)
                            {
                                codeLengthSum += codeLengths[j] = data[offset++];
                            }

                            var huffmanValues = new int[codeLengthSum];
                            for (j = 0; j < codeLengthSum; j++)
                            {
                                huffmanValues[j] = data[offset++];
                            }

                            i += 17 + codeLengthSum;
                            var table = BuildHuffmanTable(codeLengths, huffmanValues);
                            var tableList = huffmanTableSpec >> 4 == 0 ? huffmanTablesDC : huffmanTablesAC;
                            var tableIndex = huffmanTableSpec & 15;
                            while (tableList.Count <= tableIndex)
                            {
                                tableList.Add(null);
                            }

                            tableList[tableIndex] = table;
                        }

                        break;
                    case 0xFFDD:
                        ReadUint16();
                        resetInterval = ReadUint16();
                        break;
                    case 0xFFDA:
                        ReadUint16();
                        var selectorsCount = data[offset++];
                        var components = new List<Component>();
                        for (i = 0; i < selectorsCount; i++)
                        {
                            var componentIndex = frame.ComponentIds[data[offset++]];
                            var component = frame.Components[componentIndex];
                            var tableSpec = data[offset++];
                            component.HuffmanTableDC = huffmanTablesDC[tableSpec >> 4];
                            component.HuffmanTableAC = huffmanTablesAC[tableSpec & 15];
                            components.Add(component);
                        }

                        var spectralStart = data[offset++];
                        var spectralEnd = data[offset++];
                        var successiveApproximation = data[offset++];
                        var processed = DecodeScan(data, offset, frame, components, resetInterval, spectralStart, spectralEnd, successiveApproximation >> 4, successiveApproximation & 15);
                        offset += processed;
                        break;
                    case 0xFFFF:
                        if (data[offset] != 0xFF)
                        {
                            offset--;
                        }

                        break;
                    default:
                        if (data[offset - 3] == 0xFF && data[offset - 2] >= 0xC0 && data[offset - 2] <= 0xFE)
                        {
                            offset -= 3;
                            break;
                        }

                        throw new InvalidOperationException("unknown marker " + fileMarker.ToString("x2"));
                }

                fileMarker = ReadUint16();
            }

            Width = frame.SamplesPerLine;
            Height = frame.ScanLines;
            _components = frame.Components;
            foreach (var component in _components)
            {
                if (component.QuantizationId < quantizationTables.Count && quantizationTables[component.QuantizationId] != null)
                {
                    component.QuantizationTable = quantizationTables[component.QuantizationId];
                }

                BuildComponentData(frame, component);
            }
        }

        /// <summary>
        /// Returns interleaved component data (width*height*componentCount bytes).
        /// Components 0 and 2 are swapped to produce RGB(A) order from the BGR(A)
        /// layout WC3 stores. For 4-component images this is RGBA directly.
        /// </summary>
        public byte[] GetData()
        {
            var width = Width;
            var height = Height;
            var components = _components;
            var numComponents = components.Count;

            var originalBlockPerLine = components[0].BlocksPerLine;
            var originalBlocksPerColumn = components[0].BlocksPerColumn;
            var lineData = new byte[(originalBlockPerLine << 3) * originalBlocksPerColumn * 8];

            var tmp = components[0];
            components[0] = components[2];
            components[2] = tmp;

            var data = new byte[width * height * numComponents];
            for (var i = 0; i < numComponents; i++)
            {
                var component = components[i];
                var blocksPerLine = component.BlocksPerLine;
                var blocksPerColumn = component.BlocksPerColumn;
                var samplesPerLine = blocksPerLine << 3;

                for (var blockRow = 0; blockRow < blocksPerColumn; blockRow++)
                {
                    var scanLine = blockRow << 3;
                    for (var blockCol = 0; blockCol < blocksPerLine; blockCol++)
                    {
                        var bufferOffset = GetBlockBufferOffset(component, blockRow, blockCol);
                        var offset2 = 0;
                        var sample = blockCol << 3;
                        for (var j = 0; j < 8; j++)
                        {
                            var lineOffset = (scanLine + j) * samplesPerLine;
                            for (var k2 = 0; k2 < 8; k2++)
                            {
                                lineData[lineOffset + sample + k2] = (byte)component.Output[bufferOffset + offset2++];
                            }
                        }
                    }
                }

                var outputOffset = i;
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        data[outputOffset] = lineData[y * samplesPerLine + x];
                        outputOffset += numComponents;
                    }
                }
            }

            return data;
        }
    }
}
