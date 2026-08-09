using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// TT_RTS units tint player color by swapping _BaseMap to a per-slot texture variant.
    /// All meshes share one atlas whose blue pixels are recolorable via TT_RTS_Units_{color}.tga.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TtUnitTeamColor : MonoBehaviour
    {
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        [SerializeField] private Texture2D[] _teamTextures;

        public Texture2D[] TeamTextures
        {
            get => _teamTextures;
            set => _teamTextures = value;
        }

        public void ApplyTeamColor(Color slotColor)
        {
            var index = MatchPlayerColors.NearestSlotIndex(slotColor);
            if (index < 0 || _teamTextures == null || index >= _teamTextures.Length)
            {
                return;
            }

            var texture = _teamTextures[index];
            if (texture == null)
            {
                return;
            }

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetTexture(BaseMapId, texture);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
