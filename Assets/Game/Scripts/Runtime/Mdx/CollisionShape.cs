namespace Game.Mdx
{
    public enum CollisionShapeType
    {
        Box = 0,
        Plane = 1,
        Sphere = 2,
        Cylinder = 3,
    }

    /// <summary>A collision shape. Ported from <c>mdlx/collisionshape.ts</c>.</summary>
    public sealed class CollisionShape : GenericObject
    {
        public uint Type;
        public float[] Vertices0 = new float[3];
        public float[] Vertices1 = new float[3];
        public float BoundsRadius;

        public override void ReadMdx(BinaryStream stream)
        {
            base.ReadMdx(stream);

            Type = stream.ReadUint32();
            Vertices0 = stream.ReadFloat32Array(3);

            if (Type != (uint)CollisionShapeType.Sphere)
            {
                Vertices1 = stream.ReadFloat32Array(3);
            }

            if (Type == (uint)CollisionShapeType.Sphere || Type == (uint)CollisionShapeType.Cylinder)
            {
                BoundsRadius = stream.ReadFloat32();
            }
        }
    }
}
