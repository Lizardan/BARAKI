using Game.Core;

using Game.Gameplay.Data;

using Game.Gameplay.Match;



namespace Game.UI

{

    public static class MatchInspectorFormatting

    {

        public static string FormatBuildingName(string buildingId) => buildingId switch

        {

            GameIds.Buildings.Main => "Главное",

            GameIds.Buildings.BarracksLeft

                or GameIds.Buildings.BarracksCenter

                or GameIds.Buildings.BarracksRight => "Казармы",

            GameIds.Buildings.TowerNw

                or GameIds.Buildings.TowerNe

                or GameIds.Buildings.TowerSw

                or GameIds.Buildings.TowerSe => "Башня",

            _ => buildingId,

        };



        public static string FormatOwnerLabel(int ownerSlot) => $"Игрок {ownerSlot + 1}";



        public static string FormatHp(float current, float max)

        {

            var safeCurrent = current < 0f ? 0f : current;

            var safeMax = max <= 0f ? 1f : max;

            return $"{safeCurrent:0}/{safeMax:0}";

        }



        public static string FormatRole(UnitRole role) => role switch

        {

            UnitRole.Melee => "Ближний",

            UnitRole.Ranged => "Дальний",

            UnitRole.Caster => "Маг",

            UnitRole.Siege => "Осадный",

            UnitRole.Flying => "Летающий",

            UnitRole.Super => "Супер",

            _ => role.ToString(),

        };



        public static string FormatStatValue(float value) => value.ToString("0.#");



        public static string FormatDamageRange(float min, float max) => $"{min:0.#}–{max:0.#}";



        public static string FormatReadonlyHint() => "Только просмотр";



        public static string FormatNoActiveResearch() => "Нет активного исследования";



        public static string FormatResearchProgress(float progress01) =>

            $"Исследование: {progress01 * 100f:0}%";



        public static bool IsMainBuilding(string buildingId) =>

            buildingId == GameIds.Buildings.Main;



        public static bool IsBarracksBuilding(string buildingId) =>

            buildingId is GameIds.Buildings.BarracksLeft

                or GameIds.Buildings.BarracksCenter

                or GameIds.Buildings.BarracksRight;



        public static bool IsTowerBuilding(string buildingId) =>

            buildingId is GameIds.Buildings.TowerNw

                or GameIds.Buildings.TowerNe

                or GameIds.Buildings.TowerSw

                or GameIds.Buildings.TowerSe;



        /// <summary>Research progress UI is shown only for main and towers.</summary>

        public static bool IsResearchBuilding(string buildingId) =>

            IsMainBuilding(buildingId) || IsTowerBuilding(buildingId);

    }

}

