using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Mdx
{
    /// <summary>
    /// A map from MDX animation tags to their implementation classes.
    /// Ported from <c>mdlx/animationmap.ts</c>.
    /// </summary>
    public static class AnimationMap
    {
        public static Animation Create(string tag)
        {
            switch (tag)
            {
                // Uint
                case "KMTF": // TextureID
                case "KFTC": // FresnelTeamColor
                case "KRTX": // TextureSlot
                    return new UintAnimation();

                // Float
                case "KMTA": // Alpha
                case "KMTE": // EmissiveGain
                case "KFCA": // FresnelOpacity
                case "KGAO": // Alpha
                case "KLAS": // AttenuationStart
                case "KLAE": // AttenuationEnd
                case "KLAI": // Intensity
                case "KLBI": // AmbIntensity
                case "KLAV": // Visibility
                case "KATV": // Visibility
                case "KPEE": // EmissionRate
                case "KPEG": // Gravity
                case "KPLN": // Longitude
                case "KPLT": // Latitude
                case "KPEL": // LifeSpan
                case "KPES": // InitVelocity
                case "KPEV": // Visibility
                case "KP2S": // Speed
                case "KP2R": // Variation
                case "KP2L": // Latitude
                case "KP2G": // Gravity
                case "KP2E": // EmissionRate
                case "KP2N": // Width
                case "KP2W": // Length
                case "KP2V": // Visibility
                case "KPPA": // Alpha
                case "KPPE": // EmissionRate
                case "KPPL": // LifeSpan
                case "KPPS": // Speed
                case "KPPV": // Visibility
                case "KRHA": // HeightAbove
                case "KRHB": // HeightBelow
                case "KRAL": // Alpha
                case "KRVS": // Visibility
                case "KCRL": // Rotation
                    return new FloatAnimation();

                // Vector 3
                case "KFC3": // FresnelColor
                case "KTAT": // Translation
                case "KTAS": // Scaling
                case "KGTR": // Translation
                case "KGSC": // Scaling
                case "KLAC": // Color
                case "KLBC": // AmbColor
                case "KGAC": // Color
                case "KRCO": // Color
                case "KCTR": // Translation
                case "KTTR": // Translation
                case "KPPC": // Color
                    return new Vector3Animation();

                // Vector 4
                case "KTAR": // Rotation
                case "KGRT": // Rotation
                    return new Vector4Animation();

                default:
                    throw new ArgumentException("Unknown animation tag: " + tag);
            }
        }
    }
}
