using System;
using Assets.Scripts.Menu.Shop;

namespace Megabonk.Multiplayer
{
    internal static class RemoteStatScope
    {
        [ThreadStatic]
        private static bool _active;

        [ThreadStatic]
        private static StatSnapshot _snapshot;

        public static bool IsActive => _active;

        private struct Scope : IDisposable
        {
            private readonly bool _previous;
            private readonly StatSnapshot _previousSnapshot;

            public Scope(bool previous, StatSnapshot previousSnapshot)
            {
                _previous = previous;
                _previousSnapshot = previousSnapshot;
            }

            public void Dispose()
            {
                _active = _previous;
                _snapshot = _previousSnapshot;
            }
        }

        public static IDisposable Enter(StatSnapshot snapshot)
        {
            var previousSnapshot = _snapshot;
            var previous = Push(snapshot);
            return new Scope(previous, previousSnapshot);
        }

        private static bool Push(StatSnapshot snapshot)
        {
            var previous = _active;
            _snapshot = snapshot ?? StatSnapshot.Empty;
            _active = true;
            return previous;
        }

        public static float GetFallback(EStat stat)
        {
            if (_snapshot != null && _snapshot.TryGet(stat, out var value))
                return value;

            return GetDefault(stat);
        }

        public static float GetFallback(EStat stat, StatSnapshot snapshot)
        {
            if (snapshot != null && snapshot.TryGet(stat, out var value))
                return value;

            return GetDefault(stat);
        }

        private static float GetDefault(EStat stat)
        {
            switch (stat)
            {
                case EStat.MaxHealth:
                    return 100f;
                case EStat.HealthRegen:
                case EStat.Shield:
                case EStat.Thorns:
                case EStat.Armor:
                case EStat.Evasion:
                case EStat.Evolve:
                case EStat.DamageReductionMultiplier:
                case EStat.DamageCooldownMultiplier:
                case EStat.Projectiles:
                case EStat.Lifesteal:
                case EStat.CritChance:
                case EStat.CritDamage:
                case EStat.FireDamage:
                case EStat.IceDamage:
                case EStat.LightningDamage:
                case EStat.EliteDamageMultiplier:
                case EStat.KnockbackMultiplier:
                case EStat.JumpHeight:
                case EStat.FallDamageReduction:
                case EStat.Slam:
                case EStat.PickupRange:
                case EStat.Luck:
                case EStat.Holiness:
                case EStat.Wickedness:
                case EStat.Difficulty:
                    return 0f;
                case EStat.SizeMultiplier:
                case EStat.DurationMultiplier:
                case EStat.ProjectileSpeedMultiplier:
                case EStat.DamageMultiplier:
                case EStat.EffectDurationMultiplier:
                case EStat.AttackSpeed:
                case EStat.MoveSpeedMultiplier:
                case EStat.GoldIncreaseMultiplier:
                case EStat.XpIncreaseMultiplier:
                case EStat.ChestIncreaseMultiplier:
                case EStat.ChestPriceMultiplier:
                case EStat.ShopPriceReduction:
                    return 1f;
                default:
                    return 0f;
            }
        }
    }
}
