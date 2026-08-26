namespace Game.Gameplay.Combat
{
    /// <summary>Stable per-ability identifiers shared by defs, kits, the catalog and the snapshot codec.</summary>
    public static class AbilityIds
    {
        public const int CasterHeal = 1;
        public const int Frost = 2;
        public const int Resurrect = 3;

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
