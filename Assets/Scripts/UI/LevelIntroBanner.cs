using System;
using System.Collections;
using UnityEngine;
using TMPro;

namespace SpaceHawk.UI
{
    /// <summary>Full-width "blast doors" level-start transition: two dark panels slide in from
    /// off-screen to cover the whole play area, the level title and win objective show while
    /// covered, then the doors slide back open to reveal gameplay. Blocking by design -
    /// GameplayController holds off starting enemy spawns until onComplete fires, so the fight
    /// never starts hidden behind (or racing) this sequence.</summary>
    public class LevelIntroBanner : MonoBehaviour
    {
        public RectTransform leftDoor;
        public RectTransform rightDoor;
        public CanvasGroup textGroup;
        public TMP_Text titleLabel;
        public TMP_Text objectiveLabel;

        public float doorSlideInDuration = 0.4f;
        public float textFadeDuration = 0.25f;
        public float holdDuration = 1.8f;
        public float doorSlideOutDuration = 0.55f;

        public void Show(string title, string objective, Action onComplete)
        {
            if (titleLabel != null) titleLabel.text = title;
            if (objectiveLabel != null) objectiveLabel.text = objective;
            if (textGroup != null) textGroup.alpha = 0f;

            gameObject.SetActive(true);
            SetDoorsOpen(); // always start from the fully-open resting pose before closing in
            StopAllCoroutines();
            StartCoroutine(PlaySequence(onComplete));
        }

        private IEnumerator PlaySequence(Action onComplete)
        {
            yield return SlideDoors(closing: true, doorSlideInDuration);
            yield return Fade(0f, 1f, textFadeDuration);
            yield return new WaitForSecondsRealtime(holdDuration);
            yield return Fade(1f, 0f, textFadeDuration);
            yield return SlideDoors(closing: false, doorSlideOutDuration);

            gameObject.SetActive(false);
            onComplete?.Invoke();
        }

        private void SetDoorsOpen()
        {
            if (leftDoor != null)
            {
                float w = leftDoor.rect.width;
                leftDoor.anchoredPosition = new Vector2(-w, leftDoor.anchoredPosition.y);
            }
            if (rightDoor != null)
            {
                float w = rightDoor.rect.width;
                rightDoor.anchoredPosition = new Vector2(w, rightDoor.anchoredPosition.y);
            }
        }

        private IEnumerator SlideDoors(bool closing, float duration)
        {
            if (leftDoor == null || rightDoor == null) yield break;

            float leftW = leftDoor.rect.width;
            float rightW = rightDoor.rect.width;
            float leftFrom = closing ? -leftW : 0f;
            float leftTo = closing ? 0f : -leftW;
            float rightFrom = closing ? rightW : 0f;
            float rightTo = closing ? 0f : rightW;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float pct = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
                float eased = 1f - Mathf.Pow(1f - pct, 3f); // ease-out cubic - fast start, gentle stop
                leftDoor.anchoredPosition = new Vector2(Mathf.LerpUnclamped(leftFrom, leftTo, eased), leftDoor.anchoredPosition.y);
                rightDoor.anchoredPosition = new Vector2(Mathf.LerpUnclamped(rightFrom, rightTo, eased), rightDoor.anchoredPosition.y);
                yield return null;
            }
            leftDoor.anchoredPosition = new Vector2(leftTo, leftDoor.anchoredPosition.y);
            rightDoor.anchoredPosition = new Vector2(rightTo, rightDoor.anchoredPosition.y);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (textGroup == null) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float pct = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
                textGroup.alpha = Mathf.Lerp(from, to, pct);
                yield return null;
            }
            textGroup.alpha = to;
        }
    }
}
