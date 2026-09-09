namespace Game.Gameplay.Combat
{
    /// <summary>Stable per-ability identifiers shared by defs, kits, the catalog and the snapshot codec.</summary>
    public static class AbilityIds
    {
        public const int CasterHeal = 1;
        public const int Frost = 2;
        public const int Resurrect = 3;

        /// <summary>Faceless caster slot 1: single-target damage + dot (FACELESS-016).</summary>
        public const int BlightingGaze = 4;
        /// <summary>Faceless caster slot 2: ground AoE damage + lifesteal to the caster (FACELESS-016).</summary>
        public const int VoidDrain = 5;
        /// <summary>Faceless caster slot 3: raise ANY corpse as a controlled melee minion (FACELESS-016).</summary>
        public const int RaiseDrowned = 6;

        public const int Heal = 10;
        public const int Ultimate = 11;
        public const int Strike = 12;
        public const int AuraDamagePercent = 13;

        public const int Smite = 20;
        public const int Shield = 21;
        public const int Consecration = 22;
        public const int AuraAttackSpeedPercent = 23;

        public const int HolyNova = 30;
        public const int GreaterHeal = 31;
        public const int Revive = 32;
        public const int AuraArmorPercent = 33;

        public const int Rally = 40;
        public const int Stomp = 41;
        public const int Slam = 42;
        public const int AuraMaxHpPercent = 43;

        /// <summary>Siege bonus passive: +HP/s aura (PRE-006a).</summary>
        public const int AuraHpRegen = 50;
        /// <summary>Melee bonus: on-hit cleave AoE.</summary>
        public const int MeleeCleave = 51;
        /// <summary>Ranged bonus: on-hit crit ×2.</summary>
        public const int RangedCrit = 52;
        /// <summary>Caster bonus: hybrid mace melee under short range.</summary>
        public const int CasterHybrid = 53;
        /// <summary>Flying bonus: chance to spawn a base ranged on death.</summary>
        public const int FlyingSpawn = 54;
        /// <summary>Super bonus: parabolic catapult splash.</summary>
        public const int SuperCatapult = 55;

        /// <summary>King veteran (bonus slot 7): army-wide damage shout replacing Ultimate (PRE-006b).</summary>
        public const int KingsCommand = 56;
        /// <summary>Paladin veteran (bonus slot 8): armor + absorb shield replacing Shield.</summary>
        public const int Aegis = 57;
        /// <summary>Priest veteran (bonus slot 9): caster-following heal zone replacing Greater Heal.</summary>
        public const int Sanctuary = 58;
        /// <summary>Titan veteran (bonus slot 10): stronger MaxHp aura replacing Colossus.</summary>
        public const int GreaterColossus = 59;

        /// <summary>Faceless King veteran (bonus slot 7): self +50% dmg & +2 armor, AoE hit +30% (FACELESS-012).</summary>
        public const int AncientMantle = 60;
        /// <summary>Faceless Warlock veteran (bonus slot 8): enemies in radius miss all attacks (FACELESS-012).</summary>
        public const int AreaOfMiss = 61;
        /// <summary>Faceless Berserker veteran (bonus slot 9): following zone heals allies 30% of damage dealt (FACELESS-012).</summary>
        public const int FeastZone = 62;
        /// <summary>Faceless Titan veteran (bonus slot 10): army lifesteals 15% of damage dealt (FACELESS-012).</summary>
        public const int AuraOfHunger = 63;

        /// <summary>Faceless Melee bonus (slot 1): 15% on-hit heal for 50% of damage dealt (FACELESS-011).</summary>
        public const int FacelessHunger = 64;
        /// <summary>Faceless Ranged bonus (slot 2): 15% on-hit dot 3 dmg/s for 3 s (FACELESS-011).</summary>
        public const int FacelessTaint = 65;
        /// <summary>Faceless Caster bonus (slot 3): on kill spawn a ×0.5 mini-melee (FACELESS-011).</summary>
        public const int FacelessCallOfAbyss = 66;
        /// <summary>Faceless Siege bonus (slot 4): on death explode for 10% max HP in radius 3 (FACELESS-011).</summary>
        public const int FacelessDeathExplosion = 67;
        /// <summary>Faceless Flying bonus (slot 5): on kill +15% attack speed, stacks to 3 (FACELESS-011).</summary>
        public const int FacelessHungeringFlight = 68;
        /// <summary>Faceless Super bonus (slot 6): on kill +80 HP and +10% attack speed, stacks to 3 (FACELESS-011).</summary>
        public const int FacelessFeast = 69;

        /// <summary>Main extra: Кара зданий (Divine Blessing pick id 1).</summary>
        public const int MainBuildingSmite = 100;
        /// <summary>Main extra: Кара юнитов (Divine Blessing pick id 2).</summary>
        public const int MainUnitSmite = 101;
        /// <summary>Main building ability: Ледяное кольцо (MAIN-001, BuildingAbilityRules id 1).</summary>
        public const int MainIceRing = 102;
        /// <summary>Main building ability: Волна света (MAIN-001, BuildingAbilityRules id 2).</summary>
        public const int MainWaveOfLight = 103;
    }
}
