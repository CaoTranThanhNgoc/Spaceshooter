using UnityEngine;
using SpaceHawk.Core;

namespace SpaceHawk.Gameplay
{
    /// <summary>Ambient hazard: drifts down, tumbling, and shatters either when shot enough
    /// (reuses the Health/Died flow) or when it rams the player.</summary>
    [RequireComponent(typeof(Health))]
    public class Meteor : MonoBehaviour
    {
        public float speed = 1.5f;
        public float rotationSpeed = 20f;
        public int contactDamage = 15;
        public GameObject explosionPrefab;
        public float despawnY = -7f;

        private Health _health;
        private bool _removed;

        private void Awake()
        {
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            _health.Died += HandleDestroyed;
        }

        private void OnDisable()
        {
            _health.Died -= HandleDestroyed;
        }

        private void Update()
        {
            transform.position += Vector3.down * speed * Time.deltaTime;
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

            if (transform.position.y < despawnY) RemoveSelf();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            Health playerHealth = other.GetComponent<Health>();
            if (playerHealth != null) playerHealth.TakeDamage(contactDamage);
            _health.TakeDamage(_health.maxHp);
        }

        private void HandleDestroyed()
        {
            if (explosionPrefab != null) Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.15f, 0.15f);
            AudioManager.Play(GameAudio.Explosion, 0.4f, Random.Range(1.1f, 1.3f));
            RemoveSelf();
        }

        private void RemoveSelf()
        {
            if (_removed) return;
            _removed = true;
            Destroy(gameObject);
        }
    }
}
