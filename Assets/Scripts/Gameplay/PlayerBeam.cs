using System.Collections.Generic;
using UnityEngine;

namespace SpaceHawk.Gameplay
{
    /// <summary>Continuous damage beam spawned by PlayerShip while the Beam power-up is active -
    /// unlike Bullet (one Instantiate + Destroy per shot), this is ONE persistent trigger, parented
    /// to the ship's firePoint so it tracks movement, that ticks damage to everything currently
    /// overlapping it for as long as the buff lasts.
    ///
    /// The look is built from three layers so it reads as several red rays merged into one soft
    /// beam instead of a single hard bar: a wide translucent halo (glow), the bright braided body
    /// (core - three thin strands over a red haze with a white-hot centre, tiled along its length
    /// and animated so energy seems to flow outward) and a pulsing flare at the muzzle. All
    /// sprites are generated in code (ProceduralSprites), so the colour is true red rather than a
    /// tinted copy of cyan art.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class PlayerBeam : MonoBehaviour
    {
        public int damagePerTick = 6;
        public float tickInterval = 0.08f;
        public float length = 11f;
        [Tooltip("Visual width of the bright body in world units (the halo is wider still).")]
        public float beamWidth = 0.8f;

        [Tooltip("Pulsing flare at the muzzle end.")]
        public SpriteRenderer startCap;
        [Tooltip("Bright braided body, tiled along the beam's length.")]
        public SpriteRenderer core;
        [Tooltip("Wide soft halo behind the body - fades in from nothing at the muzzle.")]
        public SpriteRenderer glow;

        private const float HaloWidthMultiplier = 2.6f;
        private const float FramesPerSecond = 14f;

        private float _tickTimer;
        private float _bodyScaleX;
        private float _haloScaleX;
        private float _haloScaleY;
        private float _flareScale;
        private Sprite[] _frames;
        private readonly List<Collider2D> _inRange = new List<Collider2D>();
        private BeamImpactFX _impactFx;

        private void Awake()
        {
            _frames = ProceduralSprites.BeamFrames;
            Sprite flare = ProceduralSprites.SoftGlow;

            _bodyScaleX = beamWidth / ProceduralSprites.BeamBodyWidthUnits;
            _flareScale = beamWidth * 2.2f / flare.bounds.size.x;

            if (startCap != null)
            {
                startCap.sprite = flare;
                startCap.color = new Color(1f, 0.72f, 0.3f, 0.95f);
                startCap.transform.localScale = Vector3.one * _flareScale;
                startCap.transform.localPosition = Vector3.zero;
            }

            // The body starts at the muzzle and runs `length` up the screen.
            ConfigureBody(core, _frames[0], length, _bodyScaleX, Color.white);

            _haloScaleX = beamWidth * HaloWidthMultiplier / ProceduralSprites.BeamHaloWidthUnits;
            _haloScaleY = length / ProceduralSprites.BeamHaloHeightUnits;
            if (glow != null)
            {
                glow.drawMode = SpriteDrawMode.Simple;
                glow.sprite = ProceduralSprites.BeamHalo;
                glow.color = new Color(1f, 1f, 1f, 0.7f);
                glow.transform.localScale = new Vector3(_haloScaleX, _haloScaleY, 1f);
                glow.transform.localPosition = new Vector3(0f, length * 0.5f, 0f);
            }

            BoxCollider2D box = GetComponent<BoxCollider2D>();
            box.size = new Vector2(beamWidth * 0.7f, length);
            box.offset = new Vector2(0f, length * 0.5f);

            _impactFx = BeamImpactFX.Create();
        }

        private void OnDestroy()
        {
            // Sparks already in flight finish on their own; the FX object then removes itself.
            if (_impactFx != null) _impactFx.Retire();
        }

        private static void ConfigureBody(SpriteRenderer renderer, Sprite sprite, float bodyLength, float scaleX, Color color)
        {
            if (renderer == null) return;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.sprite = sprite;
            renderer.size = new Vector2(sprite.bounds.size.x, bodyLength);
            renderer.color = color;
            renderer.transform.localScale = new Vector3(scaleX, 1f, 1f);
            renderer.transform.localPosition = new Vector3(0f, bodyLength * 0.5f, 0f);
        }

        private void Update()
        {
            AnimateVisuals();
            SplashContacts();
            TickDamage();
        }

        /// <summary>Everything the beam is touching throws sparks from the point where the beam
        /// meets its surface - the beam keeps going through (it pierces), but each target reads as
        /// being struck rather than as a bar drawn over a sprite.</summary>
        private void SplashContacts()
        {
            if (_impactFx == null) return;
            Vector2 outward = -(Vector2)transform.up;
            for (int i = 0; i < _inRange.Count; i++)
            {
                Collider2D col = _inRange[i];
                if (col == null || !col.enabled) continue;
                _impactFx.Splash(ContactPoint(col), outward, Time.deltaTime);
            }
        }

        /// <summary>Where the beam's centre line meets a collider: the point of the collider nearest
        /// the muzzle side, slid onto the beam's axis (and kept within its width, so a target only
        /// clipping the beam's edge splashes at the edge instead of out in empty space).</summary>
        private Vector2 ContactPoint(Collider2D col)
        {
            Vector2 origin = transform.position;
            Vector2 dir = transform.up;
            Vector2 side = transform.right;

            Vector2 centre = col.bounds.center;
            float along = Mathf.Clamp(Vector2.Dot(centre - origin, dir), 0f, length);
            Vector2 probe = origin + dir * (along - 100f);          // far off on the muzzle side
            Vector2 surface = col.ClosestPoint(probe);

            float alongSurface = Mathf.Clamp(Vector2.Dot(surface - origin, dir), 0f, length);
            float lateral = Mathf.Clamp(Vector2.Dot(surface - origin, side), -beamWidth * 0.35f, beamWidth * 0.35f);
            return origin + dir * alongSurface + side * lateral;
        }

        private void AnimateVisuals()
        {
            float t = Time.time;

            // Playing the frames in order slides the ripples outward along the strands.
            int frame = Mathf.FloorToInt(t * FramesPerSecond) % _frames.Length;
            if (core != null) core.sprite = _frames[frame];

            float pulse = 1f + Mathf.Sin(t * 26f) * 0.06f + Mathf.Sin(t * 11f) * 0.04f;
            if (core != null) core.transform.localScale = new Vector3(_bodyScaleX * pulse, 1f, 1f);
            if (glow != null) glow.transform.localScale = new Vector3(_haloScaleX * (1f + (pulse - 1f) * 1.6f), _haloScaleY, 1f);
            if (startCap != null) startCap.transform.localScale = Vector3.one * (_flareScale * (1f + Mathf.Sin(t * 18f) * 0.12f));
        }

        private void TickDamage()
        {
            _tickTimer += Time.deltaTime;
            if (_tickTimer < tickInterval) return;
            _tickTimer = 0f;

            for (int i = _inRange.Count - 1; i >= 0; i--)
            {
                Collider2D col = _inRange[i];
                if (col == null) { _inRange.RemoveAt(i); continue; }
                Health h = col.GetComponent<Health>();
                if (h == null) continue;
                if (_impactFx != null) _impactFx.Pulse(ContactPoint(col));
                h.TakeDamage(damagePerTick);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Enemy")) return;
            if (!_inRange.Contains(other)) _inRange.Add(other);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            _inRange.Remove(other);
        }
    }
}
