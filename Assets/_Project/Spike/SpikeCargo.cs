using UnityEngine;

namespace Contraption.Spike
{
    /// <summary>
    /// Fragile cargo for the fun checkpoint. Impact damage above a threshold so that gentle
    /// contact is free and hard landings hurt — the whole point is that the player should feel
    /// a reason to build a *careful* machine rather than a fast one.
    /// </summary>
    public sealed class SpikeCargo : MonoBehaviour
    {
        /// <summary>Below this impact speed nothing happens, so ordinary rolling contact is free.</summary>
        private const float DamageThresholdSpeed = 2.5f;

        /// <summary>Health lost per unit of impact speed above the threshold.</summary>
        private const float DamagePerExcessSpeed = 9f;

        public float Health { get; private set; } = 100f;

        public float MaxHealth => 100f;

        public bool IsDestroyed => Health <= 0f;

        /// <summary>Set by the last impact, purely so the HUD can flash on a hit.</summary>
        public float LastImpactDamage { get; private set; }

        public float TimeOfLastImpact { get; private set; } = -999f;

        private void OnCollisionEnter2D(Collision2D collision)
        {
            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed <= DamageThresholdSpeed)
            {
                return;
            }

            float damage = (impactSpeed - DamageThresholdSpeed) * DamagePerExcessSpeed;
            Health = Mathf.Max(0f, Health - damage);
            LastImpactDamage = damage;
            TimeOfLastImpact = Time.time;
        }
    }
}
