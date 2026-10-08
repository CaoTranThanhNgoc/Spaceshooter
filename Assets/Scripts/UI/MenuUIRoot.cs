using UnityEngine;
using SpaceHawk.Core;

namespace SpaceHawk.UI
{
    /// <summary>Switches between the Landing screen and the Level Select screen inside the
    /// single MainMenu scene (no separate scene load needed for that navigation step).</summary>
    public class MenuUIRoot : MonoBehaviour
    {
        public GameObject landingPanel;
        public GameObject levelSelectPanel;

        private void Start()
        {
            ShowLevelSelect(GameManager.ReturnToLevelSelectOnMenuLoad);
        }

        public void ShowLevelSelect(bool show)
        {
            if (landingPanel != null) landingPanel.SetActive(!show);
            if (levelSelectPanel != null) levelSelectPanel.SetActive(show);
        }
    }
}
