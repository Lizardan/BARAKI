using System.Collections.Generic;
using System.Linq;

namespace Game.Mdx
{
    /// <summary>
    /// The parent class for all objects that have animated data in them.
    /// Ported from <c>mdlx/animatedobject.ts</c>.
    /// </summary>
    public class AnimatedObject
    {
        public readonly List<Animation> Animations = new();

        public void ReadAnimations(BinaryStream stream, int size)
        {
            var end = stream.Index + size;

            while (stream.Index < end)
            {
                var name = stream.ReadBinary(4);
                var animation = AnimationMap.Create(name);

                animation.ReadMdx(stream, name);

                Animations.Add(animation);
            }
        }

        public Animation FindAnimation(string name)
        {
            return Animations.FirstOrDefault(a => a.Name == name);
        }
    }
}
