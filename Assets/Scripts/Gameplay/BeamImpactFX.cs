using System.Collections.Generic;
using UnityEngine;

namespace SpaceHawk.Gameplay
{
    /// <summary>The splash where the player's beam strikes something: a hot white flare at the
    /// contact point, a shockwave ring on every damage tick, and a spray of streak sparks and glowing
    /// embers thrown back and sideways off the surface (so a strike looks like splashing energy,
    /// not a bar clipping through a sprite).
    ///
    /// Lives on its own root object rather than under the beam, so particles already in flight stay
    /// where they were when the ship moves or the buff ends. Everything is a pooled SpriteRenderer
    /// built from ProceduralSprites - no particle-system shader to ship, and nothing is instantiated
    /// per spark after the pool has grown to its working size.</summary>
    public class BeamImpactFX : MonoBehaviour
    {
        private const int MaxSparks = 120;
        private const int MaxEmbers = 40;
        private const int MaxFlares = 24;
        private const int MaxRings = 14;
        private const int SortingOrder = 12;     // above the ship (10) and the beam layers (3-4)

        private const float SparkRate = 140f;     // per second, per contact
        private const float EmberRate = 34f;

        private enum Kind { Spark, Ember, Flare, Ring }

        private struct Particle
        {
            public Transform transform;
            public SpriteRenderer renderer;
            public Kind kind;
            public bool alive;
            public Vector2 position;
            public Vector2 velocity;
            public float age;
            public float life;
            public float size;       // spark: length scale, others: base scale
            public float thickness;  // spark only
            public float drag;
            public float gravity;
            public float alpha;
        }

        private readonly List<Particle> _particles = new List<Particle>(MaxSparks + MaxEmbers + MaxFlares + MaxRings);
        private float _sparkCarry;
        private float _emberCarry;
        private bool _retired;

        private static readonly Color White = new Color(1f, 1f, 0.95f);
        private static readonly Color Yellow = new Color(1f, 0.86f, 0.35f);
        private static readonly Color Hot = new Color(1f, 0.96f, 0.78f);
        private static readonly Color Warm = new Color(1f, 0.55f, 0.12f);
        private static readonly Color Cool = new Color(0.85f, 0.10f, 0.05f);

        public static BeamImpactFX Create()
        {
            GameObject go = new GameObject("BeamImpactFX");
            return go.AddComponent<BeamImpactFX>();
        }

        /// <summary>The beam is gone - let what is still flying finish, then clean up.</summary>
        public void Retire()
        {
            _retired = true;
        }

        /// <summary>Called every frame the beam overlaps something. `outward` is the direction the
        /// splash sprays from the surface (back along the beam, toward the ship).</summary>
        public void Splash(Vector2 point, Vector2 outward, float dt)
        {
            if (_retired) return;

            // The hot spot: a big soft orange flare with a small white core, re-spawned each frame
            // with a random size so it flickers like something actually burning.
            Spawn(Kind.Flare, point, Vector2.zero, 0.09f, Random.Range(1.0f, 1.4f), 0f, 0f, 0f, 0.85f, Warm);
            Spawn(Kind.Flare, point, Vector2.zero, 0.07f, Random.Range(0.5f, 0.7f), 0f, 0f, 0f, 1f, Hot);

            _sparkCarry += SparkRate * dt;
            while (_sparkCarry >= 1f)
            {
                _sparkCarry -= 1f;
                EmitSpark(point, outward);
            }

            _emberCarry += EmberRate * dt;
            while (_emberCarry >= 1f)
            {
                _emberCarry -= 1f;
                EmitEmber(point, outward);
            }
        }

        /// <summary>Once per damage tick: a ring that blooms out of the contact point.</summary>
        public void Pulse(Vector2 point)
        {
            if (_retired) return;
            Spawn(Kind.Ring, point, Vector2.zero, 0.26f, 1.6f, 0f, 0f, 0f, 0.8f, Warm);
        }

        private void EmitSpark(Vector2 point, Vector2 outward)
        {
            // Triangular distribution around the outward direction: most sparks fly roughly back at
            // the ship, a good share spray sideways along the surface, a few skim past it.
            float angle = (Random.value + Random.value - 1f) * 130f * Mathf.Deg2Rad;
            Vector2 dir = Rotate(outward, angle);
            float speed = Random.Range(5f, 14f);
            float life = Random.Range(0.25f, 0.55f);
            Spawn(Kind.Spark, point + dir * Random.Range(0f, 0.12f), dir * speed, life,
                Random.Range(0.9f, 1.9f), Random.Range(0.75f, 1.15f), 3f, 3f, 1f, Hot);
        }

