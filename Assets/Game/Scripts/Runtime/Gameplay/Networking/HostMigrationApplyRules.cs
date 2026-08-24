using System;
using Game.Gameplay.Match;

namespace Game.Gameplay.Networking
{
    /// <summary>Pure helpers for applying last-good snapshot after host rebind.</summary>
    public static class HostMigrationApplyRules
    {
        public static bool TryApplyLastGood(
            MatchController controller,
            byte[] lastGoodBytes,
            int previousHostSlot,
            bool eliminatePreviousHost = false,
            MatchSnapshotWireContext wireContext = null)
        {
            if (controller == null || lastGoodBytes == null || lastGoodBytes.Length == 0)
            {
                return false;
            }

            MatchSnapshot snapshot;
            try
            {
                snapshot = wireContext != null
                    ? wireContext.Decode(lastGoodBytes)
                    : MatchSnapshotCodec.Deserialize(lastGoodBytes);
            }
            catch (Exception exception)
            {
                // Broken last-good is not fatal: migration continues from an empty state.
                UnityEngine.Debug.LogWarning(
                    $"HostMigration: last-good snapshot unreadable, continuing without it. {exception.Message}");
                return false;
            }

            controller.ApplyAuthoritativeSnapshot(snapshot);

            if (eliminatePreviousHost && previousHostSlot >= 0)
            {
                controller.TryEliminateForDisconnect(previousHostSlot);
            }

            return true;
        }

        public static bool TryCaptureState(
            byte[] lastGoodBytes,
            MatchController liveController,
            out byte[] captured)
        {
            if (lastGoodBytes is { Length: > 0 })
            {
                captured = lastGoodBytes;
                return true;
            }

            if (liveController == null)
            {
                captured = null;
                return false;
            }

            captured = MatchSnapshotCodec.Serialize(MatchSnapshotCodec.Capture(liveController));
            return captured is { Length: > 0 };
        }

        public static bool PreferLastGoodOverLiveCapture(byte[] lastGoodBytes, bool hasLiveController) =>
            lastGoodBytes is { Length: > 0 } || !hasLiveController;
    }
}
