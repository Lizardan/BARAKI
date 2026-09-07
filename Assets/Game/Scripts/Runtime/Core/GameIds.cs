namespace Game.Core
{
    /// <summary>
    /// Stable string identifiers — mirror <c>GameDesign/</c>.
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
            public const string Faceless = "RACE_FACELESS";
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
            public const string SetFaceless = "BUILDING_SET_FACELESS";
        }

        public static class Units
        {
            public const string HumanMelee = "UNIT_HUMAN_MELEE";
            public const string HumanRanged = "UNIT_HUMAN_RANGED";
            public const string HumanCaster = "UNIT_HUMAN_CASTER";
            public const string HumanSiege = "UNIT_HUMAN_SIEGE";
            public const string HumanFlying = "UNIT_HUMAN_FLYING";
            public const string HumanSuper = "UNIT_HUMAN_SUPER";
            public const string HumanMeleeBonus = "UNIT_HUMAN_MELEE_BONUS";
            public const string HumanRangedBonus = "UNIT_HUMAN_RANGED_BONUS";
            public const string HumanCasterBonus = "UNIT_HUMAN_CASTER_BONUS";
            public const string HumanSiegeBonus = "UNIT_HUMAN_SIEGE_BONUS";
            public const string HumanFlyingBonus = "UNIT_HUMAN_FLYING_BONUS";
            public const string HumanSuperBonus = "UNIT_HUMAN_SUPER_BONUS";

            public const string FacelessMelee = "UNIT_FACELESS_MELEE";
            public const string FacelessRanged = "UNIT_FACELESS_RANGED";
            public const string FacelessCaster = "UNIT_FACELESS_CASTER";
            public const string FacelessSiege = "UNIT_FACELESS_SIEGE";
            public const string FacelessFlying = "UNIT_FACELESS_FLYING";
            public const string FacelessSuper = "UNIT_FACELESS_SUPER";
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

            public const string Faceless1 = "HERO_FACELESS_1";
            public const string Faceless2 = "HERO_FACELESS_2";
            public const string Faceless3 = "HERO_FACELESS_3";
        }

        public static class Bonuses
        {
            public const string Melee = "BONUS_SLOT_MELEE";
            public const string Ranged = "BONUS_SLOT_RANGED";
            public const string Caster = "BONUS_SLOT_CASTER";
            public const string Siege = "BONUS_SLOT_SIEGE";
            public const string Flying = "BONUS_SLOT_FLYING";
            public const string Super = "BONUS_SLOT_SUPER";
            public const string Hero1 = "BONUS_SLOT_HERO_1";
            public const string Hero2 = "BONUS_SLOT_HERO_2";
            public const string Hero3 = "BONUS_SLOT_HERO_3";
            public const string Titan = "BONUS_SLOT_TITAN";
            public const string RaceUnique1 = "BONUS_SLOT_RACE_UNIQUE_1";
            public const string RaceUnique2 = "BONUS_SLOT_RACE_UNIQUE_2";
        }

        public static class Passives
        {
            public const string HumanSteelArms = "PASSIVE_HUMAN_STEEL_ARMS";
            public const string HumanFortifiedLine = "PASSIVE_HUMAN_FORTIFIED_LINE";
            public const string HumanLevyTax = "PASSIVE_HUMAN_LEVY_TAX";
        }

        public static class Spells
        {
            public const string HumanHeal = "SPELL_HUMAN_1";
            public const string HumanFrost = "SPELL_HUMAN_2";
            public const string HumanResurrect = "SPELL_HUMAN_3";
        }

        public static class Upgrades
        {
            public const string MainBuildingLevel = "UPG_MAIN_BUILDING_LEVEL";
            public const string MainPassiveGold = "UPG_MAIN_PASSIVE_GOLD";
            public const string MainMagic = "UPG_MAIN_MAGIC";
            public const string DivineBlessing = "UPG_MAIN_DIVINE_BLESSING";
            public const string BarracksLevel = "UPG_BARRACKS_LEVEL";
            /// <summary>Prefix for hero hire research; full id is <c>UPG_HERO_HIRE:{slot}</c>.</summary>
            public const string HeroHire = "UPG_HERO_HIRE";

            public const string MeleeDamage = "UPG_MELEE_DMG";
            public const string RangedDamage = "UPG_RANGED_DMG";
            public const string Armor = "UPG_ARMOR";

            public const string TowerHumanFlamingArrows = "UPG_TOWER_HUMAN_FLAMING_ARROWS";
            public const string TowerHumanBulwark = "UPG_TOWER_HUMAN_BULWARK";
            public const string TowerHumanBloodrage = "UPG_TOWER_HUMAN_BLOODRAGE";
            public const string TowerHumanBatteringRams = "UPG_TOWER_HUMAN_BATTERING_RAMS";
            public const string TowerHumanArcaneFocus = "UPG_TOWER_HUMAN_ARCANE_FOCUS";
            public const string TowerHumanSkirmishers = "UPG_TOWER_HUMAN_SKIRMISHERS";
            public const string TowerHumanForcedMarch = "UPG_TOWER_HUMAN_FORCED_MARCH";
            public const string TowerHumanFieldMedics = "UPG_TOWER_HUMAN_FIELD_MEDICS";
            public const string TowerHumanLastStand = "UPG_TOWER_HUMAN_LAST_STAND";
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
