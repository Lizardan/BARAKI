using System;
using System.Collections.Generic;

namespace Game.Mdx
{
    /// <summary>
    /// Evaluates raw MDX animation tracks. Ported from <c>sd.ts</c> of the mdx-m3-viewer
    /// reference. For every track, per-sequence data is precomputed together with a
    /// "constant" flag (the reference calls it variancy): a track that is constant for the
    /// current sequence can be skipped entirely, which is how the reference viewer keeps
    /// per-frame evaluation cheap.
    ///
    /// Two clock domains exist:
    ///  - per-sequence tracks evaluate with the sequence frame (ms, clamped to the interval);
    ///  - global-sequence tracks evaluate with <c>counter % length</c>, where counter is a
    ///    free-running ms clock (the reference "global sequences").
    ///
    /// Interpolation matches the reference: none/linear/hermite/bezier for scalars and
    /// vectors, and slerp/sqlerp for quaternions. Visibility tracks are forced to "none".
    /// </summary>
    public abstract class SampledTrack
    {
        public string Name = "";
        public int InterpolationType;
        public float[] DefaultValue = new[] { 0f };

        TrackSequence _global;
        TrackSequence[] _sequences = Array.Empty<TrackSequence>();

        public bool IsGlobal => _global != null;
        public int ValueCount => DefaultValue.Length;

        /// <summary>True when the track animates in the given sequence (i.e. is not constant).</summary>
        public bool IsVariant(int sequence)
        {
            if (_global != null)
            {
                return !_global.Constant;
            }

            return sequence >= 0 && sequence < _sequences.Length && !_sequences[sequence].Constant;
        }

        /// <summary>
        /// Writes the sampled value into <paramref name="result"/> (length must be
        /// <see cref="ValueCount"/>). <paramref name="frame"/> is the sequence time in ms,
        /// <paramref name="counter"/> the free-running ms clock used by global sequences.
        /// </summary>
        public void GetValue(float[] result, int sequence, float frame, float counter)
        {
            if (_global != null)
            {
                _global.GetValue(result, counter % _global.End);
            }
            else if (sequence >= 0 && sequence < _sequences.Length)
            {
                _sequences[sequence].GetValue(result, frame);
            }
            else
            {
                Copy(result, DefaultValue);
            }
        }

        public static SampledTrack Create(MdxModel model, Animation animation)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (animation == null)
            {
                throw new ArgumentNullException(nameof(animation));
            }

            SampledTrack track;
            if (animation is UintAnimation || animation is FloatAnimation)
            {
                track = new ScalarTrack();
            }
            else if (animation is Vector3Animation)
            {
                track = new Vector3Track();
            }
            else
            {
                track = new Vector4Track();
            }

            track.Name = animation.Name;
            track.InterpolationType = ForcedInterpolation(animation.Name) ?? animation.InterpolationType;
            track.DefaultValue = DefaultValueFor(animation.Name);
            track.Build(model, animation);

            return track;
        }

        void Build(MdxModel model, Animation animation)
        {
            var globalId = animation.GlobalSequenceId;

            if (globalId != -1 && globalId < model.GlobalSequences.Count)
            {
                _global = new TrackSequence(this, 0, model.GlobalSequences[globalId], animation);
                _sequences = Array.Empty<TrackSequence>();
            }
            else
            {
                _sequences = new TrackSequence[model.Sequences.Count];

                for (var i = 0; i < _sequences.Length; i++)
                {
                    var interval = model.Sequences[i].Interval;
                    _sequences[i] = new TrackSequence(this, (int)interval[0], (int)interval[1], animation);
                }
            }
        }

        public abstract void Copy(float[] result, float[] value);
        public abstract void Interpolate(float[] result, IList<float[]> values, IList<float[]> inTans, IList<float[]> outTans, int start, int end, float t);

        // --- Math helpers (ports of common/math.ts and gl-matrix) ---

        public static float Hermite(float a, float b, float c, float d, float t)
        {
            var factorTimes2 = t * t;
            var factor1 = factorTimes2 * (2 * t - 3) + 1;
            var factor2 = factorTimes2 * (t - 2) + t;
            var factor3 = factorTimes2 * (t - 1);
            var factor4 = factorTimes2 * (3 - 2 * t);

            return a * factor1 + b * factor2 + c * factor3 + d * factor4;
        }

