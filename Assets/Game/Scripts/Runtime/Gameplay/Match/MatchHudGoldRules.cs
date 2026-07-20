namespace Game.Gameplay.Match
{
    /// <summary>HUD gold source: local controller offline/host, snapshot on pure clients.</summary>
    public static class MatchHudGoldRules
    {
        public static int ResolveLocalGold(
            int localSlot,
            int controllerGold,
            int snapshotGold,
            bool useSnapshot) =>
            useSnapshot ? snapshotGold : controllerGold;

        public static bool TryGetSnapshotGold(
            Game.Gameplay.Networking.MatchSnapshot snapshot,
            int localSlot,
            out int gold)
        {
            gold = 0;
            if (snapshot?.Players == null)
            {
                return false;
            }

            for (var i = 0; i < snapshot.Players.Length; i++)
            {
                if (snapshot.Players[i].Slot == localSlot)
                {
                    gold = snapshot.Players[i].Gold;
                    return true;
                }
            }

            return false;
        }
    }
}
