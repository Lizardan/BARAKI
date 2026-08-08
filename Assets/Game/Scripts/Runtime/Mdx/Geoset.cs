using System.Collections.Generic;

namespace Game.Mdx
{
    /// <summary>A geoset. Ported from <c>mdlx/geoset.ts</c>.</summary>
    public sealed class Geoset
    {
        public float[] Vertices = System.Array.Empty<float>();
        public float[] Normals = System.Array.Empty<float>();
        public uint[] FaceTypeGroups = System.Array.Empty<uint>();
        public uint[] FaceGroups = System.Array.Empty<uint>();
        public ushort[] Faces = System.Array.Empty<ushort>();
        public byte[] VertexGroups = System.Array.Empty<byte>();
        public uint[] MatrixGroups = System.Array.Empty<uint>();
        public uint[] MatrixIndices = System.Array.Empty<uint>();
        public uint MaterialId;
        public uint SelectionGroup;
        public uint SelectionFlags;

        // @since 900
        public int Lod = -1;
        public string LodName = "";

        public Extent Extent = new();
        public readonly List<Extent> SequenceExtents = new();

        // @since 900
        public float[] Tangents = System.Array.Empty<float>();

        // @since 900 — [B0, B1, B2, B3, W0, W1, W2, W3] per vertex.
        public byte[] Skin = System.Array.Empty<byte>();

        public readonly List<float[]> UvSets = new();

        public void ReadMdx(BinaryStream stream, int version)
        {
            stream.ReadUint32(); // Don't care about the size.

            stream.Skip(4); // VRTX
            Vertices = stream.ReadFloat32Array((int)stream.ReadUint32() * 3);
            stream.Skip(4); // NRMS
            Normals = stream.ReadFloat32Array((int)stream.ReadUint32() * 3);
            stream.Skip(4); // PTYP
            FaceTypeGroups = stream.ReadUint32Array((int)stream.ReadUint32());
            stream.Skip(4); // PCNT
            FaceGroups = stream.ReadUint32Array((int)stream.ReadUint32());
            stream.Skip(4); // PVTX
            Faces = stream.ReadUint16Array((int)stream.ReadUint32());
            stream.Skip(4); // GNDX
            VertexGroups = stream.ReadUint8Array((int)stream.ReadUint32());
            stream.Skip(4); // MTGC
            MatrixGroups = stream.ReadUint32Array((int)stream.ReadUint32());
            stream.Skip(4); // MATS
            MatrixIndices = stream.ReadUint32Array((int)stream.ReadUint32());
            MaterialId = stream.ReadUint32();
            SelectionGroup = stream.ReadUint32();
            SelectionFlags = stream.ReadUint32();

            if (version > 800)
            {
                Lod = stream.ReadInt32();
                LodName = stream.Read(80);
            }

            Extent = new Extent();
            Extent.ReadMdx(stream);

            var extentCount = stream.ReadUint32();
            for (var i = 0; i < extentCount; i++)
            {
                var extent = new Extent();
                extent.ReadMdx(stream);
                SequenceExtents.Add(extent);
            }

            // Non-reforged models that come with reforged are saved with version >800,
            // however they don't have TANG and SKIN.
            if (version > 800)
            {
                if (stream.ReadBinary(4) == "TANG")
                {
                    Tangents = stream.ReadFloat32Array((int)stream.ReadUint32() * 4);
                }
                else
                {
                    stream.Skip(-4);
                }

                if (stream.ReadBinary(4) == "SKIN")
                {
                    Skin = stream.ReadUint8Array((int)stream.ReadUint32());
                }
                else
                {
                    stream.Skip(-4);
                }
            }

            stream.Skip(4); // UVAS

            var uvSetCount = stream.ReadUint32();
            for (var i = 0; i < uvSetCount; i++)
            {
                stream.Skip(4); // UVBS
                UvSets.Add(stream.ReadFloat32Array((int)stream.ReadUint32() * 2));
            }
        }
    }
}
