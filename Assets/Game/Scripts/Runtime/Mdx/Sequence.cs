namespace Game.Mdx
{
    /// <summary>A sequence. Ported from <c>mdlx/sequence.ts</c>.</summary>
    public sealed class Sequence
    {
        public string Name = "";
        public uint[] Interval = new uint[2];
        public float MoveSpeed;
        public uint NonLooping;
        public float Rarity;
        public uint SyncPoint;
        public Extent Extent = new();

        public void ReadMdx(BinaryStream stream)
        {
            Name = stream.Read(80);
            Interval = stream.ReadUint32Array(2);
            MoveSpeed = stream.ReadFloat32();
            NonLooping = stream.ReadUint32();
            Rarity = stream.ReadFloat32();
            SyncPoint = stream.ReadUint32();
            Extent = new Extent();
            Extent.ReadMdx(stream);
        }
    }
}
