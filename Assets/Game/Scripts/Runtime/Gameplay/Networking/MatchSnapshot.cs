using System;
using System.Collections.Generic;
using Game.Gameplay.Match;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Compact authoritative match state for client render sync (MVP-N01/N03).
    /// Pure C# — no NGO dependency so Edit Mode tests can round-trip it.
    /// </summary>
    public sealed class MatchSnapshot
    {
        public int PlayerCount;
        public int Phase;
        public float MatchTimeSeconds;
        public MatchPlayerSnapshot[] Players = Array.Empty<MatchPlayerSnapshot>();
        public MatchBuildingSnapshot[] Buildings = Array.Empty<MatchBuildingSnapshot>();
        public MatchUnitSnapshot[] Units = Array.Empty<MatchUnitSnapshot>();
    }

    public struct MatchPlayerSnapshot
    {
        public int Slot;
        public int Gold;
        public bool IsEliminated;
    }

    public struct MatchBuildingSnapshot
    {
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
        public float PosX;
        public float PosZ;
        public float Health;
        public bool IsAlive;
    }

    public static class MatchSnapshotCodec
    {
        public static byte[] Serialize(MatchSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            writer.Write(1); // version
            writer.Write(snapshot.PlayerCount);
            writer.Write(snapshot.Phase);
            writer.Write(snapshot.MatchTimeSeconds);

            writer.Write(snapshot.Players?.Length ?? 0);
            if (snapshot.Players != null)
            {
                foreach (var p in snapshot.Players)
                {
                    writer.Write(p.Slot);
                    writer.Write(p.Gold);
                    writer.Write(p.IsEliminated);
                }
            }

            writer.Write(snapshot.Buildings?.Length ?? 0);
            if (snapshot.Buildings != null)
            {
                foreach (var b in snapshot.Buildings)
                {
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
                    writer.Write(u.PosX);
                    writer.Write(u.PosZ);
                    writer.Write(u.Health);
                    writer.Write(u.IsAlive);
                }
            }

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
            if (version != 1)
            {
                throw new InvalidOperationException($"Unsupported snapshot version {version}.");
            }

            var snapshot = new MatchSnapshot
            {
                PlayerCount = reader.ReadInt32(),
                Phase = reader.ReadInt32(),
                MatchTimeSeconds = reader.ReadSingle(),
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
                };
            }

            var buildingCount = reader.ReadInt32();
            snapshot.Buildings = new MatchBuildingSnapshot[buildingCount];
            for (var i = 0; i < buildingCount; i++)
            {
                snapshot.Buildings[i] = new MatchBuildingSnapshot
                {
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
                snapshot.Units[i] = new MatchUnitSnapshot
                {
                    UnitId = reader.ReadInt32(),
                    OwnerSlot = reader.ReadInt32(),
                    UnitDefId = reader.ReadString(),
                    PosX = reader.ReadSingle(),
                    PosZ = reader.ReadSingle(),
                    Health = reader.ReadSingle(),
                    IsAlive = reader.ReadBoolean(),
                };
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
                });
            }

            var buildings = new List<MatchBuildingSnapshot>();
            foreach (var b in controller.Buildings.Buildings)
            {
                buildings.Add(new MatchBuildingSnapshot
                {
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
                    PosX = u.WorldPosition.x,
                    PosZ = u.WorldPosition.z,
                    Health = u.CurrentHp,
                    IsAlive = u.IsAlive,
                });
            }

            return new MatchSnapshot
            {
                PlayerCount = controller.Players.Count,
                Phase = (int)controller.Phase,
                MatchTimeSeconds = controller.MatchTimeSeconds,
                Players = players.ToArray(),
                Buildings = buildings.ToArray(),
                Units = units.ToArray(),
            };
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
