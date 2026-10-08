using System.Collections;
using UnityEngine;
using TMPro;
using SpaceHawk.Core;
using SpaceHawk.Gameplay;

namespace SpaceHawk.UI
{
    /// <summary>Shown once, ever, right as a brand-new player's very first level begins - the
    /// touch-drag control scheme isn't explained anywhere else in the game. Dismisses itself the
    /// moment the player actually drags (or after a timeout, in case they're just watching) and
    /// is never shown again on any later level or replay.</summary>
    public class MoveTutorialHint : MonoBehaviour
    {
        public CanvasGroup group;
        public RectTransform pulseCircle;
        public TMP_Text label;
        public PlayerShip player;
        public float autoDismissAfter = 6f;
        public float fadeDuration = 0.3f;

        private float _elapsed;
        private bool _dismissed;

        private void Awake()
        {
            if (pulseCircle == null) return;
            UnityEngine.UI.Image image = pulseCircle.GetComponent<UnityEngine.UI.Image>();
            if (image != null) image.sprite = ProceduralSprites.Ring;
        }

        public void Show()
        {
            if (SaveManager.HasSeenMoveTutorial())
            {
                gameObject.SetActive(false);
                return;
            }

            _elapsed = 0f;
            _dismissed = false;
            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(Fade(0f, 1f, fadeDuration));
        }

        private void Update()
        {
            if (_dismissed || !gameObject.activeInHierarchy) return;

            _elapsed += Time.unscaledDeltaTime;
            if (pulseCircle != null)
            {
                float pulse = 1f + Mathf.Sin(_elapsed * 3f) * 0.12f;
                pulseCircle.localScale = Vector3.one * pulse;
            }

            bool playerMoved = player != null && player.IsDragging;
            if (playerMoved || _elapsed >= autoDismissAfter) Dismiss();
        }

        private void Dismiss()
        {
            _dismissed = true;
            SaveManager.MarkMoveTutorialSeen();
            StopAllCoroutines();
            StartCoroutine(FadeOutAndDeactivate());
        }

        private IEnumerator FadeOutAndDeactivate()
        {
            yield return Fade(group != null ? group.alpha : 1f, 0f, fadeDuration);
            gameObject.SetActive(false);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (group == null) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, duration > 0f ? Mathf.Clamp01(t / duration) : 1f);
                yield return null;
            }
            group.alpha = to;
        }
    }
}
