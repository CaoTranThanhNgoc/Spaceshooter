using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SpaceHawk.Core;
using SpaceHawk.Data;

namespace SpaceHawk.Gameplay
{
    [RequireComponent(typeof(Health))]
    public class PlayerShip : MonoBehaviour
    {
        public Bullet bulletPrefab;
        public Transform firePoint;
        public float fireInterval = 0.25f;
        [Tooltip("0 = keep the bullet prefab's own default damage.")]
        public int bulletDamage = 0;
        public SpriteRenderer bodySprite;
        [Tooltip("The five stage sprites of the worn ship's family (filled in from `hulls` on spawn).")]
        public Sprite[] levelSprites;
        [Tooltip("One entry per ship family (Ship_01/02/03): its five stage sprites + exhaust flame. " +
                 "The worn ship (SaveManager.GetSelectedShip(), see ShipCatalog) picks family and stage.")]
        public ShipHullSprites[] hulls;
        public Rect movementBounds = new Rect(-5f, -4f, 10f, 7.5f);

        [Header("Power-up tuning")]
        public float damageBuffDuration = 8f;
        public float damageBuffMultiplier = 1.5f;
        public float rapidFireDuration = 8f;
        public float rapidFireIntervalMultiplier = 0.5f;
        public float shieldDuration = 5f;
        public float healFraction = 0.3f;
        public float armorDuration = 8f;
        public float armorDamageReduction = 0.5f;
        public float magnetDuration = 6f;
        [Tooltip("Damage dealt to every enemy on screen when the Bomb power-up is collected - high enough to one-shot anything but a Boss.")]
        public int bombDamage = 9999;
        public float slowEnemiesDuration = 5f;
        [Tooltip("How long the 5-way spread shot stays active once picked up.")]
        public float spreadBuffDuration = 8f;
        public int spreadShotCount = 5;
        public float spreadShotAngle = 20f;
        [Tooltip("How long the continuous beam stays active once picked up.")]
        public float beamDuration = 6f;
        [Tooltip("Damage applied to everything the beam overlaps, once per PlayerBeam.tickInterval.")]
        public int beamDamagePerTick = 6;
        [Tooltip("Prefab with a PlayerBeam component - instantiated as a child of firePoint while the Beam buff is active.")]
        public PlayerBeam beamPrefab;

        [Header("Power-up visual feedback")]
        [Tooltip("Glow ring behind the ship - color/visibility reflects whichever buff is currently active.")]
        public SpriteRenderer buffAura;
        [Tooltip("Faster engine flame while Rockets (rapid fire) is active.")]
        public FrameAnimatedFX exhaustFx;
        public int damageSpreadCount = 3;
        public float damageSpreadAngle = 14f;
        public Color damageBulletTint = new Color(1f, 0.45f, 0.25f, 1f);
        public Color rocketsBulletTint = new Color(0.45f, 0.95f, 1f, 1f);
        public Color barrierAuraColor = new Color(0.55f, 0.95f, 1f, 1f);
        public Color armorAuraColor = new Color(1f, 0.6f, 0.25f, 1f);
        public Color damageAuraColor = new Color(1f, 0.35f, 0.3f, 1f);
        public Color rocketsAuraColor = new Color(0.45f, 0.95f, 1f, 1f);
        public Color magnetAuraColor = new Color(0.7f, 0.5f, 1f, 1f);
        public Color slowEnemiesAuraColor = new Color(0.5f, 1f, 0.6f, 1f);
        public Color healFlashColor = new Color(0.4f, 1f, 0.5f, 1f);
        public Color spreadBulletTint = new Color(0.5f, 1f, 0.4f, 1f);
        public Color spreadAuraColor = new Color(0.5f, 1f, 0.4f, 1f);
        public Color beamAuraColor = new Color(1f, 0.85f, 0.3f, 1f);

        private Health _health;
        private float _fireTimer;
        private Camera _cam;
        private bool _dragging;
        private Vector3 _dragOffset;

        private float _damageMultiplier = 1f;
        private float _fireIntervalMultiplier = 1f;
        private Coroutine _damageBuffRoutine;
        private Coroutine _rapidFireRoutine;
        private Coroutine _shieldRoutine;
        private Coroutine _armorRoutine;
        private Coroutine _magnetRoutine;
        private Coroutine _healFlashRoutine;
        private Coroutine _beamRoutine;
        private PlayerBeam _activeBeam;
        private readonly Dictionary<PowerUpType, float> _buffEndTimes = new Dictionary<PowerUpType, float>();
        private float _auraPulseTime;
        private float _baseExhaustFrameRate;

