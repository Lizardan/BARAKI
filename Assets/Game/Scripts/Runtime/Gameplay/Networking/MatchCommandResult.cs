namespace Game.Gameplay.Networking
{
    public enum MatchCommandResult : byte
    {
        Ok = 0,
        NotEnoughGold = 1,
        InvalidBuilding = 2,
        InvalidTarget = 3,
        QueueFull = 4,
        NotAllowed = 5,
        HostMigrating = 6,
    }

    /// <summary>Maps command outcomes to short RU UI strings.</summary>
    public static class MatchCommandResultRules
    {
        public static string FormatFeedback(MatchCommandResult result) => result switch
        {
            MatchCommandResult.Ok => string.Empty,
            MatchCommandResult.NotEnoughGold => "Не хватает золота",
            MatchCommandResult.InvalidBuilding => "Неверное здание",
            MatchCommandResult.InvalidTarget => "Неверная цель",
            MatchCommandResult.QueueFull => "Очередь исследований полна",
            MatchCommandResult.HostMigrating => "Смена хоста…",
            _ => "Команда отклонена",
        };

        public static MatchCommandResult FromTrySuccess(bool success) =>
            success ? MatchCommandResult.Ok : MatchCommandResult.NotAllowed;

        public static MatchCommandResult ClassifyResearchFailure(
            bool buildingValid,
            bool hasQueueSpace,
            bool enoughGold)
        {
            if (!buildingValid)
            {
                return MatchCommandResult.InvalidBuilding;
            }

            if (!hasQueueSpace)
            {
                return MatchCommandResult.QueueFull;
            }

            if (!enoughGold)
            {
                return MatchCommandResult.NotEnoughGold;
            }

            return MatchCommandResult.NotAllowed;
        }
    }
}
