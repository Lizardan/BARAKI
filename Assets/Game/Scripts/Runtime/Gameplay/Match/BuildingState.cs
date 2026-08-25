using UnityEngine;

namespace Game.Gameplay.Match
{
    public sealed class BuildingState
    {
        public BuildingState(
            int instanceId,
            int ownerSlot,
            string buildingId,
            Vector3 worldPosition,
            float maxHp,
            float armor)
        {
            InstanceId = instanceId;
            OwnerSlot = ownerSlot;
            BuildingId = buildingId;
            WorldPosition = worldPosition;
            MaxHp = maxHp;
            Armor = armor;
            CurrentHp = maxHp;
        }

        public int InstanceId { get; }
        public int OwnerSlot { get; }
        public string BuildingId { get; }
        public Vector3 WorldPosition { get; }
        public float MaxHp { get; private set; }
        public float Armor { get; }
        public float CurrentHp { get; private set; }

        public bool IsRuins => CurrentHp <= 0f;
        public bool IsIntact => !IsRuins;

        /// <summary>
        /// Raises max HP; current HP grows by the same delta (RTS level-up heal of the bonus).
        /// </summary>
        public void SetMaxHp(float newMaxHp) => SetMaxHp(newMaxHp, scaleCurrentProportionally: false);

        /// <summary>
        /// Raises max HP. With <paramref name="scaleCurrentProportionally"/> the current HP is
        /// rescaled by the same ratio (retroactive picks such as Stone Masonry); otherwise the
        /// current HP grows by the delta (level-up heal).
        /// </summary>
        public void SetMaxHp(float newMaxHp, bool scaleCurrentProportionally)
        {
            newMaxHp = Mathf.Max(1f, newMaxHp);
            if (scaleCurrentProportionally && MaxHp > 0f)
            {
                var ratio = newMaxHp / MaxHp;
                MaxHp = newMaxHp;
                CurrentHp = Mathf.Clamp(CurrentHp * ratio, 0f, MaxHp);
                return;
            }

            var delta = newMaxHp - MaxHp;
            MaxHp = newMaxHp;
            if (delta > 0f && !IsRuins)
            {
                CurrentHp = Mathf.Min(MaxHp, CurrentHp + delta);
            }
            else
            {
                CurrentHp = Mathf.Clamp(CurrentHp, 0f, MaxHp);
            }
        }

        public bool ApplyDamage(float rawDamage)
        {
            if (IsRuins || rawDamage <= 0f)
            {
                return false;
            }

            CurrentHp = Mathf.Max(0f, CurrentHp - rawDamage);
            return IsRuins;
        }

        /// <summary>Host snapshot apply — sets HP without firing destroy events.</summary>
        public void SetAuthoritativeHp(float hp)
        {
            CurrentHp = Mathf.Clamp(hp, 0f, MaxHp);
        }
    }
}
