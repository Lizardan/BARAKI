namespace Game.Mdx
{
    /// <summary>An event object. Ported from <c>mdlx/eventobject.ts</c>.</summary>
    public sealed class EventObject : GenericObject
    {
        public int GlobalSequenceId = -1;
        public uint[] Tracks = System.Array.Empty<uint>();

        public override void ReadMdx(BinaryStream stream)
        {
            base.ReadMdx(stream);

            stream.Skip(4); // KEVT

            var count = stream.ReadUint32();

            GlobalSequenceId = stream.ReadInt32();
            Tracks = stream.ReadUint32Array((int)count);
        }
    }
}
