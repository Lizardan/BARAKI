namespace Game.Mdx
{
    /// <summary>A texture animation. Ported from <c>mdlx/textureanimation.ts</c>.</summary>
    public sealed class TextureAnimation : AnimatedObject
    {
        public void ReadMdx(BinaryStream stream)
        {
            var size = stream.ReadUint32();

            ReadAnimations(stream, (int)size - 4);
        }
    }
}
