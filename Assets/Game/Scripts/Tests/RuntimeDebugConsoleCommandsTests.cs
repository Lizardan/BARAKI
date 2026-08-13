using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class RuntimeDebugConsoleCommandsTests
    {
        [Test]
        public void TryParse_SplitsNameAndArgs()
        {
            Assert.IsTrue(RuntimeDebugConsoleCommands.TryParse("gold 1000", out var name, out var args));
            Assert.AreEqual("gold", name);
            Assert.AreEqual(1, args.Length);
            Assert.AreEqual("1000", args[0]);
        }

        [Test]
        public void TryExecute_Help_ListsRegisteredCommands()
        {
            RuntimeDebugConsoleCommands.Register("ping", "Проверка", _ => "pong");

            Assert.IsTrue(RuntimeDebugConsoleCommands.TryExecute("help", out var status));
            StringAssert.Contains("help", status);
            StringAssert.Contains("ping", status);
        }

        [Test]
        public void TryExecute_UnknownCommand_ReturnsFalse()
        {
            Assert.IsFalse(RuntimeDebugConsoleCommands.TryExecute("nope", out var status));
            StringAssert.Contains("Неизвестная", status);
        }

        [Test]
        public void TryExecute_RegisteredCommand_InvokesHandler()
        {
            RuntimeDebugConsoleCommands.Register("echo", "Echo args", args =>
                args.Length == 0 ? "empty" : args[0]);

            Assert.IsTrue(RuntimeDebugConsoleCommands.TryExecute("echo hi", out var status));
            Assert.AreEqual("hi", status);
        }
    }
}
