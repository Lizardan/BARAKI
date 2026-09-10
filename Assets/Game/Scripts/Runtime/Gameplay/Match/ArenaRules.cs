using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Фазы арены (реплицируются на клиенты как <c>byte</c>).</summary>
    public enum ArenaPhase : byte
    {
        /// <summary>Матч идёт, тикает обратный отсчёт до следующей арены.</summary>
        Idle = 0,

        /// <summary>Выбор героя участниками (или авто-титан на последней арене).</summary>
        Pick = 1,

        /// <summary>Презентация пары «кто против кого» перед боем.</summary>
        Intro = 2,

        /// <summary>Дуэль.</summary>
        Duel = 3,

        /// <summary>Итог дуэли: победитель и награда.</summary>
        Result = 4,
    }

    /// <summary>
    /// Численные правила арены. Арена — пауза матча, во время которой игроки,
    /// отсортированные по золоту, выставляют по одному бойцу и дерутся 1v1.
    /// </summary>
    public static class ArenaRules
    {
        /// <summary>Сколько игровых секунд между аренами (15 минут).</summary>
        public const float IntervalSeconds = 900f;

        /// <summary>Сколько арен за матч. Последняя — титаны.</summary>
        public const int TotalArenas = 4;

        /// <summary>Номер арены, на которой вместо героев выступают титаны.</summary>
        public const int TitanArenaIndex = 4;

        /// <summary>Секунд на выбор героя.</summary>
        public const float PickSeconds = 30f;

        /// <summary>Секунд на презентацию пары перед дуэлью.</summary>
        public const float IntroSeconds = 5f;

        /// <summary>Секунд на показ итога дуэли.</summary>
        public const float ResultSeconds = 3f;

        /// <summary>Страховка: дуэль принудительно завершается, если никто не умирает.</summary>
        public const float MaxDuelSeconds = 120f;

        /// <summary>Базовая награда победителю дуэли; умножается на номер арены.</summary>
        public const int RewardGoldStep = 1000;

        /// <summary>Слотов героев в ростере (совпадает с <see cref="HeroRules.MaxHeroSlots"/>).</summary>
        public const int HeroSlots = 3;

        /// <summary>Максимум игроков в матче — размер упакованных масок.</summary>
        public const int MaxSlots = 5;

        /// <summary>Бит в маске «использованных героев»: <c>slot * 4 + heroSlot</c>.</summary>
        public const int UsedHeroKeyStride = 4;

        /// <summary>Значение пика «титан» в 3-битном поле выбора игрока.</summary>
        public const int PickValueTitan = 4;

        /// <summary>Бит на игрока в поле пиков.</summary>
        public const int PickBits = 3;

        /// <summary>Центр арены в мировых координатах — далеко от игровой карты.</summary>
        public static readonly Vector3 Center = new(0f, 0f, -4000f);

        /// <summary>Радиус круга-арены.</summary>
        public const float Radius = 26f;

        /// <summary>Насколько далеко от центра арены может отойти камера панорамированием.</summary>
        public const float PanBoundsRadius = Radius + 16f;

        /// <summary>Насколько далеко от центра стоят бойцы на старте дуэли.</summary>
        public const float SpawnOffset = 9f;

        /// <summary>Высота платформы арены.</summary>
        public const float PlatformThickness = 1f;

        /// <summary>Награда победителю дуэли на арене <paramref name="arenaIndex"/> (1000/2000/3000/4000).</summary>
        public static int RewardGold(int arenaIndex) => RewardGoldStep * Mathf.Max(1, arenaIndex);

        /// <summary>Ключ бита «герой уже выступал»: <c>slot * 4 + heroSlot</c>.</summary>
        public static int UsedHeroKey(int slot, int heroSlot) => slot * UsedHeroKeyStride + heroSlot;

        /// <summary>Арена <paramref name="arenaIndex"/> — титанская (выбор героя отсутствует).</summary>
        public static bool IsTitanArena(int arenaIndex) => arenaIndex >= TitanArenaIndex;

        #region Упаковка

        /// <summary>Прочитать 3-битное поле выбора игрока <paramref name="slot"/>.</summary>
        public static int ReadPick(int packed, int slot)
        {
            if (slot < 0 || slot >= MaxSlots)
            {
                return 0;
            }

            return (packed >> (slot * PickBits)) & 0x7;
        }

        /// <summary>Записать 3-битное поле выбора игрока <paramref name="slot"/>.</summary>
        public static int WritePick(int packed, int slot, int value)
        {
            if (slot < 0 || slot >= MaxSlots)
            {
                return packed;
            }

            var shift = slot * PickBits;
            packed &= ~(0x7 << shift);
            packed |= (value & 0x7) << shift;
            return packed;
        }

        public static bool HasMask(byte mask, int slot) =>
            slot >= 0 && slot < MaxSlots && (mask & (1 << slot)) != 0;

        public static byte SetMask(byte mask, int slot, bool value)
        {
            if (slot < 0 || slot >= MaxSlots)
            {
                return mask;
            }

            return value ? (byte)(mask | (1 << slot)) : (byte)(mask & ~(1 << slot));
        }

        public static bool HasUsedHero(uint mask, int slot, int heroSlot)
        {
            var key = UsedHeroKey(slot, heroSlot);
            return key >= 0 && key < 32 && (mask & (1u << key)) != 0u;
        }

        public static uint MarkUsedHero(uint mask, int slot, int heroSlot)
        {
            var key = UsedHeroKey(slot, heroSlot);
            return key is >= 0 and < 32 ? mask | (1u << key) : mask;
        }

        #endregion
    }
}
