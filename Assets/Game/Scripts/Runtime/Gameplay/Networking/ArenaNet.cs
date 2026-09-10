using Game.Gameplay.Match;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>Один боец дуэли — реплицируется каждый сетевой тик.</summary>
    public struct ArenaFighterNet : INetworkSerializable
    {
        /// <summary>Слот владельца, -1 — боец отсутствует.</summary>
        public int Slot;

        /// <summary>Слот героя 1..3; 0 — титан.</summary>
        public int HeroSlot;

        public float Hp;
        public float MaxHp;
        public float X;
        public float Z;
        public float FacingDegrees;

        /// <summary><see cref="UnitBehaviorState"/> бойца (для анимации).</summary>
        public byte Behavior;

        /// <summary>Инкрементируется при каждой атаке — триггер анимации удара.</summary>
        public byte AttackSwing;

        public byte IsDead;

        public bool IsPresent => Slot >= 0;

        public Vector3 Position => new(X, ArenaRules.Center.y, Z);

        public static ArenaFighterNet Empty => new() { Slot = -1, HeroSlot = 0 };

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Slot);
            serializer.SerializeValue(ref HeroSlot);
            serializer.SerializeValue(ref Hp);
            serializer.SerializeValue(ref MaxHp);
            serializer.SerializeValue(ref X);
            serializer.SerializeValue(ref Z);
            serializer.SerializeValue(ref FacingDegrees);
            serializer.SerializeValue(ref Behavior);
            serializer.SerializeValue(ref AttackSwing);
            serializer.SerializeValue(ref IsDead);
        }
    }

    /// <summary>
    /// Полное состояние арены. Одна структура в одной <c>NetworkVariable</c> —
    /// минимум точек встраивания в <see cref="MatchNetworkAuthority"/> и никаких
    /// массивов в wire-формате.
    /// </summary>
    public struct ArenaNetState : INetworkSerializable
    {
        public byte Phase;
        public byte Index;
        public float Timer;
        public int RewardGold;

        /// <summary>Выборы игроков: по 3 бита на слот (1..3 — герой, 4 — титан).</summary>
        public int Picks;

        public byte Participants;
        public byte Submitted;
        public byte Fought;

        public int DuelOrdinal;
        public int DuelCount;

        public int PairASlot;
        public int PairAHero;
        public int PairBSlot;
        public int PairBHero;

        /// <summary>Слот нечётного игрока, выбирающий соперника; -1 если пары полные.</summary>
        public int ChallengerSlot;

        /// <summary>Выбранный соперник нечётного игрока; -1 если ещё не выбран.</summary>
        public int ChallengerTarget;

        /// <summary>0 — победил боец A, 1 — боец B, -1 — дуэль не завершена.</summary>
        public int Winner;

        /// <summary>Биты «герой уже выступал на прошлых аренах».</summary>
        public uint UsedHeroMask;

        public ArenaFighterNet A;
        public ArenaFighterNet B;

        public ArenaPhase PhaseValue => (ArenaPhase)Phase;

        public bool IsActive => Phase != (byte)ArenaPhase.Idle;

        public static ArenaNetState Empty => new()
        {
            Phase = (byte)ArenaPhase.Idle,
            Index = 0,
            Timer = ArenaRules.IntervalSeconds,
            RewardGold = ArenaRules.RewardGoldStep,
            PairASlot = -1,
            PairAHero = 0,
            PairBSlot = -1,
            PairBHero = 0,
            ChallengerSlot = -1,
            ChallengerTarget = -1,
            Winner = -1,
            A = ArenaFighterNet.Empty,
            B = ArenaFighterNet.Empty,
        };

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Phase);
            serializer.SerializeValue(ref Index);
            serializer.SerializeValue(ref Timer);
            serializer.SerializeValue(ref RewardGold);
            serializer.SerializeValue(ref Picks);
            serializer.SerializeValue(ref Participants);
            serializer.SerializeValue(ref Submitted);
            serializer.SerializeValue(ref Fought);
            serializer.SerializeValue(ref DuelOrdinal);
            serializer.SerializeValue(ref DuelCount);
            serializer.SerializeValue(ref PairASlot);
            serializer.SerializeValue(ref PairAHero);
            serializer.SerializeValue(ref PairBSlot);
            serializer.SerializeValue(ref PairBHero);
            serializer.SerializeValue(ref ChallengerSlot);
            serializer.SerializeValue(ref ChallengerTarget);
            serializer.SerializeValue(ref Winner);
            serializer.SerializeValue(ref UsedHeroMask);
            serializer.SerializeValue(ref A);
            serializer.SerializeValue(ref B);
        }
    }
}
