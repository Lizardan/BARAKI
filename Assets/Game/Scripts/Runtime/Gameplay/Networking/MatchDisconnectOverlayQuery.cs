using Game.Core;

namespace Game.Gameplay.Networking
{
    public readonly struct MatchDisconnectOverlayView
    {
        public MatchDisconnectOverlayView(
            bool isVisible,
            string statusText,
            string playerName,
            int kickSlot,
            bool showKick)
        {
            IsVisible = isVisible;
            StatusText = statusText ?? string.Empty;
            PlayerName = playerName ?? string.Empty;
            KickSlot = kickSlot;
            ShowKick = showKick;
        }

        public bool IsVisible { get; }
        public string StatusText { get; }
        public string PlayerName { get; }
        public int KickSlot { get; }
        public bool ShowKick { get; }
    }

    /// <summary>Combines lobby hold + host-loss coordinator into one HUD snapshot.</summary>
    public static class MatchDisconnectOverlayQuery
    {
        public static MatchDisconnectOverlayView Capture()
        {
            var lobby = NetworkLobbyState.Instance;
            var coordinator = HostMigrationCoordinator.Instance;
            var migrationPaused = MatchPauseGate.IsMigrationPaused
                || (coordinator != null && coordinator.IsPaused);

            if (lobby != null && lobby.MatchStartedValue)
            {
                var reserved = lobby.ReservedSlotCount;
                var phase = lobby.DisconnectUiPhase;
                var name = lobby.DisconnectUiName;
                if (string.IsNullOrWhiteSpace(name) && lobby.DisconnectUiSlot >= 0)
                {
                    name = lobby.GetSlotInfo(lobby.DisconnectUiSlot).DisplayName;
                }

                if (reserved > 0 && string.IsNullOrWhiteSpace(name))
                {
                    var focus = lobby.FindFirstReservedSlot();
                    name = focus >= 0 ? lobby.GetSlotInfo(focus).DisplayName : string.Empty;
                }

                var visible = MatchDisconnectHoldRules.ShouldPauseMatch(
                    true,
                    reserved,
                    phase,
                    migrationPaused);
                if (!visible)
                {
                    return default;
                }

                var overlayPhase = migrationPaused && phase == MatchDisconnectHoldRules.OverlayPhase.Waiting
                    ? MatchDisconnectHoldRules.OverlayPhase.Migrating
                    : phase == MatchDisconnectHoldRules.OverlayPhase.None && reserved > 0
                        ? MatchDisconnectHoldRules.OverlayPhase.Waiting
                        : phase;
                if (migrationPaused && overlayPhase == MatchDisconnectHoldRules.OverlayPhase.None)
                {
                    overlayPhase = MatchDisconnectHoldRules.OverlayPhase.Migrating;
                }

                var kickSlot = lobby.FindFirstReservedSlot();
                return new MatchDisconnectOverlayView(
                    true,
                    MatchDisconnectHoldRules.FormatStatus(overlayPhase, name),
                    name,
                    kickSlot,
                    MatchDisconnectHoldRules.ShouldShowKickButton(overlayPhase, reserved));
            }

            if (migrationPaused || MatchPauseGate.IsDisconnectHoldPaused)
            {
                var name = coordinator != null
                    ? coordinator.PreviousHostDisplayName
                    : MatchNetworkSession.GetCachedSlotName(MatchNetworkSession.ListenHostSlot);
                return new MatchDisconnectOverlayView(
                    true,
                    MatchDisconnectHoldRules.FormatMigrating(),
                    name,
                    coordinator?.PreviousHostSlot ?? -1,
                    false);
            }

            return default;
        }
    }
}
