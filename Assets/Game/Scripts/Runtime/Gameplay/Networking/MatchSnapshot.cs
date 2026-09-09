using System;
using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Compact authoritative match state for client render sync.
    /// Pure C# — no NGO dependency so Edit Mode tests can round-trip it.
    /// The wire format (v21+) is sectioned: unknown sections/event types are skipped
    /// by length, so adding data never breaks mixed peers mid-match.
    /// </summary>
    public sealed class MatchSnapshot
    {
        public int PlayerCount;
        public int Phase;
        public float MatchTimeSeconds;
        /// <summary>-1 when no winner yet.</summary>
        public int WinnerSlot = -1;
        /// <summary>Seconds left for the bonus pick overlay.</summary>
        public float BonusPickDeadlineSeconds;
        public MatchPlayerSnapshot[] Players = Array.Empty<MatchPlayerSnapshot>();
        public MatchBuildingSnapshot[] Buildings = Array.Empty<MatchBuildingSnapshot>();
        /// <summary>Merged view: alive units only, static + dynamic fields combined.</summary>
        public MatchUnitSnapshot[] Units = Array.Empty<MatchUnitSnapshot>();
        public MatchResearchSnapshot[] Research = Array.Empty<MatchResearchSnapshot>();
        public MatchBarracksSnapshot[] Barracks = Array.Empty<MatchBarracksSnapshot>();
        public MatchCenterLaneSnapshot[] CenterLanes = Array.Empty<MatchCenterLaneSnapshot>();
        /// <summary>Transient host cast events synced to clients for spell VFX.</summary>
        public MatchSpellSnapshot[] SpellCasts = Array.Empty<MatchSpellSnapshot>();
        /// <summary>
        /// One-shot projectile spawn events for client VFX. Not a continuous in-flight
        /// list — clients advance flight locally after apply.
        /// </summary>
        public MatchProjectileSnapshot[] Projectiles = Array.Empty<MatchProjectileSnapshot>();
        /// <summary>Per-slot hero roster.</summary>
        public MatchHeroSlotSnapshot[] Heroes = Array.Empty<MatchHeroSlotSnapshot>();
        /// <summary>Debug hash (0 = unset). Host fills via <see cref="MatchSnapshotChecksum"/>.</summary>
        public uint Checksum;
    }

    public struct MatchPlayerSnapshot
    {
        public int Slot;
        public int Gold;
        public bool IsEliminated;
        public int PassiveGoldLevel;
        public int MainLevel;
        public int MagicLevel;
        public int MeleeDamageLevel;
        public int RangedDamageLevel;
        public int HpArmorLevel;
        /// <summary>Tower upgrade track levels (PRE-007), index = <see cref="TowerTrackRules"/> order.</summary>
        public int[] TowerTrackLevels;
        /// <summary>Chosen bonus slot (1..12), 0 = none yet.</summary>
        public int BonusPickSlot;
        /// <summary>Chosen slot of the second (choice) window (1..12), 0 = not picked yet.</summary>
        public int BonusPickSlot2;
        /// <summary>Random subset offered in the second window (excludes the auto pick); empty until the offer is built.</summary>
        public int[] BonusPickOfferSlots;
        public float TitanResearchProgressSeconds;
        public bool TitanUnlocked;
        /// <summary><see cref="TitanLifecycleState"/> as int.</summary>
        public int TitanState;
        public int TitanLevel;
        public int TitanXp;
        /// <summary>Barracks instance id of last titan deploy. 0 = none.</summary>
        public int TitanLastBarracksInstanceId;
        /// <summary>Death cooldown remaining on that barracks.</summary>
        public float TitanDeathCooldownRemaining;
        public bool DivineBlessingComplete;
        /// <summary>Picked main extra ability id (1..6). 0 = none.</summary>
        public int MainExtraAbilityId;
        public float MainMana;
        public float MainExtraAbilityCooldownRemaining;
        /// <summary>Building ability cooldowns (MAIN-001), added in v23.</summary>
        public float IceRingCooldownRemaining;
        public float WaveOfLightCooldownRemaining;
        /// <summary>Confirmed hero order (permutation of 1..3), added in v25. Null/invalid = default.</summary>
        public int[] HeroOrder;
        public bool HeroOrderConfirmed;
    }

    public struct MatchHeroSlotSnapshot
    {
        public int OwnerSlot;
        public int HeroSlot;
        /// <summary><see cref="HeroLifecycleState"/> as int.</summary>
        public int State;
        public int Level;
        public int Xp;
        /// <summary>0 = none.</summary>
        public int LastDeployBarracksInstanceId;
        public float DeathCooldownRemaining;
    }

    public struct MatchBuildingSnapshot
    {
        public int InstanceId;
        public int OwnerSlot;
        public string BuildingId;
        public float Health;
        public bool IsRuins;
    }

    public struct MatchUnitSnapshot
    {
        public int UnitId;
        public int OwnerSlot;
        public string UnitDefId;
        public string LaneId;
        public float PosX;
        public float PosZ;
        public float FacingX;
        public float FacingZ;
        public float Health;
        public float Mana;
        /// <summary>Hero level (1 for non-heroes).</summary>
        public int Level;
        /// <summary>XP toward next level (heroes/titan only).</summary>
        public int Xp;
        /// <summary>Always true on the wire: dead units travel as removals instead.</summary>
        public bool IsAlive;
        /// <summary><see cref="Combat.UnitBehaviorState"/> as byte.</summary>
        public byte BehaviorState;
        /// <summary>Increments on each attack swing.</summary>
        public int AttackSwingSerial;
        /// <summary>Hero slot 1..3. 0 = non-heroes.</summary>
        public int HeroSlot;
        /// <summary>Parked idle champion at base.</summary>
        public bool IsParkedAtBase;
        /// <summary>Enhanced unit variant (1..6 from the bonus pick), 0 = base.</summary>
        public int BonusSlot;
        /// <summary>Passive aura disc radius (0 = none).</summary>
        public float AuraRadius;
        /// <summary>RGBA int aura color (see <see cref="AbilityFx.ToRgbaInt"/>).</summary>
        public int AuraColorPacked;
        /// <summary>Passive aura ability id (0 = none). Replaces color-based guessing.</summary>
        public int AuraAbilityId;
        /// <summary>Melee target unit id (0 = none). Client-side impact FX source.</summary>
        public int TargetUnitId;
        /// <summary>Melee target building instance id (-1 = none).</summary>
        public int TargetBuildingInstanceId;
        /// <summary>Super attack commit window (ammo hidden while true).</summary>
        public bool IsAttackCommitted;
    }

    public struct MatchResearchSnapshot
    {
        public int BuildingInstanceId;
        public int OwnerSlot;
        public string BuildingId;
        public string UpgradeId;
        public int CostPaid;
        public float DurationSeconds;
        public float RemainingSeconds;
    }

    public struct MatchBarracksSnapshot
    {
        public int OwnerSlot;
        public string BarracksId;
        public int Level;
        public bool IsRuins;
        public int FrozenSquadLevel;
        /// <summary>Per callable role (Melee..Super). Null/empty = uninitialized.</summary>
        public int[] CallCurrent;
        public int[] CallMax;
        public float[] CallNextRegen;
        public float TimeUntilNextWaveSeconds;
    }

    public struct MatchCenterLaneSnapshot
    {
        public int OwnerSlot;
        public int OpponentSlot;
    }

    public struct MatchSpellSnapshot
    {
        public int Serial;
        public int CasterUnitId;
        public int OwnerSlot;
        /// <summary>Id of the cast <see cref="Data.UnitAbilityDef"/> (resolved via the ability catalog).</summary>
        public ushort AbilityId;
        public int TargetUnitId;
        public float CenterX;
        public float CenterZ;
        public float Radius;
    }

    /// <summary>Client-render projectile spawn event. Damage resolution stays host-only.</summary>
    public struct MatchProjectileSnapshot
    {
        public int ProjectileId;
        public int AttackerOwnerSlot;
        public byte AttackerRole;
        public float StartX;
        public float StartY;
        public float StartZ;
        public float TargetX;
        public float TargetY;
        public float TargetZ;
        public float FlightDuration;
        public bool IsParabolic;
        /// <summary>-1 when not targeting a building.</summary>
        public int TargetBuildingInstanceId;
        /// <summary>-1 when not fired by a building.</summary>
        public int SourceBuildingInstanceId;
        public string SourceBuildingId;
        /// <summary>Bonus Super catapult splash.</summary>
        public bool AppliesSplashAoe;
    }

    /// <summary>Wire section identifiers (v21). Public: part of the wire contract.</summary>
    public static class MatchSnapshotSections
    {
        public const ushort StringTable = 1;
        public const ushort Players = 2;
        public const ushort Buildings = 3;
        public const ushort UnitsStatic = 4;
        public const ushort UnitsDynamic = 5;
        public const ushort RemovedUnits = 6;
        public const ushort Research = 7;
        public const ushort Barracks = 8;
        public const ushort CenterLanes = 9;
        public const ushort Heroes = 10;
        public const ushort Events = 11;
        public const ushort Checksum = 12;

        public const ushort EventAbilityCast = 1;
        public const ushort EventProjectileSpawn = 2;

        public const ushort NoStringIndex = ushort.MaxValue;
        public const int NoTargetUnitId = 0;
        public const int NoTargetBuildingInstanceId = -1;
    }

    public static class MatchSnapshotCodec
    {
        // v24: second bonus pick (auto slot + offered subset) in the Players section.
        public const int CurrentVersion = 25;

        /// <summary>Self-contained encode: full static roster, safe for any fresh decoder.</summary>
        public static byte[] Serialize(MatchSnapshot snapshot) =>
            new MatchSnapshotWireContext().Encode(snapshot);

        /// <summary>Self-contained decode: assumes the bytes carry a full static roster.</summary>
        public static MatchSnapshot Deserialize(byte[] bytes) =>
            new MatchSnapshotWireContext().Decode(bytes);

        public static MatchSnapshot Capture(MatchController controller)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            var players = new List<MatchPlayerSnapshot>();
            foreach (var p in controller.Players)
            {
                var titan = controller.GetTitanState(p.SlotIndex);
                players.Add(new MatchPlayerSnapshot
                {
                    Slot = p.SlotIndex,
                    Gold = p.Gold,
                    IsEliminated = p.IsEliminated,
                    PassiveGoldLevel = p.PassiveGoldLevel,
                    MainLevel = p.MainLevel,
                    MagicLevel = p.MagicLevel,
                    MeleeDamageLevel = p.MeleeDamageLevel,
                    RangedDamageLevel = p.RangedDamageLevel,
                    HpArmorLevel = p.HpArmorLevel,
                    BonusPickSlot = controller.GetBonusPickSlot(p.SlotIndex),
                    BonusPickSlot2 = controller.GetBonusPickSlot(p.SlotIndex, panel: 1),
                    BonusPickOfferSlots = controller.GetBonusPickOffer(p.SlotIndex),
                    TitanResearchProgressSeconds = titan?.ResearchProgressSeconds ?? 0f,
                    TitanUnlocked = titan?.IsUnlocked ?? false,
                    TitanState = titan == null ? 0 : (int)titan.State,
                    TitanLevel = titan?.Level ?? HeroLevelRules.StartingLevel,
                    TitanXp = titan?.Xp ?? 0,
                    TitanLastBarracksInstanceId = titan?.LastSummonBarracksInstanceId ?? 0,
                    TitanDeathCooldownRemaining = titan?.GetLastBarracksDeathCooldown() ?? 0f,
                    DivineBlessingComplete = p.DivineBlessingComplete,
                    MainExtraAbilityId = p.MainExtraAbilityId,
                    MainMana = p.MainMana,
                    MainExtraAbilityCooldownRemaining = p.MainExtraAbilityCooldownRemaining,
                    IceRingCooldownRemaining = p.IceRingCooldownRemaining,
                    WaveOfLightCooldownRemaining = p.WaveOfLightCooldownRemaining,
                    TowerTrackLevels = (int[])p.TowerTrackLevels.Clone(),
                    HeroOrder = (int[])p.HeroOrder.Clone(),
                    HeroOrderConfirmed = p.HeroOrderConfirmed,
                });
            }

            var buildings = new List<MatchBuildingSnapshot>();
            foreach (var b in controller.Buildings.Buildings)
            {
                buildings.Add(new MatchBuildingSnapshot
                {
                    InstanceId = b.InstanceId,
                    OwnerSlot = b.OwnerSlot,
                    BuildingId = b.BuildingId,
                    Health = b.CurrentHp,
                    IsRuins = b.IsRuins,
                });
            }

            var units = new List<MatchUnitSnapshot>();
            foreach (var u in controller.Combat.Units)
            {
                if (!u.IsAlive)
                {
                    continue;
                }

                var level = u.Level;
                var xp = 0;
                if (u.IsHero && u.HeroSlot >= 1)
                {
                    var hero = controller.GetHeroRoster(u.OwnerSlot)?.Get(u.HeroSlot);
                    if (hero != null)
                    {
                        level = hero.Level;
                        xp = hero.Xp;
                    }
                }
                else if (u.Role == UnitRole.Titan)
                {
                    var titan = controller.GetTitanState(u.OwnerSlot);
                    if (titan != null)
                    {
                        level = titan.Level;
                        xp = titan.Xp;
                    }
                }

                controller.Combat.TryGetAuraVisual(
                    u,
                    out var auraRadius,
                    out var auraColorPacked,
                    out var auraAbilityId);

                units.Add(new MatchUnitSnapshot
                {
                    UnitId = u.UnitId,
                    OwnerSlot = u.OwnerSlot,
                    UnitDefId = u.Role.ToString(),
                    LaneId = u.LaneId ?? GameIds.Lanes.Center,
                    PosX = u.WorldPosition.x,
                    PosZ = u.WorldPosition.z,
                    FacingX = u.FacingDirection.x,
                    FacingZ = u.FacingDirection.z,
                    Health = u.CurrentHp,
                    Mana = u.CurrentMana,
                    Level = level,
                    Xp = xp,
                    IsAlive = true,
                    BehaviorState = (byte)u.BehaviorState,
                    AttackSwingSerial = u.AttackSwingSerial,
                    HeroSlot = u.HeroSlot,
                    IsParkedAtBase = u.IsParkedAtBase,
                    BonusSlot = u.BonusSlot,
                    AuraRadius = auraRadius,
                    AuraColorPacked = auraColorPacked,
                    AuraAbilityId = auraAbilityId,
                    TargetUnitId = u.CurrentTargetId ?? MatchSnapshotSections.NoTargetUnitId,
                    TargetBuildingInstanceId = u.CurrentTargetBuildingInstanceId
                        ?? MatchSnapshotSections.NoTargetBuildingInstanceId,
                    IsAttackCommitted = u.AttackCommitRemainingSeconds > 0f,
                });
            }

            var research = new List<MatchResearchSnapshot>();
            foreach (var buildingInstanceId in controller.Research.BuildingInstanceIds)
            {
                if (!controller.Research.TryGetQueue(buildingInstanceId, out var queue))
                {
                    continue;
                }

                for (var i = 0; i < queue.Count; i++)
                {
                    var r = queue[i];
                    research.Add(new MatchResearchSnapshot
                    {
                        BuildingInstanceId = r.BuildingInstanceId,
                        OwnerSlot = r.OwnerSlot,
                        BuildingId = r.BuildingId ?? string.Empty,
                        UpgradeId = r.UpgradeId ?? string.Empty,
                        CostPaid = r.CostPaid,
                        DurationSeconds = r.DurationSeconds,
                        RemainingSeconds = r.RemainingSeconds,
                    });
                }
            }

            var barracks = new List<MatchBarracksSnapshot>();
            foreach (var b in controller.WaveScheduler.Barracks)
            {
                int[] callCurrent = null;
                int[] callMax = null;
                float[] callNext = null;
                if (b.CallCharges.IsInitialized)
                {
                    callCurrent = new int[BarracksCallChargeState.CallableRoleCount];
                    callMax = new int[BarracksCallChargeState.CallableRoleCount];
                    callNext = new float[BarracksCallChargeState.CallableRoleCount];
                    b.CallCharges.Capture(callCurrent, callMax, callNext);
                }

                barracks.Add(new MatchBarracksSnapshot
                {
                    OwnerSlot = b.OwnerSlot,
                    BarracksId = b.BarracksId,
                    Level = b.Level,
                    IsRuins = b.IsRuins,
                    FrozenSquadLevel = b.FrozenSquadLevel,
                    CallCurrent = callCurrent,
                    CallMax = callMax,
                    CallNextRegen = callNext,
                    TimeUntilNextWaveSeconds = b.TimeUntilNextWaveSeconds,
                });
            }

            var centerLanes = new List<MatchCenterLaneSnapshot>();
            if (controller.Graph?.Lanes != null)
            {
                foreach (var lane in controller.Graph.Lanes)
                {
                    if (lane == null || !lane.IsCenterLane)
                    {
                        continue;
                    }

                    centerLanes.Add(new MatchCenterLaneSnapshot
                    {
                        OwnerSlot = lane.OwnerSlot,
                        OpponentSlot = lane.OpponentSlot,
                    });
                }
            }

            var spellCasts = new List<MatchSpellSnapshot>();
            foreach (var c in controller.Combat.NetworkAbilityCasts)
            {
                spellCasts.Add(new MatchSpellSnapshot
                {
                    Serial = c.Serial,
                    CasterUnitId = c.CasterUnitId,
                    OwnerSlot = c.OwnerSlot,
                    AbilityId = c.Def != null ? (ushort)c.Def.AbilityId : (ushort)0,
                    TargetUnitId = c.TargetUnitId,
                    CenterX = c.CenterPosition.x,
                    CenterZ = c.CenterPosition.z,
                    Radius = c.Radius,
                });
            }

            controller.Combat.ClearNetworkAbilityCasts();

            var projectiles = new List<MatchProjectileSnapshot>(controller.Combat.NetworkProjectileSpawns.Count);
            foreach (var p in controller.Combat.NetworkProjectileSpawns)
            {
                projectiles.Add(p);
            }

            controller.Combat.ClearNetworkProjectileSpawns();

            var heroes = new List<MatchHeroSlotSnapshot>();
            for (var owner = 0; owner < controller.Players.Count; owner++)
            {
                var roster = controller.GetHeroRoster(owner);
                if (roster == null)
                {
                    continue;
                }

                for (var slot = 1; slot <= HeroRules.MaxHeroSlots; slot++)
                {
                    var hero = roster.Get(slot);
                    heroes.Add(new MatchHeroSlotSnapshot
                    {
                        OwnerSlot = owner,
                        HeroSlot = slot,
                        State = (int)hero.State,
                        Level = hero.Level,
                        Xp = hero.Xp,
                        LastDeployBarracksInstanceId = hero.LastDeployBarracksInstanceId ?? 0,
                        DeathCooldownRemaining = hero.GetLastBarracksDeathCooldown(),
                    });
                }
            }

            var snapshot = new MatchSnapshot
            {
                PlayerCount = controller.Players.Count,
                Phase = (int)controller.Phase,
                MatchTimeSeconds = controller.MatchTimeSeconds,
                WinnerSlot = controller.WinnerSlot ?? -1,
                BonusPickDeadlineSeconds = controller.BonusPickDeadlineSeconds,
                Players = players.ToArray(),
                Buildings = buildings.ToArray(),
                Units = units.ToArray(),
                Research = research.ToArray(),
                Barracks = barracks.ToArray(),
                CenterLanes = centerLanes.ToArray(),
                SpellCasts = spellCasts.ToArray(),
                Projectiles = projectiles.ToArray(),
                Heroes = heroes.ToArray(),
            };
            snapshot.Checksum = MatchSnapshotChecksum.Compute(snapshot);
            return snapshot;
        }

        public static bool TryParseUnitRole(string unitDefId, out UnitRole role)
        {
            if (!string.IsNullOrEmpty(unitDefId) && Enum.TryParse(unitDefId, ignoreCase: true, out role))
            {
                return true;
            }

            role = UnitRole.Melee;
            return false;
        }
    }

    /// <summary>
    /// Wire codec context. Production holders keep one instance per match:
    /// <c>Encode</c> diffs the static unit roster between publishes, <c>Decode</c>
    /// accumulates the client-side static cache. Fresh instances produce/accept
    /// self-contained payloads (full static roster), which is what migration captures
    /// and Edit Mode tests rely on.
    /// </summary>
    public sealed class MatchSnapshotWireContext
    {
        private Dictionary<int, ulong> _publishedStaticSignatures = new();
        private HashSet<int> _publishedAliveIds;
        private bool _encodeRosterKnown;

        private readonly Dictionary<int, WireUnitStatic> _clientStatic = new();

        private const byte DynamicFlagAttackCommitted = 1 << 0;
        private const byte DynamicFlagParkedAtBase = 1 << 1;

        private sealed class StringTableBuilder
        {
            private readonly List<string> _strings = new();
            private readonly Dictionary<string, ushort> _index = new(StringComparer.Ordinal);

            public ushort Intern(string value)
            {
                value ??= string.Empty;
                if (_index.TryGetValue(value, out var idx))
                {
                    return idx;
                }

                idx = (ushort)_strings.Count;
                _strings.Add(value);
                _index[value] = idx;
                return idx;
            }

            public IReadOnlyList<string> Strings => _strings;
        }

        private readonly struct WireUnitStatic
        {
            public WireUnitStatic(
                int ownerSlot,
                string unitDefId,
                string laneId,
                int heroSlot,
                int bonusSlot,
                int level,
                int xp,
                float auraRadius,
                int auraColorPacked,
                int auraAbilityId)
            {
                OwnerSlot = ownerSlot;
                UnitDefId = unitDefId;
                LaneId = laneId;
                HeroSlot = heroSlot;
                BonusSlot = bonusSlot;
                Level = level;
                Xp = xp;
                AuraRadius = auraRadius;
                AuraColorPacked = auraColorPacked;
                AuraAbilityId = auraAbilityId;
            }

            public int OwnerSlot { get; }
            public string UnitDefId { get; }
            public string LaneId { get; }
            public int HeroSlot { get; }
            public int BonusSlot { get; }
            public int Level { get; }
            public int Xp { get; }
            public float AuraRadius { get; }
            public int AuraColorPacked { get; }
            public int AuraAbilityId { get; }
        }

        private readonly struct WireUnitDynamic
        {
            public WireUnitDynamic(
                int unitId,
                float posX,
                float posZ,
                byte facingAngle,
                float health,
                float mana,
                byte behaviorState,
                ushort swingSerial,
                int targetUnitId,
                int targetBuildingInstanceId,
                byte flags)
            {
                UnitId = unitId;
                PosX = posX;
                PosZ = posZ;
                FacingAngle = facingAngle;
                Health = health;
                Mana = mana;
                BehaviorState = behaviorState;
                SwingSerial = swingSerial;
                TargetUnitId = targetUnitId;
                TargetBuildingInstanceId = targetBuildingInstanceId;
                Flags = flags;
            }

            public int UnitId { get; }
            public float PosX { get; }
            public float PosZ { get; }
            public byte FacingAngle { get; }
            public float Health { get; }
            public float Mana { get; }
            public byte BehaviorState { get; }
            public ushort SwingSerial { get; }
            public int TargetUnitId { get; }
            public int TargetBuildingInstanceId { get; }
            public byte Flags { get; }
        }

        /// <summary>Forget the previous publish: the next encode sends the full roster.</summary>
        public void ResetEncode()
        {
            _publishedStaticSignatures.Clear();
            _publishedAliveIds = null;
            _encodeRosterKnown = false;
        }

        /// <summary>Drop accumulated client static records.</summary>
        public void ResetDecode() => _clientStatic.Clear();

        public byte[] Encode(MatchSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var table = new StringTableBuilder();
            var aliveIds = new HashSet<int>();
            foreach (var u in snapshot.Units)
            {
                if (!u.IsAlive || u.UnitId <= 0)
                {
                    continue;
                }

                aliveIds.Add(u.UnitId);
                table.Intern(u.UnitDefId);
                table.Intern(u.LaneId);
            }

            foreach (var b in snapshot.Buildings)
            {
                table.Intern(b.BuildingId);
            }

            foreach (var r in snapshot.Research)
            {
                table.Intern(r.BuildingId);
                table.Intern(r.UpgradeId);
            }

            foreach (var b in snapshot.Barracks)
            {
                table.Intern(b.BarracksId);
            }

            foreach (var p in snapshot.Projectiles)
            {
                table.Intern(p.SourceBuildingId);
            }

            var rosterReset = !_encodeRosterKnown;
            var removed = new List<int>();
            if (_publishedAliveIds != null)
            {
                foreach (var prev in _publishedAliveIds)
                {
                    if (!aliveIds.Contains(prev))
                    {
                        removed.Add(prev);
                    }
                }
            }

            var staticEntries = new List<MatchUnitSnapshot>();
            var nextSignatures = new Dictionary<int, ulong>(aliveIds.Count);
            foreach (var u in snapshot.Units)
            {
                if (!u.IsAlive || u.UnitId <= 0)
                {
                    continue;
                }

                var signature = StaticSignature(u);
                var unchanged = !rosterReset
                                && _publishedStaticSignatures.TryGetValue(u.UnitId, out var prevSignature)
                                && prevSignature == signature;
                if (!unchanged)
                {
                    staticEntries.Add(u);
                }

                nextSignatures[u.UnitId] = signature;
            }

            _publishedStaticSignatures = nextSignatures;
            _publishedAliveIds = aliveIds;
            _encodeRosterKnown = true;

            using var stream = new System.IO.MemoryStream(2048);
            using var writer = new System.IO.BinaryWriter(stream);
            writer.Write(MatchSnapshotCodec.CurrentVersion);
            writer.Write(snapshot.PlayerCount);
            writer.Write(snapshot.Phase);
            writer.Write(snapshot.MatchTimeSeconds);
            writer.Write(snapshot.WinnerSlot);
            writer.Write(snapshot.BonusPickDeadlineSeconds);
            writer.Write(rosterReset);

            if (table.Strings.Count > 0)
            {
                WriteSection(writer, MatchSnapshotSections.StringTable, w =>
                {
                    w.Write((ushort)table.Strings.Count);
                    foreach (var s in table.Strings)
                    {
                        w.Write(s);
                    }
                });
            }

            var players = snapshot.Players;
            if (players is { Length: > 0 })
            {
                WriteSection(writer, MatchSnapshotSections.Players, w =>
                {
                    w.Write((ushort)players.Length);
                    foreach (var p in players)
                    {
                        w.Write((byte)p.Slot);
                        w.Write(p.Gold);
                        w.Write(p.IsEliminated);
                        w.Write(p.PassiveGoldLevel);
                        w.Write(p.MainLevel);
                        w.Write(p.MagicLevel);
                        w.Write(p.MeleeDamageLevel);
                        w.Write(p.RangedDamageLevel);
                        w.Write(p.HpArmorLevel);
                        w.Write(p.BonusPickSlot);
                        w.Write(p.BonusPickSlot2);
                        var offer = p.BonusPickOfferSlots;
                        if (offer is { Length: > 0 })
                        {
                            w.Write((byte)offer.Length);
                            foreach (var s in offer)
                            {
                                w.Write((byte)s);
                            }
                        }
                        else
                        {
                            w.Write((byte)0);
                        }
                        w.Write(p.TitanResearchProgressSeconds);
                        w.Write(p.TitanUnlocked);
                        w.Write(p.TitanState);
                        w.Write(p.TitanLevel);
                        w.Write(p.TitanXp);
                        w.Write(p.TitanLastBarracksInstanceId);
                        w.Write(p.TitanDeathCooldownRemaining);
                        w.Write(p.DivineBlessingComplete);
                        w.Write(p.MainExtraAbilityId);
                        w.Write(p.MainMana);
                        w.Write(p.MainExtraAbilityCooldownRemaining);
                        w.Write(p.IceRingCooldownRemaining);
                        w.Write(p.WaveOfLightCooldownRemaining);
                        for (var t = 0; t < TowerTrackRules.TrackCount; t++)
                        {
                            var level = p.TowerTrackLevels != null && t < p.TowerTrackLevels.Length
                                ? p.TowerTrackLevels[t]
                                : 0;
                            w.Write((byte)Math.Clamp(level, 0, byte.MaxValue));
                        }
                        var heroOrder = HeroOrderPickRules.IsValidOrder(p.HeroOrder)
                            ? p.HeroOrder
                            : HeroOrderPickRules.DefaultOrder();
                        for (var h = 0; h < heroOrder.Length; h++)
                        {
                            w.Write((byte)Math.Clamp(heroOrder[h], 1, heroOrder.Length));
                        }

                        w.Write(p.HeroOrderConfirmed);
                    }
                });
            }

            var buildings = snapshot.Buildings;
            if (buildings is { Length: > 0 })
            {
                WriteSection(writer, MatchSnapshotSections.Buildings, w =>
                {
                    w.Write((ushort)buildings.Length);
                    foreach (var b in buildings)
                    {
                        w.Write(b.InstanceId);
                        w.Write((byte)b.OwnerSlot);
                        w.Write(table.Intern(b.BuildingId));
                        w.Write(b.Health);
                        w.Write(b.IsRuins);
                    }
                });
            }

            if (staticEntries.Count > 0)
            {
                WriteSection(writer, MatchSnapshotSections.UnitsStatic, w =>
                {
                    w.Write((ushort)staticEntries.Count);
                    foreach (var u in staticEntries)
                    {
                        w.Write(u.UnitId);
                        w.Write((byte)u.OwnerSlot);
                        w.Write(table.Intern(u.UnitDefId));
                        w.Write(table.Intern(u.LaneId));
                        w.Write((byte)u.HeroSlot);
                        w.Write((byte)u.BonusSlot);
                        w.Write((ushort)Mathf.Clamp(u.Level, 0, ushort.MaxValue));
                        w.Write((ushort)Mathf.Clamp(u.Xp, 0, ushort.MaxValue));
                        w.Write(u.AuraRadius);
                        w.Write(u.AuraColorPacked);
                        w.Write((ushort)Mathf.Clamp(u.AuraAbilityId, 0, ushort.MaxValue));
                    }
                });
            }

            WriteSection(writer, MatchSnapshotSections.UnitsDynamic, w =>
            {
                w.Write((ushort)aliveIds.Count);
                foreach (var u in snapshot.Units)
                {
                    if (!u.IsAlive || u.UnitId <= 0)
                    {
                        continue;
                    }

                    var flags = (byte)((u.IsAttackCommitted ? DynamicFlagAttackCommitted : 0)
                                       | (u.IsParkedAtBase ? DynamicFlagParkedAtBase : 0));
                    w.Write(u.UnitId);
                    w.Write(QuantizePosition(u.PosX));
                    w.Write(QuantizePosition(u.PosZ));
                    w.Write(QuantizeFacing(u.FacingX, u.FacingZ));
                    w.Write(Mathf.Max(0f, u.Health));
                    w.Write(Mathf.Max(0f, u.Mana));
                    w.Write(u.BehaviorState);
                    w.Write(unchecked((ushort)u.AttackSwingSerial));
                    w.Write(u.TargetUnitId);
                    w.Write(u.TargetBuildingInstanceId);
                    w.Write(flags);
                }
            });

            if (removed.Count > 0)
            {
                WriteSection(writer, MatchSnapshotSections.RemovedUnits, w =>
                {
                    w.Write((ushort)removed.Count);
                    foreach (var unitId in removed)
                    {
                        w.Write(unitId);
                    }
                });
            }

            var research = snapshot.Research;
            if (research is { Length: > 0 })
            {
                WriteSection(writer, MatchSnapshotSections.Research, w =>
                {
                    w.Write((ushort)research.Length);
                    foreach (var r in research)
                    {
                        w.Write(r.BuildingInstanceId);
                        w.Write((byte)r.OwnerSlot);
                        w.Write(table.Intern(r.BuildingId));
                        w.Write(table.Intern(r.UpgradeId));
                        w.Write(r.CostPaid);
                        w.Write(r.DurationSeconds);
                        w.Write(r.RemainingSeconds);
                    }
                });
            }

            var barracksList = snapshot.Barracks;
            if (barracksList is { Length: > 0 })
            {
                WriteSection(writer, MatchSnapshotSections.Barracks, w =>
                {
                    w.Write((ushort)barracksList.Length);
                    foreach (var b in barracksList)
                    {
                        w.Write((byte)b.OwnerSlot);
                        w.Write(table.Intern(b.BarracksId));
                        w.Write(b.Level);
                        w.Write(b.IsRuins);
                        w.Write(b.FrozenSquadLevel);
                        var hasCalls = b.CallCurrent != null
                                       && b.CallMax != null
                                       && b.CallNextRegen != null
                                       && b.CallCurrent.Length >= BarracksCallChargeState.CallableRoleCount
                                       && b.CallMax.Length >= BarracksCallChargeState.CallableRoleCount
                                       && b.CallNextRegen.Length >= BarracksCallChargeState.CallableRoleCount;
                        w.Write(hasCalls);
                        if (hasCalls)
                        {
                            for (var i = 0; i < BarracksCallChargeState.CallableRoleCount; i++)
                            {
                                w.Write(b.CallCurrent[i]);
                            }

                            for (var i = 0; i < BarracksCallChargeState.CallableRoleCount; i++)
                            {
                                w.Write(b.CallMax[i]);
                            }

                            for (var i = 0; i < BarracksCallChargeState.CallableRoleCount; i++)
                            {
                                w.Write(b.CallNextRegen[i]);
                            }
                        }

                        w.Write(b.TimeUntilNextWaveSeconds);
                    }
                });
            }

            var centerLanes = snapshot.CenterLanes;
            if (centerLanes is { Length: > 0 })
            {
                WriteSection(writer, MatchSnapshotSections.CenterLanes, w =>
                {
                    w.Write((ushort)centerLanes.Length);
                    foreach (var c in centerLanes)
                    {
                        w.Write((byte)c.OwnerSlot);
                        w.Write((byte)c.OpponentSlot);
                    }
                });
            }

            var heroes = snapshot.Heroes;
            if (heroes is { Length: > 0 })
            {
                WriteSection(writer, MatchSnapshotSections.Heroes, w =>
                {
                    w.Write((ushort)heroes.Length);
                    foreach (var h in heroes)
                    {
                        w.Write((byte)h.OwnerSlot);
                        w.Write((byte)h.HeroSlot);
                        w.Write(h.State);
                        w.Write(h.Level);
                        w.Write(h.Xp);
                        w.Write(h.LastDeployBarracksInstanceId);
                        w.Write(h.DeathCooldownRemaining);
                    }
                });
            }

            var spellCasts = snapshot.SpellCasts;
            var projectiles = snapshot.Projectiles;
            if ((spellCasts is { Length: > 0 } || projectiles is { Length: > 0 }))
            {
                WriteSection(writer, MatchSnapshotSections.Events, w =>
                {
                    var total = (spellCasts?.Length ?? 0) + (projectiles?.Length ?? 0);
                    w.Write((ushort)total);
                    if (spellCasts != null)
                    {
                        foreach (var c in spellCasts)
                        {
                            WriteEvent(w, MatchSnapshotSections.EventAbilityCast, ew =>
                            {
                                ew.Write(c.Serial);
                                ew.Write(c.CasterUnitId);
                                ew.Write((byte)c.OwnerSlot);
                                ew.Write(c.AbilityId);
                                ew.Write(c.TargetUnitId);
                                ew.Write(c.CenterX);
                                ew.Write(c.CenterZ);
                                ew.Write(c.Radius);
                            });
                        }
                    }

                    if (projectiles != null)
                    {
                        foreach (var p in projectiles)
                        {
                            WriteEvent(w, MatchSnapshotSections.EventProjectileSpawn, ew =>
                            {
                                ew.Write(p.ProjectileId);
                                ew.Write((byte)p.AttackerOwnerSlot);
                                ew.Write(p.AttackerRole);
                                ew.Write(p.StartX);
                                ew.Write(p.StartY);
                                ew.Write(p.StartZ);
                                ew.Write(p.TargetX);
                                ew.Write(p.TargetY);
                                ew.Write(p.TargetZ);
                                ew.Write(p.FlightDuration);
                                ew.Write(p.IsParabolic);
                                ew.Write(p.TargetBuildingInstanceId);
                                ew.Write(p.SourceBuildingInstanceId);
                                ew.Write(string.IsNullOrEmpty(p.SourceBuildingId)
                                    ? MatchSnapshotSections.NoStringIndex
                                    : table.Intern(p.SourceBuildingId));
                                ew.Write(p.AppliesSplashAoe);
                            });
                        }
                    }
                });
            }

            WriteSection(writer, MatchSnapshotSections.Checksum, w => { w.Write(snapshot.Checksum); });

            return stream.ToArray();
        }

        public MatchSnapshot Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new ArgumentException("Snapshot bytes required.", nameof(bytes));
            }

            using var stream = new System.IO.MemoryStream(bytes);
            using var reader = new System.IO.BinaryReader(stream);
            var version = reader.ReadInt32();
            if (version != MatchSnapshotCodec.CurrentVersion)
            {
                throw new InvalidOperationException($"Unsupported snapshot version {version}.");
            }

            var snapshot = new MatchSnapshot
            {
                PlayerCount = reader.ReadInt32(),
                Phase = reader.ReadInt32(),
                MatchTimeSeconds = reader.ReadSingle(),
                WinnerSlot = reader.ReadInt32(),
                BonusPickDeadlineSeconds = reader.ReadSingle(),
            };

            var rosterReset = reader.ReadBoolean();
            if (rosterReset)
            {
                _clientStatic.Clear();
            }

            List<string> strings = null;
            var dynamics = new List<WireUnitDynamic>();
            var removedIds = new List<int>();

            try
            {
                ParseSections(reader, snapshot, dynamics, removedIds, ref strings);
            }
            catch (System.IO.EndOfStreamException exception)
            {
                throw new InvalidOperationException(
                    "Corrupted snapshot: unexpected end of payload.",
                    exception);
            }

            foreach (var unitId in removedIds)
            {
                _clientStatic.Remove(unitId);
            }

            var merged = new List<MatchUnitSnapshot>(dynamics.Count);
            foreach (var d in dynamics)
            {
                if (!_clientStatic.TryGetValue(d.UnitId, out var st))
                {
                    continue;
                }

                DequantizeFacing(d.FacingAngle, out var facingX, out var facingZ);
                merged.Add(new MatchUnitSnapshot
                {
                    UnitId = d.UnitId,
                    OwnerSlot = st.OwnerSlot,
                    UnitDefId = st.UnitDefId,
                    LaneId = st.LaneId,
                    PosX = d.PosX,
                    PosZ = d.PosZ,
                    FacingX = facingX,
                    FacingZ = facingZ,
                    Health = d.Health,
                    Mana = d.Mana,
                    Level = st.Level,
                    Xp = st.Xp,
                    IsAlive = true,
                    BehaviorState = d.BehaviorState,
                    AttackSwingSerial = unchecked((int)d.SwingSerial),
                    HeroSlot = st.HeroSlot,
                    IsParkedAtBase = (d.Flags & DynamicFlagParkedAtBase) != 0,
                    BonusSlot = st.BonusSlot,
                    AuraRadius = st.AuraRadius,
                    AuraColorPacked = st.AuraColorPacked,
                    AuraAbilityId = st.AuraAbilityId,
                    TargetUnitId = d.TargetUnitId,
                    TargetBuildingInstanceId = d.TargetBuildingInstanceId,
                    IsAttackCommitted = (d.Flags & DynamicFlagAttackCommitted) != 0,
                });
            }

            snapshot.Units = merged.ToArray();
            return snapshot;
        }

        private void ParseSections(
            System.IO.BinaryReader reader,
            MatchSnapshot snapshot,
            List<WireUnitDynamic> dynamics,
            List<int> removedIds,
            ref List<string> strings)
        {
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                var sectionId = reader.ReadUInt16();
                var payloadLength = reader.ReadInt32();
                if (payloadLength < 0 || reader.BaseStream.Position + payloadLength > reader.BaseStream.Length)
                {
                    throw new InvalidOperationException(
                        $"Corrupted snapshot section {sectionId}: payload length {payloadLength} exceeds buffer.");
                }

                var sectionEnd = reader.BaseStream.Position + payloadLength;
                switch (sectionId)
                {
                    case MatchSnapshotSections.Buildings:
                    case MatchSnapshotSections.UnitsStatic:
                    case MatchSnapshotSections.Research:
                    case MatchSnapshotSections.Barracks:
                        if (strings == null)
                        {
                            throw new InvalidOperationException(
                                $"Corrupted snapshot: section {sectionId} precedes StringTable.");
                        }

                        break;
                }

                switch (sectionId)
                {
                    case MatchSnapshotSections.StringTable:
                        strings = ReadStringTable(reader);
                        break;
                    case MatchSnapshotSections.Players:
                        snapshot.Players = ReadPlayers(reader);
                        break;
                    case MatchSnapshotSections.Buildings:
                        snapshot.Buildings = ReadBuildings(reader, strings);
                        break;
                    case MatchSnapshotSections.UnitsStatic:
                        ReadUnitsStatic(reader, strings, _clientStatic);
                        break;
                    case MatchSnapshotSections.UnitsDynamic:
                        ReadUnitsDynamic(reader, dynamics);
                        break;
                    case MatchSnapshotSections.RemovedUnits:
                        ReadRemovedUnits(reader, removedIds);
                        break;
                    case MatchSnapshotSections.Research:
                        snapshot.Research = ReadResearch(reader, strings);
                        break;
                    case MatchSnapshotSections.Barracks:
                        snapshot.Barracks = ReadBarracks(reader, strings);
                        break;
                    case MatchSnapshotSections.CenterLanes:
                        snapshot.CenterLanes = ReadCenterLanes(reader);
                        break;
                    case MatchSnapshotSections.Heroes:
                        snapshot.Heroes = ReadHeroes(reader);
                        break;
                    case MatchSnapshotSections.Events:
                        ReadEvents(reader, strings, snapshot);
                        break;
                    case MatchSnapshotSections.Checksum:
                        snapshot.Checksum = reader.ReadUInt32();
                        break;
                    default:
                        // Unknown sections are skipped by length — forward compatibility.
                        break;
                }

                reader.BaseStream.Position = sectionEnd;
            }
        }

        private static void WriteSection(System.IO.BinaryWriter writer, ushort sectionId, Action<System.IO.BinaryWriter> fill)
        {
            using var payload = new System.IO.MemoryStream();
            using (var payloadWriter = new System.IO.BinaryWriter(payload))
            {
                fill(payloadWriter);
                payloadWriter.Flush();
            }

            var bytes = payload.ToArray();
            writer.Write(sectionId);
            writer.Write(bytes.Length);
            if (bytes.Length > 0)
            {
                writer.Write(bytes);
            }
        }

        private static void WriteEvent(System.IO.BinaryWriter writer, ushort eventTypeId, Action<System.IO.BinaryWriter> fill)
        {
            using var payload = new System.IO.MemoryStream();
            using (var payloadWriter = new System.IO.BinaryWriter(payload))
            {
                fill(payloadWriter);
                payloadWriter.Flush();
            }

            var bytes = payload.ToArray();
            writer.Write(eventTypeId);
            writer.Write((ushort)bytes.Length);
            if (bytes.Length > 0)
            {
                writer.Write(bytes);
            }
        }

        private static List<string> ReadStringTable(System.IO.BinaryReader reader)
        {
            var count = reader.ReadUInt16();
            var strings = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                strings.Add(reader.ReadString());
            }

            return strings;
        }

        private static string Resolve(List<string> strings, ushort index) =>
            index >= 0 && index < strings.Count ? strings[index] : string.Empty;

        private static MatchPlayerSnapshot[] ReadPlayers(System.IO.BinaryReader reader)
        {
            var count = reader.ReadUInt16();
            var players = new MatchPlayerSnapshot[count];
            for (var i = 0; i < count; i++)
            {
                players[i] = new MatchPlayerSnapshot
                {
                    Slot = reader.ReadByte(),
                    Gold = reader.ReadInt32(),
                    IsEliminated = reader.ReadBoolean(),
                    PassiveGoldLevel = reader.ReadInt32(),
                    MainLevel = reader.ReadInt32(),
                    MagicLevel = reader.ReadInt32(),
                    MeleeDamageLevel = reader.ReadInt32(),
                    RangedDamageLevel = reader.ReadInt32(),
                    HpArmorLevel = reader.ReadInt32(),
                    BonusPickSlot = reader.ReadInt32(),
                    BonusPickSlot2 = reader.ReadInt32(),
                    BonusPickOfferSlots = ReadBonusPickOfferSlots(reader),
                    TitanResearchProgressSeconds = reader.ReadSingle(),
                    TitanUnlocked = reader.ReadBoolean(),
                    TitanState = reader.ReadInt32(),
                    TitanLevel = reader.ReadInt32(),
                    TitanXp = reader.ReadInt32(),
                    TitanLastBarracksInstanceId = reader.ReadInt32(),
                    TitanDeathCooldownRemaining = reader.ReadSingle(),
                    DivineBlessingComplete = reader.ReadBoolean(),
                    MainExtraAbilityId = reader.ReadInt32(),
                    MainMana = reader.ReadSingle(),
                    MainExtraAbilityCooldownRemaining = reader.ReadSingle(),
                    IceRingCooldownRemaining = Math.Max(0f, reader.ReadSingle()),
                    WaveOfLightCooldownRemaining = Math.Max(0f, reader.ReadSingle()),
                    TowerTrackLevels = ReadTowerTrackLevels(reader),
                    HeroOrder = ReadHeroOrder(reader),
                    HeroOrderConfirmed = reader.ReadBoolean(),
                };

                if (players[i].TitanLevel <= 0)
                {
                    players[i].TitanLevel = HeroLevelRules.StartingLevel;
                }
            }

            return players;
        }

        private static int[] ReadTowerTrackLevels(System.IO.BinaryReader reader)
        {
            var levels = new int[TowerTrackRules.TrackCount];
            for (var i = 0; i < levels.Length; i++)
            {
                levels[i] = reader.ReadByte();
            }

            return levels;
        }

        private static int[] ReadHeroOrder(System.IO.BinaryReader reader)
        {
            var order = new int[HeroRules.MaxHeroSlots];
            for (var i = 0; i < order.Length; i++)
            {
                var raw = reader.ReadByte();
                order[i] = Math.Clamp((int)raw, 1, HeroRules.MaxHeroSlots);
            }

            return order;
        }

        private static int[] ReadBonusPickOfferSlots(System.IO.BinaryReader reader)
        {
            var count = reader.ReadByte();
            if (count == 0)
            {
                return Array.Empty<int>();
            }

            var offer = new int[count];
            for (var i = 0; i < count; i++)
            {
                offer[i] = reader.ReadByte();
            }

            return offer;
        }

        private static MatchBuildingSnapshot[] ReadBuildings(System.IO.BinaryReader reader, List<string> strings)
        {
            var count = reader.ReadUInt16();
            var buildings = new MatchBuildingSnapshot[count];
            for (var i = 0; i < count; i++)
            {
                buildings[i] = new MatchBuildingSnapshot
                {
                    InstanceId = reader.ReadInt32(),
                    OwnerSlot = reader.ReadByte(),
                    BuildingId = Resolve(strings, reader.ReadUInt16()),
                    Health = reader.ReadSingle(),
                    IsRuins = reader.ReadBoolean(),
                };
            }

            return buildings;
        }

        private static void ReadUnitsStatic(
            System.IO.BinaryReader reader,
            List<string> strings,
            Dictionary<int, WireUnitStatic> cache)
        {
            var count = reader.ReadUInt16();
            for (var i = 0; i < count; i++)
            {
                var unitId = reader.ReadInt32();
                cache[unitId] = new WireUnitStatic(
                    reader.ReadByte(),
                    Resolve(strings, reader.ReadUInt16()),
                    Resolve(strings, reader.ReadUInt16()),
                    reader.ReadByte(),
                    reader.ReadByte(),
                    reader.ReadUInt16(),
                    reader.ReadUInt16(),
                    reader.ReadSingle(),
                    reader.ReadInt32(),
                    reader.ReadUInt16());
            }
        }

        private static void ReadUnitsDynamic(System.IO.BinaryReader reader, List<WireUnitDynamic> dynamics)
        {
            var count = reader.ReadUInt16();
            for (var i = 0; i < count; i++)
            {
                dynamics.Add(new WireUnitDynamic(
                    reader.ReadInt32(),
                    DequantizePosition(reader.ReadInt16()),
                    DequantizePosition(reader.ReadInt16()),
                    reader.ReadByte(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadByte(),
                    reader.ReadUInt16(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadByte()));
            }
        }

        private static void ReadRemovedUnits(System.IO.BinaryReader reader, List<int> removedIds)
        {
            var count = reader.ReadUInt16();
            for (var i = 0; i < count; i++)
            {
                removedIds.Add(reader.ReadInt32());
            }
        }

        private static MatchResearchSnapshot[] ReadResearch(System.IO.BinaryReader reader, List<string> strings)
        {
            var count = reader.ReadUInt16();
            var research = new MatchResearchSnapshot[count];
            for (var i = 0; i < count; i++)
            {
                research[i] = new MatchResearchSnapshot
                {
                    BuildingInstanceId = reader.ReadInt32(),
                    OwnerSlot = reader.ReadByte(),
                    BuildingId = Resolve(strings, reader.ReadUInt16()),
                    UpgradeId = Resolve(strings, reader.ReadUInt16()),
                    CostPaid = reader.ReadInt32(),
                    DurationSeconds = reader.ReadSingle(),
                    RemainingSeconds = reader.ReadSingle(),
                };
            }

            return research;
        }

        private static MatchBarracksSnapshot[] ReadBarracks(System.IO.BinaryReader reader, List<string> strings)
        {
            var count = reader.ReadUInt16();
            var barracks = new MatchBarracksSnapshot[count];
            for (var i = 0; i < count; i++)
            {
                var snap = new MatchBarracksSnapshot
                {
                    OwnerSlot = reader.ReadByte(),
                    BarracksId = Resolve(strings, reader.ReadUInt16()),
                    Level = reader.ReadInt32(),
                    IsRuins = reader.ReadBoolean(),
                    FrozenSquadLevel = reader.ReadInt32(),
                };
                if (reader.ReadBoolean())
                {
                    var current = new int[BarracksCallChargeState.CallableRoleCount];
                    var max = new int[BarracksCallChargeState.CallableRoleCount];
                    var next = new float[BarracksCallChargeState.CallableRoleCount];
                    for (var r = 0; r < current.Length; r++)
                    {
                        current[r] = reader.ReadInt32();
                    }

                    for (var r = 0; r < max.Length; r++)
                    {
                        max[r] = reader.ReadInt32();
                    }

                    for (var r = 0; r < next.Length; r++)
                    {
                        next[r] = reader.ReadSingle();
                    }

                    snap.CallCurrent = current;
                    snap.CallMax = max;
                    snap.CallNextRegen = next;
                }

                snap.TimeUntilNextWaveSeconds = reader.ReadSingle();
                barracks[i] = snap;
            }

            return barracks;
        }

        private static MatchCenterLaneSnapshot[] ReadCenterLanes(System.IO.BinaryReader reader)
        {
            var count = reader.ReadUInt16();
            var lanes = new MatchCenterLaneSnapshot[count];
            for (var i = 0; i < count; i++)
            {
                lanes[i] = new MatchCenterLaneSnapshot
                {
                    OwnerSlot = reader.ReadByte(),
                    OpponentSlot = reader.ReadByte(),
                };
            }

            return lanes;
        }

        private static MatchHeroSlotSnapshot[] ReadHeroes(System.IO.BinaryReader reader)
        {
            var count = reader.ReadUInt16();
            var heroes = new MatchHeroSlotSnapshot[count];
            for (var i = 0; i < count; i++)
            {
                heroes[i] = new MatchHeroSlotSnapshot
                {
                    OwnerSlot = reader.ReadByte(),
                    HeroSlot = reader.ReadByte(),
                    State = reader.ReadInt32(),
                    Level = reader.ReadInt32(),
                    Xp = reader.ReadInt32(),
                    LastDeployBarracksInstanceId = reader.ReadInt32(),
                    DeathCooldownRemaining = reader.ReadSingle(),
                };
            }

            return heroes;
        }

        private static void ReadEvents(
            System.IO.BinaryReader reader,
            List<string> strings,
            MatchSnapshot snapshot)
        {
            var count = reader.ReadUInt16();
            var spells = new List<MatchSpellSnapshot>();
            var projectiles = new List<MatchProjectileSnapshot>();
            for (var i = 0; i < count; i++)
            {
                var eventTypeId = reader.ReadUInt16();
                var payloadLength = reader.ReadUInt16();
                var eventEnd = reader.BaseStream.Position + payloadLength;
                switch (eventTypeId)
                {
                    case MatchSnapshotSections.EventAbilityCast:
                        spells.Add(new MatchSpellSnapshot
                        {
                            Serial = reader.ReadInt32(),
                            CasterUnitId = reader.ReadInt32(),
                            OwnerSlot = reader.ReadByte(),
                            AbilityId = reader.ReadUInt16(),
                            TargetUnitId = reader.ReadInt32(),
                            CenterX = reader.ReadSingle(),
                            CenterZ = reader.ReadSingle(),
                            Radius = reader.ReadSingle(),
                        });
                        break;
                    case MatchSnapshotSections.EventProjectileSpawn:
                        var projectile = new MatchProjectileSnapshot
                        {
                            ProjectileId = reader.ReadInt32(),
                            AttackerOwnerSlot = reader.ReadByte(),
                            AttackerRole = reader.ReadByte(),
                            StartX = reader.ReadSingle(),
                            StartY = reader.ReadSingle(),
                            StartZ = reader.ReadSingle(),
                            TargetX = reader.ReadSingle(),
                            TargetY = reader.ReadSingle(),
                            TargetZ = reader.ReadSingle(),
                            FlightDuration = reader.ReadSingle(),
                            IsParabolic = reader.ReadBoolean(),
                            TargetBuildingInstanceId = reader.ReadInt32(),
                            SourceBuildingInstanceId = reader.ReadInt32(),
                        };
                        var sourceBuildingIdx = reader.ReadUInt16();
                        projectile.AppliesSplashAoe = reader.ReadBoolean();
                        projectile.SourceBuildingId = sourceBuildingIdx == MatchSnapshotSections.NoStringIndex
                            ? string.Empty
                            : Resolve(strings, sourceBuildingIdx);
                        projectiles.Add(projectile);
                        break;
                    default:
                        // Unknown event types are skipped by length — forward compatibility.
                        break;
                }

                reader.BaseStream.Position = eventEnd;
            }

            snapshot.SpellCasts = spells.ToArray();
            snapshot.Projectiles = projectiles.ToArray();
        }

        private static short QuantizePosition(float value) =>
            (short)Mathf.Clamp(Mathf.RoundToInt(value * 100f), short.MinValue, short.MaxValue);

        private static float DequantizePosition(short value) => value * 0.01f;

        private static byte QuantizeFacing(float x, float z)
        {
            var angle = Mathf.Atan2(z, x) * Mathf.Rad2Deg;
            if (angle < 0f)
            {
                angle += 360f;
            }

            return (byte)Mathf.Clamp(Mathf.RoundToInt(angle * 255f / 360f), 0, 255);
        }

        private static void DequantizeFacing(byte angleByte, out float x, out float z)
        {
            var radians = angleByte * 360f / 255f * Mathf.Deg2Rad;
            x = Mathf.Cos(radians);
            z = Mathf.Sin(radians);
        }

        private static ulong StaticSignature(MatchUnitSnapshot u)
        {
            ulong hash = 14695981039346656037UL;
            hash = Mix(hash, u.OwnerSlot);
            hash = Mix(hash, u.UnitDefId ?? string.Empty);
            hash = Mix(hash, u.LaneId ?? string.Empty);
            hash = Mix(hash, u.HeroSlot);
            hash = Mix(hash, u.BonusSlot);
            hash = Mix(hash, u.Level);
            hash = Mix(hash, u.Xp);
            hash = Mix(hash, BitConverter.SingleToInt32Bits(u.AuraRadius));
            hash = Mix(hash, u.AuraColorPacked);
            hash = Mix(hash, u.AuraAbilityId);
            return hash;
        }

        private static ulong Mix(ulong hash, int value)
        {
            const ulong prime = 1099511628211UL;
            hash = (hash ^ unchecked((ulong)(long)value)) * prime;
            hash ^= hash >> 29;
            hash *= prime;
            hash ^= hash >> 32;
            return hash;
        }

        private static ulong Mix(ulong hash, string value)
        {
            foreach (var c in value)
            {
                hash = Mix(hash, c);
            }

            return hash;
        }
    }

    public enum MatchTickMode
    {
        Offline = 0,
        Server = 1,
        Client = 2,
    }

    public static class MatchTickAuthority
    {
        public static bool ShouldTickSimulation(MatchTickMode mode) =>
            mode is MatchTickMode.Offline or MatchTickMode.Server;

        /// <summary>
        /// Listen-host drop despawns authority. Keep Client mode while the session
        /// handle is still held so peers do not start independent Offline sims
        /// (split-brain) during host-migration grace/rebind.
        /// </summary>
        public static MatchTickMode TickModeAfterAuthorityDespawn(bool networkedSessionHeld) =>
            networkedSessionHeld ? MatchTickMode.Client : MatchTickMode.Offline;
    }
}
