using Game.Gameplay.Match;

namespace Game.UI
{
    public static class MatchHudFormatting
    {
        public static string FormatMatchTime(float seconds)
        {
            var total = seconds < 0f ? 0 : (int)seconds;
            var minutes = total / 60;
            var secs = total % 60;
            return $"{minutes:00}:{secs:00}";
        }

        public static string FormatPhase(MatchPhase phase) => phase switch
        {
            MatchPhase.Start => "Старт",
            MatchPhase.Early => "Ранняя",
            MatchPhase.Mid => "Средняя",
            MatchPhase.Late => "Поздняя",
            MatchPhase.End => "Конец",
            _ => "—",
        };

        public static string FormatBarracksTimer(float secondsRemaining, bool schedulerActive)
        {
            if (!schedulerActive)
            {
                var preview = secondsRemaining < 0f ? 0 : (int)System.Math.Ceiling(secondsRemaining);
                return preview.ToString();
            }

            var value = secondsRemaining <= 0f ? 0 : (int)System.Math.Ceiling(secondsRemaining);
            return value.ToString();
        }

        public static string FormatGold(int gold)
        {
            var value = gold < 0 ? 0 : gold;
            return value.ToString();
        }

        public static string FormatBountyPopup(int goldGranted) => $"+{goldGranted}";

        public static string FormatMatchResult(int winnerSlot) => $"Победа: игрок {winnerSlot + 1}";
    }
}
