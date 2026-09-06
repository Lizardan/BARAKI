using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Faceless units tint player color via the ReviewUnlit shader's _TeamColor property.
    /// Team-colored geosets are baked with _UseTeam=1 and an albedo alpha mask; the shader
    /// blends toward _TeamColor by (1 - albedo.a). Applied per-instance through
    /// MaterialPropertyBlock, so player colors never duplicate materials.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FacelessUnitTeamColor : MonoBehaviour
    {
        static readonly int TeamColorId = Shader.PropertyToID("_TeamColor");

        public void ApplyTeamColor(Color slotColor)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor(TeamColorId, slotColor);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}