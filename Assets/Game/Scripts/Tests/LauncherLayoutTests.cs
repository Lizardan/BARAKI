using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Game.Tests
{
    public sealed class LauncherLayoutTests
    {
        [Test]
        public void LauncherUxml_HasThreePanelShellAndProgressBlock()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/Game/UI/Runtime/UXML/Launcher.uxml");
            Assert.IsNotNull(asset, "Launcher.uxml should exist.");

            var root = asset.CloneTree();
            Assert.IsNotNull(root.Q<VisualElement>("LauncherScreen"), "LauncherScreen root.");
            Assert.IsNotNull(root.Q<VisualElement>("LeftPanel"), "Left panel.");
            Assert.IsNotNull(root.Q<VisualElement>("CenterPanel"), "Center panel.");
            Assert.IsNotNull(root.Q<VisualElement>("RightPanel"), "Right panel.");

            Assert.IsNotNull(root.Q<VisualElement>("GameLogo"), "Game logo.");
            Assert.IsNotNull(root.Q<Label>("GameTitle"), "Game title.");
            Assert.IsNotNull(root.Q<Label>("GameTagline"), "Game tagline.");
            Assert.IsNotNull(root.Q<Button>("PlayButton"), "PLAY button.");
            Assert.IsNotNull(root.Q<Label>("ClientVersionLabel"), "Client version.");
            Assert.IsNull(root.Q<VisualElement>("ClientMeta"), "ClientMeta removed.");
            Assert.IsNull(root.Q<Label>("UpdateRangeLabel"), "UpdateRangeLabel removed.");
            Assert.IsNotNull(root.Q<Label>("ProgressErrorLabel"), "Progress error.");

            Assert.IsNotNull(root.Q<VisualElement>("ProgressBlock"), "Progress block.");
            Assert.IsNotNull(root.Q<Label>("ProgressStatusLabel"), "Progress status.");
            Assert.IsNotNull(root.Q<Label>("ProgressPercentLabel"), "Progress percent.");
            Assert.IsNotNull(root.Q<VisualElement>("ProgressTrack"), "Progress track.");
            Assert.IsNotNull(root.Q<VisualElement>("ProgressFill"), "Progress fill.");
            Assert.IsNotNull(root.Q<VisualElement>("ProgressShine"), "Progress shine animation.");
            Assert.IsNull(root.Q<Label>("ProgressBytesLabel"), "Progress bytes removed.");
            Assert.IsNull(root.Q<Label>("ProgressSpeedLabel"), "Progress speed removed.");
            Assert.IsNull(root.Q<Label>("ProgressEtaLabel"), "Progress ETA removed.");

            Assert.IsNull(root.Q<Button>("SettingsButton"), "Settings removed.");
            Assert.IsNull(root.Q<Button>("RepairButton"), "Repair removed.");
            Assert.IsNull(root.Q<Button>("ModsButton"), "Mods removed.");
            Assert.IsNull(root.Q<Button>("LogsButton"), "Logs removed.");
            Assert.IsNull(root.Q<VisualElement>("ProfileCard"), "Profile card removed.");
            Assert.IsNull(root.Q<VisualElement>("SecondaryActions"), "Secondary actions removed.");

            Assert.IsNotNull(root.Q<VisualElement>("HeroBanner"), "Hero banner.");
            Assert.IsNotNull(root.Q<Label>("HeroTitle"), "Hero title.");
            Assert.IsNotNull(root.Q<Label>("HeroBody"), "Hero body.");
            Assert.IsNotNull(root.Q<Button>("HeroReadMoreButton"), "Hero Read More.");
            Assert.IsNotNull(root.Q<VisualElement>("NewsList"), "News list container.");

            Assert.IsNotNull(root.Q<Label>("ChatTitle"), "Chat title.");
            Assert.IsNotNull(root.Q<ScrollView>("ChatScroll"), "Chat scroll.");
            Assert.IsNotNull(root.Q<VisualElement>("ChatMessages"), "Chat messages.");
            Assert.IsNotNull(root.Q<TextField>("ChatInput"), "Chat input.");
            Assert.IsNotNull(root.Q<Button>("ChatSendButton"), "Chat send.");
            Assert.IsNotNull(root.Q<VisualElement>("WindowFrame"), "Window contour.");
            Assert.IsNotNull(root.Q<Button>("CloseButton"), "Close button.");
        }

        [Test]
        public void LauncherUss_MatchesProjectSteelPaletteAndSquareFrames()
        {
            var uss = System.IO.File.ReadAllText(
                "Assets/Game/UI/Runtime/USS/Launcher.uss");

            StringAssert.Contains("rgb(15, 15, 16)", uss);
            StringAssert.Contains("rgba(34, 34, 36", uss);
            StringAssert.Contains("rgb(125, 117, 104)", uss);
            StringAssert.Contains("rgb(239, 228, 207)", uss);
            StringAssert.Contains("rgb(86, 103, 122)", uss);
            StringAssert.Contains("border-radius: 0", uss);
            StringAssert.DoesNotContain("rgb(47, 128, 237)", uss);
            StringAssert.DoesNotContain("border-radius: 12px", uss);
            StringAssert.Contains("flex-grow", uss);
            StringAssert.Contains("flex-direction", uss);
            StringAssert.Contains("transition-duration", uss);
        }

        [Test]
        public void LauncherUss_WindowContourMatchesInnerPanelFrames()
        {
            var uss = System.IO.File.ReadAllText(
                "Assets/Game/UI/Runtime/USS/Launcher.uss");
            var frameIndex = uss.IndexOf(".ln-window-frame {", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(frameIndex, 0, ".ln-window-frame block.");
            var nextBlock = uss.IndexOf("\n.", frameIndex + 1, System.StringComparison.Ordinal);
            Assert.Greater(nextBlock, frameIndex);
            var block = uss.Substring(frameIndex, nextBlock - frameIndex);

            StringAssert.Contains("border-width: 2px", block);
            StringAssert.Contains("border-color: rgba(125, 117, 104, 0.55)", block);
            StringAssert.Contains("border-radius: 0", block);
        }

        [Test]
        public void LauncherUss_CloseButtonBorderIsBrassAccent()
        {
            var uss = System.IO.File.ReadAllText(
                "Assets/Game/UI/Runtime/USS/Launcher.uss");
            var closeIndex = uss.IndexOf(".ln-btn--close {", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(closeIndex, 0, ".ln-btn--close block.");
            var nextBlock = uss.IndexOf("\n.", closeIndex + 1, System.StringComparison.Ordinal);
            Assert.Greater(nextBlock, closeIndex);
            var block = uss.Substring(closeIndex, nextBlock - closeIndex);

            StringAssert.Contains("border-color: rgb(184, 169, 130)", block);
            StringAssert.Contains("top: 0", uss);
            StringAssert.Contains("right: 0", uss);
        }
    }
}
