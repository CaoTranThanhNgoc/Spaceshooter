using UnityEngine;

namespace SpaceHawk.UI
{
    /// <summary>Repositions same-size child cells (top-left pivot, anchored to this RectTransform's
    /// top-left) into as many columns as fit the current width, centering any leftover space.
    /// Runs on enable and whenever this RectTransform is resized, so a grid baked for one aspect
    /// ratio (e.g. a narrow tablet) automatically spreads into more columns on a wider one
    /// (e.g. a phone in landscape) instead of leaving empty space on one side.</summary>
    [ExecuteAlways]
    public class ResponsiveGridReflow : MonoBehaviour
    {
        public RectTransform[] cells;
        public Vector2 cellSize = new Vector2(210, 280);
        public Vector2 spacing = new Vector2(22, 30);
        public int minColumns = 1;
        public int maxColumns = 6;

        private RectTransform _rect;

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        private void OnEnable()
        {
            Reflow();
        }

        private void OnRectTransformDimensionsChange()
        {
            Reflow();
        }

        public int ComputeColumns(float width)
        {
            int cap = (cells != null && cells.Length > 0) ? Mathf.Min(maxColumns, cells.Length) : maxColumns;
            int fit = Mathf.FloorToInt((width + spacing.x) / (cellSize.x + spacing.x));
            return Mathf.Clamp(fit, minColumns, Mathf.Max(minColumns, cap));
        }

        /// <summary>Reflows using the RectTransform's own current width - correct at runtime,
        /// once this sits under a live Canvas that has actually resolved a device size.</summary>
        public void Reflow()
        {
            if (_rect == null) _rect = (RectTransform)transform;
            if (_rect != null) Reflow(_rect.rect.width);
        }

        /// <summary>Reflows against an explicit width. Used by the Editor build scripts to bake a
        /// sane starting arrangement into the saved prefab (outside Play mode, before this sits
        /// under a live Canvas, RectTransform.rect.width isn't resolved yet) - runtime still
        /// recomputes for the real device via OnEnable/OnRectTransformDimensionsChange.</summary>
        public void Reflow(float width)
        {
            if (cells == null || cells.Length == 0 || width <= 0f) return;

            int columns = ComputeColumns(width);
            float usedWidth = columns * cellSize.x + (columns - 1) * spacing.x;
            float offsetX = Mathf.Max(0f, (width - usedWidth) * 0.5f);

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == null) continue;
                int col = i % columns;
                int row = i / columns;
                float x = offsetX + col * (cellSize.x + spacing.x);
                float y = -row * (cellSize.y + spacing.y);
                cells[i].anchoredPosition = new Vector2(x, y);
            }
        }
    }
}
