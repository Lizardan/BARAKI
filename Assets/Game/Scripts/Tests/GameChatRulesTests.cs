using System;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class GameChatRulesTests
    {
        [Test]
        public void FriendsFeedChannelName_PrefixesPlayerId()
        {
            Assert.AreEqual("friends-feed-abc", GameChatRules.FriendsFeedChannelName("abc"));
            Assert.AreEqual(string.Empty, GameChatRules.FriendsFeedChannelName(" "));
        }

        [Test]
        public void SanitizeMessage_TrimsAndCapsLength()
        {
            Assert.IsFalse(GameChatRules.TrySanitizeMessage("   ", out _));
            Assert.IsTrue(GameChatRules.TrySanitizeMessage("  hi  ", out var ok));
            Assert.AreEqual("hi", ok);

            var longText = new string('a', GameChatRules.MaxMessageLength + 20);
            Assert.IsTrue(GameChatRules.TrySanitizeMessage(longText, out var capped));
            Assert.AreEqual(GameChatRules.MaxMessageLength, capped.Length);
        }

        [Test]
        public void JsonEscape_EscapesControlChars_KeepsValidJson()
        {
            Assert.AreEqual("\"\"", GameChatRules.JsonEscape(null));
            Assert.AreEqual("\"\"", GameChatRules.JsonEscape(""));
            Assert.AreEqual("\"hi\"", GameChatRules.JsonEscape("hi"));
            Assert.AreEqual("\"a\\\"b\"", GameChatRules.JsonEscape("a\"b"));
            Assert.AreEqual("\"a\\\\b\"", GameChatRules.JsonEscape("a\\b"));
            Assert.AreEqual("\"line\\nbreak\"", GameChatRules.JsonEscape("line\nbreak"));

            // Raw control characters must never survive unescaped.
            var crafted = "bad\u0001\u001fmsg";
            var escaped = GameChatRules.JsonEscape(crafted);
            Assert.IsFalse(escaped.Contains('\u0001'));
            Assert.IsFalse(escaped.Contains('\u001f'));
            Assert.IsTrue(escaped.Contains("\\u0001"));
            Assert.IsTrue(escaped.Contains("\\u001f"));

            // The result must round-trip through a real JSON parser.
            var wrapped = "{\"text\":" + escaped + "}";
            var parsed = JsonUtility.FromJson<JsonProbe>(wrapped);
            Assert.AreEqual(crafted, parsed.text);
        }

        [Serializable]
        private sealed class JsonProbe
        {
            public string text;
        }

        [Test]
        public void ChannelClassifiers_Work()
        {
            Assert.IsTrue(GameChatRules.IsGlobalChannel(GameChatRules.GlobalChannelName));
            Assert.IsTrue(GameChatRules.IsFriendsFeedChannel("friends-feed-x"));
            Assert.IsFalse(GameChatRules.IsFriendsFeedChannel(GameChatRules.GlobalChannelName));
        }

        [Test]
        public void FormatDisplayName_StripsUgsSuffix()
        {
            Assert.AreEqual("Alpha", GameChatRules.FormatDisplayName("Alpha#1234"));
            Assert.AreEqual("Игрок", GameChatRules.FormatDisplayName(" "));
        }

        [Test]
        public void MatchMessageAlpha_FadesThenClears()
        {
            Assert.AreEqual(1f, GameChatRules.MatchMessageAlpha(0f), 0.001f);
            Assert.AreEqual(1f, GameChatRules.MatchMessageAlpha(1f), 0.001f);
            Assert.IsTrue(GameChatRules.MatchMessageAlpha(GameChatRules.MatchMessageVisibleSeconds - 0.1f) < 1f);
            Assert.AreEqual(0f, GameChatRules.MatchMessageAlpha(GameChatRules.MatchMessageVisibleSeconds), 0.001f);
            Assert.IsTrue(GameChatRules.ShouldRemoveMatchMessage(GameChatRules.MatchMessageVisibleSeconds));
            Assert.IsFalse(GameChatRules.ShouldRemoveMatchMessage(1f));
        }

        [Test]
        public void WarmingStatusLabel_IsChat()
        {
            Assert.AreEqual("Чат", GameChatRules.WarmingStatusLabel);
        }

        [Test]
        public void HistoryCaps_AreBounded()
        {
            Assert.LessOrEqual(GameChatRules.MaxChannelHistory, 100);
            Assert.LessOrEqual(GameChatRules.MaxDirectHistory, 80);
            Assert.Greater(GameChatRules.HistoryRetentionHours, 0);
        }

        [Test]
        public void IsWithinRetention_DropsOldMessages()
        {
            Assert.IsTrue(GameChatRules.IsWithinRetention(System.DateTime.Now));
            Assert.IsFalse(GameChatRules.IsWithinRetention(
                System.DateTime.Now.AddHours(-GameChatRules.HistoryRetentionHours - 1)));
        }

        [Test]
        public void IsComposerSubmit_AcceptsEnterAndNewline()
        {
            Assert.IsTrue(GameChatRules.IsComposerSubmit(KeyCode.Return, '\0'));
            Assert.IsTrue(GameChatRules.IsComposerSubmit(KeyCode.KeypadEnter, '\0'));
            Assert.IsTrue(GameChatRules.IsComposerSubmit(KeyCode.None, '\n'));
            Assert.IsTrue(GameChatRules.IsComposerSubmit(KeyCode.None, '\r'));
            Assert.IsFalse(GameChatRules.IsComposerSubmit(KeyCode.None, 'a'));
            Assert.IsFalse(GameChatRules.IsComposerSubmit(KeyCode.Space, '\0'));
        }
    }

    public sealed class NetworkMatchChatRulesTests
    {
        [Test]
        public void CanSend_RespectsInterval()
        {
            Assert.IsTrue(NetworkMatchChatRules.CanSend(10f, 0f));
            Assert.IsFalse(NetworkMatchChatRules.CanSend(10f, 9.5f));
            Assert.IsTrue(NetworkMatchChatRules.CanSend(10f, 10f - NetworkMatchChatRules.MinSendIntervalSeconds));
        }

        [Test]
        public void TryNormalize_RejectsEmpty()
        {
            Assert.IsFalse(NetworkMatchChatRules.TryNormalize("  ", out _));
            Assert.IsTrue(NetworkMatchChatRules.TryNormalize(" ping ", out var msg));
            Assert.AreEqual("ping", msg);
        }
    }
}
