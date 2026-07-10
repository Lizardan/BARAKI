namespace Game.Core
{
    /// <summary>
    /// Stable string identifiers — mirror <c>Assets/Game/GameDesign/</c>.
    /// Do not rename without updating GDD and ScriptableObject assets.
    /// </summary>
    public static class GameIds
    {
        public static class Resources
        {
            public const string Gold = "RES_GOLD";
        }

        public static class Races
        {
            public const string Human = "RACE_HUMAN";
            public const string Bug = "RACE_BUG";
            public const string Slot3 = "RACE_SLOT_3";
            public const string Slot4 = "RACE_SLOT_4";
        }

        public static class Topology
        {
            public const string Duel = "TOPOLOGY_DUEL";
            public const string Ring = "TOPOLOGY_RING";
        }

        public static class Lanes
        {
            public const string Left = "LANE_LEFT";
            public const string Center = "LANE_CENTER";
            public const string Right = "LANE_RIGHT";
        }

        public static class Buildings
        {
            public const string Main = "BUILDING_MAIN";
            public const string Barracks = "BUILDING_BARRACKS";
            public const string BarracksLeft = "BUILDING_BARRACKS_LEFT";
            public const string BarracksCenter = "BUILDING_BARRACKS_CENTER";
            public const string BarracksRight = "BUILDING_BARRACKS_RIGHT";
            public const string Tower = "BUILDING_TOWER";
            public const string TowerNw = "BUILDING_TOWER_NW";
            public const string TowerNe = "BUILDING_TOWER_NE";
            public const string TowerSw = "BUILDING_TOWER_SW";
            public const string TowerSe = "BUILDING_TOWER_SE";

            public const string SetHuman = "BUILDING_SET_HUMAN";
            public const string SetBug = "BUILDING_SET_BUG";
        }

        public static class UnitTypes
        {
            public const string Melee = "UNIT_TYPE_MELEE";
            public const string Ranged = "UNIT_TYPE_RANGED";
            public const string Caster = "UNIT_TYPE_CASTER";
            public const string Siege = "UNIT_TYPE_SIEGE";
            public const string Flying = "UNIT_TYPE_FLYING";
            public const string Super = "UNIT_TYPE_SUPER";
        }

        public static class Units
        {
            public const string HumanMelee = "UNIT_HUMAN_MELEE";
            public const string HumanRanged = "UNIT_HUMAN_RANGED";
            public const string HumanCaster = "UNIT_HUMAN_CASTER";
            public const string HumanSiege = "UNIT_HUMAN_SIEGE";
            public const string HumanFlying = "UNIT_HUMAN_FLYING";
            public const string HumanSuper = "UNIT_HUMAN_SUPER";

            public const string BugMelee = "UNIT_BUG_MELEE";
            public const string BugRanged = "UNIT_BUG_RANGED";
            public const string BugCaster = "UNIT_BUG_CASTER";
            public const string BugSiege = "UNIT_BUG_SIEGE";
            public const string BugFlying = "UNIT_BUG_FLYING";
            public const string BugSuper = "UNIT_BUG_SUPER";
        }

        public static class Squads
        {
            public const string BarracksL1 = "SQUAD_BARRACKS_L1";
            public const string BarracksL2 = "SQUAD_BARRACKS_L2";
            public const string BarracksL3 = "SQUAD_BARRACKS_L3";
            public const string BarracksL4 = "SQUAD_BARRACKS_L4";
        }

        public static class Heroes
        {
            public const string Human1 = "HERO_HUMAN_1";
            public const string Human2 = "HERO_HUMAN_2";
            public const string Human3 = "HERO_HUMAN_3";
            public const string Bug1 = "HERO_BUG_1";
            public const string Bug2 = "HERO_BUG_2";
            public const string Bug3 = "HERO_BUG_3";
        }

        public static class Passives
        {
            public const string HumanSteelArms = "PASSIVE_HUMAN_STEEL_ARMS";
            public const string HumanFortifiedLine = "PASSIVE_HUMAN_FORTIFIED_LINE";
            public const string HumanLevyTax = "PASSIVE_HUMAN_LEVY_TAX";

            public const string BugFrenzy = "PASSIVE_BUG_FRENZY";
            public const string BugBroodSurge = "PASSIVE_BUG_BROOD_SURGE";
            public const string BugGlassChitin = "PASSIVE_BUG_GLASS_CHITIN";
        }

        public static class Spells
        {
            public const string HumanHeal = "SPELL_HUMAN_1";
            public const string HumanFrost = "SPELL_HUMAN_2";
            public const string HumanResurrect = "SPELL_HUMAN_3";

            public const string BugInfect = "SPELL_BUG_1";
            public const string BugEgg = "SPELL_BUG_2";
            public const string BugMutate = "SPELL_BUG_3";
        }

        public static class Upgrades
        {
            public const string MainBuildingLevel = "UPG_MAIN_BUILDING_LEVEL";
            public const string MainPassiveGold = "UPG_MAIN_PASSIVE_GOLD";
            public const string MainMagic = "UPG_MAIN_MAGIC";
            public const string BarracksLevel = "UPG_BARRACKS_LEVEL";

            public const string MeleeDamage = "UPG_MELEE_DMG";
            public const string RangedDamage = "UPG_RANGED_DMG";
            public const string Armor = "UPG_ARMOR";
            public const string CasterHeal = "UPG_CASTER_HEAL";

            public const string TowerHumanSteelTemper = "UPG_TOWER_HUMAN_STEEL_TEMPER";
            public const string TowerHumanHoldTheLine = "UPG_TOWER_HUMAN_HOLD_THE_LINE";
            public const string TowerHumanBallistaOverdraw = "UPG_TOWER_HUMAN_BALLISTA_OVERDRAW";
            public const string TowerHumanArcaneRelay = "UPG_TOWER_HUMAN_ARCANE_RELAY";
            public const string TowerHumanLastStand = "UPG_TOWER_HUMAN_LAST_STAND";

            public const string TowerBugAdrenalGland = "UPG_TOWER_BUG_ADRENAL_GLAND";
            public const string TowerBugCarapaceWeave = "UPG_TOWER_BUG_CARAPACE_WEAVE";
            public const string TowerBugNeurotoxin = "UPG_TOWER_BUG_NEUROTOXIN";
            public const string TowerBugHatcheryPulse = "UPG_TOWER_BUG_HATCHERY_PULSE";
            public const string TowerBugAcidSac = "UPG_TOWER_BUG_ACID_SAC";
        }

        public static class Match
        {
            public const string Ffa = "MATCH_FFA";
            public const string PhaseLobby = "PHASE_LOBBY";
            public const string PhaseStart = "PHASE_START";
            public const string PhaseEarly = "PHASE_EARLY";
            public const string PhaseMid = "PHASE_MID";
            public const string PhaseLate = "PHASE_LATE";
            public const string PhaseEnd = "PHASE_END";
            public const string WinLastStanding = "WIN_LAST_STANDING";
            public const string PlayerElimination = "PLAYER_ELIMINATION";
        }

        public static class Economy
        {
            public const string Start = "ECON_START";
            public const string IncomeKill = "INCOME_KILL";
            public const string IncomeMainPassive = "INCOME_MAIN_PASSIVE";
        }
    }
}
