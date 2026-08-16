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
    /// </summary>
    public sealed class MatchSnapshot
    {
        public int PlayerCount;
        public int Phase;
        public float MatchTimeSeconds;
        /// <summary>-1 when no winner yet.</summary>
        public int WinnerSlot = -1;
        /// <summary>Seconds left for the bonus pick overlay. 0 on pre-v13 snapshots.</summary>
        public float BonusPickDeadlineSeconds;
        public MatchPlayerSnapshot[] Players = Array.Empty<MatchPlayerSnapshot>();
        public MatchBuildingSnapshot[] Buildings = Array.Empty<MatchBuildingSnapshot>();
        public MatchUnitSnapshot[] Units = Array.Empty<MatchUnitSnapshot>();
        public MatchResearchSnapshot[] Research = Array.Empty<MatchResearchSnapshot>();
        public MatchBarracksSnapshot[] Barracks = Array.Empty<MatchBarracksSnapshot>();
        public MatchCenterLaneSnapshot[] CenterLanes = Array.Empty<MatchCenterLaneSnapshot>();
        /// <summary>Transient host cast events synced to clients for spell VFX (v9+).</summary>
        public MatchSpellSnapshot[] SpellCasts = Array.Empty<MatchSpellSnapshot>();
        /// <summary>
        /// One-shot projectile spawn events for client VFX (v12+), drained like <see cref="SpellCasts"/>.
        /// Not a continuous in-flight list — clients advance flight locally after apply.
        /// </summary>
        public MatchProjectileSnapshot[] Projectiles = Array.Empty<MatchProjectileSnapshot>();
        /// <summary>Per-slot hero roster. Empty on pre-v15 snapshots.</summary>
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
        /// <summary>0 on pre-v7 snapshots.</summary>
        public int MagicLevel;
        /// <summary>0 on pre-v7 snapshots.</summary>
        public int MeleeDamageLevel;
        /// <summary>0 on pre-v7 snapshots.</summary>
        public int RangedDamageLevel;
        /// <summary>0 on pre-v7 snapshots.</summary>
        public int HpArmorLevel;
        /// <summary>Chosen bonus slot (1..12), 0 = none yet. 0 on pre-v13 snapshots.</summary>
        public int BonusPickSlot;
        /// <summary>Completed titan research seconds. 0 on pre-v14 snapshots.</summary>
        public float TitanResearchProgressSeconds;
        /// <summary>Titan unlocked (research complete). False on pre-v14 snapshots.</summary>
        public bool TitanUnlocked;
        /// <summary><see cref="TitanLifecycleState"/> as int. 0 on pre-v14 snapshots.</summary>
        public int TitanState;
        /// <summary>Titan level. 1 default on pre-v15 snapshots.</summary>
        public int TitanLevel;
        /// <summary>Titan XP toward next level. 0 on pre-v15 snapshots.</summary>
        public int TitanXp;
        /// <summary>Barracks instance id of last titan deploy. 0 = none. Pre-v15 = 0.</summary>
        public int TitanLastBarracksInstanceId;
        /// <summary>Death cooldown remaining on that barracks. 0 on pre-v15 snapshots.</summary>
        public float TitanDeathCooldownRemaining;
        /// <summary>True after Divine Blessing research. False on pre-v17 snapshots.</summary>
        public bool DivineBlessingComplete;
        /// <summary>Picked main extra ability id (1..6). 0 = none. Pre-v17 = 0.</summary>
        public int MainExtraAbilityId;
        /// <summary>Main building mana. 0 on pre-v18 snapshots.</summary>
        public float MainMana;
        /// <summary>Remaining CD for picked main extra ability. 0 on pre-v18 snapshots.</summary>
        public float MainExtraAbilityCooldownRemaining;
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
        /// <summary>Current mana. 0 on pre-v8 snapshots.</summary>
        public float Mana;
        /// <summary>Hero level (1 for non-heroes). 0 on pre-v10 snapshots.</summary>
        public int Level;
        /// <summary>XP toward next level (heroes only). 0 on pre-v10 snapshots.</summary>
        public int Xp;
        public bool IsAlive;
        /// <summary><see cref="Combat.UnitBehaviorState"/> as byte. 0 on pre-v6 snapshots.</summary>
        public byte BehaviorState;
        /// <summary>Increments on each attack swing. 0 on pre-v6 snapshots.</summary>
        public int AttackSwingSerial;
        /// <summary>Hero slot 1..3. 0 on pre-v15 or non-heroes.</summary>
        public int HeroSlot;
        /// <summary>Parked idle champion at base. False on pre-v15 snapshots.</summary>
        public bool IsParkedAtBase;
        /// <summary>Enhanced unit variant (1..6 from the bonus pick), 0 = base. 0 on pre-v19 snapshots.</summary>
        public int BonusSlot;
        /// <summary>Passive aura disc radius (0 = none). 0 on pre-v19 snapshots.</summary>
        public float AuraRadius;
        /// <summary>RGBA int aura color (see <see cref="AbilityFx.ToRgbaInt"/>). 0 on pre-v19 snapshots.</summary>
        public int AuraColorPacked;
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
        /// <summary>Per callable role (Melee..Super). Null/empty on pre-v5 snapshots.</summary>
        public int[] CallCurrent;
        public int[] CallMax;
        public float[] CallNextRegen;
        /// <summary>Wave countdown. 0 on pre-v6 snapshots.</summary>
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
        /// <summary>Always 0 on spawn events; kept for v12 wire compatibility.</summary>
        public float Elapsed;
        public bool IsParabolic;
        /// <summary>-1 when not targeting a building.</summary>
        public int TargetBuildingInstanceId;
        /// <summary>-1 when not fired by a building.</summary>
        public int SourceBuildingInstanceId;
        public string SourceBuildingId;
    }

    public static class MatchSnapshotCodec
    {
        public const int CurrentVersion = 19;

        public static byte[] Serialize(MatchSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            writer.Write(CurrentVersion);
            writer.Write(snapshot.PlayerCount);
            writer.Write(snapshot.Phase);
            writer.Write(snapshot.MatchTimeSeconds);
            writer.Write(snapshot.WinnerSlot);

            writer.Write(snapshot.Players?.Length ?? 0);
            if (snapshot.Players != null)
            {
                foreach (var p in snapshot.Players)
                {
                    writer.Write(p.Slot);
                    writer.Write(p.Gold);
                    writer.Write(p.IsEliminated);
                    writer.Write(p.PassiveGoldLevel);
                    writer.Write(p.MainLevel);
                    writer.Write(p.MagicLevel);
                    writer.Write(p.MeleeDamageLevel);
                    writer.Write(p.RangedDamageLevel);
                    writer.Write(p.HpArmorLevel);
                    writer.Write(p.BonusPickSlot);
                    writer.Write(p.TitanResearchProgressSeconds);
                    writer.Write(p.TitanUnlocked);
                    writer.Write(p.TitanState);
                    writer.Write(p.TitanLevel);
                    writer.Write(p.TitanXp);
                    writer.Write(p.TitanLastBarracksInstanceId);
                    writer.Write(p.TitanDeathCooldownRemaining);
                    writer.Write(p.DivineBlessingComplete);
                    writer.Write(p.MainExtraAbilityId);
                    writer.Write(p.MainMana);
                    writer.Write(p.MainExtraAbilityCooldownRemaining);
                }
            }

            writer.Write(snapshot.Buildings?.Length ?? 0);
            if (snapshot.Buildings != null)
            {
                foreach (var b in snapshot.Buildings)
                {
                    writer.Write(b.InstanceId);
                    writer.Write(b.OwnerSlot);
                    writer.Write(b.BuildingId ?? string.Empty);
                    writer.Write(b.Health);
                    writer.Write(b.IsRuins);
                }
            }

            writer.Write(snapshot.Units?.Length ?? 0);
            if (snapshot.Units != null)
            {
                foreach (var u in snapshot.Units)
                {
                    writer.Write(u.UnitId);
                    writer.Write(u.OwnerSlot);
                    writer.Write(u.UnitDefId ?? string.Empty);
                    writer.Write(u.LaneId ?? string.Empty);
                    writer.Write(u.PosX);
                    writer.Write(u.PosZ);
                    writer.Write(u.FacingX);
                    writer.Write(u.FacingZ);
                    writer.Write(u.Health);
                    writer.Write(u.IsAlive);
                    writer.Write(u.BehaviorState);
                    writer.Write(u.AttackSwingSerial);
                    writer.Write(u.Mana);
                    writer.Write(u.Level);
                    writer.Write(u.Xp);
                    writer.Write(u.HeroSlot);
                    writer.Write(u.IsParkedAtBase);
                    writer.Write(u.BonusSlot);
                    writer.Write(u.AuraRadius);
                    writer.Write(u.AuraColorPacked);
                }
            }

            writer.Write(snapshot.Research?.Length ?? 0);
            if (snapshot.Research != null)
            {
                foreach (var r in snapshot.Research)
                {
                    writer.Write(r.BuildingInstanceId);
                    writer.Write(r.OwnerSlot);
                    writer.Write(r.BuildingId ?? string.Empty);
                    writer.Write(r.UpgradeId ?? string.Empty);
                    writer.Write(r.CostPaid);
                    writer.Write(r.DurationSeconds);
                    writer.Write(r.RemainingSeconds);
                }
            }

            writer.Write(snapshot.Barracks?.Length ?? 0);
            if (snapshot.Barracks != null)
            {
                foreach (var b in snapshot.Barracks)
                {
                    writer.Write(b.OwnerSlot);
                    writer.Write(b.BarracksId ?? string.Empty);
                    writer.Write(b.Level);
                    writer.Write(b.IsRuins);
                    writer.Write(b.FrozenSquadLevel);
                    WriteCallCharges(writer, b);
                    writer.Write(b.TimeUntilNextWaveSeconds);
                }
            }

            writer.Write(snapshot.CenterLanes?.Length ?? 0);
            if (snapshot.CenterLanes != null)
            {
                foreach (var c in snapshot.CenterLanes)
                {
                    writer.Write(c.OwnerSlot);
                    writer.Write(c.OpponentSlot);
                }
            }

            writer.Write(snapshot.SpellCasts?.Length ?? 0);
            if (snapshot.SpellCasts != null)
            {
                foreach (var c in snapshot.SpellCasts)
                {
                    writer.Write(c.Serial);
                    writer.Write(c.CasterUnitId);
                    writer.Write(c.OwnerSlot);
                    writer.Write(c.AbilityId);
                    writer.Write(c.TargetUnitId);
                    writer.Write(c.CenterX);
                    writer.Write(c.CenterZ);
                    writer.Write(c.Radius);
                }
            }

            writer.Write(snapshot.Projectiles?.Length ?? 0);
            if (snapshot.Projectiles != null)
            {
                foreach (var p in snapshot.Projectiles)
                {
                    writer.Write(p.ProjectileId);
                    writer.Write(p.AttackerOwnerSlot);
                    writer.Write(p.AttackerRole);
                    writer.Write(p.StartX);
                    writer.Write(p.StartY);
                    writer.Write(p.StartZ);
                    writer.Write(p.TargetX);
                    writer.Write(p.TargetY);
                    writer.Write(p.TargetZ);
                    writer.Write(p.FlightDuration);
                    writer.Write(p.Elapsed);
                    writer.Write(p.IsParabolic);
                    writer.Write(p.TargetBuildingInstanceId);
                    writer.Write(p.SourceBuildingInstanceId);
                    writer.Write(p.SourceBuildingId ?? string.Empty);
                }
            }

            writer.Write(snapshot.BonusPickDeadlineSeconds);
            writer.Write(snapshot.Heroes?.Length ?? 0);
            if (snapshot.Heroes != null)
            {
                foreach (var h in snapshot.Heroes)
                {
                    writer.Write(h.OwnerSlot);
                    writer.Write(h.HeroSlot);
                    writer.Write(h.State);
                    writer.Write(h.Level);
                    writer.Write(h.Xp);
                    writer.Write(h.LastDeployBarracksInstanceId);
                    writer.Write(h.DeathCooldownRemaining);
                }
            }

            writer.Write(snapshot.Checksum);

            return stream.ToArray();
        }

        public static MatchSnapshot Deserialize(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new ArgumentException("Snapshot bytes required.", nameof(bytes));
            }

            using var stream = new System.IO.MemoryStream(bytes);
            using var reader = new System.IO.BinaryReader(stream);
            var version = reader.ReadInt32();
            if (version < 1 || version > CurrentVersion)
            {
                throw new InvalidOperationException($"Unsupported snapshot version {version}.");
            }

            var snapshot = new MatchSnapshot
            {
                PlayerCount = reader.ReadInt32(),
                Phase = reader.ReadInt32(),
                MatchTimeSeconds = reader.ReadSingle(),
                WinnerSlot = version >= 2 ? reader.ReadInt32() : -1,
            };

            var playerCount = reader.ReadInt32();
            snapshot.Players = new MatchPlayerSnapshot[playerCount];
            for (var i = 0; i < playerCount; i++)
            {
                snapshot.Players[i] = new MatchPlayerSnapshot
                {
                    Slot = reader.ReadInt32(),
                    Gold = reader.ReadInt32(),
                    IsEliminated = reader.ReadBoolean(),
                    PassiveGoldLevel = version >= 3 ? reader.ReadInt32() : 0,
                    MainLevel = version >= 3 ? reader.ReadInt32() : MatchEconomyRules.DefaultMainLevel,
                };
                if (version >= 7)
                {
                    snapshot.Players[i].MagicLevel = reader.ReadInt32();
                    snapshot.Players[i].MeleeDamageLevel = reader.ReadInt32();
                    snapshot.Players[i].RangedDamageLevel = reader.ReadInt32();
                    snapshot.Players[i].HpArmorLevel = reader.ReadInt32();
                }

                if (version >= 13)
                {
                    snapshot.Players[i].BonusPickSlot = reader.ReadInt32();
                }

                if (version >= 14)
                {
                    snapshot.Players[i].TitanResearchProgressSeconds = reader.ReadSingle();
                    snapshot.Players[i].TitanUnlocked = reader.ReadBoolean();
                    snapshot.Players[i].TitanState = reader.ReadInt32();
                }

                if (version >= 15)
                {
                    snapshot.Players[i].TitanLevel = reader.ReadInt32();
                    snapshot.Players[i].TitanXp = reader.ReadInt32();
                    snapshot.Players[i].TitanLastBarracksInstanceId = reader.ReadInt32();
                    snapshot.Players[i].TitanDeathCooldownRemaining = reader.ReadSingle();
                }
                else if (snapshot.Players[i].TitanLevel <= 0)
                {
                    snapshot.Players[i].TitanLevel = 1;
                }

                if (version >= 17)
                {
                    snapshot.Players[i].DivineBlessingComplete = reader.ReadBoolean();
                    snapshot.Players[i].MainExtraAbilityId = reader.ReadInt32();
                }

                if (version >= 18)
                {
                    snapshot.Players[i].MainMana = reader.ReadSingle();
                    snapshot.Players[i].MainExtraAbilityCooldownRemaining = reader.ReadSingle();
                }
            }

            var buildingCount = reader.ReadInt32();
            snapshot.Buildings = new MatchBuildingSnapshot[buildingCount];
            for (var i = 0; i < buildingCount; i++)
            {
                snapshot.Buildings[i] = new MatchBuildingSnapshot
                {
                    InstanceId = version >= 3 ? reader.ReadInt32() : 0,
                    OwnerSlot = reader.ReadInt32(),
                    BuildingId = reader.ReadString(),
                    Health = reader.ReadSingle(),
                    IsRuins = reader.ReadBoolean(),
                };
            }

            var unitCount = reader.ReadInt32();
            snapshot.Units = new MatchUnitSnapshot[unitCount];
            for (var i = 0; i < unitCount; i++)
            {
                var unit = new MatchUnitSnapshot
                {
                    UnitId = reader.ReadInt32(),
                    OwnerSlot = reader.ReadInt32(),
                    UnitDefId = reader.ReadString(),
                };
                if (version >= 3)
                {
                    unit.LaneId = reader.ReadString();
                    unit.PosX = reader.ReadSingle();
                    unit.PosZ = reader.ReadSingle();
                    unit.FacingX = reader.ReadSingle();
                    unit.FacingZ = reader.ReadSingle();
                    unit.Health = reader.ReadSingle();
                    unit.IsAlive = reader.ReadBoolean();
                }
                else
                {
                    unit.LaneId = GameIds.Lanes.Center;
                    unit.PosX = reader.ReadSingle();
                    unit.PosZ = reader.ReadSingle();
                    unit.FacingX = 0f;
                    unit.FacingZ = 1f;
                    unit.Health = reader.ReadSingle();
                    unit.IsAlive = reader.ReadBoolean();
                }

                if (version >= 6)
                {
                    unit.BehaviorState = reader.ReadByte();
                    unit.AttackSwingSerial = reader.ReadInt32();
                }

                if (version >= 8)
                {
                    unit.Mana = reader.ReadSingle();
                }

                if (version >= 10)
                {
                    unit.Level = reader.ReadInt32();
                    unit.Xp = reader.ReadInt32();
                }
                else
                {
                    unit.Level = 1;
                }

                if (version >= 15)
                {
                    unit.HeroSlot = reader.ReadInt32();
                    unit.IsParkedAtBase = reader.ReadBoolean();
                }

                if (version >= 19)
                {
                    unit.BonusSlot = reader.ReadInt32();
                    unit.AuraRadius = reader.ReadSingle();
                    unit.AuraColorPacked = reader.ReadInt32();
                }

                snapshot.Units[i] = unit;
            }

            if (version >= 3)
            {
                var researchCount = reader.ReadInt32();
                snapshot.Research = new MatchResearchSnapshot[researchCount];
                for (var i = 0; i < researchCount; i++)
                {
                    snapshot.Research[i] = new MatchResearchSnapshot
                    {
                        BuildingInstanceId = reader.ReadInt32(),
                        OwnerSlot = reader.ReadInt32(),
                        BuildingId = reader.ReadString(),
                        UpgradeId = reader.ReadString(),
                        CostPaid = reader.ReadInt32(),
                        DurationSeconds = reader.ReadSingle(),
                        RemainingSeconds = reader.ReadSingle(),
                    };
                }

                var barracksCount = reader.ReadInt32();
                snapshot.Barracks = new MatchBarracksSnapshot[barracksCount];
                for (var i = 0; i < barracksCount; i++)
                {
                    var barracksSnap = new MatchBarracksSnapshot
                    {
                        OwnerSlot = reader.ReadInt32(),
                        BarracksId = reader.ReadString(),
                        Level = reader.ReadInt32(),
                        IsRuins = reader.ReadBoolean(),
                        FrozenSquadLevel = reader.ReadInt32(),
                    };
                    if (version >= 5)
                    {
                        ReadCallCharges(reader, ref barracksSnap);
                    }

                    if (version >= 6)
                    {
                        barracksSnap.TimeUntilNextWaveSeconds = reader.ReadSingle();
                    }

                    snapshot.Barracks[i] = barracksSnap;
                }

                var centerCount = reader.ReadInt32();
                snapshot.CenterLanes = new MatchCenterLaneSnapshot[centerCount];
                for (var i = 0; i < centerCount; i++)
                {
                    snapshot.CenterLanes[i] = new MatchCenterLaneSnapshot
                    {
                        OwnerSlot = reader.ReadInt32(),
                        OpponentSlot = reader.ReadInt32(),
                    };
                }
            }

            if (version >= 9)
            {
                var spellCount = reader.ReadInt32();
                snapshot.SpellCasts = new MatchSpellSnapshot[spellCount];
                for (var i = 0; i < spellCount; i++)
                {
                    var spell = new MatchSpellSnapshot
                    {
                        Serial = reader.ReadInt32(),
                        CasterUnitId = reader.ReadInt32(),
                        OwnerSlot = reader.ReadInt32(),
                    };
                    if (version >= 16)
                    {
                        spell.AbilityId = reader.ReadUInt16();
                    }
                    else
                    {
                        reader.ReadByte(); // legacy SpellType
                        if (version >= 11)
                        {
                            reader.ReadByte(); // legacy SpellKind
                            reader.ReadByte(); // legacy HeroAbility
                        }
                    }

                    spell.TargetUnitId = reader.ReadInt32();
                    spell.CenterX = reader.ReadSingle();
                    spell.CenterZ = reader.ReadSingle();
                    spell.Radius = reader.ReadSingle();
                    snapshot.SpellCasts[i] = spell;
                }
            }

            if (version >= 12)
            {
                var projectileCount = reader.ReadInt32();
                snapshot.Projectiles = new MatchProjectileSnapshot[projectileCount];
                for (var i = 0; i < projectileCount; i++)
                {
                    snapshot.Projectiles[i] = new MatchProjectileSnapshot
                    {
                        ProjectileId = reader.ReadInt32(),
                        AttackerOwnerSlot = reader.ReadInt32(),
                        AttackerRole = reader.ReadByte(),
                        StartX = reader.ReadSingle(),
                        StartY = reader.ReadSingle(),
                        StartZ = reader.ReadSingle(),
                        TargetX = reader.ReadSingle(),
                        TargetY = reader.ReadSingle(),
                        TargetZ = reader.ReadSingle(),
                        FlightDuration = reader.ReadSingle(),
                        Elapsed = reader.ReadSingle(),
                        IsParabolic = reader.ReadBoolean(),
                        TargetBuildingInstanceId = reader.ReadInt32(),
                        SourceBuildingInstanceId = reader.ReadInt32(),
                        SourceBuildingId = reader.ReadString(),
                    };
                }
            }

            if (version >= 13)
            {
                snapshot.BonusPickDeadlineSeconds = reader.ReadSingle();
            }

            if (version >= 15)
            {
                var heroCount = reader.ReadInt32();
                snapshot.Heroes = new MatchHeroSlotSnapshot[heroCount];
                for (var i = 0; i < heroCount; i++)
                {
                    snapshot.Heroes[i] = new MatchHeroSlotSnapshot
                    {
                        OwnerSlot = reader.ReadInt32(),
                        HeroSlot = reader.ReadInt32(),
                        State = reader.ReadInt32(),
                        Level = reader.ReadInt32(),
                        Xp = reader.ReadInt32(),
                        LastDeployBarracksInstanceId = reader.ReadInt32(),
                        DeathCooldownRemaining = reader.ReadSingle(),
                    };
                }
            }

            if (version >= 4 && reader.BaseStream.Position < reader.BaseStream.Length)
            {
                snapshot.Checksum = reader.ReadUInt32();
            }

            return snapshot;
        }

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

                var auraRadius = 0f;
                var auraColorPacked = 0;
                if (controller.Combat.TryGetAuraVisual(u, out var visualRadius, out var visualColorPacked))
                {
                    auraRadius = visualRadius;
                    auraColorPacked = visualColorPacked;
                }

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
                    IsAlive = u.IsAlive,
                    BehaviorState = (byte)u.BehaviorState,
                    AttackSwingSerial = u.AttackSwingSerial,
                    HeroSlot = u.HeroSlot,
                    IsParkedAtBase = u.IsParkedAtBase,
                    BonusSlot = u.BonusSlot,
                    AuraRadius = auraRadius,
                    AuraColorPacked = auraColorPacked,
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

        static void WriteCallCharges(System.IO.BinaryWriter writer, MatchBarracksSnapshot barracks)
        {
            var has = barracks.CallCurrent != null
                      && barracks.CallMax != null
                      && barracks.CallNextRegen != null
                      && barracks.CallCurrent.Length >= BarracksCallChargeState.CallableRoleCount
                      && barracks.CallMax.Length >= BarracksCallChargeState.CallableRoleCount
                      && barracks.CallNextRegen.Length >= BarracksCallChargeState.CallableRoleCount;
            writer.Write(has);
            if (!has)
            {
                return;
            }

            for (var i = 0; i < BarracksCallChargeState.CallableRoleCount; i++)
            {
                writer.Write(barracks.CallCurrent[i]);
            }

            for (var i = 0; i < BarracksCallChargeState.CallableRoleCount; i++)
            {
                writer.Write(barracks.CallMax[i]);
            }

            for (var i = 0; i < BarracksCallChargeState.CallableRoleCount; i++)
            {
                writer.Write(barracks.CallNextRegen[i]);
            }
        }

        static void ReadCallCharges(System.IO.BinaryReader reader, ref MatchBarracksSnapshot barracks)
        {
            if (!reader.ReadBoolean())
            {
                return;
            }

            var current = new int[BarracksCallChargeState.CallableRoleCount];
            var max = new int[BarracksCallChargeState.CallableRoleCount];
            var next = new float[BarracksCallChargeState.CallableRoleCount];
            for (var i = 0; i < BarracksCallChargeState.CallableRoleCount; i++)
            {
                current[i] = reader.ReadInt32();
            }

            for (var i = 0; i < BarracksCallChargeState.CallableRoleCount; i++)
            {
                max[i] = reader.ReadInt32();
            }

            for (var i = 0; i < BarracksCallChargeState.CallableRoleCount; i++)
            {
                next[i] = reader.ReadSingle();
            }

            barracks.CallCurrent = current;
            barracks.CallMax = max;
            barracks.CallNextRegen = next;
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
