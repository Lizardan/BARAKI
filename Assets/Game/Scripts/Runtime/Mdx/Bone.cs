namespace Game.Mdx
{
    /// <summary>A bone. Ported from <c>mdlx/bone.ts</c>.</summary>
    public sealed class Bone : GenericObject
    {
        public int GeosetId = -1;
        public int GeosetAnimationId = -1;

        public override void ReadMdx(BinaryStream stream)
        {
            base.ReadMdx(stream);

            GeosetId = stream.ReadInt32();
            GeosetAnimationId = stream.ReadInt32();
        }
    }
}
