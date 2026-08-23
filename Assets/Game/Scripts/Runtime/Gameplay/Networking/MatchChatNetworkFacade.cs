using System;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Bridge so Game.UI can send/receive match chat without referencing NetworkBehaviour.
    /// </summary>
    public static class MatchChatNetworkFacade
    {
        static Action<string> s_send;

        public static event Action<string, string> MessageReceived;

        public static bool HasBridge => s_send != null;

        public static void Register(Action<string> send) => s_send = send;

        public static void Unregister(Action<string> send)
        {
            if (ReferenceEquals(s_send, send))
            {
                s_send = null;
            }
        }

        public static void Send(string text) => s_send?.Invoke(text);

        public static void RaiseReceived(string displayName, string message) =>
            MessageReceived?.Invoke(displayName, message);
    }
}
