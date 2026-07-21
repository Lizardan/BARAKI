namespace Game.Core
{
    /// <summary>Single launcher news / tip card for Bootstrap feed binding.</summary>
    public readonly struct LauncherNewsItem
    {
        public LauncherNewsItem(string tag, string title, string body)
        {
            Tag = tag ?? string.Empty;
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
        }

        public string Tag { get; }

        public string Title { get; }

        public string Body { get; }
    }
}
