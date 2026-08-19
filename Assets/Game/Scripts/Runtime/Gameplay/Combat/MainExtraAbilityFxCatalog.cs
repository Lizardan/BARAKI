using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Authored VFX for Divine Blessing smites (ids 100/101). Loaded from Resources so the
    /// runtime FX defs pick up viewer changes without a scene reference.
    /// </summary>
    [CreateAssetMenu(fileName = "MainExtraAbilityFxCatalog", menuName = "BARAKI/Main Extra Ability FX Catalog")]
    public sealed class MainExtraAbilityFxCatalog : ScriptableObject
    {
        public const string ResourcesPath = "Fx/MainExtraAbilityFxCatalog";
        public const string AssetPath = "Assets/Game/Resources/Fx/MainExtraAbilityFxCatalog.asset";

        [SerializeField] private AbilityFx _buildingSmite;
        [SerializeField] private AbilityFx _unitSmite;

        public AbilityFx BuildingSmite => WithFallback(_buildingSmite);
        public AbilityFx UnitSmite => WithFallback(_unitSmite);

        public AbilityFx GetFx(int abilityId) => abilityId switch
        {
            AbilityIds.MainBuildingSmite => BuildingSmite,
            AbilityIds.MainUnitSmite => UnitSmite,
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
            }
        }

        public static MainExtraAbilityFxCatalog Load() =>
            Resources.Load<MainExtraAbilityFxCatalog>(ResourcesPath);

        static AbilityFx WithFallback(AbilityFx fx)
        {
            if (fx.Color.a <= 0.01f)
            {
                fx.Color = AbilityFxColors.DivineSmite;
            }

            return fx;
        }
    }
}
