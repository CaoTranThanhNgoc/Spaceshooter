using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceHawk.Online;

namespace SpaceHawk.UI
{
    public class LeaderboardRowView : MonoBehaviour
    {
        public Image background;
        public TMP_Text rankLabel;
        public TMP_Text nameLabel;
        public TMP_Text scoreLabel;

        // Highlighting the current player by tinting the row's own (dark) background sprite
        // couldn't reliably brighten it - multiplying a dark sprite's pixels can only ever darken
        // them further. Recoloring the text itself gold instead is visible no matter how dark the
        // background renders.
        private static readonly Color NormalTextColor = new Color(0.85f, 0.95f, 0.97f, 1f);
        private static readonly Color HighlightTextColor = new Color(1f, 0.83f, 0.2f, 1f);

        public void Setup(LeaderboardRow data)
        {
            if (rankLabel != null) rankLabel.text = $"#{data.rank}";
            if (nameLabel != null) nameLabel.text = data.playerName;
            if (scoreLabel != null) scoreLabel.text = data.score.ToString();

            Color textColor = data.isCurrentPlayer ? HighlightTextColor : NormalTextColor;
            if (rankLabel != null) rankLabel.color = textColor;
            if (nameLabel != null) nameLabel.color = textColor;
        }
    }
}
