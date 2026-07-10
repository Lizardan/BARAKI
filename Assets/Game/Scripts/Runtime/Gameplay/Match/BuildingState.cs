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
        public float MaxHp { get; }
        public float Armor { get; }
        public float CurrentHp { get; private set; }

        public bool IsRuins => CurrentHp <= 0f;
        public bool IsIntact => !IsRuins;

        public bool ApplyDamage(float rawDamage)
        {
            if (IsRuins || rawDamage <= 0f)
            {
                return false;
            }

            CurrentHp = Mathf.Max(0f, CurrentHp - rawDamage);
            return IsRuins;
        }
    }
}
