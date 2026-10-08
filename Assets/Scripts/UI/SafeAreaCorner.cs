using UnityEngine;

namespace SpaceHawk.UI
{
    /// <summary>Nudges a corner-anchored RectTransform inward to clear Screen.safeArea. Without
    /// this, anything anchored straight to a raw canvas corner (a Settings icon at anchor (1,1),
    /// an overlay's X close button, etc.) can land under a real device's rounded corners or
    /// camera cutout - visually off, and on some devices the OS itself won't deliver touches
    /// there even though Unity still considers it valid canvas space.
    ///
    /// Attach directly to the element being adjusted; it reads its own anchors to know which
    /// edges to inset and by how much, so the same component works on any corner (top-right,
    /// top-left, bottom-right, ...) without configuration.</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaCorner : MonoBehaviour
    {
        private void Start()
        {
            RectTransform rt = (RectTransform)transform;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null || canvas.scaleFactor <= 0f) return;

            Rect safe = Screen.safeArea;
            float scale = canvas.scaleFactor;

            float unsafeLeft = safe.xMin / scale;
            float unsafeRight = (Screen.width - safe.xMax) / scale;
            float unsafeBottom = safe.yMin / scale;
            float unsafeTop = (Screen.height - safe.yMax) / scale;

            // Midpoint comparison, not an exact 0/1 anchor check - some corner elements (the
            // shared header close button) sit at a fractional anchor like 0.938 to track a notch
            // drawn into the header art itself, not a plain (1,1) corner. Anything closer to an
            // edge than to the center still needs pushing off that edge.
            Vector2 pos = rt.anchoredPosition;
            if (rt.anchorMin.x > 0.5f) pos.x -= unsafeRight;        // right-ish anchored: push left
            else if (rt.anchorMax.x < 0.5f) pos.x += unsafeLeft;    // left-ish anchored: push right
            if (rt.anchorMin.y > 0.5f) pos.y -= unsafeTop;          // top-ish anchored: push down
            else if (rt.anchorMax.y < 0.5f) pos.y += unsafeBottom;  // bottom-ish anchored: push up
            rt.anchoredPosition = pos;
        }
    }
}
