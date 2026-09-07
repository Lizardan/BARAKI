using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class AbilityAnimRulesTests
    {
        [Test]
        public void ResolveKind_StrikeAndSlam_AreAttack()
        {
            Assert.AreEqual(AbilityAnimKind.Attack, AbilityAnimRules.ResolveKind(AbilityIds.Strike));
            Assert.AreEqual(AbilityAnimKind.Attack, AbilityAnimRules.ResolveKind(AbilityIds.Slam));
            Assert.AreEqual(AbilityAnimKind.Attack, AbilityAnimRules.ResolveKind(AbilityIds.Smite));
            Assert.AreEqual(AbilityAnimKind.Attack, AbilityAnimRules.ResolveKind(AbilityIds.Ultimate));
            Assert.AreEqual(AbilityAnimKind.Attack, AbilityAnimRules.ResolveKind(AbilityIds.Stomp));
            Assert.AreEqual(AbilityAnimKind.Attack, AbilityAnimRules.ResolveKind(AbilityIds.Consecration));
            Assert.AreEqual(AbilityAnimKind.Attack, AbilityAnimRules.ResolveKind(AbilityIds.MeleeCleave));
            Assert.AreEqual(AbilityAnimKind.Attack, AbilityAnimRules.ResolveKind(AbilityIds.RangedCrit));
            Assert.AreEqual(AbilityAnimKind.Attack, AbilityAnimRules.ResolveKind(AbilityIds.CasterHybrid));
            Assert.AreEqual(AbilityAnimKind.Attack, AbilityAnimRules.ResolveKind(AbilityIds.SuperCatapult));
        }

        [Test]
        public void ResolveKind_HealsAndShouts_AreCast()
        {
            Assert.AreEqual(AbilityAnimKind.Cast, AbilityAnimRules.ResolveKind(AbilityIds.CasterHeal));
            Assert.AreEqual(AbilityAnimKind.Cast, AbilityAnimRules.ResolveKind(AbilityIds.Heal));
            Assert.AreEqual(AbilityAnimKind.Cast, AbilityAnimRules.ResolveKind(AbilityIds.Rally));
            Assert.AreEqual(AbilityAnimKind.Cast, AbilityAnimRules.ResolveKind(AbilityIds.Shield));
            Assert.AreEqual(AbilityAnimKind.Cast, AbilityAnimRules.ResolveKind(AbilityIds.Frost));
            Assert.AreEqual(AbilityAnimKind.Cast, AbilityAnimRules.ResolveKind(AbilityIds.HolyNova));
        }

        [Test]
        public void ResolveKind_PassivesAndLastCall_AreNone()
        {
            Assert.AreEqual(AbilityAnimKind.None, AbilityAnimRules.ResolveKind(AbilityIds.AuraDamagePercent));
            Assert.AreEqual(AbilityAnimKind.None, AbilityAnimRules.ResolveKind(AbilityIds.AuraAttackSpeedPercent));
            Assert.AreEqual(AbilityAnimKind.None, AbilityAnimRules.ResolveKind(AbilityIds.FlyingSpawn));
        }

        [Test]
        public void ResolveAttackClipSeconds_InfantryPoolIsLongerThanBallista()
        {
            Assert.AreEqual(
                AbilityAnimRules.InfantryAttackClipSeconds,
                AbilityAnimRules.ResolveAttackClipSeconds(UnitRole.Melee));
            Assert.AreEqual(
                AbilityAnimRules.InfantryAttackClipSeconds,
                AbilityAnimRules.ResolveAttackClipSeconds(UnitRole.Caster));
            Assert.AreEqual(
                AbilityAnimRules.InfantryAttackClipSeconds,
                AbilityAnimRules.ResolveAttackClipSeconds(UnitRole.Hero, HeroAbilityRules.KingSlot));
            Assert.AreEqual(
                AbilityAnimRules.CavalryOrMachineAttackClipSeconds,
                AbilityAnimRules.ResolveAttackClipSeconds(UnitRole.Super));
            Assert.AreEqual(
                AbilityAnimRules.CavalryOrMachineAttackClipSeconds,
                AbilityAnimRules.ResolveAttackClipSeconds(UnitRole.Hero, HeroAbilityRules.PaladinSlot));
            Assert.AreEqual(
                AbilityAnimRules.CavalryOrMachineAttackClipSeconds,
                AbilityAnimRules.ResolveAttackClipSeconds(UnitRole.Siege));
            Assert.AreEqual(
                AbilityAnimRules.InfantryAttackClipSeconds,
                AbilityAnimRules.ResolveAttackClipSeconds(
                    UnitRole.Siege,
                    bonusSlot: HumanBonusUnitRules.BonusSlotForRole(UnitRole.Siege)));
        }

        [Test]
        public void ResolveAttackClipSeconds_FacelessUsesBakedClipLengths()
        {
            const string faceless = Game.Core.GameIds.Races.Faceless;
            Assert.AreEqual(0.99f, AbilityAnimRules.ResolveAttackClipSeconds(UnitRole.Melee, raceId: faceless), 0.001f);
            Assert.AreEqual(1.49f, AbilityAnimRules.ResolveAttackClipSeconds(UnitRole.Ranged, raceId: faceless), 0.001f);
            Assert.AreEqual(1.06f, AbilityAnimRules.ResolveAttackClipSeconds(UnitRole.Caster, raceId: faceless), 0.001f);
            Assert.AreEqual(0.96f, AbilityAnimRules.ResolveAttackClipSeconds(UnitRole.Siege, raceId: faceless), 0.001f);
            Assert.AreEqual(0.99f, AbilityAnimRules.ResolveAttackClipSeconds(UnitRole.Flying, raceId: faceless), 0.001f);
            Assert.AreEqual(0.99f, AbilityAnimRules.ResolveAttackClipSeconds(UnitRole.Super, raceId: faceless), 0.001f);
            Assert.AreEqual(0.96f, AbilityAnimRules.ResolveAttackClipSeconds(UnitRole.Hero, heroSlot: 1, raceId: faceless), 0.001f);
            Assert.AreEqual(1.06f, AbilityAnimRules.ResolveAttackClipSeconds(UnitRole.Hero, heroSlot: 2, raceId: faceless), 0.001f);
            Assert.AreEqual(1.16f, AbilityAnimRules.ResolveAttackClipSeconds(UnitRole.Hero, heroSlot: 3, raceId: faceless), 0.001f);
            Assert.AreEqual(1.32f, AbilityAnimRules.ResolveAttackClipSeconds(UnitRole.Titan, raceId: faceless), 0.001f);
        }

        [Test]
        public void ResolveAttackPlaybackSpeed_BallistaStretchesOneSecondClipOverTwoSecondInterval()
        {
            const float interval = 2f; // Super AS 0.5
            const float clip = AbilityAnimRules.CavalryOrMachineAttackClipSeconds;
            Assert.AreEqual(
                0.5f,
                Game.Gameplay.Match.UnitCombatAnimatorDriver.ResolveAttackPlaybackSpeed(interval, clip),
                0.001f);
            Assert.AreEqual(1f, CombatAttackRules.ResolveSwingImpactDelay(interval), 0.001f);
        }

        [Test]
        public void ResolveAttackPlaybackSpeed_MeleeFitsOnePointFiveClipIntoOneSecondInterval()
        {
            const float interval = 1f;
            const float clip = AbilityAnimRules.InfantryAttackClipSeconds;
            Assert.AreEqual(
                1.5f,
                Game.Gameplay.Match.UnitCombatAnimatorDriver.ResolveAttackPlaybackSpeed(interval, clip),
                0.001f);
            Assert.AreEqual(0.5f, CombatAttackRules.ResolveSwingImpactDelay(interval), 0.001f);
        }

        [Test]
        public void ResolveAnim_Unspecified_UsesDefault_AuthoredWins()
        {
            Assert.AreEqual(
                AbilityAnimKind.Attack,
                AbilityAnimRules.ResolveAnim(AbilityIds.Strike, AbilityAnimKind.Unspecified));
            Assert.AreEqual(
                AbilityAnimKind.Cast,
                AbilityAnimRules.ResolveAnim(AbilityIds.Strike, AbilityAnimKind.Cast));
            Assert.AreEqual(
                AbilityAnimKind.None,
                AbilityAnimRules.ResolveAnim(AbilityIds.Heal, AbilityAnimKind.None));
        }

        [Test]
        public void ResolveAnim_StateWinsOverKindAndDefault()
        {
            Assert.AreEqual(
                AbilityAnimKind.Attack,
                AbilityAnimRules.ResolveAnim(AbilityIds.Heal, AbilityAnimKind.Cast, "Attack"));
            Assert.AreEqual(
                AbilityAnimKind.Cast,
                AbilityAnimRules.ResolveAnim(AbilityIds.Strike, AbilityAnimKind.Attack, "Cast"));
            Assert.AreEqual(
                AbilityAnimKind.None,
                AbilityAnimRules.ResolveAnim(AbilityIds.Strike, AbilityAnimKind.Attack, "Stand"));
            Assert.AreEqual(
                AbilityAnimKind.Attack,
                AbilityAnimRules.ResolveAnim(AbilityIds.Strike, AbilityAnimKind.Unspecified, null));
        }

        [Test]
        public void ResolveKindFromState_EmptyIsUnspecified()
        {
            Assert.AreEqual(AbilityAnimKind.Unspecified, AbilityAnimRules.ResolveKindFromState(null));
            Assert.AreEqual(AbilityAnimKind.Unspecified, AbilityAnimRules.ResolveKindFromState(""));
            Assert.AreEqual(AbilityAnimKind.Attack, AbilityAnimRules.ResolveKindFromState("Attack"));
            Assert.AreEqual(AbilityAnimKind.Cast, AbilityAnimRules.ResolveKindFromState("Cast"));
            Assert.AreEqual(AbilityAnimKind.None, AbilityAnimRules.ResolveKindFromState("Death"));
        }

        [Test]
        public void ResolveLockSeconds_CastUsesClipLength_AttackUsesInterval()
        {
            Assert.AreEqual(
                AbilityAnimRules.StaffCastClipSeconds,
                AbilityAnimRules.ResolveLockSeconds(AbilityAnimKind.Cast, 0.5f, AbilityIds.Heal),
                0.001f);
            Assert.AreEqual(
                AbilityAnimRules.PunchCastClipSeconds,
                AbilityAnimRules.ResolveLockSeconds(AbilityAnimKind.Cast, 0.5f, AbilityIds.Rally),
                0.001f);
            Assert.AreEqual(
                0.8f,
                AbilityAnimRules.ResolveLockSeconds(AbilityAnimKind.Attack, 0.8f, AbilityIds.Strike),
                0.001f);
            Assert.AreEqual(0f, AbilityAnimRules.ResolveLockSeconds(AbilityAnimKind.None, 1f));
        }
    }
}
