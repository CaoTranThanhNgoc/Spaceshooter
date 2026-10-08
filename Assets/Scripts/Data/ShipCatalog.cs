using UnityEngine;

namespace SpaceHawk.Data
{
    /// <summary>What one ship of the roster is and does. Every ship is its own purchase with its
    /// own sprite, price, unlock gate, stat multipliers and special perks - not just a reskin.</summary>
    public sealed class ShipSpec
    {
        public int index;
        public int family;                 // 0 Hawk (blue), 1 Viper (red), 2 Phantom (gold)
        public int tier;                   // 0..4 (I..V) - the stage of the family's sprite line
        public int price;                  // Crystals; 0 = the starter
        public int requiresClearedLevels;  // campaign levels that must be cleared before it can be bought

        // Stat multipliers on top of the shared upgrade level (1 = unchanged).
        public float hp = 1f;
        public float dmg = 1f;
        public float rate = 1f;            // shots per second

        // Perks.
        public int volley = 1;             // bullets per shot: 1 single, 2 twin, 3 triple
        public int pierce;                 // extra enemies each bullet passes through
        public float critChance;           // chance for a bullet to deal double damage
        public float damageTaken = 1f;     // < 1 = armour plating
        public float regenPerSecond;       // fraction of max HP restored per second while out of danger
        public float startBarrier;         // seconds of invulnerability at the start of every level
        public bool secondWind;            // survives one fatal hit per level
        public float magnetRadius;         // world units: pickups inside this range fly to the ship
        public float buffDuration = 1f;    // power-up duration multiplier

        public bool HasPerks => volley > 1 || pierce > 0 || critChance > 0f || damageTaken < 1f ||
                                regenPerSecond > 0f || startBarrier > 0f || secondWind || magnetRadius > 0f || buffDuration > 1f;
    }

    /// <summary>The ship roster: three families (the three sprite lines Ship_01/02/03), five tiers
    /// each - fifteen ships. Index = family * TiersPerFamily + tier, which is also what
    /// SaveManager stores as the selected / owned ship.
    ///
    /// Each family has a job - Hawk: fast and handy, Viper: tough, Phantom: hard-hitting - and each
    /// tier up adds to it and costs a lot more, with a campaign gate and the previous tier of the
    /// same family required first, so the best ships are earned over a long stretch of play.</summary>
    public static class ShipCatalog
    {
        public const int FamilyCount = 3;
        public const int TiersPerFamily = 5;
        public const int Count = FamilyCount * TiersPerFamily;

        /// <summary>Shots per second of a ship with rate 1 (PlayerShip fires every 0.25 s).</summary>
        public const float BaseShotsPerSecond = 4f;

        /// <summary>Damage each bullet of a volley deals, as a fraction of a single shot.</summary>
        public static float VolleyDamageFactor(int volley) => volley >= 3 ? 0.55f : volley == 2 ? 0.7f : 1f;

        private static readonly string[] Roman = { "I", "II", "III", "IV", "V" };
        private static readonly ShipSpec[] Ships = Build();

        public static ShipSpec Get(int index) => Ships[Mathf.Clamp(index, 0, Count - 1)];
        public static int IndexOf(int family, int tier) => family * TiersPerFamily + tier;
        public static int FamilyOf(int index) => Mathf.Clamp(index, 0, Count - 1) / TiersPerFamily;
        public static int TierOf(int index) => Mathf.Clamp(index, 0, Count - 1) % TiersPerFamily;
        public static string RomanTier(int index) => Roman[TierOf(index)];

        /// <summary>The ship that must be owned before this one can be bought (-1 for a family's first).</summary>
        public static int PreviousTier(int index) => TierOf(index) > 0 ? index - 1 : -1;

        /// <summary>Best value of each multiplier across the roster - the full length of a stat bar.</summary>
        public static float MaxHp { get; private set; }
        public static float MaxDamage { get; private set; }
        public static float MaxRate { get; private set; }

        private static ShipSpec[] Build()
        {
            // family -> tier -> value
            int[][] price =
            {
                new[] { 0, 1200, 2800, 5500, 9000 },
                new[] { 1000, 2300, 4500, 7500, 11000 },
                new[] { 1500, 3200, 5500, 8500, 12500 },
            };
            int[][] gate =
            {
                new[] { 0, 2, 5, 8, 11 },
                new[] { 3, 4, 7, 10, 12 },
                new[] { 6, 7, 9, 11, 13 },
            };

            ShipSpec[] ships = new ShipSpec[Count];
            for (int f = 0; f < FamilyCount; f++)
            {
                for (int t = 0; t < TiersPerFamily; t++)
                {
                    int i = IndexOf(f, t);
                    ships[i] = new ShipSpec { index = i, family = f, tier = t, price = price[f][t], requiresClearedLevels = gate[f][t] };
                }
            }

            // Hawk - interceptor: ever faster guns, then twin and triple cannons, a tractor beam
            // that scoops pickups in, and longer power-ups.
            Set(ships[0], 1.00f, 1.00f, 1.00f);
            Set(ships[1], 1.00f, 1.00f, 1.10f).magnetRadius = 2.2f;
            Set(ships[2], 1.00f, 1.05f, 1.18f).volley = 2; ships[2].magnetRadius = 2.2f;
            Set(ships[3], 1.05f, 1.08f, 1.26f).volley = 2; ships[3].magnetRadius = 2.2f; ships[3].buffDuration = 1.25f;
            Set(ships[4], 1.10f, 1.12f, 1.35f).volley = 3; ships[4].magnetRadius = 2.8f; ships[4].buffDuration = 1.30f;

            // Viper - bulwark: a lot more hull, armour plating, then self-repair, a start-of-level
            // barrier and finally a second chance.
            Set(ships[5], 1.20f, 0.95f, 0.95f).damageTaken = 0.90f;
            Set(ships[6], 1.30f, 0.95f, 0.95f).damageTaken = 0.85f;
            Set(ships[7], 1.40f, 1.00f, 1.00f).damageTaken = 0.85f; ships[7].regenPerSecond = 0.015f;
            Set(ships[8], 1.50f, 1.00f, 1.00f).damageTaken = 0.80f; ships[8].regenPerSecond = 0.015f; ships[8].startBarrier = 4f;
            Set(ships[9], 1.65f, 1.05f, 1.00f).damageTaken = 0.75f; ships[9].regenPerSecond = 0.020f; ships[9].secondWind = true;

            // Phantom - striker: glass-cannon damage whose bullets pierce, plus critical hits.
            Set(ships[10], 0.90f, 1.15f, 0.95f).pierce = 1;
            Set(ships[11], 0.92f, 1.25f, 1.00f).pierce = 1; ships[11].critChance = 0.10f;
            Set(ships[12], 0.95f, 1.35f, 1.05f).pierce = 2; ships[12].critChance = 0.12f;
            Set(ships[13], 1.00f, 1.45f, 1.10f).pierce = 2; ships[13].critChance = 0.15f; ships[13].startBarrier = 3f;
            Set(ships[14], 1.05f, 1.60f, 1.15f).pierce = 3; ships[14].critChance = 0.20f;

            foreach (ShipSpec s in ships)
            {
                MaxHp = Mathf.Max(MaxHp, s.hp);
                MaxDamage = Mathf.Max(MaxDamage, s.dmg);
                MaxRate = Mathf.Max(MaxRate, s.rate);
            }
            return ships;
        }

        private static ShipSpec Set(ShipSpec s, float hp, float dmg, float rate)
        {
            s.hp = hp;
            s.dmg = dmg;
            s.rate = rate;
            return s;
        }
    }
}
