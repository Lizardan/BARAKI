namespace Game.Mdx
{
    public enum ParticleEmitter2Flags
    {
        Unshaded = 0x8000,
        SortPrimsFarZ = 0x10000,
        LineEmitter = 0x20000,
        Unfogged = 0x40000,
        ModelSpace = 0x80000,
        XYQuad = 0x100000,
    }

    public enum ParticleEmitter2FilterMode
    {
        Blend = 0,
        Additive = 1,
        Modulate = 2,
        Modulate2x = 3,
        AlphaKey = 4,
    }

    public enum HeadOrTail
    {
        Head = 0,
        Tail = 1,
        Both = 2,
    }

    /// <summary>A particle emitter type 2. Ported from <c>mdlx/particleemitter2.ts</c>.</summary>
    public sealed class ParticleEmitter2 : GenericObject
    {
        public float Speed;
        public float Variation;
        public float Latitude;
        public float Gravity;
        public float LifeSpan;
        public float EmissionRate;
        public float Width;
        public float Length;
        public uint FilterMode;
        public uint Rows;
        public uint Columns;
        public uint HeadOrTail;
        public float TailLength;
        public float TimeMiddle;
        public readonly float[][] SegmentColors = { new float[3], new float[3], new float[3] };
        public byte[] SegmentAlphas = new byte[3];
        public float[] SegmentScaling = new float[3];
        public readonly uint[][] HeadIntervals = { new uint[3], new uint[3] };
        public readonly uint[][] TailIntervals = { new uint[3], new uint[3] };
        public int TextureId = -1;
        public uint Squirt;
        public int PriorityPlane;
        public uint ReplaceableId;

        public override void ReadMdx(BinaryStream stream)
        {
            var start = stream.Index;
            var size = stream.ReadUint32();

            base.ReadMdx(stream);

            Speed = stream.ReadFloat32();
            Variation = stream.ReadFloat32();
            Latitude = stream.ReadFloat32();
            Gravity = stream.ReadFloat32();
            LifeSpan = stream.ReadFloat32();
            EmissionRate = stream.ReadFloat32();
            Width = stream.ReadFloat32();
            Length = stream.ReadFloat32();
            FilterMode = stream.ReadUint32();
            Rows = stream.ReadUint32();
            Columns = stream.ReadUint32();
            HeadOrTail = stream.ReadUint32();
            TailLength = stream.ReadFloat32();
            TimeMiddle = stream.ReadFloat32();
            SegmentColors[0] = stream.ReadFloat32Array(3);
            SegmentColors[1] = stream.ReadFloat32Array(3);
            SegmentColors[2] = stream.ReadFloat32Array(3);
            SegmentAlphas = stream.ReadUint8Array(3);
            SegmentScaling = stream.ReadFloat32Array(3);
            HeadIntervals[0] = stream.ReadUint32Array(3);
            HeadIntervals[1] = stream.ReadUint32Array(3);
            TailIntervals[0] = stream.ReadUint32Array(3);
            TailIntervals[1] = stream.ReadUint32Array(3);
            TextureId = stream.ReadInt32();
            Squirt = stream.ReadUint32();
            PriorityPlane = stream.ReadInt32();
            ReplaceableId = stream.ReadUint32();

            ReadAnimations(stream, (int)(size - (stream.Index - start)));
        }
    }
}
