using UnityEngine;

namespace SpaceHawk.Gameplay
{
    /// <summary>Simple singleton screen shake - punchier feedback for hits, deaths and impacts.</summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        private Vector3 _originalLocalPos;
        private float _duration;
        private float _magnitude;
        private float _timer;

        private void Awake()
        {
            Instance = this;
            _originalLocalPos = transform.localPosition;
        }

        public void Shake(float duration, float magnitude)
        {
            _duration = duration;
            _magnitude = magnitude;
            _timer = duration;
        }

        private void LateUpdate()
        {
            if (_timer <= 0f) return;

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                transform.localPosition = _originalLocalPos;
                return;
            }

            Vector2 offset = Random.insideUnitCircle * _magnitude * (_timer / _duration);
            transform.localPosition = _originalLocalPos + (Vector3)offset;
        }
    }
}
