using System;
using System.Collections.Generic;

namespace Game.Mdx
{
    public enum InterpolationKind
    {
        DontInterp = 0,
        Linear = 1,
        Hermite = 2,
        Bezier = 3,
    }

    /// <summary>
    /// An animation. Ported from <c>mdlx/animations.ts</c>.
    /// Values are stored as float arrays regardless of the source type (u32/f32)
    /// since every consumer (mesh building, runtime evaluation) treats them numerically.
    /// </summary>
    public abstract class Animation
    {
        public string Name = "";
        public int InterpolationType;
        public int GlobalSequenceId = -1;
        public readonly List<int> Frames = new();
        public readonly List<float[]> Values = new();
        public readonly List<float[]> InTans = new();
        public readonly List<float[]> OutTans = new();

        public void ReadMdx(BinaryStream stream, string name)
        {
            var tracksCount = stream.ReadUint32();
            var interpolationType = stream.ReadUint32();

            Name = name;
            InterpolationType = (int)interpolationType;
            GlobalSequenceId = stream.ReadInt32();

            for (var i = 0; i < tracksCount; i++)
            {
                Frames.Add(stream.ReadInt32());
                Values.Add(ReadMdxValue(stream));

                if (interpolationType > (uint)InterpolationKind.Linear)
                {
                    InTans.Add(ReadMdxValue(stream));
                    OutTans.Add(ReadMdxValue(stream));
                }
            }
        }

        /// <summary>Convenience: total value count (sum of all track element lengths).</summary>
        public int TrackCount => Frames.Count;

        protected abstract float[] ReadMdxValue(BinaryStream stream);
    }

    /// <summary>A uint animation.</summary>
    public sealed class UintAnimation : Animation
    {
        protected override float[] ReadMdxValue(BinaryStream stream)
        {
            return new[] { (float)stream.ReadUint32() };
        }
    }

    /// <summary>A float animation.</summary>
    public sealed class FloatAnimation : Animation
    {
        protected override float[] ReadMdxValue(BinaryStream stream)
        {
            return new[] { stream.ReadFloat32() };
        }
    }

    /// <summary>A vector 3 animation.</summary>
    public sealed class Vector3Animation : Animation
    {
        protected override float[] ReadMdxValue(BinaryStream stream)
        {
            return stream.ReadFloat32Array(3);
        }
    }

    /// <summary>A vector 4 animation.</summary>
    public sealed class Vector4Animation : Animation
    {
        protected override float[] ReadMdxValue(BinaryStream stream)
        {
            return stream.ReadFloat32Array(4);
        }
    }
}
