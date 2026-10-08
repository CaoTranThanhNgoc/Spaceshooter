using System.Collections.Generic;
using UnityEngine;
using SpaceHawk.Core;

namespace SpaceHawk.Gameplay
{
    [RequireComponent(typeof(Health))]
    public class Enemy : MonoBehaviour
    {
        public float speed = 2f;
        public float fireInterval = 2f;
        [Tooltip("0 = keep the bullet prefab's own default damage.")]
        public int bulletDamage = 0;
        public Bullet bulletPrefab;
        public Transform firePoint;
        public GameObject explosionPrefab;
        [Tooltip("Stamped per-spawn by EnemySpawner from the current level's LevelData.levelTintColor - tints this enemy's death explosion so each level reads as visually distinct.")]
        public Color explosionTint = Color.white;
        public float despawnY = -7f;
        public bool isBoss = false;
        [Tooltip("Leaderboard points for destroying this enemy - stamped per-spawn by EnemySpawner from its kind (see SkillScore).")]
        public int pointsValue = 10;
        public GameObject[] powerUpDropPrefabs;
        [Range(0f, 1f)] public float powerUpDropChance = 0.3f;
        [Tooltip("Side-to-side drift amplitude in world units. 0 = straight down.")]
        public float weaveAmplitude = 0f;
        public float weaveFrequency = 1.5f;
        [Tooltip("Boss-only: fire this many bullets in a spread instead of a single shot straight down.")]
        public int spreadShotCount = 3;
        public float spreadShotAngle = 18f;
        [Tooltip("Boss-only: stop descending at hoverY and patrol (weaving on X, still firing) " +
                 "instead of continuing down to despawnY - the despawn check is skipped entirely " +
                 "while true, so this enemy can only ever leave by being killed.")]
        public bool holdPosition = false;
        public float hoverY = 3f;

        /// <summary>True once RemoveSelf ran via an actual kill (Health.Died), as opposed to
        /// drifting past despawnY unfought - EnemySpawner uses this to tell real kills apart from
        /// enemies that were simply allowed to leave the screen.</summary>
        public bool WasKilled { get; private set; }

        private Health _health;
        private float _fireTimer;
        private float _weaveTime;
        private float _spawnX;
        private bool _removed;

        /// <summary>Fired exactly once, whether the enemy was killed or despawned off-screen.
        /// EnemySpawner uses this to know when a wave is fully cleared.</summary>
        public event System.Action<Enemy> Removed;

        /// <summary>Fired whenever a Boss-kind enemy spawns, so HUDController can show a boss
        /// health bar without EnemySpawner or Enemy needing to know the HUD exists.</summary>
        public static event System.Action<Enemy> BossSpawned;

        // Every enemy currently alive, so the Bomb power-up can hit them all - and a shared
        // "everyone is slowed" window (Slow Enemies power-up) that every instance reads from
        // instead of each needing its own timer/original-speed bookkeeping.
        private static readonly List<Enemy> s_active = new List<Enemy>();
        private static float s_slowUntil = -1f;
        private const float SlowFactor = 0.4f;

        public static void Detonate(int damage)
        {
            Enemy[] snapshot = s_active.ToArray();
            foreach (Enemy e in snapshot)
            {
                if (e != null) e._health.TakeDamage(damage);
            }
        }

        public static void ApplyGlobalSlow(float duration)
        {
            s_slowUntil = Time.time + duration;
        }

        private static bool GlobalSlowActive => Time.time < s_slowUntil;

        private void Awake()
        {
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            _health.Died += HandleKilled;
            _fireTimer = Random.Range(0f, fireInterval);
            _weaveTime = Random.Range(0f, 10f); // desync enemies spawned in the same frame
            _spawnX = transform.position.x;
            s_active.Add(this);
            if (isBoss) BossSpawned?.Invoke(this);
        }

        private void OnDisable()
        {
            _health.Died -= HandleKilled;
            s_active.Remove(this);
        }

        private void Update()
        {
            float slow = GlobalSlowActive ? SlowFactor : 1f;

            Vector3 pos = transform.position;
            bool holding = holdPosition && pos.y <= hoverY;
            pos.y = holding ? hoverY : pos.y - speed * slow * Time.deltaTime;

            if (weaveAmplitude > 0f)
            {
                _weaveTime += Time.deltaTime;
                pos.x = _spawnX + Mathf.Sin(_weaveTime * weaveFrequency) * weaveAmplitude;
            }

            transform.position = pos;

            if (bulletPrefab != null)
            {
                _fireTimer += Time.deltaTime * slow;
                if (_fireTimer >= fireInterval)
                {
                    _fireTimer = 0f;
                    Fire();
                }
            }

            if (!holdPosition && transform.position.y < despawnY)
            {
                RemoveSelf();
            }
        }

        private void Fire()
        {
            Vector3 pos = firePoint != null ? firePoint.position : transform.position;

            if (isBoss && spreadShotCount > 1)
            {
                float startAngle = -spreadShotAngle * (spreadShotCount - 1) / 2f;
                for (int i = 0; i < spreadShotCount; i++)
                {
                    float angle = startAngle + i * spreadShotAngle;
                    Vector2 dir = Quaternion.Euler(0f, 0f, angle) * Vector2.down;
                    FireBullet(pos, dir);
                }
            }
            else
            {
                FireBullet(pos, Vector2.down);
            }
        }

        private void FireBullet(Vector3 pos, Vector2 direction)
        {
            Bullet b = Instantiate(bulletPrefab, pos, Quaternion.identity);
            b.Init(BulletOwner.Enemy, direction);
            if (bulletDamage > 0) b.damage = bulletDamage;
        }

        private void HandleKilled()
        {
            WasKilled = true;
            if (explosionPrefab != null)
            {
                GameObject fx = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
                SpriteRenderer fxRenderer = fx.GetComponent<SpriteRenderer>();
                if (fxRenderer != null) fxRenderer.color = explosionTint;
            }
            if (CameraShake.Instance != null)
                CameraShake.Instance.Shake(isBoss ? 0.4f : 0.12f, isBoss ? 0.35f : 0.12f);
            AudioManager.Play(GameAudio.Explosion, isBoss ? 0.8f : 0.45f, isBoss ? 0.7f : Random.Range(0.9f, 1.15f));
            // Reserved for the boss - vibrating on every regular kill would feel spammy given how
            // often those happen.
            if (isBoss) HapticFeedback.Play();
            SaveManager.AddEnemyKill();
            TryDropPowerUp();
            RemoveSelf();
        }

        private void TryDropPowerUp()
        {
            if (powerUpDropPrefabs == null || powerUpDropPrefabs.Length == 0) return;
            if (Random.value > powerUpDropChance) return;
            GameObject prefab = powerUpDropPrefabs[Random.Range(0, powerUpDropPrefabs.Length)];
            if (prefab != null) Instantiate(prefab, transform.position, Quaternion.identity);
        }

        private void RemoveSelf()
        {
            if (_removed) return;
            _removed = true;
            Removed?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
