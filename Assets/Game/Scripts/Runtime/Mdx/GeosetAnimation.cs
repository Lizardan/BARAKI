namespace Game.Mdx
{
    /// <summary>A geoset animation. Ported from <c>mdlx/geosetanimation.ts</c>.</summary>
    public sealed class GeosetAnimation : AnimatedObject
    {
        public float Alpha = 1;
        public uint Flags;
        public float[] Color = { 1, 1, 1 };
        public int GeosetId = -1;

        public void ReadMdx(BinaryStream stream)
        {
            var size = stream.ReadUint32();

            Alpha = stream.ReadFloat32();
            Flags = stream.ReadUint32();
            Color = stream.ReadFloat32Array(3);
            GeosetId = stream.ReadInt32();

            ReadAnimations(stream, (int)size - 28);
        }
    }
}
