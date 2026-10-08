using UnityEngine;

namespace SpaceHawk.Gameplay
{
    /// <summary>
    /// Plays a plain sequence of sprites at a fixed rate. Reused for ship exhaust (loop),
    /// explosions and muzzle flashes (play once, then optionally destroy the object) since
    /// all three asset sets are just numbered frame sequences.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class FrameAnimatedFX : MonoBehaviour
    {
        public Sprite[] frames;
        public float frameRate = 12f;
        public bool loop = true;
        public bool destroyOnComplete = false;

        private SpriteRenderer _renderer;
        private float _timer;
        private int _index;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            _timer = 0f;
            _index = 0;
            if (frames != null && frames.Length > 0) _renderer.sprite = frames[0];
        }

        /// <summary>Safe to call before this component's own Awake has run (e.g. a parent's Awake
        /// reaching into a child right after Instantiate/scene load - Unity doesn't guarantee
        /// parent-before-child Awake order, and on IL2CPP this reliably ran before Awake in
        /// practice even though it happened to work out under Mono in the Editor).</summary>
        public void SetFrames(Sprite[] newFrames)
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();

            frames = newFrames;
            _index = 0;
            _timer = 0f;
            if (frames != null && frames.Length > 0) _renderer.sprite = frames[0];
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0 || frameRate <= 0f) return;

            _timer += Time.deltaTime;
            float frameDuration = 1f / frameRate;
            while (_timer >= frameDuration)
            {
                _timer -= frameDuration;
                _index++;
                if (_index >= frames.Length)
                {
                    if (loop)
                    {
                        _index = 0;
                    }
                    else
                    {
                        _index = frames.Length - 1;
                        _renderer.sprite = frames[_index];
                        if (destroyOnComplete) Destroy(gameObject);
                        return;
                    }
                }
                _renderer.sprite = frames[_index];
            }
        }
    }
}
