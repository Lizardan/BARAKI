using Game.Gameplay.Match;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// MVP-N01 scaffold: server-only bridge between NGO and <see cref="MatchRuntime"/>.
    /// Match simulation remains in pure C# <see cref="MatchController"/> until full replication lands.
    /// </summary>
    public sealed class MatchNetworkAuthority : NetworkBehaviour
    {
        [SerializeField] private MatchRuntime _matchRuntime;

        public override void OnNetworkSpawn()
        {
            if (_matchRuntime == null)
            {
                _matchRuntime = FindAnyObjectByType<MatchRuntime>();
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            // MatchRuntime.Update already ticks MatchController locally for offline MVP.
            // When NGO session is active, move Tick here and gate MatchRuntime.Update to !IsServer.
        }
    }
}
