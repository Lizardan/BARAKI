using System;
using Game.Gameplay.Data;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// UI-safe façade over <see cref="MatchNetworkAuthority"/> (no Netcode types in signature).
    /// </summary>
    public static class MatchNetworkCommands
    {
        public static bool IsAvailable
        {
            get
            {
                var authority = MatchNetworkAuthority.Instance;
                return authority != null && authority.IsSpawned;
            }
        }

        public static bool AreCommandsBlocked => MatchPauseGate.IsPaused;

        public static event Action<MatchCommandResult> CommandResultReceived
        {
            add => MatchNetworkAuthority.CommandResultReceived += value;
            remove => MatchNetworkAuthority.CommandResultReceived -= value;
        }

        /// <summary>
        /// Any player may pause/resume the whole match. Falls back to the local pause
        /// gate when no network authority is present (offline/local-dev play).
        /// </summary>
        public static void RequestSetPaused(bool paused)
        {
            var authority = MatchNetworkAuthority.Instance;
            if (authority == null || !authority.IsSpawned)
            {
                MatchPauseGate.SetUserPaused(paused);
                return;
            }

            authority.RequestSetPaused(paused);
        }

        public static void RequestStartResearch(int buildingInstanceId, string upgradeId) =>
            MatchNetworkAuthority.Instance?.RequestStartResearch(buildingInstanceId, upgradeId);

        public static void RequestHireHero(int heroSlot) =>
            MatchNetworkAuthority.Instance?.RequestHireHero(heroSlot);

        public static void RequestDeployHero(int buildingInstanceId, int heroSlot) =>
            MatchNetworkAuthority.Instance?.RequestDeployHero(buildingInstanceId, heroSlot);

        public static void RequestSetTowerTarget(int towerInstanceId, int unitId) =>
            MatchNetworkAuthority.Instance?.RequestSetTowerTarget(towerInstanceId, unitId);

        public static void RequestManualCall(int barracksBuildingInstanceId, UnitRole role) =>
            MatchNetworkAuthority.Instance?.RequestManualCall(barracksBuildingInstanceId, role);
    }
}
