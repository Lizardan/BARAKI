using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Authored VFX for Divine Blessing smites (ids 100/101) and main building
    /// abilities (MAIN-001, ids 102/103). Loaded from Resources so the
    /// runtime FX defs pick up viewer changes without a scene reference.
    /// </summary>
    [CreateAssetMenu(fileName = "MainExtraAbilityFxCatalog", menuName = "BARAKI/Main Extra Ability FX Catalog")]
    public sealed class MainExtraAbilityFxCatalog : ScriptableObject
    {
        public const string ResourcesPath = "Fx/MainExtraAbilityFxCatalog";
        public const string AssetPath = "Assets/Game/Resources/Fx/MainExtraAbilityFxCatalog.asset";

        [SerializeField] private AbilityFx _buildingSmite;
        [SerializeField] private AbilityFx _unitSmite;
        [SerializeField] private AbilityFx _iceRing;
        [SerializeField] private AbilityFx _waveOfLight;

        public AbilityFx BuildingSmite => WithFallback(_buildingSmite, AbilityFxColors.DivineSmite);
        public AbilityFx UnitSmite => WithFallback(_unitSmite, AbilityFxColors.DivineSmite);
        public AbilityFx IceRing => WithFallback(_iceRing, AbilityFxColors.Frost);
        public AbilityFx WaveOfLight => WithFallback(_waveOfLight, AbilityFxColors.Priest);

        public AbilityFx GetFx(int abilityId) => abilityId switch
        {
            AbilityIds.MainBuildingSmite => BuildingSmite,
            AbilityIds.MainUnitSmite => UnitSmite,
            AbilityIds.MainIceRing => IceRing,
            AbilityIds.MainWaveOfLight => WaveOfLight,
            _ => default,
        };

        public void EditorSetFx(int abilityId, AbilityFx fx)
        {
            switch (abilityId)
            {
                case AbilityIds.MainBuildingSmite:
                    _buildingSmite = fx;
                    break;
                case AbilityIds.MainUnitSmite:
                    _unitSmite = fx;
                    break;
                case AbilityIds.MainIceRing:
                    _iceRing = fx;
                    break;
                case AbilityIds.MainWaveOfLight:
                    _waveOfLight = fx;
                    break;
            }
        }

        public static MainExtraAbilityFxCatalog Load() =>
            Resources.Load<MainExtraAbilityFxCatalog>(ResourcesPath);

        static AbilityFx WithFallback(AbilityFx fx, Color fallbackColor)
        {
            if (fx.Color.a <= 0.01f)
            {
                fx.Color = fallbackColor;
            }

            return fx;
        }
    }
}
