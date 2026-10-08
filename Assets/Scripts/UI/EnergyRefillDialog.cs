using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceHawk.Core;

namespace SpaceHawk.UI
{
    /// <summary>Shown instead of a bare "not enough energy" toast: the player can spend Crystals to
    /// refill completely right now, or close it and come back when energy has regenerated.</summary>
    public class EnergyRefillDialog : MonoBehaviour
    {
        public Button refillButton;
        public TMP_Text refillLabel;
        public Button closeButton;

        private void Awake()
        {
            if (refillButton != null) refillButton.onClick.AddListener(RefillWithCrystals);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            RefreshLabels();
            Localization.LanguageChanged += RefreshLabels;
        }

        private void OnDisable()
        {
            Localization.LanguageChanged -= RefreshLabels;
        }

        /// <summary>Falls back to the plain toast if the prefab hasn't been built, so running out
        /// of energy is still explained rather than silently doing nothing.</summary>
        public static void Show(Transform parent)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/EnergyRefillDialog");
            if (prefab == null)
            {
                ToastUI.ShowToast(parent, Localization.Get("levelselect.not_enough_energy"));
                return;
            }
            Instantiate(prefab, parent);
        }

        private void RefreshLabels()
        {
            if (refillLabel != null)
                refillLabel.text = Localization.Format("energy.refill_fmt", SaveManager.EnergyRefillCrystalCost);
        }

        private void RefillWithCrystals()
        {
            if (SaveManager.GetEnergy() >= SaveManager.MaxEnergy)
            {
                ToastUI.ShowToast(transform, Localization.Get("energy.already_full"));
                return;
            }

            if (!SaveManager.TryRefillEnergyWithCrystals())
            {
                ToastUI.ShowToast(transform, Localization.Get("common.not_enough_crystals"));
                return;
            }

            AudioManager.Play(GameAudio.Pickup, 0.55f);
            ToastUI.ShowToast(transform.parent != null ? transform.parent : transform, Localization.Get("energy.refilled"));
            Close();
        }

        private void Close()
        {
            Destroy(gameObject);
        }
    }
}
