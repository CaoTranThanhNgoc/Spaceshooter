using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace SpaceHawk.UI
{
    /// <summary>A short notice at the bottom of the screen. Its frame fits whatever it says: the text wraps,
    /// shrinks a little before a long message gets tall, and the frame grows to hold every line - so a
    /// long Vietnamese sentence never spills out of its box. Several notices at once stack upwards (the
    /// newest at the bottom) instead of being drawn on top of each other, and saying the same thing
    /// twice only keeps the one that is already showing.</summary>
    public class ToastUI : MonoBehaviour
    {
        public TMP_Text label;
        public float lifetime = 1.5f;

        private const float MaxWidth = 760f;
        private const float MinWidth = 360f;
        private const float MinHeight = 74f;
        private const float HorizontalPadding = 36f;
        private const float VerticalPadding = 16f;
        private const float StartFontSize = 30f;
        private const float MinFontSize = 18f;
        private const int ComfortableLines = 2;      // below this size the text is allowed to take more lines
        private const float ScreenMargin = 24f;
        private const float BottomMargin = 20f;
        private const float Gap = 10f;
        private const int MaxStacked = 4;

        private static readonly List<ToastUI> Active = new List<ToastUI>();

        private Coroutine _closing;

        public string Message { get; private set; }

        public void Show(string message)
        {
            Message = message;
            Fit(message);
            Active.Add(this);
            TrimOldest();
            Relayout(transform.parent);
            Restart();
        }

        /// <summary>Keeps the notice up for another while (the same message was asked for again).</summary>
        private void Restart()
        {
            if (_closing != null) StopCoroutine(_closing);
            // Longer messages need longer to read.
            float seconds = lifetime + Mathf.Clamp((Message ?? "").Length * 0.02f, 0f, 2.5f);
            _closing = StartCoroutine(CloseAfter(seconds));
        }

        // Realtime, not Invoke's game-time scheduling - a toast can be triggered from a panel
        // shown with Time.timeScale=0 (Victory/Game Over), where Invoke would simply never fire
        // and leave the toast stuck on screen forever.
        private IEnumerator CloseAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            Active.Remove(this);
            Relayout(transform.parent);
        }

        // ------------------------------------------------------------------ sizing

        /// <summary>Sizes the frame and the text for `message`: the biggest font (down to MinFontSize) that
        /// keeps it within a couple of lines, then a frame exactly as wide and tall as that text needs.</summary>
        private void Fit(string message)
        {
            RectTransform frame = (RectTransform)transform;
            float maxWidth = AvailableWidth();
            float textWidth = maxWidth - 2f * HorizontalPadding;
            if (label == null) return;

            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;

            float size = StartFontSize;
            Vector2 preferred = Vector2.zero;
            for (; size >= MinFontSize; size -= 2f)
            {
                label.fontSize = size;
                preferred = label.GetPreferredValues(message, textWidth, 0f);
                if (preferred.y <= size * 1.3f * ComfortableLines) break;
            }
            size = Mathf.Max(size, MinFontSize);
            label.fontSize = size;

            // Narrower frame when the text is short; the height is re-measured at the final width.
            float width = Mathf.Clamp(preferred.x + 2f * HorizontalPadding + 4f, MinWidth, maxWidth);
            preferred = label.GetPreferredValues(message, width - 2f * HorizontalPadding, 0f);
            float height = Mathf.Max(MinHeight, preferred.y + 2f * VerticalPadding);

            frame.sizeDelta = new Vector2(width, height);
            RectTransform text = label.rectTransform;
            text.anchorMin = Vector2.zero;
            text.anchorMax = Vector2.one;
            text.offsetMin = new Vector2(HorizontalPadding, VerticalPadding);
            text.offsetMax = new Vector2(-HorizontalPadding, -VerticalPadding);
            label.text = message;
        }

        private float AvailableWidth()
        {
            float parentWidth = transform.parent is RectTransform parent ? parent.rect.width : 0f;
            return parentWidth > 0f ? Mathf.Clamp(parentWidth - 2f * ScreenMargin, MinWidth, MaxWidth) : MaxWidth;
        }

        // ------------------------------------------------------------------ stacking

        /// <summary>Piles the live notices of one parent from the bottom up, newest first.</summary>
        private static void Relayout(Transform parent)
        {
            float y = BottomMargin;
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                ToastUI toast = Active[i];
                if (toast == null || toast.transform.parent != parent) continue;
                RectTransform rt = (RectTransform)toast.transform;
                rt.anchoredPosition = new Vector2(0f, y);
                y += rt.rect.height + Gap;
            }
        }

        private void TrimOldest()
        {
            int count = 0;
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                if (Active[i] == null || Active[i].transform.parent != transform.parent) continue;
                if (++count > MaxStacked) Destroy(Active[i].gameObject);
            }
        }

        public static void ShowToast(Transform parent, string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            // The same notice is already up (e.g. a button tapped again): just keep it a little longer.
            for (int i = 0; i < Active.Count; i++)
            {
                ToastUI shown = Active[i];
                if (shown != null && shown.transform.parent == parent && shown.Message == message)
                {
                    shown.Restart();
                    return;
                }
            }

            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/Toast");
            if (prefab == null)
            {
                Debug.Log("[Toast] " + message);
                return;
            }
            GameObject go = Instantiate(prefab, parent);
            ToastUI toast = go.GetComponent<ToastUI>();
            if (toast != null) toast.Show(message);
        }
    }
}
