using System;

namespace Game.Core
{
    /// <summary>Pure helpers for Bootstrap launcher news placeholders and validation.</summary>
    public static class LauncherNewsRules
    {
        public const string TagNews = "НОВОСТИ";
        public const string TagPatch = "ПАТЧ";
        public const string TagTip = "СОВЕТ";

        /// <summary>Default feed: first item is featured, the rest fill the secondary list.</summary>
        public static LauncherNewsItem[] CreateDefaultFeed() =>
            new[]
            {
                new LauncherNewsItem(
                    TagNews,
                    "Добро пожаловать в BARAKI",
                    "Три пути, одна победа. Собирайте армию, контролируйте карту и не дайте соперникам опомниться."),
                new LauncherNewsItem(
                    TagPatch,
                    "Клиент обновляется автоматически",
                    "Перед входом лаунчер проверяет версию и скачивает обязательные обновления."),
                new LauncherNewsItem(
                    TagTip,
                    "Друзья и лобби — в главном меню",
                    "После запуска откройте хаб друзей, чтобы пригласить игроков в матч."),
            };

        public static bool IsValidItem(LauncherNewsItem item) =>
            !string.IsNullOrWhiteSpace(item.Tag)
            && !string.IsNullOrWhiteSpace(item.Title)
            && !string.IsNullOrWhiteSpace(item.Body);

        public static bool IsValidFeed(LauncherNewsItem[] feed)
        {
            if (feed == null || feed.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < feed.Length; i++)
            {
                if (!IsValidItem(feed[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public static LauncherNewsItem GetFeatured(LauncherNewsItem[] feed)
        {
            if (feed == null || feed.Length == 0)
            {
                return default;
            }

            return feed[0];
        }

        public static ArraySegment<LauncherNewsItem> GetSecondaryItems(LauncherNewsItem[] feed)
        {
            if (feed == null || feed.Length <= 1)
            {
                return ArraySegment<LauncherNewsItem>.Empty;
            }

            return new ArraySegment<LauncherNewsItem>(feed, 1, feed.Length - 1);
        }
    }
}
