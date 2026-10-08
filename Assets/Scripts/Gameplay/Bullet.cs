using UnityEngine;

namespace SpaceHawk.Gameplay
{
    public enum BulletOwner { Player, Enemy }

    [RequireComponent(typeof(SpriteRenderer))]
    public class Bullet : MonoBehaviour
    {
        public BulletOwner owner;
        public float speed = 12f;
        public int damage = 10;
        public float lifetime = 3f;
        public Vector2 direction = Vector2.up;
        public GameObject impactEffectPrefab;
        [Tooltip("Extra enemies this bullet passes through before it is spent (player ships with piercing shots).")]
        public int pierce;

        private float _age;

        /// <summary>Speed/damage come from the prefab's own defaults; only owner + direction vary per shot.</summary>
        public void Init(BulletOwner bulletOwner, Vector2 dir)
        {
            owner = bulletOwner;
            direction = dir.normalized;
        }

        private void Update()
        {
            transform.position += (Vector3)(direction * speed * Time.deltaTime);
            _age += Time.deltaTime;
            if (_age >= lifetime) Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            bool isPlayerTarget = other.CompareTag("Player");
            bool isEnemyTarget = other.CompareTag("Enemy");

            if (owner == BulletOwner.Player && !isEnemyTarget) return;
            if (owner == BulletOwner.Enemy && !isPlayerTarget) return;

            Health health = other.GetComponent<Health>();
            if (health == null) return;

            health.TakeDamage(damage);
            if (impactEffectPrefab != null) Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);
            if (pierce > 0)
            {
                pierce--;
                return;
            }
            Destroy(gameObject);
        }
    }
}
