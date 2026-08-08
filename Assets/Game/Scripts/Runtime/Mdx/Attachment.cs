namespace Game.Mdx
{
    /// <summary>An attachment. Ported from <c>mdlx/attachment.ts</c>.</summary>
    public sealed class Attachment : GenericObject
    {
        public string Path = "";
        public int AttachmentId;

        public override void ReadMdx(BinaryStream stream)
        {
            var start = stream.Index;
            var size = stream.ReadUint32();

            base.ReadMdx(stream);

            Path = stream.Read(260);
            AttachmentId = stream.ReadInt32();

            ReadAnimations(stream, (int)(size - (stream.Index - start)));
        }
    }
}
