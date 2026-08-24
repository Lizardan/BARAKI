using System;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class GameChatSocketRulesTests
    {
        [Test]
        public void NextReconnectDelay_DoublesAndCaps()
        {
            Assert.AreEqual(1f, GameChatSocketRules.NextReconnectDelaySeconds(0), 0.001f);
            Assert.AreEqual(2f, GameChatSocketRules.NextReconnectDelaySeconds(1), 0.001f);
            Assert.AreEqual(4f, GameChatSocketRules.NextReconnectDelaySeconds(2), 0.001f);
            Assert.AreEqual(
                GameChatSocketRules.MaxReconnectDelaySeconds,
                GameChatSocketRules.NextReconnectDelaySeconds(10),
                0.001f,
                "Backoff must cap at the max delay.");
            Assert.AreEqual(1f, GameChatSocketRules.NextReconnectDelaySeconds(-5), 0.001f,
                "Negative attempt is treated as first attempt.");
        }

        [Test]
        public void OutboxCount_EnqueuesUpToLimit()
        {
            var count = 0;
            for (var i = 0; i < GameChatSocketRules.MaxOutboxMessages + 5; i++)
            {
                count = GameChatSocketRules.OutboxCountAfterEnqueue(count);
            }

            Assert.AreEqual(GameChatSocketRules.MaxOutboxMessages, count);
        }

        [Test]
        public void BuildSendFrame_ChannelAndEscapedText()
        {
            var frame = GameChatSocketRules.BuildSendFrame("global", "hi \"there\"\n");
            StringAssert.Contains("\"type\":\"send\"", frame);
            StringAssert.Contains("\"channel\":\"global\"", frame);
            StringAssert.DoesNotContain("\n\"", frame);

            var dm = GameChatSocketRules.BuildSendFrame("dm", "hello", "peer-1234");
            StringAssert.Contains("\"peerId\":\"peer-1234\"", dm);
        }

        [Test]
        public void BuildSyncFrame_EmptyDmDefaultsToEmptyObject()
        {
            var frame = GameChatSocketRules.BuildSyncFrame("2026-01-01T00:00:00Z", "", null);
            StringAssert.Contains("\"globalAfter\":\"2026-01-01T00:00:00Z\"", frame);
            StringAssert.Contains("\"friendsAfter\":\"\"", frame);
            StringAssert.Contains("\"dm\":{}", frame);
        }

        [Test]
        public void ParseServerFrame_MsgAckError()
        {
            var msgJson = "{\"type\":\"msg\",\"message\":{"
                          + "\"id\":\"a1\",\"ts\":\"2026-08-24T10:00:00.000Z\","
                          + "\"playerId\":\"player-abcd\",\"displayName\":\"Nick\","
                          + "\"text\":\"привет\",\"channel\":\"global\",\"peerId\":\"\"}}";
            var (msgKind, msgFrame) = GameChatSocketRules.ParseServerFrame(msgJson);
            Assert.AreEqual(GameChatSocketRules.FrameKind.Message, msgKind);
            Assert.AreEqual("a1", msgFrame.message.id);
            Assert.AreEqual("привет", msgFrame.message.text);

            var ackJson = "{\"type\":\"ack\",\"message\":{\"id\":\"a2\"}}";
            var (ackKind, _) = GameChatSocketRules.ParseServerFrame(ackJson);
            Assert.AreEqual(GameChatSocketRules.FrameKind.Ack, ackKind);

            var errJson = "{\"type\":\"error\",\"code\":\"empty\"}";
            var (errKind, errFrame) = GameChatSocketRules.ParseServerFrame(errJson);
            Assert.AreEqual(GameChatSocketRules.FrameKind.Error, errKind);
            Assert.AreEqual("empty", errFrame.code);
        }

        [Test]
        public void ParseServerFrame_GarbageIsUnknown()
        {
            Assert.AreEqual(
                GameChatSocketRules.FrameKind.Unknown,
                GameChatSocketRules.ParseServerFrame("not json {").Kind);
            Assert.AreEqual(
                GameChatSocketRules.FrameKind.Unknown,
                GameChatSocketRules.ParseServerFrame("pong").Kind);
            Assert.AreEqual(
                GameChatSocketRules.FrameKind.Unknown,
                GameChatSocketRules.ParseServerFrame(null).Kind);
            Assert.AreEqual(
                GameChatSocketRules.FrameKind.Unknown,
                GameChatSocketRules.ParseServerFrame("{\"type\":\"msg\"}").Kind,
                "msg without message payload is not ingestable.");
        }
    }
}
