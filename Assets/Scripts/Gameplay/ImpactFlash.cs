using UnityEngine;

namespace SpaceHawk.Gameplay
{
    /// <summary>Quick scale-up + fade-out flash used for bullet impacts - cheap "hit feedback"
    /// that doesn't need a frame sequence.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ImpactFlash : MonoBehaviour
    {
        public float duration = 0.18f;
        public float startScale = 0.35f;
        public float endScale = 0.9f;

        private SpriteRenderer _sr;
        private float _timer;
        private Color _baseColor;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _baseColor = _sr.color;
            transform.localScale = Vector3.one * startScale;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / duration);

            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
            Color c = _baseColor;
            c.a = Mathf.Lerp(_baseColor.a, 0f, t);
            _sr.color = c;

            if (t >= 1f) Destroy(gameObject);
        }
    }
}