        // The worn ship's own traits (ShipCatalog) - read once on spawn.
        private ShipSpec _spec;
        private float _baseFireInterval;
        private float _baseDamageTaken = 1f;
        private float _lastHp;
        private float _lastHitTime;
        private float _regenCarry;
        private bool _secondWindReady;

        public static PlayerShip ActiveInstance { get; private set; }
        public bool MagnetActive { get; private set; }
        /// <summary>World-unit radius within which this ship's tractor beam pulls pickups in on its own.</summary>
        public float PassiveMagnetRadius => _spec != null ? _spec.magnetRadius : 0f;
        public ShipSpec Spec => _spec;
        /// <summary>True the moment the player has an active drag/touch moving the ship - used by
        /// MoveTutorialHint to dismiss itself as soon as the control is actually demonstrated.</summary>
        public bool IsDragging => _dragging;

        public event System.Action Died;
        /// <summary>Raised when the ship's second wind saved it from a fatal hit (UI shows a toast).</summary>
        public event System.Action SecondWindUsed;

        /// <summary>Seconds left on a timed buff (Hp is instant and always returns 0), for the HUD
        /// to show a countdown icon while it's active.</summary>
        public float GetBuffRemaining(PowerUpType type)
        {
            return _buffEndTimes.TryGetValue(type, out float endTime) ? Mathf.Max(0f, endTime - Time.time) : 0f;
        }

        public void CollectPowerUp(PowerUpType type)
        {
            DailyMissions.Report(MissionKind.CollectPowerUps);
            float duration = GetDuration(type);
            if (duration > 0f) _buffEndTimes[type] = Time.time + duration;

            switch (type)
            {
                case PowerUpType.Hp:
                    _health.Heal(Mathf.RoundToInt(_health.maxHp * healFraction));
                    RestartRoutine(ref _healFlashRoutine, HealFlashRoutine());
                    break;
                case PowerUpType.Damage:
                    RestartRoutine(ref _damageBuffRoutine, DamageBuffRoutine());
                    break;
                case PowerUpType.Rockets:
                    RestartRoutine(ref _rapidFireRoutine, RapidFireRoutine());
                    break;
                case PowerUpType.Barrier:
                    RestartRoutine(ref _shieldRoutine, ShieldRoutine(GetDuration(PowerUpType.Barrier)));
                    break;
                case PowerUpType.Armor:
                    RestartRoutine(ref _armorRoutine, ArmorRoutine());
                    break;
                case PowerUpType.Magnet:
                    RestartRoutine(ref _magnetRoutine, MagnetRoutine());
                    break;
                case PowerUpType.Bomb:
                    Enemy.Detonate(bombDamage);
                    if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.5f, 0.5f);
                    AudioManager.Play(GameAudio.Explosion, 1f, 0.55f);
                    break;
                case PowerUpType.SlowEnemies:
                    Enemy.ApplyGlobalSlow(slowEnemiesDuration);
                    break;
                case PowerUpType.Beam:
                    RestartRoutine(ref _beamRoutine, BeamBuffRoutine());
                    break;
                // Spread needs no case here - GetDuration already populates _buffEndTimes for it
                // above, and HandleShooting/UpdateBuffVisuals read that directly with no other
                // state or GameObject to manage, unlike Beam which owns a persistent child object.
            }
        }

        /// <summary>Brings the ship back after a defeat: partial HP, a few seconds of Barrier so it
        /// isn't instantly killed again, and every enemy bullet already in flight cleared away.</summary>
        public void Revive(float hpFraction)
        {
            _health.Revive(hpFraction);
            CollectPowerUp(PowerUpType.Barrier);

            foreach (Bullet b in FindObjectsByType<Bullet>())
            {
                if (b.owner == BulletOwner.Enemy) Destroy(b.gameObject);
            }
        }

