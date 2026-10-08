using System.Collections;
using UnityEngine;

namespace SpaceHawk.Gameplay
{
    /// <summary>Ambient hazard spawner, independent of the enemy wave system - just keeps
    /// drifting meteors coming for the whole level so the play area feels alive.</summary>
    public class MeteorSpawner : MonoBehaviour
    {
        public GameObject[] meteorPrefabs;
        public float minInterval = 1.2f;
        public float maxInterval = 3.0f;
        public float spawnY = 6f;
        public Vector2 xRange = new Vector2(-5f, 5f);
        public Vector2 speedRange = new Vector2(1f, 2.5f);
        public Vector2 scaleRange = new Vector2(1.1f, 1.9f);

        private Coroutine _routine;

        private void OnEnable()
        {
            _routine = StartCoroutine(SpawnLoop());
        }

        private void OnDisable()
        {
            if (_routine != null) StopCoroutine(_routine);
        }

        private IEnumerator SpawnLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
                SpawnOne();
            }
        }

        private void SpawnOne()
        {
            if (meteorPrefabs == null || meteorPrefabs.Length == 0) return;
            GameObject prefab = meteorPrefabs[Random.Range(0, meteorPrefabs.Length)];
            float x = Random.Range(xRange.x, xRange.y);
            GameObject instance = Instantiate(prefab, new Vector3(x, spawnY, 0f), Quaternion.identity);

            Meteor meteor = instance.GetComponent<Meteor>();
            if (meteor != null)
            {
                meteor.speed = Random.Range(speedRange.x, speedRange.y);
                meteor.rotationSpeed = Random.Range(-40f, 40f);
            }
            instance.transform.localScale = Vector3.one * Random.Range(scaleRange.x, scaleRange.y);
        }
    }
}
