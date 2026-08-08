namespace Game.Mdx
{
    public enum GenericObjectFlags
    {
        None = 0x0,
        DontInheritTranslation = 0x1,
        DontInheritScaling = 0x2,
        DontInheritRotation = 0x4,
        Billboarded = 0x8,
        BillboardedLockX = 0x10,
        BillboardedLockY = 0x20,
        BillboardedLockZ = 0x40,
        CameraAnchored = 0x80,
    }

    /// <summary>
    /// The parent class for all objects that exist in the world and may contain spatial animations.
    /// Includes bones, particle emitters, and many other things.
    /// Ported from <c>mdlx/genericobject.ts</c>.
    ///
    /// Generic object header layout (96 bytes):
    ///   nodeSize u32 | name[80] | objectId i32 | parentId i32 | flags u32
    /// followed by the generic animation tracks (KGTR/KGRT/KGSC).
    /// </summary>
    public abstract class GenericObject : AnimatedObject
    {
        public string Name = "";
        public int ObjectId = -1;
        public int ParentId = -1;
        public int Flags;

        public virtual void ReadMdx(BinaryStream stream)
        {
            var size = stream.ReadUint32();

            Name = stream.Read(80);
            ObjectId = stream.ReadInt32();
            ParentId = stream.ReadInt32();
            Flags = (int)stream.ReadUint32();

            ReadAnimations(stream, (int)size - 96);
        }
    }
}
