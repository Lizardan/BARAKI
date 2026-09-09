namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Zero-dependency bridge for UI (order pick): exposes the hero order request without
    /// leaking the <see cref="NetworkBonusPickState"/> NetworkBehaviour type into Game.UI.
    /// Mirrors <see cref="BonusPickNetworkFacade"/>.
    /// </summary>
    public static class HeroOrderNetworkFacade
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
        public static bool TryRequestHeroOrder(int[] heroOrder)
        {
            if (s_bridge == null)
            {
                return false;
            }

            s_bridge.RequestHeroOrder(heroOrder);
            return true;
        }
    }
}