        private float GetDuration(PowerUpType type)
        {
            float baseDuration;
            switch (type)
            {
                case PowerUpType.Damage: baseDuration = damageBuffDuration; break;
                case PowerUpType.Rockets: baseDuration = rapidFireDuration; break;
                case PowerUpType.Barrier: baseDuration = shieldDuration; break;
                case PowerUpType.Armor: baseDuration = armorDuration; break;
                case PowerUpType.Magnet: baseDuration = magnetDuration; break;
                case PowerUpType.SlowEnemies: baseDuration = slowEnemiesDuration; break;
                case PowerUpType.Spread: baseDuration = spreadBuffDuration; break;
                case PowerUpType.Beam: baseDuration = beamDuration; break;
                default: return 0f;
            }
            return baseDuration * (_spec != null ? _spec.buffDuration : 1f);
        }

        private void RestartRoutine(ref Coroutine slot, IEnumerator routine)
        {
            if (slot != null) StopCoroutine(slot);
            slot = StartCoroutine(routine);
        }

        private IEnumerator DamageBuffRoutine()
        {
            _damageMultiplier = damageBuffMultiplier;
            yield return new WaitForSeconds(GetDuration(PowerUpType.Damage));
            _damageMultiplier = 1f;
            _damageBuffRoutine = null;
        }

        private IEnumerator RapidFireRoutine()
        {
            _fireIntervalMultiplier = rapidFireIntervalMultiplier;
            yield return new WaitForSeconds(GetDuration(PowerUpType.Rockets));
            _fireIntervalMultiplier = 1f;
            _rapidFireRoutine = null;
        }

        private IEnumerator ShieldRoutine(float duration)
        {
            _health.SetInvincible(true);
            yield return new WaitForSeconds(duration);
            _health.SetInvincible(false);
            _shieldRoutine = null;
        }

        /// <summary>A Barrier that is not a pickup - the ship's own start-of-level shield or its second
        /// wind - so it must not count towards the "collect power-ups" mission.</summary>
        private void GrantBarrier(float seconds)
        {
            if (seconds <= 0f) return;
            float end = Time.time + seconds;
            if (!_buffEndTimes.TryGetValue(PowerUpType.Barrier, out float current) || current < end)
                _buffEndTimes[PowerUpType.Barrier] = end;
            RestartRoutine(ref _shieldRoutine, ShieldRoutine(Mathf.Max(seconds, _buffEndTimes[PowerUpType.Barrier] - Time.time)));
        }

        private IEnumerator ArmorRoutine()
        {
            _health.SetDamageReduction(armorDamageReduction * _baseDamageTaken);
            yield return new WaitForSeconds(GetDuration(PowerUpType.Armor));
            _health.SetDamageReduction(_baseDamageTaken);
            _armorRoutine = null;
        }

        private IEnumerator MagnetRoutine()
        {
            MagnetActive = true;
            yield return new WaitForSeconds(GetDuration(PowerUpType.Magnet));
            MagnetActive = false;
            _magnetRoutine = null;
        }

        private IEnumerator BeamBuffRoutine()
        {
            if (beamPrefab != null && _activeBeam == null)
            {
                // PlayerBeam.Awake() runs synchronously inside Instantiate and positions itself
                // from its own length - must not touch transform.localPosition afterward or it
                // would stomp that offset back to the firePoint's origin.
                Transform parent = firePoint != null ? firePoint : transform;
                _activeBeam = Instantiate(beamPrefab, parent);
                _activeBeam.damagePerTick = beamDamagePerTick;
            }
            yield return new WaitForSeconds(GetDuration(PowerUpType.Beam));
            if (_activeBeam != null) Destroy(_activeBeam.gameObject);
            _activeBeam = null;
            _beamRoutine = null;
        }

