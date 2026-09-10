using Game.Gameplay.Match;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Мост между UI/презентацией и сетевым состоянием арены.
    /// В сети состояние живёт в <c>NetworkVariable</c> на <see cref="MatchNetworkAuthority"/>;
    /// офлайн (или до спавна authority) — в статическом поле этого класса.
    /// </summary>
    public static class ArenaNetworkBridge
    {
        static ArenaNetState _offline = ArenaNetState.Empty;

        /// <summary>Текущее состояние арены (актуально и на хосте, и на клиентах).</summary>
        public static ArenaNetState Current
        {
            get
            {
                var authority = MatchNetworkAuthority.Instance;
                return authority != null && authority.IsSpawned
                    ? authority.ArenaNetworkState.Value
                    : _offline;
            }
        }

        /// <summary>True, если этот пир управляет ареной (хоcт или офлайн).</summary>
        public static bool IsAuthority
        {
            get
            {
                var authority = MatchNetworkAuthority.Instance;
                return authority == null || !authority.IsSpawned || authority.IsServer;
            }
        }

        /// <summary>Публикация состояния: на хосте пишется в NetworkVariable, офлайн — в статику.</summary>
        public static void Publish(in ArenaNetState state)
        {
            // Клиент состояние не публикует — только читает реплицированное.
            if (!IsAuthority)
            {
                return;
            }

            var authority = MatchNetworkAuthority.Instance;
            if (authority != null && authority.IsSpawned)
            {
                authority.SetArenaState(state);
                return;
            }

            _offline = state;
        }

        /// <summary>
        /// Запрос выбора от локального игрока. В сети уходит ServerRpc на хост,
        /// офлайн применяется сразу к локальному <see cref="ArenaDirector"/>.
        /// </summary>
        /// <param name="heroSlot">1..3 — слот героя; 0 — титан.</param>
        /// <param name="challengerTarget">Соперник для нечётного игрока; -1 если не применимо.</param>
        public static void RequestPick(int heroSlot, int challengerTarget)
        {
            var authority = MatchNetworkAuthority.Instance;
            if (authority != null && authority.IsSpawned)
            {
                if (authority.IsServer)
                {
                    authority.ApplyArenaPick(ResolveLocalSlot(), heroSlot, challengerTarget);
                }
                else
                {
                    authority.SubmitArenaPickServerRpc(heroSlot, challengerTarget);
                }

                return;
            }

            ArenaDirector.Current?.SubmitPick(ResolveLocalSlot(), heroSlot, challengerTarget);
        }

        /// <summary>Обнулить офлайн-состояние (возврат в лобби, тесты, конец матча).</summary>
        public static void PublishOffline(in ArenaNetState state) => _offline = state;

        /// <summary>Локальный слот: из сессии, из лобби-стейта или 0 в офлайне.</summary>
        public static int ResolveLocalSlot()
        {
            if (MatchNetworkSession.LocalSlot >= 0)
            {
                return MatchNetworkSession.LocalSlot;
            }

            var authority = MatchNetworkAuthority.Instance;
            if (authority != null && authority.IsSpawned)
            {
                return authority.ResolveLocalSlot();
            }

            return 0;
        }
    }
}
