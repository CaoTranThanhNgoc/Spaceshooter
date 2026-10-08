using UnityEngine;

namespace SpaceHawk.Gameplay
{
    /// <summary>Turns on the one background set (out of the 4 pre-built Space_BG_0X sets) that
    /// the current level asks for; the other sets stay inactive so their ParallaxLayers don't run.</summary>
    public class ParallaxBackgroundController : MonoBehaviour
    {
        public GameObject[] sets;

        public int SetCount => sets != null ? sets.Length : 0;

        public void ActivateSet(int index)
        {
            if (sets == null || sets.Length == 0) return;
            index = Mathf.Clamp(index, 0, sets.Length - 1);
            for (int i = 0; i < sets.Length; i++)
            {
                if (sets[i] != null) sets[i].SetActive(i == index);
            }
        }
    }
}
