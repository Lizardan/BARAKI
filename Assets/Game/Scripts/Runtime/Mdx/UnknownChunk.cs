using System.Collections.Generic;

namespace Game.Mdx
{
    /// <summary>An unknown chunk. Ported from <c>mdlx/unknownchunk.ts</c>.</summary>
    public sealed class UnknownChunk
    {
        public string Tag;
        public byte[] Chunk;

        public UnknownChunk(BinaryStream stream, int size, string tag)
        {
            Tag = tag;
            Chunk = stream.ReadUint8Array(size);
        }
    }
}
