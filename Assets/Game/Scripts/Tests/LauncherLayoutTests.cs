using NUnit.Framework;
using UnityEditor;
using UnityEngine;
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

            Assert.IsNull(root.Q<VisualElement>("GameLogo"), "Game logo square removed.");
            Assert.IsNotNull(root.Q<Label>("GameTitle"), "Game title.");
            Assert.IsNull(root.Q<Label>("GameTagline"), "Game tagline removed.");
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
        public void LauncherPanelSettings_Uses1280x720Reference()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(
                "Assets/Game/Settings/UI/LauncherPanelSettings.asset");
            Assert.IsNotNull(settings, "LauncherPanelSettings should exist.");
            Assert.AreEqual(PanelScaleMode.ScaleWithScreenSize, settings.scaleMode);
            Assert.AreEqual(1280, Mathf.RoundToInt(settings.referenceResolution.x));
            Assert.AreEqual(720, Mathf.RoundToInt(settings.referenceResolution.y));
        }

        [Test]
        public void LauncherUss_MatchesGraphiteBronzePaletteAndSquareFrames()
        {
            var uss = System.IO.File.ReadAllText(
                "Assets/Game/UI/Runtime/USS/Launcher.uss");

            StringAssert.Contains("rgb(8, 9, 8)", uss);
            StringAssert.Contains("rgb(21, 23, 22)", uss);
            StringAssert.Contains("rgb(52, 56, 50)", uss);
            StringAssert.Contains("rgb(222, 219, 210)", uss);
            StringAssert.Contains("rgb(185, 154, 98)", uss);
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

            StringAssert.Contains("border-width: 1px", block);
            StringAssert.Contains("border-color: rgb(52, 56, 50)", block);
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

            StringAssert.Contains("border-color: rgb(185, 154, 98)", block);
            StringAssert.Contains("top: 0", uss);
            StringAssert.Contains("right: 0", uss);
        }

        [Test]
        public void MainMenuUxml_UsesLauncherShellAndKeepsMenuContracts()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/Game/UI/Runtime/UXML/MainMenu.uxml");
            Assert.IsNotNull(asset, "MainMenu.uxml should exist.");

            var root = asset.CloneTree();
            Assert.IsNotNull(root.Q<VisualElement>("MenuScreen"), "MenuScreen root.");
            Assert.IsNotNull(root.Q<VisualElement>("MenuBrand"), "MenuBrand.");
            Assert.IsNotNull(root.Q<Label>("TitleLabel"), "Title.");
            Assert.IsNotNull(root.Q<Label>("VersionLabel"), "Version.");
            Assert.IsNotNull(root.Q<VisualElement>("MenuPanel"), "Menu panel.");
            Assert.IsNotNull(root.Q<VisualElement>("ProfileBlock"), "Profile block.");
            Assert.IsNotNull(root.Q<Button>("PlayButton"), "Play.");
            Assert.IsNotNull(root.Q<Button>("GameTabButton"), "Game hub tab.");
            Assert.IsNotNull(root.Q<Button>("HubFriendsTabButton"), "Friends hub tab.");
            Assert.IsNotNull(root.Q<Button>("HubFriendsTabButton").Q<Label>("FriendsCountLabel"),
                "Friends count lives inside the friends tab.");
            Assert.IsNotNull(root.Q<VisualElement>("GameTabContent"), "Game tab pane.");
            Assert.IsNotNull(root.Q<VisualElement>("FriendsHost"), "Friends host.");
            Assert.IsNotNull(root.Q<VisualElement>("ModeGrid"), "Mode grid on game tab.");
            Assert.IsNotNull(root.Q<VisualElement>("ModeDossier"), "Selected mode dossier.");
            Assert.IsNotNull(root.Q<VisualElement>("ModeDossierPreview"), "Mode dossier map.");
            Assert.IsNotNull(root.Q<Label>("ModeDossierTitle"), "Mode dossier title.");
            Assert.IsNotNull(root.Q<Label>("ModeDossierBody"), "Mode dossier body.");
            Assert.IsNotNull(root.Q<Label>("ModeDossierNote"), "Mode dossier note.");
            Assert.IsNotNull(root.Q<VisualElement>("GameTabContent").Q<Button>("PlayButton"), "Play lives on game tab.");
            Assert.IsNotNull(root.Q<Button>("ReturnToMatchButton"), "Return to match.");
            Assert.IsNotNull(root.Q<Button>("SettingsButton"), "Settings.");
            Assert.IsNotNull(root.Q<Button>("QuitButton"), "Quit.");
            Assert.IsNotNull(root.Q<Button>("ChatTabButton"), "Chat tab.");
            Assert.IsNotNull(root.Q<Button>("MatchHistoryTabButton"), "Match history tab.");
            Assert.IsNotNull(root.Q<Button>("PublicGamesTabButton"), "Public games tab.");
            Assert.IsNotNull(root.Q<VisualElement>("ChatMessages"), "Chat messages.");
            Assert.IsNotNull(root.Q<VisualElement>("MatchHistoryList"), "Match history list.");
            Assert.IsNotNull(root.Q<VisualElement>("PublicGamesList"), "Public games list.");
            Assert.IsNotNull(root.Q<VisualElement>("SettingsTabContent"), "Settings tab pane.");
            Assert.IsNotNull(root.Q<VisualElement>("HubPanel"), "Friends hub.");
            Assert.IsNotNull(root.Q<VisualElement>("SettingsOverlay"), "Settings overlay.");
            Assert.IsNotNull(root.Q<VisualElement>("MatchEntryOverlay"), "Match entry overlay.");
            Assert.IsNotNull(root.Q<VisualElement>("ProfileEditOverlay"), "Profile edit overlay.");
            var lobbyEntry = root.Q<VisualElement>("LobbyEntryOverlay");
            Assert.IsNotNull(lobbyEntry, "Lobby entry overlay.");
            Assert.IsTrue(lobbyEntry.ClassListContains("ui-overlay"),
                "Lobby entry is a centered overlay, not a layout sibling.");
            Assert.IsNotNull(root.Q<Button>("JoinConfirmButton"), "Join confirm.");
            Assert.IsNotNull(root.Q<VisualElement>("JoinCodeRow"), "Join code slot.");
            Assert.IsNull(root.Q<Label>("JoinCodeHint"), "Join hint is a field placeholder, not a label.");
            Assert.IsNull(root.Q<Label>(className: "mm__friends-add-label"), "Friends hint is a field placeholder.");
            var joinError = root.Q<Label>("MatchEntryErrorLabel");
            var modeError = root.Q<Label>("ModeSelectErrorLabel");
            var friendsError = root.Q<Label>("FriendsErrorLabel");
            Assert.IsNotNull(joinError, "Join error.");
            Assert.IsNotNull(modeError, "Mode error.");
            Assert.IsNotNull(friendsError, "Friends error.");
            Assert.IsTrue(joinError.parent.ClassListContains("mm-join-meta"),
                "Join error overlays join meta, not the mode grid flow.");
            Assert.AreSame(joinError.parent, modeError.parent,
                "Mode error shares join meta overlay host.");
            Assert.AreEqual("AddFriendSection", friendsError.parent.name,
                "Friends error overlays the add-friend block.");
            Assert.IsNull(root.Q<Label>(className: "ui-tagline"), "No slogan label in main menu.");
        }

        [Test]
        public void MainMenuUss_MatchesLauncherGraphiteShell()
        {
            var uss = System.IO.File.ReadAllText(
                "Assets/Game/UI/Runtime/USS/MainMenu.uss");

            StringAssert.Contains(".mm__shell", uss);
            StringAssert.Contains(".mm__header", uss);
            StringAssert.Contains(".mm__body", uss);
            StringAssert.Contains("rgb(16, 18, 17)", uss);
            StringAssert.Contains("rgb(21, 23, 22)", uss);
            StringAssert.Contains("rgb(52, 56, 50)", uss);
            StringAssert.Contains("rgb(185, 154, 98)", uss);
            StringAssert.Contains("border-radius: 0", uss);
            StringAssert.DoesNotContain("linear-gradient", uss);
            StringAssert.DoesNotContain("ui-vignette", uss);
            StringAssert.DoesNotContain("border-left-width: 2px", uss);
            StringAssert.Contains(".mm-join-error", uss);
            StringAssert.Contains("position: absolute", uss);
            StringAssert.Contains("bottom: 100%", uss);
            StringAssert.Contains("justify-content: flex-start", uss);
            StringAssert.Contains("unity-text-element--inner-input-field-component", uss);
            StringAssert.Contains("aspect-ratio: 1", uss);
            StringAssert.Contains(".mm-mode--gap", uss);
            StringAssert.Contains(".mm-mode-dossier", uss);
        }
    }
}
