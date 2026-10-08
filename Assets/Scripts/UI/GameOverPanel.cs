using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceHawk.Core;
using SpaceHawk.Data;

namespace SpaceHawk.UI
{
    public class GameOverPanel : MonoBehaviour
    {
        public Button retryButton;
        public Button menuButton;
        public TMP_Text reasonLabel;
        [Tooltip("Hidden when reviving isn't possible (not-enough-kills defeat, or already revived once this attempt).")]
        public GameObject reviveRow;
        public Button reviveButton;
        public TMP_Text reviveLabel;

        private Action _onRevive;

        private void Awake()
        {
            if (retryButton != null) retryButton.onClick.AddListener(Retry);
            if (menuButton != null) menuButton.onClick.AddListener(ToMenu);
            if (reviveButton != null) reviveButton.onClick.AddListener(Revive);
        }

        private void OnEnable()
        {
            RefreshReviveLabel();
            Localization.LanguageChanged += RefreshReviveLabel;
        }

        private void OnDisable()
        {
            Localization.LanguageChanged -= RefreshReviveLabel;
        }

        /// <summary>Called by GameplayController right after instantiating this panel, before the
        /// player sees it, so it's clear whether they died or simply didn't destroy enough enemies.</summary>
        public void SetReason(string text)
        {
            if (reasonLabel != null) reasonLabel.text = text;
        }

        /// <summary>`onRevive` runs once the player has paid the Crystals - the caller does the
        /// actual bringing-back-to-life and closes this panel.</summary>
        public void SetReviveOption(bool allowed, Action onRevive)
        {
            _onRevive = onRevive;
            if (reviveRow != null) reviveRow.SetActive(allowed);
        }

        private void RefreshReviveLabel()
        {
            if (reviveLabel != null)
                reviveLabel.text = Localization.Format("gameover.revive_fmt", SaveManager.ReviveCrystalCost);
        }

        private void Revive()
        {
            if (!SaveManager.TrySpendCrystals(SaveManager.ReviveCrystalCost))
            {
                ToastUI.ShowToast(transform, Localization.Get("common.not_enough_crystals"));
                return;
            }
            _onRevive?.Invoke();
        }

        private void Retry()
        {
            LevelData level = GameManager.SelectedLevel;
            int cost = level != null ? level.energyCost : 0;

            // Same energy gate LevelSelectUI applies when first entering a level - without this,
            // retrying after a loss was completely free, unlike every other way into a level.
            if (!SaveManager.TrySpendEnergy(cost))
            {
                EnergyRefillDialog.Show(transform);
                return;
            }

            GameManager.RestartCurrentLevel();
        }

        private void ToMenu()
        {
            GameManager.ReturnToMenu(true);
        }
    }
}
