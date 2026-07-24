using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class UiThemePaletteTests
    {
        [Test]
        public void BarakiTheme_UsesAshenFortressSteelPalette()
        {
            var theme = ReadProjectFile("Assets/Game/UI/Runtime/USS/BarakiTheme.uss");

            StringAssert.Contains("rgb(15, 15, 16)", theme);
            StringAssert.Contains("rgb(125, 117, 104)", theme);
            StringAssert.Contains("rgba(125, 117, 104", theme);
            StringAssert.DoesNotContain("rgb(194, 119, 58)", theme);
            StringAssert.DoesNotContain("rgba(194, 119, 58", theme);
            StringAssert.DoesNotContain("rgb(83, 168, 255)", theme);
            StringAssert.DoesNotContain("rgb(201, 163, 90)", theme);
        }

        [Test]
        public void ScreenStyles_UseAshenFortressSteelAccent()
        {
            var files = new[]
            {
                "Assets/Game/UI/Runtime/USS/BootstrapLoading.uss",
                "Assets/Game/UI/Runtime/USS/MainMenu.uss",
                "Assets/Game/UI/Runtime/USS/Lobby.uss",
                "Assets/Game/UI/Runtime/USS/MatchHud.uss",
                "Assets/Game/UI/Runtime/USS/MatchBottomDock.uss",
                "Assets/Game/UI/Runtime/USS/RacePick.uss",
            };

            foreach (var file in files)
            {
                var text = ReadProjectFile(file);
                StringAssert.Contains("125, 117, 104", text, file);
                StringAssert.DoesNotContain("194, 119, 58", text, file);
            }
        }

        [Test]
        public void RuntimeStyles_UseSquareFrames()
        {
            var files = new[]
            {
                "Assets/Game/UI/Runtime/USS/BarakiTheme.uss",
                "Assets/Game/UI/Runtime/USS/BootstrapLoading.uss",
                "Assets/Game/UI/Runtime/USS/MainMenu.uss",
                "Assets/Game/UI/Runtime/USS/Lobby.uss",
                "Assets/Game/UI/Runtime/USS/MatchHud.uss",
                "Assets/Game/UI/Runtime/USS/MatchBottomDock.uss",
                "Assets/Game/UI/Runtime/USS/RacePick.uss",
            };

            foreach (var file in files)
            {
                AssertNoRoundedFrames(file, ReadProjectFile(file));
            }
        }

        [Test]
        public void RuntimeStyles_DoNotUseDarkTextOnPrimaryButtons()
        {
            var files = new[]
            {
                "Assets/Game/UI/Runtime/USS/BarakiTheme.uss",
                "Assets/Game/UI/Runtime/USS/BootstrapLoading.uss",
                "Assets/Game/UI/Runtime/USS/MainMenu.uss",
                "Assets/Game/UI/Runtime/USS/RacePick.uss",
            };

            foreach (var file in files)
            {
                StringAssert.DoesNotContain("color: rgb(26, 16, 10)", ReadProjectFile(file), file);
            }
        }

        [Test]
        public void RuntimeStyles_UseSteelBlueButtonsWithGreyBorders()
        {
            var theme = ReadProjectFile("Assets/Game/UI/Runtime/USS/BarakiTheme.uss");
            var bootstrap = ReadProjectFile("Assets/Game/UI/Runtime/USS/BootstrapLoading.uss");
            var bootstrapUxml = ReadProjectFile("Assets/Game/UI/Runtime/UXML/BootstrapLoading.uxml");
            var mainMenu = ReadProjectFile("Assets/Game/UI/Runtime/USS/MainMenu.uss");
            var racePick = ReadProjectFile("Assets/Game/UI/Runtime/USS/RacePick.uss");

            StringAssert.Contains("background-color: rgb(86, 103, 122)", theme);
            StringAssert.Contains("border-color: rgb(125, 117, 104)", theme);
            StringAssert.Contains("background-color: rgb(86, 103, 122)", bootstrap);
            StringAssert.Contains("border-color: rgb(125, 117, 104)", bootstrap);
            StringAssert.Contains("color: rgb(86, 103, 122)", bootstrap);
            StringAssert.Contains("color: rgb(86, 103, 122)", mainMenu);
            StringAssert.Contains("background-color: rgb(86, 103, 122)", mainMenu);
            StringAssert.Contains("border-color: rgb(125, 117, 104)", mainMenu);
            StringAssert.Contains("background-color: rgb(86, 103, 122)", racePick);
            StringAssert.Contains("border-color: rgb(125, 117, 104)", racePick);
            StringAssert.Contains("name=\"EnterGameButton\" text=\"ИГРАТЬ\" class=\"ui-btn ui-btn--primary bl__cta ui-overlay--hidden\"", bootstrapUxml);
            StringAssert.Contains("name=\"UpdateButton\" text=\"ОБНОВИТЬ\" class=\"ui-btn ui-btn--primary bl__cta bl__cta--update\"", bootstrapUxml);
            StringAssert.Contains(".ui-btn.bl__cta.bl__cta--update", bootstrap);
            StringAssert.Contains("background-color: rgb(88, 204, 104)", bootstrap);
        }

        private static string ReadProjectFile(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            var localPath = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return File.ReadAllText(Path.Combine(projectRoot, localPath));
        }

        private static void AssertNoRoundedFrames(string file, string text)
        {
            using var reader = new StringReader(text);
            var lineNumber = 0;
            while (reader.ReadLine() is { } line)
            {
                lineNumber++;
                var trimmed = line.Trim();
                if (trimmed.StartsWith("border-radius:") &&
                    trimmed != "border-radius: 0;" &&
                    trimmed != "border-radius: 0px;")
                {
                    Assert.Fail($"{file}:{lineNumber} should use square frames: {trimmed}");
                }
            }
        }
    }
}
