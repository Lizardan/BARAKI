namespace Game.Mdx
{
    /// <summary>
    /// A popcorn particle emitter. References a pkfx file used by the PopcornFX runtime.
    /// Ported from <c>mdlx/particleemitterpopcorn.ts</c>. @since 900.
    /// </summary>
    public sealed class ParticleEmitterPopcorn : GenericObject
    {
        public float LifeSpan;
        public float EmissionRate;
        public float Speed;
        public float[] Color = new float[3];
        public float Alpha = 1;
        public uint ReplaceableId;
        public string Path = "";
        public string AnimationVisiblityGuide = "";

        public override void ReadMdx(BinaryStream stream)
        {
            var start = stream.Index;
            var size = stream.ReadUint32();

            base.ReadMdx(stream);

            LifeSpan = stream.ReadFloat32();
            EmissionRate = stream.ReadFloat32();
            Speed = stream.ReadFloat32();
            Color = stream.ReadFloat32Array(3);
            Alpha = stream.ReadFloat32();
            ReplaceableId = stream.ReadUint32();
            Path = stream.Read(260);
            AnimationVisiblityGuide = stream.Read(260);

            ReadAnimations(stream, (int)(size - (stream.Index - start)));
        }
    }
}
