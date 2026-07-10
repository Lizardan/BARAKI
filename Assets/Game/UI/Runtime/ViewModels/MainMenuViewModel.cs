using UniRx;

namespace Game.UI.ViewModels
{
    public sealed class MainMenuViewModel
    {
        public ReactiveProperty<string> Title { get; } = new("BARAKI");
        public ReactiveCommand PlayCommand { get; } = new();
        public ReactiveCommand QuitCommand { get; } = new();
    }
}
