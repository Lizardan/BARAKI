using System.Collections.Generic;

namespace Game.Mdx
{
    public enum MaterialFlags
    {
        None = 0x0,
        ConstantColor = 0x1,
        TwoSided = 0x2,
        SortPrimsNearZ = 0x8,
        SortPrimsFarZ = 0x10,
        FullResolution = 0x20,
    }

    /// <summary>A material. Ported from <c>mdlx/material.ts</c>.</summary>
    public sealed class Material
    {
        public int PriorityPlane;
        public uint Flags;
        public string Shader = "";
        public readonly List<Layer> Layers = new();

        public void ReadMdx(BinaryStream stream, int version)
        {
            stream.ReadUint32(); // Don't care about the size.

            PriorityPlane = stream.ReadInt32();
            Flags = stream.ReadUint32();

            // The shader string exists since v900 but was dropped again in v1200 (Reforged 1.33+).
            if (version > 800 && version < 1200)
            {
                Shader = stream.Read(80);
            }

            stream.Skip(4); // LAYS

            var count = stream.ReadUint32();
            for (var i = 0; i < count; i++)
            {
                var layer = new Layer();
                layer.ReadMdx(stream, version);
                Layers.Add(layer);
            }
        }
    }
}