        private IEnumerator HealFlashRoutine()
        {
            if (bodySprite == null) yield break;
            const float duration = 0.4f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                bodySprite.color = Color.Lerp(healFlashColor, Color.white, t / duration);
                yield return null;
            }
            bodySprite.color = Color.white;
            _healFlashRoutine = null;
        }

        // The largest footprint a ship may take on screen (world units). The art ranges from a compact
        // 1.6 x 1.3 to a 1.2 x 2.6 spike, which would make the late ships swallow the view.
        private const float MaxShipWidth = 1.7f;
        private const float MaxShipHeight = 1.9f;

        /// <summary>Puts the given ship of the roster on screen: its sprite (stage `tier` of its family's
        /// line), scaled down if it is oversized, a hitbox fitted to that silhouette, and the nose /
        /// engine positions to match.</summary>
        public void ApplyShip(int shipIndex)
        {
            ApplyHull(shipIndex);
            if (bodySprite == null || levelSprites == null || levelSprites.Length == 0) return;
            int tier = Mathf.Clamp(ShipCatalog.TierOf(shipIndex), 0, levelSprites.Length - 1);
            bodySprite.sprite = levelSprites[tier];
            if (bodySprite.sprite == null) return;

            // The ships are distinct silhouettes (not recolours) of quite different sizes, so the
            // hitbox is fitted to each - a little smaller than the art, and never more than 1.6
            // units tall, so a long ship is not an easier target than a short one.
            Vector2 size = bodySprite.sprite.bounds.size;

            // Oversized ships are scaled down as a whole; the nose point is scaled back up so the
            // beam (a child of it) and anything else parented there keep their real-world size.
            float fit = Mathf.Min(1f, MaxShipWidth / size.x, MaxShipHeight / size.y);
            transform.localScale = new Vector3(fit, fit, 1f);
            if (firePoint != null && firePoint != transform) firePoint.localScale = Vector3.one / fit;

            BoxCollider2D box = GetComponent<BoxCollider2D>();
            if (box != null) box.size = new Vector2(size.x * 0.7f, Mathf.Min(size.y * 0.7f, 1.6f));

            float half = size.y * 0.5f;
            if (firePoint != null && firePoint != transform) firePoint.localPosition = new Vector3(0f, half * 0.9f, 0f);
            if (exhaustFx != null) exhaustFx.transform.localPosition = new Vector3(0f, -half * 0.95f, 0.1f);
        }

        private void Awake()
        {
            _health = GetComponent<Health>();
            _cam = Camera.main;
            ActiveInstance = this;

            // Pull the Inventory-upgraded stats directly from SaveManager here, on spawn, rather
            // than relying on GameplayController to apply them afterwards - the ship is always
            // correct as soon as it exists, regardless of caller order.
            int worn = SaveManager.GetSelectedShip();
            _spec = ShipCatalog.Get(worn);
            _health.SetMax(SaveManager.GetShipMaxHp());
            bulletDamage = SaveManager.GetShipDamage();
            _baseFireInterval = fireInterval;
            fireInterval = _baseFireInterval / Mathf.Max(0.1f, _spec.rate);
            _baseDamageTaken = _spec.damageTaken;
            _health.SetDamageReduction(_baseDamageTaken);
            _secondWindReady = _spec.secondWind;
            _lastHp = _health.CurrentHp;
            ApplyShip(worn);

            if (buffAura != null) buffAura.sprite = ProceduralSprites.SoftGlow;
            if (exhaustFx != null) _baseExhaustFrameRate = exhaustFx.frameRate;
        }

        /// <summary>Swaps in the sprite set and exhaust flame of the ship's family.</summary>
        private void ApplyHull(int shipIndex)
        {
            if (hulls == null || hulls.Length == 0) return;
            ShipHullSprites hull = hulls[Mathf.Clamp(ShipCatalog.FamilyOf(shipIndex), 0, hulls.Length - 1)];
            if (hull == null) return;
            if (hull.levelSprites != null && hull.levelSprites.Length > 0) levelSprites = hull.levelSprites;
            if (exhaustFx != null && hull.exhaustFrames != null && hull.exhaustFrames.Length > 0) exhaustFx.SetFrames(hull.exhaustFrames);
        }

        /// <summary>Called by GameplayController the moment the fight really starts (after the level
        /// intro): ships with a start barrier get it now, not while the doors are still opening.</summary>
        public void BeginFight()
        {
            if (_spec != null) GrantBarrier(_spec.startBarrier);
        }

        private void OnEnable()
        {
            _health.Died += HandleDied;
            _health.DamagedPercent += HandleDamaged;
        }

        private void OnDisable()
        {
            _health.Died -= HandleDied;
            _health.DamagedPercent -= HandleDamaged;
            if (ActiveInstance == this) ActiveInstance = null;
        }

        private void Update()
        {
            // Isolated in its own try/catch: this is the one piece that had to change to work on
            // a real touchscreen (see HandleMovement's own comment) - if anything about input
            // reading ever throws again on some other device, firing/buffs must keep working
            // rather than silently stopping right along with movement, the way a single shared
            // exception used to take out this whole method (and everything after it) at once.
            try
            {
                HandleMovement();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }

            HandleShooting();
            UpdateBuffVisuals();
            HandleRepair();
        }

        /// <summary>Nano-repair: a ship with it patches itself up once nothing has hit it for a couple of seconds.</summary>
        private void HandleRepair()
        {
            if (_health.IsDead) return;
            if (_health.CurrentHp < _lastHp) _lastHitTime = Time.time;
            _lastHp = _health.CurrentHp;

            if (_spec == null || _spec.regenPerSecond <= 0f || _health.CurrentHp >= _health.maxHp) return;
            if (Time.time - _lastHitTime < 2f) return;

            _regenCarry += _spec.regenPerSecond * _health.maxHp * Time.deltaTime;
            int whole = Mathf.FloorToInt(_regenCarry);
            if (whole <= 0) return;
            _regenCarry -= whole;
            _health.Heal(whole);
            _lastHp = _health.CurrentHp;
        }

        private void UpdateBuffVisuals()
        {
            bool rapidFireActive = GetBuffRemaining(PowerUpType.Rockets) > 0f;
            if (exhaustFx != null)
                exhaustFx.frameRate = rapidFireActive ? _baseExhaustFrameRate * 1.8f : _baseExhaustFrameRate;

            if (buffAura == null) return;

            Color color;
            bool anyActive;
            if (GetBuffRemaining(PowerUpType.Barrier) > 0f) { color = barrierAuraColor; anyActive = true; }
            else if (GetBuffRemaining(PowerUpType.Armor) > 0f) { color = armorAuraColor; anyActive = true; }
            else if (GetBuffRemaining(PowerUpType.Beam) > 0f) { color = beamAuraColor; anyActive = true; }
            else if (GetBuffRemaining(PowerUpType.Spread) > 0f) { color = spreadAuraColor; anyActive = true; }
            else if (GetBuffRemaining(PowerUpType.Damage) > 0f) { color = damageAuraColor; anyActive = true; }
            else if (rapidFireActive) { color = rocketsAuraColor; anyActive = true; }
            else if (GetBuffRemaining(PowerUpType.Magnet) > 0f) { color = magnetAuraColor; anyActive = true; }
            else if (GetBuffRemaining(PowerUpType.SlowEnemies) > 0f) { color = slowEnemiesAuraColor; anyActive = true; }
            else { color = Color.clear; anyActive = false; }

            if (buffAura.gameObject.activeSelf != anyActive) buffAura.gameObject.SetActive(anyActive);
            if (!anyActive) return;

            _auraPulseTime += Time.deltaTime * 4f;
            color.a = 0.5f + Mathf.Sin(_auraPulseTime) * 0.18f;
            buffAura.color = color;
        }

        /// <summary>Checks the concrete Touchscreen/Mouse devices directly instead of the generic
        /// Pointer base class. Pointer.current worked fine in the Editor (Mono) but left the ship
        /// completely unresponsive to touch in an Android/IL2CPP build - the abstract Pointer API
        /// resolves its controls more dynamically, which is a known rough edge under IL2CPP AOT,
        /// whereas Touchscreen/Mouse are concrete types with none of that ambiguity.</summary>
        private void HandleMovement()
        {
            if (_cam == null) return;
            if (!TryGetPressedScreenPosition(out Vector2 screenPos))
            {
                _dragging = false;
                return;
            }

            float camDistance = -_cam.transform.position.z; // 2D setup: camera sits behind the scene at z < 0
            Vector3 world = _cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, camDistance));
            world.z = transform.position.z;

            if (!_dragging)
            {
                _dragging = true;
                _dragOffset = transform.position - world;
            }

            Vector3 target = world + _dragOffset;
            target.x = Mathf.Clamp(target.x, movementBounds.xMin, movementBounds.xMax);
            target.y = Mathf.Clamp(target.y, movementBounds.yMin, movementBounds.yMax);
            transform.position = target;
        }

        private static bool TryGetPressedScreenPosition(out Vector2 screenPos)
        {
            Touchscreen touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                screenPos = touch.primaryTouch.position.ReadValue();
                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                screenPos = mouse.position.ReadValue();
                return true;
            }

            screenPos = default;
            return false;
        }

        private void HandleShooting()
        {
            if (bulletPrefab == null) return;
            _fireTimer += Time.deltaTime;
            float effectiveInterval = fireInterval * _fireIntervalMultiplier;
            if (_fireTimer < effectiveInterval) return;
            _fireTimer = 0f;

            Vector3 pos = firePoint != null ? firePoint.position : transform.position;
            int baseDamage = bulletDamage > 0 ? bulletDamage : bulletPrefab.damage;
            int finalDamage = Mathf.RoundToInt(baseDamage * _damageMultiplier);

            bool spreadBuffActive = GetBuffRemaining(PowerUpType.Spread) > 0f;
            bool damageBuffActive = GetBuffRemaining(PowerUpType.Damage) > 0f;
            bool rapidFireActive = GetBuffRemaining(PowerUpType.Rockets) > 0f;
            Color tint = spreadBuffActive ? spreadBulletTint : damageBuffActive ? damageBulletTint : rapidFireActive ? rocketsBulletTint : Color.white;

            // Spread takes priority over Damage's own 3-way spread rather than stacking with it -
            // firing both patterns at once would be 15 bullets a volley, way past what reads as a
            // deliberate "new weapon" and into bullet-hell clutter.
            if (spreadBuffActive && spreadShotCount > 1)
            {
                float startAngle = -spreadShotAngle * (spreadShotCount - 1) / 2f;
                for (int i = 0; i < spreadShotCount; i++)
                {
                    Vector2 dir = Quaternion.Euler(0f, 0f, startAngle + i * spreadShotAngle) * Vector2.up;
                    FireOne(pos, dir, finalDamage, tint);
                }
            }
            else if (damageBuffActive && damageSpreadCount > 1)
            {
                float startAngle = -damageSpreadAngle * (damageSpreadCount - 1) / 2f;
                for (int i = 0; i < damageSpreadCount; i++)
                {
                    Vector2 dir = Quaternion.Euler(0f, 0f, startAngle + i * damageSpreadAngle) * Vector2.up;
                    FireOne(pos, dir, finalDamage, tint);
                }
            }
            else
            {
                FireVolley(pos, finalDamage, tint);
            }

            // Once per volley, not per bullet, so the Damage buff's 3-way spread doesn't triple the volume.
            AudioManager.Play(GameAudio.Shoot, 0.35f, Random.Range(0.92f, 1.08f));
        }

        /// <summary>The ship's own shot: one bullet, or twin parallel / triple fanned bullets that
        /// share the damage out (each one a bit less than a single shot, together more).</summary>
        private void FireVolley(Vector3 pos, int damage, Color tint)
        {
            int count = _spec != null ? Mathf.Max(1, _spec.volley) : 1;
            if (count == 1)
            {
                FireOne(pos, Vector2.up, damage, tint);
                return;
            }

            int each = Mathf.Max(1, Mathf.RoundToInt(damage * ShipCatalog.VolleyDamageFactor(count)));
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0f : (i / (float)(count - 1)) * 2f - 1f;   // -1 .. 1
                Vector3 offset = new Vector3(t * 0.2f, 0f, 0f);
                Vector2 dir = count == 3 ? (Vector2)(Quaternion.Euler(0f, 0f, -t * 7f) * Vector2.up) : Vector2.up;
                FireOne(pos + offset, dir, each, tint);
            }
        }

        private void FireOne(Vector3 pos, Vector2 direction, int damage, Color tint)
        {
            Bullet b = Instantiate(bulletPrefab, pos, Quaternion.identity);
            b.Init(BulletOwner.Player, direction);
            b.damage = damage;
            b.pierce = _spec != null ? _spec.pierce : 0;

            SpriteRenderer sr = b.GetComponent<SpriteRenderer>();
            if (_spec != null && _spec.critChance > 0f && Random.value < _spec.critChance)
            {
                b.damage = damage * 2;
                tint = new Color(1f, 0.85f, 0.25f, 1f);
                b.transform.localScale *= 1.35f;
            }
            if (sr != null) sr.color = tint;
        }

        private void HandleDamaged(float remainingPct)
        {
            if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.15f, 0.18f);
            AudioManager.Play(ProceduralAudio.Hit, 0.5f);
            HapticFeedback.Play();
        }

        private void HandleDied()
        {
            // Second wind: the fatal hit leaves the ship at a sliver of health behind a barrier -
            // once per level - and the defeat never happens.
            if (_secondWindReady)
            {
                _secondWindReady = false;
                _health.Revive(0.35f);
                _lastHp = _health.CurrentHp;
                _lastHitTime = Time.time;
                GrantBarrier(3f);
                if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.3f, 0.3f);
                AudioManager.Play(GameAudio.Pickup, 0.7f, 0.8f);
                SecondWindUsed?.Invoke();
                return;
            }

            HapticFeedback.Play();
            Died?.Invoke();
        }
    }
}
