using System;
using System.Collections.Generic;
using Game.Core;
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
        public MatchPlayerSnapshot[] Players = Array.Empty<MatchPlayerSnapshot>();
        public MatchBuildingSnapshot[] Buildings = Array.Empty<MatchBuildingSnapshot>();
        public MatchUnitSnapshot[] Units = Array.Empty<MatchUnitSnapshot>();
        public MatchResearchSnapshot[] Research = Array.Empty<MatchResearchSnapshot>();
        public MatchBarracksSnapshot[] Barracks = Array.Empty<MatchBarracksSnapshot>();
        public MatchCenterLaneSnapshot[] CenterLanes = Array.Empty<MatchCenterLaneSnapshot>();
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
        public bool IsAlive;
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
    }

    public struct MatchCenterLaneSnapshot
    {
        public int OwnerSlot;
        public int OpponentSlot;
    }

    public static class MatchSnapshotCodec
    {
        public const int CurrentVersion = 5;

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
            if (version is not (1 or 2 or 3 or 4 or 5))
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
                players.Add(new MatchPlayerSnapshot
                {
                    Slot = p.SlotIndex,
                    Gold = p.Gold,
                    IsEliminated = p.IsEliminated,
                    PassiveGoldLevel = p.PassiveGoldLevel,
                    MainLevel = p.MainLevel,
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
                    IsAlive = u.IsAlive,
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

            var snapshot = new MatchSnapshot
            {
                PlayerCount = controller.Players.Count,
                Phase = (int)controller.Phase,
                MatchTimeSeconds = controller.MatchTimeSeconds,
                WinnerSlot = controller.WinnerSlot ?? -1,
                Players = players.ToArray(),
                Buildings = buildings.ToArray(),
                Units = units.ToArray(),
                Research = research.ToArray(),
                Barracks = barracks.ToArray(),
                CenterLanes = centerLanes.ToArray(),
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
    }
}
