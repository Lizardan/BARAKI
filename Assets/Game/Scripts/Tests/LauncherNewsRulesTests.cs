using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class LauncherNewsRulesTests
    {
        [Test]
        public void CreateDefaultFeed_IsValidAndHasFeaturedPlusSecondary()
        {
            var feed = LauncherNewsRules.CreateDefaultFeed();

            Assert.IsTrue(LauncherNewsRules.IsValidFeed(feed));
            Assert.GreaterOrEqual(feed.Length, 3);

            var featured = LauncherNewsRules.GetFeatured(feed);
            Assert.IsTrue(LauncherNewsRules.IsValidItem(featured));
            Assert.AreEqual(LauncherNewsRules.TagNews, featured.Tag);

            var secondary = LauncherNewsRules.GetSecondaryItems(feed);
            Assert.AreEqual(feed.Length - 1, secondary.Count);
            foreach (var item in secondary)
            {
                Assert.IsTrue(LauncherNewsRules.IsValidItem(item));
            }
        }

        [Test]
        public void IsValidFeed_RejectsEmptyOrBrokenItems()
        {
            Assert.IsFalse(LauncherNewsRules.IsValidFeed(null));
            Assert.IsFalse(LauncherNewsRules.IsValidFeed(System.Array.Empty<LauncherNewsItem>()));
            Assert.IsFalse(LauncherNewsRules.IsValidFeed(new[]
            {
                new LauncherNewsItem("", "Title", "Body"),
            }));
        }
    }
}
