using System.Collections;
using UnityEngine;
using SpaceHawk.Data;

namespace SpaceHawk.Gameplay
{
    public class EnemySpawner : MonoBehaviour
    {
        [Tooltip("One or more visual reskins of the same tier - a random one is picked per spawn, stats come from the wave regardless of which is chosen.")]
        public Enemy[] lightEnemyPrefabs;
        public Enemy[] heavyEnemyPrefabs;
        public Enemy bossEnemyPrefab;
        public Transform[] spawnPoints;

        // Endless mode: a beat on the "WAVE N" banner before anything appears, and a pause after
        // the field is cleared so waves still read as distinct moments.
        private const float EndlessBannerSeconds = 1.8f;
        private const float EndlessBreatherSeconds = 1.2f;
        private const float EndlessWaveTimeCap = 25f;
        private const float EndlessBossTimeCap = 90f;
        private const float EndlessDropChance = 0.4f;

        private int _aliveCount;
        private bool _allSpawned;
        private int _totalSpawned;
        private int _totalKilled;
        private int _totalPoints;
        private Color _explosionTint = Color.white;
        private bool _endless;

        public event System.Action AllEnemiesCleared;
        /// <summary>Fired with the running kill total (real kills only, not despawns) every time it
        /// changes - lets the HUD show live progress toward the level's requiredKillRatio.</summary>
        public event System.Action<int> KillCountChanged;
        /// <summary>Fired with the running points total (what each destroyed enemy was worth, see
        /// SkillScore) every time it changes - drives the HUD score.</summary>
        public event System.Action<int> PointsChanged;
        /// <summary>Endless mode only: fired with the new wave number as each wave begins.</summary>
        public event System.Action<int> WaveStarted;

        /// <summary>Endless mode: the wave currently (or last) in progress; 0 before the first.</summary>
        public int CurrentWave { get; private set; }
        public int TotalKilled => _totalKilled;
        public int TotalPoints => _totalPoints;
        public int AliveCount => _aliveCount;

        /// <summary>Fraction of everything this level spawned that was actually killed, as
        /// opposed to left to despawn off-screen unfought. 1 when nothing has spawned yet, so an
        /// empty level (or one not yet begun) never reads as a failure by default.</summary>
        public float KillRatio => _totalSpawned > 0 ? (float)_totalKilled / _totalSpawned : 1f;

        public void BeginLevel(LevelData level)
        {
            _aliveCount = 0;
            _allSpawned = false;
            _totalSpawned = 0;
            _totalKilled = 0;
            _totalPoints = 0;
            _explosionTint = level.levelTintColor;
            _endless = level.isEndless;
            CurrentWave = 0;

            StopAllCoroutines();
            StartCoroutine(_endless ? EndlessRoutine() : SpawnRoutine(level));
        }

        private IEnumerator SpawnRoutine(LevelData level)
        {
            if (level.waves != null)
            {
                foreach (EnemyWave wave in level.waves)
                {
                    if (wave.delayBeforeWave > 0f) yield return new WaitForSeconds(wave.delayBeforeWave);
                    for (int i = 0; i < wave.count; i++)
                    {
                        SpawnOne(wave);
                        yield return new WaitForSeconds(wave.spawnInterval);
                    }
                }
            }
            _allSpawned = true;
            CheckCleared();
        }

        /// <summary>Never sets _allSpawned, so AllEnemiesCleared never fires: an Endless run has
        /// no victory, it ends only when the player's ship does.</summary>
        private IEnumerator EndlessRoutine()
        {
            while (true)
            {
                CurrentWave++;
                _explosionTint = EndlessWaves.TintFor(CurrentWave);
                WaveStarted?.Invoke(CurrentWave);
                yield return new WaitForSeconds(EndlessBannerSeconds);

                foreach (EnemyWave group in EndlessWaves.Build(CurrentWave))
                {
                    if (group.delayBeforeWave > 0f) yield return new WaitForSeconds(group.delayBeforeWave);
                    for (int i = 0; i < group.count; i++)
                    {
                        SpawnOne(group);
                        yield return new WaitForSeconds(group.spawnInterval);
                    }
                }

                // Next wave starts once the field is (nearly) clear - or after a time cap, so one
                // enemy hanging about can't stall the run. A boss wave waits for the boss itself.
                bool boss = EndlessWaves.IsBossWave(CurrentWave);
                int allowedLeft = boss ? 0 : 2;
                float cap = boss ? EndlessBossTimeCap : EndlessWaveTimeCap;
                float waited = 0f;
                while (_aliveCount > allowedLeft && waited < cap)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }

                yield return new WaitForSeconds(EndlessBreatherSeconds);
            }
        }

        private static Enemy PickRandom(Enemy[] prefabs)
        {
            return prefabs != null && prefabs.Length > 0 ? prefabs[Random.Range(0, prefabs.Length)] : null;
        }

        private void SpawnOne(EnemyWave wave)
        {
            Enemy prefab = wave.kind switch
            {
                EnemyKind.Light => PickRandom(lightEnemyPrefabs),
                EnemyKind.Heavy => PickRandom(heavyEnemyPrefabs),
                EnemyKind.Boss => bossEnemyPrefab,
                _ => PickRandom(lightEnemyPrefabs)
            };
            if (prefab == null) return;

            Vector3 pos = spawnPoints != null && spawnPoints.Length > 0
                ? spawnPoints[Random.Range(0, spawnPoints.Length)].position
                : transform.position;

            Enemy e = Instantiate(prefab, pos, Quaternion.identity);
            e.speed = wave.speed;
            e.fireInterval = wave.fireInterval;
            e.bulletDamage = wave.bulletDamage;
            e.explosionTint = _explosionTint;
            e.pointsValue = SkillScore.EnemyPoints(wave.kind);
            if (_endless) e.powerUpDropChance = EndlessDropChance;
            e.GetComponent<Health>().SetMax(wave.hp);
            e.Removed += HandleEnemyRemoved;

            _aliveCount++;
            _totalSpawned++;
        }

        private void HandleEnemyRemoved(Enemy e)
        {
            e.Removed -= HandleEnemyRemoved;
            _aliveCount--;
            if (e.WasKilled)
            {
                _totalKilled++;
                _totalPoints += e.pointsValue;
                KillCountChanged?.Invoke(_totalKilled);
                PointsChanged?.Invoke(_totalPoints);
            }
            CheckCleared();
        }

        private void CheckCleared()
        {
            if (_allSpawned && _aliveCount <= 0)
                AllEnemiesCleared?.Invoke();
        }
    }
}
