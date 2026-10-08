using UnityEngine;
using UnityEngine.UI;
using SpaceHawk.Core;

namespace SpaceHawk.UI
{
    /// <summary>Attached to every button built by UIFactory so the whole game gets a click sound
    /// for free, without each screen's own script needing to wire it up.</summary>
    [RequireComponent(typeof(Button))]
    public class ButtonClickSound : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(() => AudioManager.Play(ProceduralAudio.Click, 0.4f));
        }
    }
}