        public static float Bezier(float a, float b, float c, float d, float t)
        {
            var invt = 1 - t;
            var factorTimes2 = t * t;
            var inverseFactorTimesTwo = invt * invt;
            var factor1 = inverseFactorTimesTwo * invt;
            var factor2 = 3 * t * inverseFactorTimesTwo;
            var factor3 = 3 * factorTimes2 * invt;
            var factor4 = factorTimes2 * t;

            return a * factor1 + b * factor2 + c * factor3 + d * factor4;
        }

        /// <summary>gl-matrix quat.slerp, without mutating its inputs.</summary>
        public static void Slerp(float[] result, float[] a, float[] b, float t)
        {
            var bx = b[0];
            var by = b[1];
            var bz = b[2];
            var bw = b[3];

            var cosHalfTheta = a[0] * bx + a[1] * by + a[2] * bz + a[3] * bw;
            if (cosHalfTheta < 0)
            {
                bx = -bx;
                by = -by;
                bz = -bz;
                bw = -bw;
                cosHalfTheta = -cosHalfTheta;
            }

            if (cosHalfTheta >= 1)
            {
                result[0] = a[0];
                result[1] = a[1];
                result[2] = a[2];
                result[3] = a[3];
                return;
            }

            var halfTheta = Math.Acos(cosHalfTheta);
            var sinHalfTheta = Math.Sqrt(1 - cosHalfTheta * cosHalfTheta);

            float ratioA;
            float ratioB;
            if (Math.Abs(sinHalfTheta) < 0.001)
            {
                ratioA = 0.5f;
                ratioB = 0.5f;
            }
            else
            {
                ratioA = (float)(Math.Sin((1 - t) * halfTheta) / sinHalfTheta);
                ratioB = (float)(Math.Sin(t * halfTheta) / sinHalfTheta);
            }

            result[0] = a[0] * ratioA + bx * ratioB;
            result[1] = a[1] * ratioA + by * ratioB;
            result[2] = a[2] * ratioA + bz * ratioB;
            result[3] = a[3] * ratioA + bw * ratioB;
        }

        /// <summary>gl-matrix quat.sqlerp: squad(a, b, c, d, t).</summary>
        public static void Sqlerp(float[] result, float[] a, float[] b, float[] c, float[] d, float t)
        {
            var temp1 = new float[4];
            var temp2 = new float[4];

            Slerp(temp1, a, d, t);
            Slerp(temp2, b, c, t);
            Slerp(result, temp1, temp2, 2 * t * (1 - t));
        }

        static readonly Dictionary<string, float[]> DefVals = new()
        {
            // Scalars.
            { "KMTF", new[] { 0f } },
            { "KFTC", new[] { 0f } },
            { "KRTX", new[] { 0f } },
            { "KMTA", new[] { 1f } },
            { "KMTE", new[] { 0f } },
            { "KFCA", new[] { 0f } },
            { "KGAO", new[] { 1f } },
            { "KLAS", new[] { 0f } },
            { "KLAE", new[] { 0f } },
            { "KLAI", new[] { 0f } },
            { "KLBI", new[] { 0f } },
            { "KLAV", new[] { 1f } },
            { "KATV", new[] { 1f } },
            { "KPEE", new[] { 0f } },
            { "KPEG", new[] { 0f } },
            { "KPLN", new[] { 0f } },
            { "KPLT", new[] { 0f } },
            { "KPEL", new[] { 0f } },
            { "KPES", new[] { 0f } },
            { "KPEV", new[] { 1f } },
            { "KP2S", new[] { 0f } },
            { "KP2R", new[] { 0f } },
            { "KP2L", new[] { 0f } },
            { "KP2G", new[] { 0f } },
            { "KP2E", new[] { 0f } },
            { "KP2N", new[] { 0f } },
            { "KP2W", new[] { 0f } },
            { "KP2V", new[] { 1f } },
            { "KRHA", new[] { 0f } },
            { "KRHB", new[] { 0f } },
            { "KRAL", new[] { 0f } },
            { "KRVS", new[] { 1f } },
            { "KCRL", new[] { 0f } },
            // Vectors.
            { "KTAT", new[] { 0f, 0f, 0f } },
            { "KGTR", new[] { 0f, 0f, 0f } },
            { "KCTR", new[] { 0f, 0f, 0f } },
            { "KTTR", new[] { 0f, 0f, 0f } },
            { "KGAC", new[] { 0f, 0f, 0f } },
            { "KLAC", new[] { 0f, 0f, 0f } },
            { "KLBC", new[] { 0f, 0f, 0f } },
            { "KRCO", new[] { 0f, 0f, 0f } },
            { "KFC3", new[] { 0f, 0f, 0f } },
            { "KPPC", new[] { 0f, 0f, 0f } },
            { "KTAS", new[] { 1f, 1f, 1f } },
            { "KGSC", new[] { 1f, 1f, 1f } },
            // Quaternions.
            { "KTAR", new[] { 0f, 0f, 0f, 1f } },
            { "KGRT", new[] { 0f, 0f, 0f, 1f } },
        };

