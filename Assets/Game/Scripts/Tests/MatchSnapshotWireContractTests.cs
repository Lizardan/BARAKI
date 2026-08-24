using System;
using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Wire v21 contract: section framing, forward compatibility (unknown sections and
    /// event types are skipped), corruption handling, static/dynamic diffing and the
    /// snapshot size budget. These tests are the guard against "visuals host-only" and
    /// silent wire bloat regressions.
    /// </summary>
    public sealed class MatchSnapshotWireContractTests
    {
        private static MatchSnapshot BuildFullSnapshot()
        {
            var controller = new MatchController();
            controller.StartMatch(MatchConfig.MvpDefault(2));
            controller.BeginEarlyPhase();

            // Waves do not spawn instantly — seed a small skirmish so unit sections carry data.
            var attackerStats = new UnitCombatStats(UnitRole.Melee, 200f, 0f, 10f, 10f, 1f, 1.2f, 0f, 1);
            var victimStats = new UnitCombatStats(UnitRole.Melee, 500f, 0f, 1f, 1f, 0.1f, 1f, 0f, 1);
            var attacker = controller.Combat.SpawnUnit(0, GameIds.Lanes.Center, UnitRole.Melee, attackerStats);
            var victim = controller.Combat.SpawnUnit(1, GameIds.Lanes.Center, UnitRole.Melee, victimStats);
            attacker.WorldPosition = new Vector3(0f, 0.15f, 0f);
            victim.WorldPosition = new Vector3(0f, 0.15f, 1f);

            return MatchSnapshotCodec.Capture(controller);
        }

        [Test]
        public void RoundTrip_FullCapture_PreservesAllSections()
        {
            var original = BuildFullSnapshot();
            var restored = MatchSnapshotCodec.Deserialize(MatchSnapshotCodec.Serialize(original));

            Assert.AreEqual(original.PlayerCount, restored.PlayerCount);
            Assert.AreEqual(original.Phase, restored.Phase);
            Assert.AreEqual(original.MatchTimeSeconds, restored.MatchTimeSeconds, 0.001f);
            Assert.AreEqual(original.WinnerSlot, restored.WinnerSlot);
            Assert.AreEqual(
                original.BonusPickDeadlineSeconds,
                restored.BonusPickDeadlineSeconds, 0.001f);

            Assert.AreEqual(original.Players.Length, restored.Players.Length);
            for (var i = 0; i < original.Players.Length; i++)
            {
                var expected = original.Players[i];
                var actual = restored.Players[i];
                Assert.AreEqual(expected.Slot, actual.Slot);
                Assert.AreEqual(expected.Gold, actual.Gold);
                Assert.AreEqual(expected.IsEliminated, actual.IsEliminated);
                Assert.AreEqual(expected.MagicLevel, actual.MagicLevel);
                Assert.AreEqual(expected.BonusPickSlot, actual.BonusPickSlot);
                Assert.AreEqual(expected.TitanState, actual.TitanState);
                Assert.AreEqual(expected.DivineBlessingComplete, actual.DivineBlessingComplete);
                Assert.AreEqual(expected.MainMana, actual.MainMana, 0.001f);
            }

            Assert.AreEqual(original.Buildings.Length, restored.Buildings.Length);
            for (var i = 0; i < original.Buildings.Length; i++)
            {
                Assert.AreEqual(original.Buildings[i].InstanceId, restored.Buildings[i].InstanceId);
                Assert.AreEqual(original.Buildings[i].BuildingId, restored.Buildings[i].BuildingId);
                Assert.AreEqual(
                    original.Buildings[i].Health,
                    restored.Buildings[i].Health, 0.01f);
                Assert.AreEqual(original.Buildings[i].IsRuins, restored.Buildings[i].IsRuins);
            }

            Assert.AreEqual(original.Units.Length, restored.Units.Length);
            for (var i = 0; i < original.Units.Length; i++)
            {
                Assert.AreEqual(original.Units[i].UnitId, restored.Units[i].UnitId);
                Assert.AreEqual(original.Units[i].UnitDefId, restored.Units[i].UnitDefId);
                Assert.AreEqual(original.Units[i].LaneId, restored.Units[i].LaneId);
                Assert.AreEqual(
                    original.Units[i].PosX,
                    restored.Units[i].PosX, 0.02f);
                Assert.AreEqual(
                    original.Units[i].PosZ,
                    restored.Units[i].PosZ, 0.02f);
                Assert.AreEqual(original.Units[i].BehaviorState, restored.Units[i].BehaviorState);
                Assert.AreEqual(original.Units[i].AttackSwingSerial, restored.Units[i].AttackSwingSerial);
                Assert.AreEqual(original.Units[i].HeroSlot, restored.Units[i].HeroSlot);
                Assert.AreEqual(original.Units[i].BonusSlot, restored.Units[i].BonusSlot);
                Assert.AreEqual(original.Units[i].AuraRadius, restored.Units[i].AuraRadius, 0.001f);
                Assert.AreEqual(original.Units[i].AuraColorPacked, restored.Units[i].AuraColorPacked);
                Assert.IsTrue(restored.Units[i].IsAlive);
            }

            Assert.AreEqual(original.Research.Length, restored.Research.Length);
            Assert.AreEqual(original.Barracks.Length, restored.Barracks.Length);
            for (var i = 0; i < original.Barracks.Length; i++)
            {
                Assert.AreEqual(original.Barracks[i].BarracksId, restored.Barracks[i].BarracksId);
                Assert.AreEqual(original.Barracks[i].Level, restored.Barracks[i].Level);
                if (original.Barracks[i].CallCurrent != null)
                {
                    Assert.IsNotNull(restored.Barracks[i].CallCurrent);
                    for (var r = 0; r < original.Barracks[i].CallCurrent.Length; r++)
                    {
                        Assert.AreEqual(
                            original.Barracks[i].CallCurrent[r],
                            restored.Barracks[i].CallCurrent[r]);
                    }
                }
            }

            Assert.AreEqual(original.CenterLanes.Length, restored.CenterLanes.Length);
            Assert.AreEqual(original.Heroes.Length, restored.Heroes.Length);
        }

        [Test]
        public void Encode_SecondPublishWithoutChanges_OmitsStaticSection()
        {
            var context = new MatchSnapshotWireContext();
            var snapshot = BuildFullSnapshot();

            var first = context.Encode(snapshot);
            context.Decode(first);
            var second = context.Encode(snapshot);

            Assert.Less(
                second.Length,
                first.Length,
                "Second publish with unchanged roster must be smaller (no UnitsStatic).");
        }

        [Test]
        public void Encode_IncrementalDecode_PreservesUnitsAcrossSnapshots()
        {
            var encoder = new MatchSnapshotWireContext();
            var decoder = new MatchSnapshotWireContext();
            var first = BuildFullSnapshot();

            var applied = decoder.Decode(encoder.Encode(first));
            Assert.AreEqual(first.Units.Length, applied.Units.Length);

            // Simulate movement-only publish: same units, shifted positions.
            var moved = ShallowClone(first);
            for (var i = 0; i < moved.Units.Length; i++)
            {
                moved.Units[i].PosX += 1f;
            }

            var secondApplied = decoder.Decode(encoder.Encode(moved));
            Assert.AreEqual(first.Units.Length, secondApplied.Units.Length);
            for (var i = 0; i < secondApplied.Units.Length; i++)
            {
                Assert.AreEqual(
                    first.Units[i].UnitDefId,
                    secondApplied.Units[i].UnitDefId,
                    "Static record must survive incremental publishes.");
            }
        }

        [Test]
        public void Encode_RemovedUnit_DisappearsFromDecodedRoster()
        {
            var encoder = new MatchSnapshotWireContext();
            var decoder = new MatchSnapshotWireContext();
            var first = BuildFullSnapshot();
            Assert.Greater(first.Units.Length, 1, $"Scenario needs two units (probe v2, got {first.Units.Length}).");

            var withoutOne = ShallowClone(first);
            var removedId = withoutOne.Units[0].UnitId;
            var remaining = new List<MatchUnitSnapshot>(withoutOne.Units);
            remaining.RemoveAll(u => u.UnitId == removedId);
            withoutOne.Units = remaining.ToArray();

            decoder.Decode(encoder.Encode(first));
            var applied = decoder.Decode(encoder.Encode(withoutOne));

            Assert.AreEqual(remaining.Count, applied.Units.Length);
            foreach (var unit in applied.Units)
            {
                Assert.AreNotEqual(removedId, unit.UnitId);
            }

            // Re-adding the unit later must restore its static record.
            var backAgain = ShallowClone(first);
            var reapplied = decoder.Decode(encoder.Encode(backAgain));
            Assert.AreEqual(backAgain.Units.Length, reapplied.Units.Length);
        }

        [Test]
        public void Decode_UnknownSection_IsSkippedAndOtherDataIntact()
        {
            var bytes = MatchSnapshotCodec.Serialize(BuildFullSnapshot());
            var foreign = new byte[]
            {
                0x99, 0x00, // sectionId = 0x0099 (unknown)
                0x04, 0x00, 0x00, 0x00, // payloadLength = 4
                0xDE, 0xAD, 0xBE, 0xEF, // payload
            };
            var stitched = InsertAfterHeader(bytes, foreign);

            var restored = MatchSnapshotCodec.Deserialize(stitched);
            var reference = MatchSnapshotCodec.Deserialize(bytes);

            Assert.AreEqual(reference.Units.Length, restored.Units.Length);
            Assert.AreEqual(reference.Buildings.Length, restored.Buildings.Length);
        }

        [Test]
        public void Decode_UnknownEventType_IsSkippedAndKnownEventsArrive()
        {
            var snapshot = BuildFullSnapshot();
            snapshot.SpellCasts = new[]
            {
                new MatchSpellSnapshot
                {
                    Serial = 5,
                    CasterUnitId = 3,
                    OwnerSlot = 1,
                    AbilityId = 42,
                    TargetUnitId = 7,
                    CenterX = 1f,
                    CenterZ = 2f,
                    Radius = 3f,
                },
            };

            var bytes = MatchSnapshotCodec.Serialize(snapshot);
            var foreign = new byte[]
            {
                0x7F, 0x01, // eventTypeId = 0x017F (unknown)
                0x03, 0x00, // payloadLength = 3
                0x01, 0x02, 0x03,
            };
            var stitched = SpliceEventIntoEventsSection(bytes, foreign);

            var restored = MatchSnapshotCodec.Deserialize(stitched);
            Assert.AreEqual(1, restored.SpellCasts.Length);
            Assert.AreEqual(5, restored.SpellCasts[0].Serial);
            Assert.AreEqual(42, restored.SpellCasts[0].AbilityId);
        }

        [Test]
        public void Decode_TruncatedPayload_Throws()
        {
            var bytes = MatchSnapshotCodec.Serialize(BuildFullSnapshot());
            var truncated = new byte[bytes.Length - 8];
            Array.Copy(bytes, truncated, truncated.Length);

            Assert.Throws<InvalidOperationException>(
                () => MatchSnapshotCodec.Deserialize(truncated));
        }

        [TestCase(0)]
        [TestCase(-3)]
        [TestCase(int.MaxValue)]
        public void Decode_UnsupportedVersion_Throws(int version)
        {
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            writer.Write(version);
            writer.Write(16); // garbage body

            Assert.Throws<InvalidOperationException>(
                () => MatchSnapshotCodec.Deserialize(stream.ToArray()));
        }

        [Test]
        public void Decode_MissingStringTableBeforeReferencingSection_Throws()
        {
            // Header + a Buildings section that references strings, but no StringTable.
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            writer.Write(MatchSnapshotCodec.CurrentVersion);
            writer.Write(2);   // playerCount
            writer.Write(1);   // phase
            writer.Write(10f); // matchTime
            writer.Write(-1);  // winnerSlot
            writer.Write(0f);  // bonusPickDeadline
            writer.Write(true); // rosterReset

            // Buildings section payload: count=1 + one building referencing string idx 0.
            byte[] buildingsPayload;
            using (var payload = new System.IO.MemoryStream())
            using (var pw = new System.IO.BinaryWriter(payload))
            {
                pw.Write((ushort)1);
                pw.Write(11);      // instanceId
                pw.Write((byte)0); // ownerSlot
                pw.Write((ushort)0); // buildingIdIdx
                pw.Write(100f);    // health
                pw.Write(false);   // isRuins
                pw.Flush();
                buildingsPayload = payload.ToArray();
            }

            writer.Write(MatchSnapshotSections.Buildings);
            writer.Write(buildingsPayload.Length);
            writer.Write(buildingsPayload);

            Assert.Throws<InvalidOperationException>(
                () => MatchSnapshotCodec.Deserialize(stream.ToArray()));
        }

        [Test]
        public void RoundTrip_NewV21Fields_SurviveWire()
        {
            var snapshot = BuildFullSnapshot();
            if (snapshot.Units.Length == 0)
            {
                Assert.Ignore("Scenario produced no units.");
            }

            for (var i = 0; i < snapshot.Units.Length; i++)
            {
                snapshot.Units[i].TargetUnitId = i % 2 == 0 ? 123 : 0;
                snapshot.Units[i].TargetBuildingInstanceId = i % 2 == 0 ? -1 : 456;
                snapshot.Units[i].IsAttackCommitted = i % 2 == 0;
                snapshot.Units[i].AuraAbilityId = 13;
            }

            var restored = MatchSnapshotCodec.Deserialize(MatchSnapshotCodec.Serialize(snapshot));
            for (var i = 0; i < snapshot.Units.Length; i++)
            {
                Assert.AreEqual(snapshot.Units[i].TargetUnitId, restored.Units[i].TargetUnitId);
                Assert.AreEqual(
                    snapshot.Units[i].TargetBuildingInstanceId,
                    restored.Units[i].TargetBuildingInstanceId);
                Assert.AreEqual(
                    snapshot.Units[i].IsAttackCommitted,
                    restored.Units[i].IsAttackCommitted);
                Assert.AreEqual(snapshot.Units[i].AuraAbilityId, restored.Units[i].AuraAbilityId);
            }
        }

        /// <summary>
        /// Size budget: the wire format must stay lean as content grows. A late-game-like
        /// scene (200 moving units) must fit under the budget — this fails when someone
        /// adds per-unit fields without thinking about traffic.
        /// </summary>
        [Test]
        public void Serialize_DynamicHeavyScene_StaysUnderBudget()
        {
            var snapshot = BuildSyntheticSnapshot(unitCount: 200);
            var bytes = MatchSnapshotCodec.Serialize(snapshot);

            // First publish carries the full static roster (~40 B/unit) + dynamic (~28 B/unit).
            const int fullPublishBudgetBytes = 200 * 96;
            Assert.Less(
                bytes.Length,
                fullPublishBudgetBytes,
                $"Full publish {bytes.Length} B exceeded budget {fullPublishBudgetBytes} B.");

            // Steady-state publishes skip the static roster entirely.
            var context = new MatchSnapshotWireContext();
            context.Decode(context.Encode(snapshot));
            for (var i = 0; i < snapshot.Units.Length; i++)
            {
                snapshot.Units[i].PosX += 0.05f;
            }

            var steadyState = context.Encode(snapshot);
            const int steadyStateBudgetBytes = 200 * 48;
            Assert.Less(
                steadyState.Length,
                steadyStateBudgetBytes,
                $"Steady-state publish {steadyState.Length} B exceeded budget {steadyStateBudgetBytes} B.");
        }

        // --- helpers ---

        private static MatchSnapshot ShallowClone(MatchSnapshot source)
        {
            return new MatchSnapshot
            {
                PlayerCount = source.PlayerCount,
                Phase = source.Phase,
                MatchTimeSeconds = source.MatchTimeSeconds,
                WinnerSlot = source.WinnerSlot,
                BonusPickDeadlineSeconds = source.BonusPickDeadlineSeconds,
                Players = source.Players,
                Buildings = source.Buildings,
                Units = source.Units,
                Research = source.Research,
                Barracks = source.Barracks,
                CenterLanes = source.CenterLanes,
                SpellCasts = source.SpellCasts,
                Projectiles = source.Projectiles,
                Heroes = source.Heroes,
                Checksum = source.Checksum,
            };
        }

        private static MatchSnapshot BuildSyntheticSnapshot(int unitCount)
        {
            var units = new MatchUnitSnapshot[unitCount];
            for (var i = 0; i < unitCount; i++)
            {
                units[i] = new MatchUnitSnapshot
                {
                    UnitId = i + 1,
                    OwnerSlot = i % 5,
                    UnitDefId = "Melee",
                    LaneId = GameIds.Lanes.Center,
                    PosX = (i % 40) * 1.5f,
                    PosZ = (i / 40) * 1.5f,
                    FacingX = 0f,
                    FacingZ = 1f,
                    Health = 50f,
                    Mana = 0f,
                    Level = 1,
                    Xp = 0,
                    IsAlive = true,
                    BehaviorState = 2,
                    AttackSwingSerial = i,
                    TargetUnitId = 0,
                    TargetBuildingInstanceId = -1,
                };
            }

            return new MatchSnapshot
            {
                PlayerCount = 5,
                Phase = (int)MatchPhase.Early,
                MatchTimeSeconds = 120f,
                Units = units,
            };
        }

        /// <summary>Insert raw bytes right after the fixed header (version + 5 ints/floats + bool).</summary>
        private static byte[] InsertAfterHeader(byte[] payload, byte[] extra)
        {
            // header: int version, int playerCount, int phase, float matchTime, int winnerSlot,
            // float bonusDeadline, bool rosterReset → 4+4+4+4+4+4+1 = 25 bytes.
            const int headerLength = 25;
            var stitched = new byte[payload.Length + extra.Length];
            Array.Copy(payload, 0, stitched, 0, headerLength);
            Array.Copy(extra, 0, stitched, headerLength, extra.Length);
            Array.Copy(payload, headerLength, stitched, headerLength + extra.Length, payload.Length - headerLength);
            return stitched;
        }

        private static byte[] SpliceEventIntoEventsSection(byte[] payload, byte[] foreignEvent)
        {
            // Locate the Events section by scanning sections after the header.
            const int headerLength = 25;
            var position = headerLength;
            while (position + 8 <= payload.Length)
            {
                var sectionId = BitConverter.ToUInt16(payload, position);
                var length = BitConverter.ToInt32(payload, position + 2);
                if (sectionId == MatchSnapshotSections.Events)
                {
                    // Events layout: ushort count, then events. Prepend our unknown event
                    // before existing events, bump the count AND the section payload length.
                    var eventsStart = position + 6;
                    var stitched = new byte[payload.Length + foreignEvent.Length];
                    Array.Copy(payload, stitched, eventsStart + 2);
                    stitched[eventsStart] += 1; // count is little-endian ushort; +1 on low byte
                    Array.Copy(foreignEvent, 0, stitched, eventsStart + 2, foreignEvent.Length);
                    Array.Copy(
                        payload,
                        eventsStart + 2,
                        stitched,
                        eventsStart + 2 + foreignEvent.Length,
                        payload.Length - eventsStart - 2);

                    var newLength = length + foreignEvent.Length;
                    var lengthBytes = BitConverter.GetBytes(newLength);
                    Array.Copy(lengthBytes, 0, stitched, position + 2, 4);
                    return stitched;
                }

                position += 6 + length;
            }

            Assert.Fail("Events section not found in serialized snapshot.");
            return null;
        }
    }
}
