using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceHawk.Data;

namespace SpaceHawk.UI
{
    public class LevelNodeView : MonoBehaviour
    {
        public Button button;
        public Image background;
        public TMP_Text numberLabel;
        public GameObject lockIcon;

        [Tooltip("Index 0..3 = the node's look with 0,1,2,3 stars earned (Lvl0Star..Lvl3Star).")]
        public Sprite[] starBackgrounds;
        public Sprite lockedBackground;

        private LevelData _level;
        private System.Action<LevelData> _onSelected;

        public void Setup(LevelData level, bool unlocked, int stars, System.Action<LevelData> onSelected)
        {
            _level = level;
            _onSelected = onSelected;

            RectTransform rt = (RectTransform)transform;
            rt.anchorMin = level.mapAnchor;
            rt.anchorMax = level.mapAnchor;
            rt.anchoredPosition = Vector2.zero;

            if (numberLabel != null) numberLabel.text = level.displayNumber.ToString();
            if (lockIcon != null) lockIcon.SetActive(!unlocked);

            if (background != null)
            {
                if (!unlocked && lockedBackground != null)
                {
                    background.sprite = lockedBackground;
                }
                else if (starBackgrounds != null && stars >= 0 && stars < starBackgrounds.Length && starBackgrounds[stars] != null)
                {
                    background.sprite = starBackgrounds[stars];
                }
            }

            if (button != null)
            {
                button.interactable = unlocked;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _onSelected?.Invoke(_level));
            }
        }
    }
}
