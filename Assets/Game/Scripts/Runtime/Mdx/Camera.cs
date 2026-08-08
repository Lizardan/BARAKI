namespace Game.Mdx
{
    /// <summary>A camera. Ported from <c>mdlx/camera.ts</c>.</summary>
    public sealed class Camera : AnimatedObject
    {
        public string Name = "";
        public float[] Position = new float[3];
        public float FieldOfView;
        public float FarClippingPlane;
        public float NearClippingPlane;
        public float[] TargetPosition = new float[3];

        public void ReadMdx(BinaryStream stream)
        {
            var size = stream.ReadUint32();

            Name = stream.Read(80);
            Position = stream.ReadFloat32Array(3);
            FieldOfView = stream.ReadFloat32();
            FarClippingPlane = stream.ReadFloat32();
            NearClippingPlane = stream.ReadFloat32();
            TargetPosition = stream.ReadFloat32Array(3);

            ReadAnimations(stream, (int)size - 120);
        }
    }
}
