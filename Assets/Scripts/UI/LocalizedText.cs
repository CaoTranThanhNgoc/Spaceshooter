using TMPro;
using UnityEngine;
using SpaceHawk.Core;

namespace SpaceHawk.UI
{
    /// <summary>Drives a TMP_Text's content from Localization instead of a fixed string, and
    /// re-applies live whenever the language changes (even on already-open panels).</summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        public string key;

        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            Apply();
            Localization.LanguageChanged += Apply;
        }

        private void OnDisable()
        {
            Localization.LanguageChanged -= Apply;
        }

        private void Apply()
        {
            if (_text != null && !string.IsNullOrEmpty(key)) _text.text = Localization.Get(key);
        }
    }
}
