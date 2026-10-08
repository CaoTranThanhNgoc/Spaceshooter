using UnityEngine;
using UnityEditor;
using SpaceHawk.Gameplay;

namespace SpaceHawk.EditorTools
{
    /// <summary>
    /// Builds the gameplay prefabs: player, the three enemy tiers (Boss_01/02/03 from the boss
    /// pack), their bullets/explosions, and a handful of meteor hazard variants.
    /// </summary>
    public static class GameplayPrefabBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs/Gameplay";
        private const string MeteorFolder = PrefabFolder + "/Meteors";
        private const float PickupWorldSize = 0.95f;

        public class BuiltPrefabs
        {
            public GameObject player;
            public GameObject[] enemyLights;
            public GameObject[] enemyHeavys;
            public GameObject enemyBoss;
            public GameObject playerBullet;
            public GameObject explosion;
            public GameObject[] meteors;
            public GameObject[] powerUps;
        }

        [MenuItem("Tools/Space Hawk/3. Build Gameplay Prefabs")]
        public static BuiltPrefabs Build()
        {
            System.IO.Directory.CreateDirectory(PrefabFolder);
            System.IO.Directory.CreateDirectory(MeteorFolder);
            SceneBuilderUtil.EnsureTag("Player");
            SceneBuilderUtil.EnsureTag("Enemy");

            BuiltPrefabs result = new BuiltPrefabs();
            GameObject impactFlash = BuildImpactFlash();

            result.explosion = BuildExplosion("Explosion", "Ship_Effects/Explosion", "Explosion_1_", 7, 1);
            result.playerBullet = BuildPlayerBullet(impactFlash);
            result.player = BuildPlayer(result.playerBullet);

            GameObject bulletLight = BuildEnemyBullet("EnemyBulletLight", "Boss/Boss_01/Effects_Sprites", "Shot_", 8, 1, 6f, 8, impactFlash, 3.4f, 0.15f);
            GameObject bulletHeavy = BuildEnemyBullet("EnemyBulletHeavy", "Boss/Boss_02/Effects_Sprites", "Laser_", 10, 0, 5.5f, 12, impactFlash, 1.6f, 0.2f);
            GameObject bulletBoss = BuildEnemyBullet("EnemyBulletBoss", "Boss/Boss_03/Effects_Sprites", "Missile_", 10, 0, 4f, 20, impactFlash, 1.5f, 0.22f);

            GameObject explosionLight = BuildExplosion("BossExplosion1", "Boss/Boss_01/Effects_Sprites", "Explosion_", 8, 1);
            GameObject explosionHeavy = BuildExplosion("BossExplosion2", "Boss/Boss_02/Effects_Sprites", "Explosion_", 8, 1);
            GameObject explosionBoss = BuildExplosion("BossExplosion3", "Boss/Boss_03/Effects_Sprites", "Explosion_", 8, 1);

            result.powerUps = BuildPowerUps();

            // Built after the Player prefab already exists (it needs its own saved prefab path),
            // so wire it back onto the player here and mark the asset dirty to persist the change.
            GameObject beamPrefab = BuildPlayerBeam();
            PlayerShip playerShip = result.player.GetComponent<PlayerShip>();
            playerShip.beamPrefab = beamPrefab.GetComponent<PlayerBeam>();
            EditorUtility.SetDirty(result.player);

            // Light/Heavy were rendering at their native pixel size (scale 1), which is much
            // smaller on screen than the Boss (already scaled 1.3x) or the player ship - bumped
            // up so regular enemies read clearly during fast-paced dodging, while Boss keeps its
            // already-correct size.
            //
            // Each tier now builds a second reskin variant from the user-added Spaceships pack
            // (stats stay tier-identical - EnemySpawner assigns hp/speed/fireInterval per-wave
            // regardless of which variant gets picked - so this is purely visual spawn variety).
            //
            // enemy_unit/enemy_mothership are authored at a much smaller native pixel size than
            // the Boss_0X flight frames (116px/230px square vs 1080px wide) - reusing the base
            // tier's scale+halfHeight made them read as tiny circular specks next to the properly
            // sized ships. Both are recalculated here so each reskin's on-screen height matches
            // its tier's actual ship, and halfHeight matches the new sprite's own half-height
            // (bottom rim of the orb) instead of the original ship's.
            GameObject enemyLightBase = BuildEnemy("EnemyLight", UIFactory.LoadSequence("Boss/Boss_01/Boss_Sprites", "Flight_", 10, 0, 3), 0.49f, bulletLight, explosionLight, 1.4f, false, result.powerUps, 1.6f);
            GameObject enemyLightUnit = BuildEnemy("EnemyLightUnit", new[] { UIFactory.LoadShip("Spaceships", "enemy_unit") }, 0.09f, bulletLight, explosionLight, 9.1f, false, result.powerUps, 1.6f);
            result.enemyLights = new[] { enemyLightBase, enemyLightUnit };

            GameObject enemyHeavyBase = BuildEnemy("EnemyHeavy", UIFactory.LoadSequence("Boss/Boss_02/Boss_Sprites", "Flight_", 10, 0, 3), 0.42f, bulletHeavy, explosionHeavy, 1.8f, false, result.powerUps, 0.9f);
            GameObject enemyHeavyMothership = BuildEnemy("EnemyHeavyMothership", new[] { UIFactory.LoadShip("Spaceships", "enemy_mothership") }, 0.18f, bulletHeavy, explosionHeavy, 5.2f, false, result.powerUps, 0.9f);
            result.enemyHeavys = new[] { enemyHeavyBase, enemyHeavyMothership };

            result.enemyBoss = BuildEnemy("EnemyBoss", UIFactory.LoadSequence("Boss/Boss_03/Boss_Sprites", "Flight_", 10, 0, 3), 0.9f, bulletBoss, explosionBoss, 1.3f, true, result.powerUps, 3.2f);

            result.meteors = BuildMeteors(result.explosion);

            AssetDatabase.SaveAssets();
            Debug.Log("[GameplayPrefabBuilder] Gameplay prefabs built.");
            return result;
        }

