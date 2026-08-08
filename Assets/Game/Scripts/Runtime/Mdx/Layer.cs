namespace Game.Mdx
{
    public enum FilterMode
    {
        None = 0,
        Transparent = 1,
        Blend = 2,
        Additive = 3,
        AddAlpha = 4,
        Modulate = 5,
        Modulate2x = 6,
    }

    public enum LayerFlags
    {
        None = 0x0,
        Unshaded = 0x1,
        SphereEnvMap = 0x2,
        TwoSided = 0x10,
        Unfogged = 0x20,
        NoDepthTest = 0x40,
        NoDepthSet = 0x80,
        Unlit = 0x100,
    }

    /// <summary>A layer. Ported from <c>mdlx/layer.ts</c>.</summary>
    public sealed class Layer : AnimatedObject
    {
        public uint FilterMode;
        public uint Flags;
        public int TextureId = -1;
        public int TextureAnimationId = -1;
        public uint CoordId;
        public float Alpha = 1;

        // @since 900
        public float EmissiveGain = 1;
        public float[] FresnelColor = { 1, 1, 1 };
        public float FresnelOpacity;
        public float FresnelTeamColor;

        // @since 1200
        public float[] Extra = new float[4];

        public void ReadMdx(BinaryStream stream, int version)
        {
            var start = stream.Index;
            var size = stream.ReadUint32();

            FilterMode = stream.ReadUint32();
            Flags = stream.ReadUint32();
            TextureId = stream.ReadInt32();
            TextureAnimationId = stream.ReadInt32();
            CoordId = stream.ReadUint32();
            Alpha = stream.ReadFloat32();

            // Note that even though these fields were introduced in versions 900 and 1000
            // separately, the game does not offer backwards compatibility.
            if (version > 800)
            {
                EmissiveGain = stream.ReadFloat32();
                FresnelColor = stream.ReadFloat32Array(3);
                FresnelOpacity = stream.ReadFloat32();
                FresnelTeamColor = stream.ReadFloat32();

                // v1200 (Reforged 1.33+) adds 16 extra bytes after the Fresnel fields.
                if (version >= 1200)
                {
                    Extra = stream.ReadFloat32Array(4);
                }
            }

            ReadAnimations(stream, (int)(size - (stream.Index - start)));
        }
    }
}
