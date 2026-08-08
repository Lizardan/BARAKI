namespace Game.Mdx
{
    /// <summary>An extent. Ported from <c>mdlx/extent.ts</c>.</summary>
    public sealed class Extent
    {
        public float BoundsRadius;
        public float[] Min = new float[3];
        public float[] Max = new float[3];

        public void ReadMdx(BinaryStream stream)
        {
            BoundsRadius = stream.ReadFloat32();
            Min = stream.ReadFloat32Array(3);
            Max = stream.ReadFloat32Array(3);
        }
    }
}
