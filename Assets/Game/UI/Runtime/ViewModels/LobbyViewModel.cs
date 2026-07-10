using UniRx;

namespace Game.UI.ViewModels
{
    public sealed class LobbyViewModel
    {
        public ReactiveCommand StartMatchCommand { get; } = new();
        public ReactiveCommand BackCommand { get; } = new();
    }
}
