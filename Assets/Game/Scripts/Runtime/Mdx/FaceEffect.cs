namespace Game.Mdx
{
    /// <summary>A face effect. Ported from <c>mdlx/faceeffect.ts</c>. @since 900.</summary>
    public sealed class FaceEffect
    {
        public string Type = "";
        public string Path = "";

        public void ReadMdx(BinaryStream stream)
        {
            Type = stream.Read(80);
            Path = stream.Read(260);
        }
    }
}