        static readonly Dictionary<string, int> ForcedInterp = new()
        {
            { "KLAV", (int)InterpolationKind.DontInterp },
            { "KATV", (int)InterpolationKind.DontInterp },
            { "KPEV", (int)InterpolationKind.DontInterp },
            { "KP2V", (int)InterpolationKind.DontInterp },
            { "KRVS", (int)InterpolationKind.DontInterp },
        };

        static float[] DefaultValueFor(string name)
        {
            return DefVals.TryGetValue(name, out var value) ? value : new[] { 0f };
        }

        static int? ForcedInterpolation(string name)
        {
            return ForcedInterp.TryGetValue(name, out var value) ? value : null;
        }

        /// <summary>Per-sequence key data for one track (port of <c>SdSequence</c>).</summary>
        sealed class TrackSequence
        {
            readonly SampledTrack _track;
            readonly List<int> _frames = new();
            readonly List<float[]> _values = new();
            readonly List<float[]> _inTans = new();
            readonly List<float[]> _outTans = new();

            public int Start;
            public int End;
            public bool Constant;
            public float[] ConstantValue;

            public TrackSequence(SampledTrack track, int start, int end, Animation animation)
            {
                _track = track;
                Start = start;
                End = end;

                var interpolationType = track.InterpolationType;
                var frames = animation.Frames;
                var values = animation.Values;
                var inTans = animation.InTans;
                var outTans = animation.OutTans;
                var defval = track.DefaultValue;
                var isGlobal = track.IsGlobal;

                // When using a global sequence, where the first key is outside of the
                // sequence's length, it becomes its constant value (only that edge case is
                // handled, matching the reference - the mixed case is non-deterministic
                // even in the game).
                if (isGlobal && frames.Count > 0 && frames[0] > end)
                {
                    _frames.Add(frames[0]);
                    _values.Add(values[0]);
                }

                for (var i = 0; i < frames.Count; i++)
                {
                    var frame = frames[i];
                    if (frame >= start && frame <= end)
                    {
                        _frames.Add(frame);
                        _values.Add(values[i]);

                        if (interpolationType > (int)InterpolationKind.Linear)
                        {
                            _inTans.Add(inTans[i]);
                            _outTans.Add(outTans[i]);
                        }
                    }
                }

                var count = _frames.Count;
                if (count == 0)
                {
                    // No keys in range: use the default value directly.
                    Constant = true;
                    _frames.Add(start);
                    _values.Add(defval);
                }
                else if (count == 1)
                {
                    // A single key: constant.
                    Constant = true;
                }
                else
                {
                    // All keys the same: constant.
                    Constant = AllEqual(_values);
                }

                ConstantValue = _values[0];
            }

            public void GetValue(float[] result, float frame)
            {
                var count = _frames.Count;

                if (Constant || frame < Start)
                {
                    _track.Copy(result, ConstantValue);
                    return;
                }

                var lengthLessOne = count - 1;
                int startFrameIndex;
                int endFrameIndex;

                if (frame < _frames[0] || frame >= _frames[lengthLessOne])
                {
                    // Wrap around the sequence.
                    startFrameIndex = lengthLessOne;
                    endFrameIndex = 0;
                }
                else
                {
                    startFrameIndex = -1;
                    endFrameIndex = -1;

                    for (var i = 1; i < count; i++)
                    {
                        if (_frames[i] > frame)
                        {
                            startFrameIndex = i - 1;
                            endFrameIndex = i;
                            break;
                        }
                    }
                }

                var startFrame = _frames[startFrameIndex];
                var endFrame = _frames[endFrameIndex];
                var timeBetweenFrames = endFrame - startFrame;

                if (timeBetweenFrames < 0)
                {
                    timeBetweenFrames += End - Start;

                    if (frame < startFrame)
                    {
                        startFrame = endFrame;
                    }
                }

                var t = timeBetweenFrames == 0 ? 0f : (frame - startFrame) / timeBetweenFrames;

                _track.Interpolate(result, _values, _inTans, _outTans, startFrameIndex, endFrameIndex, t);
            }

