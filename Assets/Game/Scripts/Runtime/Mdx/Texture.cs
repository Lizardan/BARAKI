namespace Game.Mdx
{
    public enum WrapMode
    {
        RepeatBoth = 0,
        WrapWidth = 1,
        WrapHeight = 2,
        WrapBoth = 3,
    }

    /// <summary>A texture. Ported from <c>mdlx/texture.ts</c>.</summary>
    public sealed class Texture
    {
        public uint ReplaceableId;
        public string Path = "";
        public uint WrapMode;

        public void ReadMdx(BinaryStream stream)
        {
            ReplaceableId = stream.ReadUint32();
            Path = stream.Read(260);
            WrapMode = stream.ReadUint32();
        }
    }
}