        private static GameObject BuildExplosion(string name, string folder, string prefix, int count, int startIndex)
        {
            GameObject go = new GameObject(name, typeof(SpriteRenderer));
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            sr.sortingOrder = 10;

            FrameAnimatedFX fx = go.AddComponent<FrameAnimatedFX>();
            fx.frames = UIFactory.LoadSequence(folder, prefix, count, startIndex, 3);
            fx.frameRate = 16f;
            fx.loop = false;
            fx.destroyOnComplete = true;

            return SceneBuilderUtil.SaveAsPrefab(go, $"{PrefabFolder}/{name}.prefab");
        }

        private static GameObject BuildImpactFlash()
        {
            GameObject go = new GameObject("BulletImpact", typeof(SpriteRenderer));
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = UIFactory.LoadShip("Props", "Explosion_01");
            sr.sortingOrder = 8;
            go.AddComponent<ImpactFlash>();

            return SceneBuilderUtil.SaveAsPrefab(go, $"{PrefabFolder}/BulletImpact.prefab");
        }

        private static GameObject BuildPlayerBullet(GameObject impactFlash)
        {
            GameObject go = new GameObject("PlayerBullet", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Bullet));

            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            sr.sortingOrder = 5;

            FrameAnimatedFX fx = go.AddComponent<FrameAnimatedFX>();
            fx.frames = UIFactory.LoadSequence("Ship_Effects/Fire_Shots", "Shot_1_", 6, 1, 3);
            fx.frameRate = 20f;
            fx.loop = true;

            SetupBulletPhysics(go, 9f, 12, impactFlash);
            // Same story as the enemy bullets: the Shot_1_ art is only ~10px wide on a 1080p
            // screen at native size. Visual only - the hit radius stays at its old world size.
            const float playerBulletScale = 1.8f;
            go.transform.localScale = Vector3.one * playerBulletScale;
            go.GetComponent<CircleCollider2D>().radius = 0.12f / playerBulletScale;

