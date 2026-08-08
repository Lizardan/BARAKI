namespace Game.Mdx
{
    /// <summary>A ribbon emitter. Ported from <c>mdlx/ribbonemitter.ts</c>.</summary>
    public sealed class RibbonEmitter : GenericObject
    {
        public float HeightAbove;
        public float HeightBelow;
        public float Alpha;
        public float[] Color = new float[3];
        public float LifeSpan;
        public uint TextureSlot;
        public uint EmissionRate;
        public uint Rows;
        public uint Columns;
        public int MaterialId;
        public float Gravity;

        public override void ReadMdx(BinaryStream stream)
        {
            var start = stream.Index;
            var size = stream.ReadUint32();

            base.ReadMdx(stream);

            HeightAbove = stream.ReadFloat32();
            HeightBelow = stream.ReadFloat32();
            Alpha = stream.ReadFloat32();
            Color = stream.ReadFloat32Array(3);
            LifeSpan = stream.ReadFloat32();
            TextureSlot = stream.ReadUint32();
            EmissionRate = stream.ReadUint32();
            Rows = stream.ReadUint32();
            Columns = stream.ReadUint32();
            MaterialId = stream.ReadInt32();
            Gravity = stream.ReadFloat32();

            ReadAnimations(stream, (int)(size - (stream.Index - start)));
        }
    }
}
