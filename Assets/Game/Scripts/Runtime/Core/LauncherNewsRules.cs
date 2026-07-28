using System;

namespace Game.Core
{
    /// <summary>Pure helpers for Bootstrap launcher news/patch feed placeholders and validation.</summary>
    public static class LauncherNewsRules
    {
        public const string TagNews = "НОВОСТИ";
        public const string TagPatch = "ПАТЧ";
        public const string TagTip = "СОВЕТ";

        /// <summary>Default feed: first item is featured, the rest fill the secondary vertical list.</summary>
        public static LauncherNewsItem[] CreateDefaultFeed() =>
            new[]
            {
                new LauncherNewsItem(
                    TagPatch,
                    "Новый лаунчер и быстрый вход в игру",
                    "Bootstrap теперь показывает обновления, новости команды и будущий чат в едином стартовом окне."),
                new LauncherNewsItem(
                    TagNews,
                    "Обновления ставятся в несколько шагов",
                    "Сначала скачивание, затем подготовка файлов, после чего игрок сам подтверждает перезапуск клиента."),
                new LauncherNewsItem(
                    TagPatch,
                    "Сетевой матч стал стабильнее",
                    "Улучшена синхронизация юнитов и восстановление после кратких потерь соединения."),
                new LauncherNewsItem(
                    TagNews,
                    "Лента заменит старые подсказки",
                    "В правой колонке будут публиковаться патчи, анонсы матчей и короткие заметки команды."),
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