            return SceneBuilderUtil.SaveAsPrefab(go, $"{PrefabFolder}/PlayerBullet.prefab");
        }

        /// <summary>visualScale enlarges what the player sees; hitRadiusWorld is the collision
        /// radius in world units and is divided back down by that scale, so making a bullet easier
        /// to spot never makes it harder to dodge (the Boss_01 "Shot_" teardrop, for one, is only
        /// ~9x17 pixels on a 1080p screen at native size).</summary>
        private static GameObject BuildEnemyBullet(string name, string folder, string prefix, int count, int startIndex, float speed, int damage, GameObject impactFlash, float visualScale, float hitRadiusWorld)
        {
            GameObject go = new GameObject(name, typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Bullet));

            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            sr.sortingOrder = 5;

            FrameAnimatedFX fx = go.AddComponent<FrameAnimatedFX>();
            fx.frames = UIFactory.LoadSequence(folder, prefix, count, startIndex, 3);
            fx.frameRate = 20f;
            fx.loop = true;

            SetupBulletPhysics(go, speed, damage, impactFlash);
            go.transform.localScale = Vector3.one * visualScale;
            go.GetComponent<CircleCollider2D>().radius = hitRadiusWorld / visualScale;

            return SceneBuilderUtil.SaveAsPrefab(go, $"{PrefabFolder}/{name}.prefab");
        }

        private static void SetupBulletPhysics(GameObject go, float speed, int damage, GameObject impactFlash)
        {
            Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            CircleCollider2D col = go.GetComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.12f;

            Bullet bullet = go.GetComponent<Bullet>();
            bullet.speed = speed;
            bullet.damage = damage;
            bullet.lifetime = 3f;
            bullet.impactEffectPrefab = impactFlash;
        }

        private static GameObject BuildPlayer(GameObject bulletPrefab)
        {
            GameObject go = new GameObject("Player", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Health), typeof(PlayerShip));
            go.tag = "Player";

            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = UIFactory.LoadShip("Ship_01", "Ship_LVL_1");
            sr.sortingOrder = 2;

            BoxCollider2D col = go.GetComponent<BoxCollider2D>();
            col.isTrigger = true;
            if (sr.sprite != null) col.size = sr.sprite.bounds.size * 0.8f;

            Health health = go.GetComponent<Health>();
            health.maxHp = 100;

            GameObject exhaust = new GameObject("Exhaust", typeof(SpriteRenderer));
            exhaust.transform.SetParent(go.transform, false);
            exhaust.transform.localPosition = new Vector3(0f, -0.62f, 0.1f);
            exhaust.GetComponent<SpriteRenderer>().sortingOrder = 1;
            FrameAnimatedFX exhaustFx = exhaust.AddComponent<FrameAnimatedFX>();
            exhaustFx.frames = UIFactory.LoadSequence("Ship_01/Exhaust", "Exhaust_1_1_", 10, 0, 3);
            exhaustFx.frameRate = 14f;
            exhaustFx.loop = true;

            // Glow ring behind the ship - its sprite is generated at runtime (ProceduralSprites)
            // since it's a plain tintable radial glow, not hand-authored art. Starts hidden;
            // PlayerShip turns it on/colors it per whichever power-up buff is currently active.
            GameObject aura = new GameObject("BuffAura", typeof(SpriteRenderer));
            aura.transform.SetParent(go.transform, false);
            aura.transform.localPosition = new Vector3(0f, 0f, 0.2f);
            aura.transform.localScale = Vector3.one * 1.8f;
            SpriteRenderer auraRenderer = aura.GetComponent<SpriteRenderer>();
            auraRenderer.sortingOrder = 0;
            aura.SetActive(false);

            GameObject firePoint = new GameObject("FirePoint");
            firePoint.transform.SetParent(go.transform, false);
            firePoint.transform.localPosition = new Vector3(0f, 0.62f, 0f);

            PlayerShip ship = go.GetComponent<PlayerShip>();
            ship.bulletPrefab = bulletPrefab.GetComponent<Bullet>();
            ship.firePoint = firePoint.transform;
            ship.bodySprite = sr;
            ship.hulls = UIFactory.LoadAllShipHulls();
            ship.levelSprites = ship.hulls[0].levelSprites;
            ship.fireInterval = 0.25f;
            ship.buffAura = auraRenderer;
            ship.exhaustFx = exhaustFx;

            return SceneBuilderUtil.SaveAsPrefab(go, $"{PrefabFolder}/Player.prefab");
        }

        /// <summary>Continuous Beam power-up weapon - a single persistent trigger collider rather
        /// than Bullet's spawn-and-destroy pattern. Three empty SpriteRenderer children (halo, body,
        /// muzzle flare) are filled in with generated sprites by PlayerBeam.Awake, which also sizes
        /// the beam from its own length/width fields - so tuning those on PlayerShip or here never
        /// needs the art rebuilt. See PlayerBeam's class comment for the look.</summary>
        private static GameObject BuildPlayerBeam()
        {
            // Enemy's own collider carries no Rigidbody2D (see BuildEnemy) - matching the
            // Bullet/PowerUpPickup convention elsewhere in this file, the moving/detecting side
            // needs its own kinematic Rigidbody2D or Unity's 2D physics won't raise trigger
            // events for either collider at all.
            GameObject go = new GameObject("PlayerBeam", typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(PlayerBeam));

            Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            BoxCollider2D col = go.GetComponent<BoxCollider2D>();
            col.isTrigger = true;

            // In front of the enemies (order 2) and pickups (4) but behind enemy bullets (5), so the
            // beam visibly cuts through whatever it is burning.
            SpriteRenderer glowSr = NewBeamLayer(go.transform, "Glow", 3);
            SpriteRenderer coreSr = NewBeamLayer(go.transform, "Core", 4);
            SpriteRenderer startCapSr = NewBeamLayer(go.transform, "StartCap", 4);

            PlayerBeam beam = go.GetComponent<PlayerBeam>();
            beam.glow = glowSr;
            beam.core = coreSr;
            beam.startCap = startCapSr;

            return SceneBuilderUtil.SaveAsPrefab(go, $"{PrefabFolder}/PlayerBeam.prefab");
        }

        private static SpriteRenderer NewBeamLayer(Transform parent, string name, int sortingOrder)
        {
            GameObject layer = new GameObject(name, typeof(SpriteRenderer));
            layer.transform.SetParent(parent, false);
            SpriteRenderer sr = layer.GetComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        /// <summary>Boss_0X ships already face down (nose/mouth at the bottom of the frame) so,
        /// unlike the player ship, they need no 180 degree flip - they're drawn as enemies already.
        /// Takes the flight frames directly (rather than a folder to load) so a reskin variant can
        /// pass a single static sprite instead of a real Flight_000..009 animation sequence -
        /// FrameAnimatedFX already renders a 1-length array as a plain static sprite with no extra
        /// handling needed.</summary>
        private static GameObject BuildEnemy(string name, Sprite[] flightFrames, float halfHeight, GameObject bulletPrefab, GameObject explosionPrefab, float scale, bool isBoss, GameObject[] powerUps, float weaveAmplitude)
        {
            GameObject go = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Health), typeof(Enemy), typeof(FrameAnimatedFX));
            go.tag = "Enemy";
            go.transform.localScale = Vector3.one * scale;

            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            sr.sortingOrder = 2;

            FrameAnimatedFX fx = go.GetComponent<FrameAnimatedFX>();
            fx.frames = flightFrames;
            fx.frameRate = 12f;
            fx.loop = true;

            BoxCollider2D col = go.GetComponent<BoxCollider2D>();
            col.isTrigger = true;
            if (flightFrames.Length > 0) col.size = flightFrames[0].bounds.size * 0.7f;

            Health health = go.GetComponent<Health>();
            health.maxHp = 20;

            GameObject firePoint = new GameObject("FirePoint");
            firePoint.transform.SetParent(go.transform, false);
            firePoint.transform.localPosition = new Vector3(0f, -halfHeight, 0f);

            Enemy enemy = go.GetComponent<Enemy>();
            enemy.bulletPrefab = bulletPrefab.GetComponent<Bullet>();
            enemy.firePoint = firePoint.transform;
            enemy.explosionPrefab = explosionPrefab;
            enemy.despawnY = -6f;
            enemy.isBoss = isBoss;
            enemy.powerUpDropPrefabs = powerUps;
            enemy.weaveAmplitude = weaveAmplitude;

            // A boss holds at hoverY and patrols side to side instead of flying straight through
            // to despawnY - so the only way it ever leaves is by actually being killed, unlike the
            // regular enemies above it which are still fine to just let scroll past.
            if (isBoss)
            {
                enemy.holdPosition = true;
                // Comfortably above the player's own movementBounds ceiling (3.5) so the boss
                // never physically overlaps the player's flyable area, but still well inside the
                // camera's orthographic view (top edge ~5) instead of hugging the screen edge.
                enemy.hoverY = 4.2f;
                enemy.weaveFrequency = 0.4f;
            }

            return SceneBuilderUtil.SaveAsPrefab(go, $"{PrefabFolder}/{name}.prefab");
        }

        private static GameObject[] BuildPowerUps()
        {
            (string folder, string sprite, PowerUpType type, Color tint)[] defs =
            {
                ("Bonus_Items", "HP_Bonus", PowerUpType.Hp, Color.white),
                ("Bonus_Items", "Damage_Bonus", PowerUpType.Damage, Color.white),
                ("Bonus_Items", "Rockets_Bonus", PowerUpType.Rockets, Color.white),
                ("Bonus_Items", "Barrier_Bonus", PowerUpType.Barrier, Color.white),
                ("Bonus_Items", "Armor_Bonus", PowerUpType.Armor, Color.white),
                ("Bonus_Items", "Magnet_Bonus", PowerUpType.Magnet, Color.white),
                ("Bonus_Items", "Enemy_Destroy_Bonus", PowerUpType.Bomb, Color.white),
                ("Bonus_Items", "Enemy_Speed_Debuff", PowerUpType.SlowEnemies, Color.white),
                // No dedicated 5-way-spread icon art exists - reuse the Rockets icon tinted green
                // so the world pickup still reads as a distinct item, matching the HUD buff icon.
                ("Bonus_Items", "Rockets_Bonus", PowerUpType.Spread, new Color(0.5f, 1f, 0.4f, 1f)),
                ("Weapons", "ray_start", PowerUpType.Beam, new Color(1f, 0.22f, 0.16f, 1f)),
            };

            GameObject[] result = new GameObject[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                GameObject go = new GameObject("PowerUp_" + defs[i].type, typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(PowerUpPickup));

                SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = UIFactory.LoadShip(defs[i].folder, defs[i].sprite);
                sr.color = defs[i].tint;
                sr.sortingOrder = 4;
                // Sized from the sprite's own bounds so every pickup reads as the same ~1 world
                // unit (about 100px on a 1080p screen) whatever its source art's native size - at
                // the old fixed 0.9 scale the 300px round badges were only ~45px, far too small to
                // spot (let alone aim for) on a phone.
                float spriteSize = sr.sprite != null ? Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y) : 0.46f;
                go.transform.localScale = Vector3.one * (PickupWorldSize / spriteSize);

                Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;

                CircleCollider2D col = go.GetComponent<CircleCollider2D>();
                col.isTrigger = true;
                if (sr.sprite != null) col.radius = sr.sprite.bounds.size.x * 0.5f;

                PowerUpPickup pickup = go.GetComponent<PowerUpPickup>();
                pickup.type = defs[i].type;

                result[i] = SceneBuilderUtil.SaveAsPrefab(go, $"{PrefabFolder}/PowerUp_{defs[i].type}.prefab");
            }

            return result;
        }

        private static GameObject[] BuildMeteors(GameObject explosionPrefab)
        {
            // Every-other pick out of each pack's own numbered set, same convention already used
            // for the original 10 Meteor_0X sprites - keeps variety high without building all of
            // either pack's near-duplicate frames as separate prefabs.
            (string folder, string name)[] variants =
            {
                ("Meteors", "Meteor_01"), ("Meteors", "Meteor_03"), ("Meteors", "Meteor_05"), ("Meteors", "Meteor_07"), ("Meteors", "Meteor_09"),
                ("Asteroids", "asteroid_01"), ("Asteroids", "asteroid_03"), ("Asteroids", "asteroid_05"), ("Asteroids", "asteroid_07"),
                ("Asteroids", "asteroid_09"), ("Asteroids", "asteroid_11"), ("Asteroids", "asteroid_13"), ("Asteroids", "asteroid_15"),
            };
            GameObject[] result = new GameObject[variants.Length];

            for (int i = 0; i < variants.Length; i++)
            {
                Sprite sprite = UIFactory.LoadShip(variants[i].folder, variants[i].name);

                GameObject go = new GameObject(variants[i].name, typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Health), typeof(Meteor));
                go.tag = "Enemy"; // reuses the player bullet's existing "Enemy" hit-test, not part of wave tracking

                SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = 3;

                Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;

                CircleCollider2D col = go.GetComponent<CircleCollider2D>();
                col.isTrigger = true;
                if (sprite != null) col.radius = sprite.bounds.size.x * 0.4f;

                Health health = go.GetComponent<Health>();
                health.maxHp = 15;

                Meteor meteor = go.GetComponent<Meteor>();
                meteor.explosionPrefab = explosionPrefab;

                result[i] = SceneBuilderUtil.SaveAsPrefab(go, $"{MeteorFolder}/{variants[i].name}.prefab");
            }

            return result;
        }
    }
}
