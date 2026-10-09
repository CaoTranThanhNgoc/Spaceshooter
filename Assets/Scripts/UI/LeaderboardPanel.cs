using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceHawk.Core;
using SpaceHawk.Online;

namespace SpaceHawk.UI
{
    /// <summary>Fixed set of pre-built row slots (same pattern as AchievementsPanel/HowToPlay),
    /// populated from a live GetTopScores call once the panel opens instead of at build time -
    /// this is the one panel whose content genuinely can't be known ahead of time.</summary>
    public class LeaderboardPanel : MonoBehaviour
    {
        public Button closeButton;
        public LeaderboardRowView[] rows;
        public GameObject loadingLabel;
        public GameObject errorLabel;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        // Naming now happens up front on Level Select (see LevelSelectUI's player icon button and
        // its own first-visit auto-prompt) - by the time a player can even reach this panel, a
        // name already exists, so this just loads scores straight away.
        private void OnEnable()
        {
            SaveManager.ProfileChanged += OnProfileChanged;
            Load();
        }

        private void OnDisable()
        {
            SaveManager.ProfileChanged -= OnProfileChanged;
        }

        // Another account took over while the board is open: its rows and highlight are somebody else's now. A
        // guest has no board at all (the way here is closed for guests), so the panel goes away.
        private void OnProfileChanged()
        {
            if (this == null) return;
            if (!AccountManager.IsLinked)
            {
                Close();
                return;
            }
            Load();
        }

        private async void Load()
        {
            SetRowsActive(false);
            if (loadingLabel != null) loadingLabel.SetActive(true);
            if (errorLabel != null) errorLabel.SetActive(false);

            int requestCount = rows != null ? rows.Length : 10;
            List<LeaderboardRow> top = await LeaderboardManager.GetTopScores(requestCount);

            // The panel (or whole scene) may have been closed/unloaded while this was in flight.
            if (this == null || !gameObject) return;

            if (loadingLabel != null) loadingLabel.SetActive(false);

            if (top.Count == 0)
            {
                if (errorLabel != null) errorLabel.SetActive(true);
                return;
            }

            int count = rows != null ? Mathf.Min(rows.Length, top.Count) : 0;
            for (int i = 0; i < count; i++)
            {
                if (rows[i] == null) continue;
                rows[i].gameObject.SetActive(true);
                rows[i].Setup(top[i]);
            }
        }

        private void SetRowsActive(bool active)
        {
            if (rows == null) return;
            foreach (LeaderboardRowView row in rows)
                if (row != null) row.gameObject.SetActive(active);
        }

        private void Close()
        {
            Destroy(gameObject);
        }
    }
}
