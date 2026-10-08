using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceHawk.UI
{
    /// <summary>Fades this scene in from black on load - softens the hard cut whenever
    /// SceneManager.LoadScene swaps scenes. Only needed on MainMenu: Gameplay already gets its own
    /// reveal from LevelIntroBanner's doors, so adding this there too would just be a redundant,
    /// clashing second transition layered on top of that one.</summary>
    public class SceneFadeIn : MonoBehaviour
    {
        public Image overlay;
        public float duration = 0.35f;

        private void Start()
        {
            if (overlay == null) return;
            overlay.color = Color.black;
            StartCoroutine(Fade());
        }

        private IEnumerator Fade()
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float alpha = 1f - Mathf.Clamp01(t / duration);
                overlay.color = new Color(0f, 0f, 0f, alpha);
                yield return null;
            }
            overlay.color = new Color(0f, 0f, 0f, 0f);
            overlay.gameObject.SetActive(false);
        }
    }
}