        private void EmitEmber(Vector2 point, Vector2 outward)
        {
            float angle = (Random.value + Random.value - 1f) * 120f * Mathf.Deg2Rad;
            Vector2 dir = Rotate(outward, angle);
            float speed = Random.Range(2f, 6f);
            float life = Random.Range(0.35f, 0.75f);
            Spawn(Kind.Ember, point, dir * speed, life, Random.Range(0.16f, 0.34f), 0f, 1.6f, 4f, 1f, Hot);
        }

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private void Spawn(Kind kind, Vector2 position, Vector2 velocity, float life, float size,
            float thickness, float drag, float gravity, float alpha, Color start)
        {
            int index = Acquire(kind);
            if (index < 0) return;

            Particle p = _particles[index];
            p.alive = true;
            p.kind = kind;
            p.position = position;
            p.velocity = velocity;
            p.age = 0f;
            p.life = life;
            p.size = size;
            p.thickness = thickness;
            p.drag = drag;
            p.gravity = gravity;
            p.alpha = alpha;

            p.renderer.enabled = true;
            switch (kind)
            {
                case Kind.Spark: p.renderer.sprite = ProceduralSprites.Spark; break;
                case Kind.Ring: p.renderer.sprite = ProceduralSprites.Ring; break;
                default: p.renderer.sprite = ProceduralSprites.SoftGlow; break;
            }
            p.renderer.color = new Color(start.r, start.g, start.b, alpha);
            p.transform.position = position;
            _particles[index] = p;
            Apply(index);
        }

        /// <summary>Finds a free slot of the right kind, growing the pool up to its cap.</summary>
        private int Acquire(Kind kind)
        {
            int used = 0;
            for (int i = 0; i < _particles.Count; i++)
            {
                if (_particles[i].kind != kind) continue;
                used++;
                if (!_particles[i].alive) return i;
            }

            if (used >= Cap(kind)) return -1;

            GameObject go = new GameObject(kind.ToString());
            go.transform.SetParent(transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = SortingOrder + (kind == Kind.Flare ? 0 : 1);
            sr.enabled = false;
            _particles.Add(new Particle { transform = go.transform, renderer = sr, kind = kind });
            return _particles.Count - 1;
        }

        private static int Cap(Kind kind)
        {
            switch (kind)
            {
                case Kind.Spark: return MaxSparks;
                case Kind.Ember: return MaxEmbers;
                case Kind.Flare: return MaxFlares;
                default: return MaxRings;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            int alive = 0;

            for (int i = 0; i < _particles.Count; i++)
            {
                Particle p = _particles[i];
                if (!p.alive) continue;

                p.age += dt;
                if (p.age >= p.life)
                {
                    p.alive = false;
                    p.renderer.enabled = false;
                    _particles[i] = p;
                    continue;
                }

                p.velocity *= Mathf.Max(0f, 1f - p.drag * dt);
                p.velocity.y -= p.gravity * dt;
                p.position += p.velocity * dt;
                _particles[i] = p;
                Apply(i);
                alive++;
            }

            if (_retired && alive == 0) Destroy(gameObject);
        }

        /// <summary>Writes a particle's transform and colour for its current age.</summary>
        private void Apply(int index)
        {
            Particle p = _particles[index];
            float t = Mathf.Clamp01(p.age / p.life);
            p.transform.position = p.position;

            Color color;
            switch (p.kind)
            {
                case Kind.Spark:
                {
                    // Hot white-yellow at birth, cooling to deep red as it flies; thinner and
                    // shorter toward the end so it seems to burn out.
                    color = t < 0.3f ? Color.Lerp(White, Yellow, t / 0.3f)
                        : t < 0.65f ? Color.Lerp(Yellow, Warm, (t - 0.3f) / 0.35f)
                        : Color.Lerp(Warm, Cool, (t - 0.65f) / 0.35f);
                    color.a = p.alpha * (1f - t * t);
                    float shrink = 1f - t * 0.65f;
                    p.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(p.velocity.y, p.velocity.x) * Mathf.Rad2Deg);
                    p.transform.localScale = new Vector3(p.size * shrink, p.thickness * shrink, 1f);
                    break;
                }
                case Kind.Ember:
                {
                    color = Color.Lerp(Hot, Cool, Mathf.Sqrt(t));
                    color.a = p.alpha * (1f - t);
                    p.transform.localScale = Vector3.one * (p.size * (1f - t * 0.5f));
                    break;
                }
                case Kind.Ring:
                {
                    color = Color.Lerp(Hot, Warm, t);
                    color.a = p.alpha * (1f - t) * (1f - t);
                    p.transform.localScale = Vector3.one * Mathf.Lerp(0.35f, p.size, 1f - (1f - t) * (1f - t));
                    break;
                }
                default: // Flare
                {
                    color = p.renderer.color;
                    color.a = p.alpha * (1f - t);
                    p.transform.localScale = Vector3.one * p.size;
                    break;
                }
            }

            p.renderer.color = color;
        }
    }
}
