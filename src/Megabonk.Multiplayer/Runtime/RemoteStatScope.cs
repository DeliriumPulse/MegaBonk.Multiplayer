using System;
using Assets.Scripts.Menu.Shop;

namespace Megabonk.Multiplayer
{
    internal static class RemoteStatScope
    {
        [ThreadStatic]
        private static bool _active;

        public static bool IsActive => _active;

        private struct Scope : IDisposable
        {
            private readonly bool _previous;

            public Scope(bool previous) => _previous = previous;

            public void Dispose() => _active = _previous;
        }

        public static IDisposable Enter() => new Scope(Push());

        private static bool Push()
        {
            var previous = _active;
            _active = true;
            return previous;
        }

        public static float GetFallback(EStat stat)
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
