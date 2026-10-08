using System;
using UnityEngine;

namespace SpaceHawk.Gameplay
{
    public class Health : MonoBehaviour
    {
        public int maxHp = 100;
        public int CurrentHp { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsInvincible { get; private set; }
        public float DamageReductionMultiplier { get; private set; } = 1f;

        /// <summary>Hit points actually lost since this entity spawned (never more than it had left,
        /// ignoring blocked hits and healing) - the skill measure behind a level's health bonus.</summary>
        public int TotalDamageTaken { get; private set; }

        /// <summary>Remaining HP as 0..1, raised whenever damage is taken or healed.</summary>
        public event Action<float> DamagedPercent;
        public event Action Died;

        private void Awake()
        {
            CurrentHp = maxHp;
        }

        public void SetMax(int value, bool refill = true)
        {
            maxHp = value;
            if (refill) CurrentHp = maxHp;
        }

        public void TakeDamage(int amount)
        {
            if (IsDead || IsInvincible || amount <= 0) return;
            int reduced = Mathf.Max(1, Mathf.RoundToInt(amount * DamageReductionMultiplier));
            TotalDamageTaken += Mathf.Min(reduced, CurrentHp);
            CurrentHp = Mathf.Max(0, CurrentHp - reduced);
            DamagedPercent?.Invoke(maxHp > 0 ? (float)CurrentHp / maxHp : 0f);
            if (CurrentHp <= 0)
            {
                IsDead = true;
                Died?.Invoke();
            }
        }

        public void Heal(int amount)
        {
            if (IsDead || amount <= 0) return;
            CurrentHp = Mathf.Min(maxHp, CurrentHp + amount);
            DamagedPercent?.Invoke(maxHp > 0 ? (float)CurrentHp / maxHp : 0f);
        }

        /// <summary>Brings a dead entity back with the given fraction of its max HP (never less
        /// than 1). No-op while alive.</summary>
        public void Revive(float hpFraction)
        {
            if (!IsDead) return;
            IsDead = false;
            CurrentHp = Mathf.Clamp(Mathf.RoundToInt(maxHp * hpFraction), 1, maxHp);
            DamagedPercent?.Invoke(maxHp > 0 ? (float)CurrentHp / maxHp : 0f);
        }

        public void SetInvincible(bool value)
        {
            IsInvincible = value;
        }

        public void SetDamageReduction(float multiplier)
        {
            DamageReductionMultiplier = multiplier;
        }
    }
}
