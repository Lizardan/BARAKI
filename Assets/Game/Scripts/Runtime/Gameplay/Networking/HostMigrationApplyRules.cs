using Game.Gameplay.Match;

namespace Game.Gameplay.Networking
{
    /// <summary>Pure helpers for applying last-good snapshot after host rebind.</summary>
    public static class HostMigrationApplyRules
    {
        public static bool TryApplyLastGood(
            MatchController controller,
            byte[] lastGoodBytes,
            int previousHostSlot)
        {
            if (controller == null || lastGoodBytes == null || lastGoodBytes.Length == 0)
            {
                return false;
            }

            var snapshot = MatchSnapshotCodec.Deserialize(lastGoodBytes);
            controller.ApplyAuthoritativeSnapshot(snapshot);

            if (previousHostSlot >= 0)
            {
                controller.TryEliminateForDisconnect(previousHostSlot);
            }

            return true;
        }

        public static bool PreferLastGoodOverLiveCapture(byte[] lastGoodBytes, bool hasLiveController) =>
            lastGoodBytes is { Length: > 0 } || !hasLiveController;
    }
}
