using UnityEngine;
using SpaceHawk.Core;

namespace SpaceHawk.Gameplay
{
    public enum PowerUpType { Hp, Damage, Rockets, Barrier, Armor, Magnet, Bomb, SlowEnemies, Spread, Beam }

    /// <summary>Drifting pickup dropped by enemies - the player flies into it to collect.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PowerUpPickup : MonoBehaviour
    {
        public PowerUpType type;
        public float speed = 1.8f;
        public float magnetSpeed = 6f;
        public float despawnY = -7f;
        [Tooltip("How much the icon grows/shrinks while drifting, so it reads as a pickup rather than background clutter.")]
        public float pulseAmount = 0.18f;
        public float pulseSpeed = 4f;

        private Vector3 _baseScale;
        private float _pulseTime;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            PlayerShip player = PlayerShip.ActiveInstance;
            bool pulled = player != null && (player.MagnetActive ||
                (player.PassiveMagnetRadius > 0f && Vector2.Distance(transform.position, player.transform.position) <= player.PassiveMagnetRadius));
            if (pulled)
            {
                transform.position = Vector3.MoveTowards(transform.position, player.transform.position, magnetSpeed * Time.deltaTime);
            }
            else
            {
                transform.position += Vector3.down * speed * Time.deltaTime;
            }

            _pulseTime += Time.deltaTime * pulseSpeed;
            float pulse = 1f + Mathf.Sin(_pulseTime) * pulseAmount;
            transform.localScale = _baseScale * pulse;

            if (transform.position.y < despawnY) Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            PlayerShip player = other.GetComponent<PlayerShip>();
            if (player != null) player.CollectPowerUp(type);
            AudioManager.Play(GameAudio.Pickup, 0.55f);
            Destroy(gameObject);
        }
    }
}
