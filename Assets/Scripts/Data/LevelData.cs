using System;
using UnityEngine;

namespace SpaceHawk.Data
{
    public enum EnemyKind
    {
        Light,  // Boss_01, used as the common enemy
        Heavy,  // Boss_02, used as the tougher enemy
        Boss    // Boss_03, reserved for a level-ending boss fight
    }

    [Serializable]
    public struct EnemyWave
    {
        public EnemyKind kind;
        public int count;
        public float spawnInterval;
        public int hp;
        public float speed;
        public float fireInterval;
        public int bulletDamage;
        [Tooltip("Pause before this wave starts spawning, so waves read as distinct beats instead of one continuous stream.")]
        public float delayBeforeWave;
    }

    [CreateAssetMenu(fileName = "Level_", menuName = "Space Hawk/Level Data")]
    public class LevelData : ScriptableObject
    {
        [Tooltip("Stable id used as the save-file key. Do not rename after players have progress.")]
        public string levelId = "level_01";

        public int displayNumber = 1;
        public int energyCost = 1;
        public int crystalReward = 20;
        public EnemyWave[] waves;

        [Header("Endless mode")]
        [Tooltip("True only for the runtime-created Endless level (see EndlessLevel): its waves are " +
                 "generated forever by EnemySpawner (see EndlessWaves) instead of read from `waves`, " +
                 "and it can only end in defeat.")]
        public bool isEndless = false;

        [Header("Win condition")]
        [Tooltip("Minimum fraction of this level's spawned enemies that must be destroyed (not " +
                 "just dodged/let fly past) to clear it - letting most of them escape now fails " +
                 "the level instead of quietly still counting as a clear.")]
        [Range(0f, 1f)]
        public float requiredKillRatio = 0.6f;

        [Header("Level Select map")]
        [Tooltip("Which horizontally-scrolled copy of the Level Select background this level's " +
                 "node sits on - the map tiles LevelSelect_Background.png once per distinct page " +
                 "value found across the whole database, so levels can keep being added in groups " +
                 "without crowding new nodes onto the same single screen's worth of art.")]
        public int mapPage = 0;

        [Tooltip("Normalized (0..1) position of this level's node on its own page of the Level " +
                 "Select background (NOT the whole scrollable map - just the one page it's on).")]
        public Vector2 mapAnchor = new Vector2(0.5f, 0.5f);

        [Header("Gameplay presentation")]
        [Tooltip("Which Space_BG_0X parallax set (0-3) this level scrolls through.")]
        public int backgroundSetIndex = 0;

        [Tooltip("Tints this level's enemy explosions - gives every level its own distinct look " +
                 "(see EnemySpawner/Enemy) instead of every kill across the whole game looking " +
                 "identical, without needing new explosion art.")]
        public Color levelTintColor = Color.white;

        /// <summary>Everything this level will ever spawn, summed straight from its own wave data -
        /// known upfront, before a single enemy actually spawns.</summary>
        public int GetTotalEnemyCount()
        {
            if (waves == null) return 0;
            int total = 0;
            foreach (EnemyWave wave in waves) total += wave.count;
            return total;
        }

        /// <summary>requiredKillRatio applied to the total, rounded up so a razor-thin percentage
        /// still asks for at least 1 real kill.</summary>
        public int GetRequiredKillCount()
        {
            return Mathf.CeilToInt(GetTotalEnemyCount() * requiredKillRatio);
        }

        /// <summary>Whether any wave in this level is the level-ending boss fight - a Boss-kind
        /// enemy holds position and only ever leaves by being killed (see Enemy.holdPosition), so
        /// beating it is already a far higher bar than the ordinary kill-ratio check.</summary>
        public bool HasBossWave()
        {
            if (waves == null) return false;
            foreach (EnemyWave wave in waves)
                if (wave.kind == EnemyKind.Boss) return true;
            return false;
        }
    }
}
