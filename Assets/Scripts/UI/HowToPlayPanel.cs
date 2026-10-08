using UnityEngine;
using UnityEngine.UI;

namespace SpaceHawk.UI
{
    /// <summary>Reference panel accessible any time from Level Select - separate from
    /// MoveTutorialHint, which only ever shows once during a brand-new player's first level and
    /// only covers movement. This covers the whole ruleset (objective, power-ups, boss fights,
    /// energy, upgrades) and can be reopened whenever the player wants a reminder.</summary>
    public class HowToPlayPanel : MonoBehaviour
    {
        public Button closeButton;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        private void Close()
        {
            Destroy(gameObject);
        }
    }
}
