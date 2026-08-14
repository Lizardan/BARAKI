namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Zero-dependency bridge for UI (PRE-001): exposes the bonus pick request without
    /// leaking the <see cref="NetworkBonusPickState"/> NetworkBehaviour type into Game.UI.
    /// </summary>
    public static class BonusPickNetworkFacade
    {
        static NetworkBonusPickState s_bridge;

        public static bool HasBridge => s_bridge != null;

        public static void Register(NetworkBonusPickState bridge) => s_bridge = bridge;

        public static void Unregister(NetworkBonusPickState bridge)
        {
            if (ReferenceEquals(s_bridge, bridge))
            {
                s_bridge = null;
            }
        }

        /// <summary>True when a networked bridge consumed the request.</summary>
        public static bool TryRequestPick(int bonusSlot)
        {
            if (s_bridge == null)
            {
                return false;
            }

            s_bridge.RequestPick(bonusSlot);
            return true;
        }
    }
}
