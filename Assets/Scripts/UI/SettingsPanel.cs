using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceHawk.Core;

namespace SpaceHawk.UI
{
    public class SettingsPanel : MonoBehaviour
    {
        [Tooltip("Tap to cycle through available screen resolutions.")]
        public Button resolutionButton;
        public TMP_Text resolutionLabel;
        [Tooltip("Dropdown-style arrow icon - hidden when the device only reports one resolution, since there's nothing to cycle through.")]
        public Image resolutionArrow;
        public Slider volumeSlider;
        public Button optionOnButton;
        public Image optionOnImage;
        public Button optionOffButton;
        public Image optionOffImage;
        public Toggle fullscreenToggle;
        public Button languageButton;
        public TMP_Text languageLabel;
        public Button closeButton;

        private Resolution[] _resolutions;
        private int _resolutionIndex;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (optionOnButton != null) optionOnButton.onClick.AddListener(() => SetSfxEnabled(true));
            if (optionOffButton != null) optionOffButton.onClick.AddListener(() => SetSfxEnabled(false));
            if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(SetVolume);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
            if (resolutionButton != null) resolutionButton.onClick.AddListener(CycleResolution);
            if (languageButton != null) languageButton.onClick.AddListener(Localization.CycleLanguage);
        }

        private void OnEnable()
        {
            PopulateResolutions();

            SaveData data = SaveManager.Data;
            if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(data.masterVolume);
            if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(data.fullscreen);
            RefreshOptionButtons(data.sfxEnabled);
            RefreshLanguageLabel();

            AudioListener.volume = data.masterVolume;
            Localization.LanguageChanged += RefreshLanguageLabel;
        }

        private void OnDisable()
        {
            Localization.LanguageChanged -= RefreshLanguageLabel;
        }

        private void RefreshLanguageLabel()
        {
            if (languageLabel == null) return;
            languageLabel.text = Localization.Get(Localization.Current == Language.Vietnamese ? "settings.language_vi" : "settings.language_en");
        }

        private void PopulateResolutions()
        {
            _resolutions = Screen.resolutions;
            if (_resolutions == null || _resolutions.Length == 0)
            {
                _resolutions = new[] { Screen.currentResolution };
            }

            int saved = SaveManager.Data.resolutionIndex;
            if (saved >= 0 && saved < _resolutions.Length)
            {
                _resolutionIndex = saved;
            }
            else
            {
                _resolutionIndex = 0;
                for (int i = 0; i < _resolutions.Length; i++)
                {
                    if (_resolutions[i].width == Screen.currentResolution.width &&
                        _resolutions[i].height == Screen.currentResolution.height)
                    {
                        _resolutionIndex = i;
                        break;
                    }
                }
            }

            RefreshResolutionLabel();
            if (resolutionArrow != null) resolutionArrow.gameObject.SetActive(_resolutions.Length > 1);
        }

        private void CycleResolution()
        {
            if (_resolutions == null || _resolutions.Length == 0) return;
            _resolutionIndex = (_resolutionIndex + 1) % _resolutions.Length;
            Resolution r = _resolutions[_resolutionIndex];
            Screen.SetResolution(r.width, r.height, Screen.fullScreen);
            SaveManager.SetDisplaySettings(Screen.fullScreen, _resolutionIndex);
            RefreshResolutionLabel();
        }

        // The row's own title label above this button already reads "Resolution"/"Độ phân giải" -
        // repeating that word inside the button too was what overflowed past the arrow in
        // Vietnamese (a much longer word than "RES"), so this only ever shows the value itself,
        // matching how the Language row below it already just shows "Tiếng Việt"/"English".
        private void RefreshResolutionLabel()
        {
            if (resolutionLabel == null || _resolutions == null || _resolutions.Length == 0) return;
            Resolution r = _resolutions[_resolutionIndex];
            resolutionLabel.text = $"{r.width} X {r.height}";
        }

        private void SetVolume(float value)
        {
            AudioListener.volume = value;
            SaveManager.SetAudioSettings(value, SaveManager.Data.sfxEnabled);
        }

        private void SetSfxEnabled(bool enabled)
        {
            SaveManager.SetAudioSettings(SaveManager.Data.masterVolume, enabled);
            RefreshOptionButtons(enabled);
        }

        // Both buttons always stay clickable - tap either one any time to pick it. Which one is
        // currently active is shown by brightness alone (dimming the unselected one) instead of
        // Unity's disabled-sprite state, since that showed up as a plain white box rather than a
        // properly styled button.
        private static readonly Color SelectedTint = Color.white;
        private static readonly Color UnselectedTint = new Color(1f, 1f, 1f, 0.4f);

        private void RefreshOptionButtons(bool sfxEnabled)
        {
            if (optionOnImage != null) optionOnImage.color = sfxEnabled ? SelectedTint : UnselectedTint;
            if (optionOffImage != null) optionOffImage.color = sfxEnabled ? UnselectedTint : SelectedTint;
        }

        private void SetFullscreen(bool value)
        {
            Screen.fullScreen = value;
            SaveManager.SetDisplaySettings(value, SaveManager.Data.resolutionIndex);
        }

        private void Close()
        {
            Destroy(gameObject);
        }
    }
}