            static bool AllEqual(IList<float[]> values)
            {
                var first = values[0];

                for (var i = 1; i < values.Count; i++)
                {
                    var value = values[i];
                    if (value.Length != first.Length)
                    {
                        return false;
                    }

                    for (var j = 0; j < first.Length; j++)
                    {
                        if (first[j] != value[j])
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
        }
    }

    /// <summary>A single-float track (uint and float MDX animations).</summary>
    public sealed class ScalarTrack : SampledTrack
    {
        public override void Copy(float[] result, float[] value)
        {
            result[0] = value[0];
        }

        public override void Interpolate(float[] result, IList<float[]> values, IList<float[]> inTans, IList<float[]> outTans, int start, int end, float t)
        {
            var startValue = values[start][0];

            switch ((InterpolationKind)InterpolationType)
            {
                case InterpolationKind.DontInterp:
                    result[0] = startValue;
                    break;
                case InterpolationKind.Linear:
                    result[0] = startValue + t * (values[end][0] - startValue);
                    break;
                case InterpolationKind.Hermite:
                    result[0] = Hermite(startValue, outTans[start][0], inTans[end][0], values[end][0], t);
                    break;
                case InterpolationKind.Bezier:
                    result[0] = Bezier(startValue, outTans[start][0], inTans[end][0], values[end][0], t);
                    break;
                default:
                    result[0] = startValue;
                    break;
            }
        }
    }

    /// <summary>A 3-float vector track.</summary>
    public sealed class Vector3Track : SampledTrack
    {
        public override void Copy(float[] result, float[] value)
        {
            result[0] = value[0];
            result[1] = value[1];
            result[2] = value[2];
        }

        public override void Interpolate(float[] result, IList<float[]> values, IList<float[]> inTans, IList<float[]> outTans, int start, int end, float t)
        {
            var startValue = values[start];
            var endValue = values[end];

            switch ((InterpolationKind)InterpolationType)
            {
                case InterpolationKind.DontInterp:
                    Copy(result, startValue);
                    break;
                case InterpolationKind.Linear:
                    result[0] = startValue[0] + t * (endValue[0] - startValue[0]);
                    result[1] = startValue[1] + t * (endValue[1] - startValue[1]);
                    result[2] = startValue[2] + t * (endValue[2] - startValue[2]);
                    break;
                case InterpolationKind.Hermite:
                case InterpolationKind.Bezier:
                    var s0 = outTans[start];
                    var e0 = inTans[end];
                    for (var i = 0; i < 3; i++)
                    {
                        result[i] = InterpolationType == (int)InterpolationKind.Hermite
                            ? Hermite(startValue[i], s0[i], e0[i], endValue[i], t)
                            : Bezier(startValue[i], s0[i], e0[i], endValue[i], t);
                    }
                    break;
                default:
                    Copy(result, startValue);
                    break;
            }
        }
    }

    /// <summary>A 4-float quaternion track (rotation, (x, y, z, w)).</summary>
    public sealed class Vector4Track : SampledTrack
    {
        public override void Copy(float[] result, float[] value)
        {
            result[0] = value[0];
            result[1] = value[1];
            result[2] = value[2];
            result[3] = value[3];
        }

        public override void Interpolate(float[] result, IList<float[]> values, IList<float[]> inTans, IList<float[]> outTans, int start, int end, float t)
        {
            switch ((InterpolationKind)InterpolationType)
            {
                case InterpolationKind.DontInterp:
                    Copy(result, values[start]);
                    break;
                case InterpolationKind.Linear:
                    Slerp(result, values[start], values[end], t);
                    break;
                case InterpolationKind.Hermite:
                case InterpolationKind.Bezier:
                    Sqlerp(result, values[start], outTans[start], inTans[end], values[end], t);
                    break;
                default:
                    Copy(result, values[start]);
                    break;
            }
        }
    }
}
