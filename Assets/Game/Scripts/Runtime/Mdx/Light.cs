namespace Game.Mdx
{
    public enum LightType
    {
        None = -1,
        Omnidirectional = 0,
        Directional = 1,
        Ambient = 2,
    }

    /// <summary>A light. Ported from <c>mdlx/light.ts</c>.</summary>
    public sealed class Light : GenericObject
    {
        public uint Type;
        public float[] Attenuation = new float[2];
        public float[] Color = new float[3];
        public float Intensity;
        public float[] AmbientColor = new float[3];
        public float AmbientIntensity;

        public override void ReadMdx(BinaryStream stream)
        {
            var start = stream.Index;
            var size = stream.ReadUint32();

            base.ReadMdx(stream);

            Type = stream.ReadUint32();
            Attenuation = stream.ReadFloat32Array(2);
            Color = stream.ReadFloat32Array(3);
            Intensity = stream.ReadFloat32();
            AmbientColor = stream.ReadFloat32Array(3);
            AmbientIntensity = stream.ReadFloat32();

            ReadAnimations(stream, (int)(size - (stream.Index - start)));
        }
    }
}
