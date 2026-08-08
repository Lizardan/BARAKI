namespace Game.Mdx
{
    /// <summary>A particle emitter. Ported from <c>mdlx/particleemitter.ts</c>.</summary>
    public sealed class ParticleEmitter : GenericObject
    {
        public float EmissionRate;
        public float Gravity;
        public float Longitude;
        public float Latitude;
        public string Path = "";
        public float LifeSpan;
        public float Speed;

        public override void ReadMdx(BinaryStream stream)
        {
            var start = stream.Index;
            var size = stream.ReadUint32();

            base.ReadMdx(stream);

            EmissionRate = stream.ReadFloat32();
            Gravity = stream.ReadFloat32();
            Longitude = stream.ReadFloat32();
            Latitude = stream.ReadFloat32();
            Path = stream.Read(260);
            LifeSpan = stream.ReadFloat32();
            Speed = stream.ReadFloat32();

            ReadAnimations(stream, (int)(size - (stream.Index - start)));
        }
    }
}
