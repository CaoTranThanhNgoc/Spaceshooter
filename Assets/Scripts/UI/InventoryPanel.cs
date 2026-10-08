using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceHawk.Core;
using SpaceHawk.Data;

namespace SpaceHawk.UI
{
    /// <summary>The Inventory: level the ship up with Crystals, and a hangar of fifteen ships
    /// (three families of five tiers - see ShipCatalog). Each ship is bought once for good and has
    /// its own stats and abilities. Tapping a card previews that ship on the big portrait with its
    /// numbers and abilities; the button under the portrait then equips it (free, any time, for
    /// ships already bought) or buys it when its requirements are met.</summary>
    public class InventoryPanel : MonoBehaviour
    {
        public Button closeButton;
        public ShipHullSprites[] hulls;

        [Header("Preview")]
        public Image shipImage;
        public TMP_Text nameLabel;
        public TMP_Text roleLabel;
        public Image hpFill;
        public TMP_Text hpLabel;
        public Image dmgFill;
        public TMP_Text dmgLabel;
        public Image rateFill;
        public TMP_Text rateLabel;
        public TMP_Text perksLabel;
        public Button hullActionButton;
        public TMP_Text hullActionLabel;

        [Header("Upgrade")]
        public Button upgradeButton;
        public TMP_Text upgradeButtonLabel;
        public TMP_Text techLabel;
        public TMP_Text crystalLabel;

        [Header("Hangar: family tabs")]
        public Button[] familyTabs;
        public Image[] familyTabBackgrounds;
        public TMP_Text[] familyTabLabels;

        [Header("Hangar: the five ships of the open family")]
        public Button[] cardButtons;
        public Image[] cardBackgrounds;
        public Image[] cardIcons;
        public GameObject[] cardLocks;
        public TMP_Text[] cardNames;
        public TMP_Text[] cardStatusLabels;
        public GameObject[] cardPriceRows;
        public TMP_Text[] cardPriceLabels;

        public Sprite plainSprite;
        public Sprite selectedSprite;
        public Transform toastRoot;

        private static readonly Color EquippedColor = new Color(1f, 0.83f, 0.2f, 1f);
        private static readonly Color OwnedColor = new Color(0.63f, 0.93f, 0.93f, 1f);
        private static readonly Color LockedIconTint = new Color(0.45f, 0.45f, 0.5f, 1f);
        private static readonly Color LockedPortraitTint = new Color(0.7f, 0.7f, 0.76f, 1f);
        private static readonly Color PriceColor = new Color(1f, 0.83f, 0.2f, 1f);
        private static readonly Color PriceBlockedColor = new Color(0.62f, 0.62f, 0.6f, 1f);

        // The ship shown on the big portrait. Starts on the worn one; tapping a card moves it.
        private int _preview = -1;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (upgradeButton != null) upgradeButton.onClick.AddListener(Upgrade);
            if (hullActionButton != null) hullActionButton.onClick.AddListener(OnActionClicked);

            if (familyTabs != null)
            {
                for (int i = 0; i < familyTabs.Length; i++)
                {
                    int family = i;
                    if (familyTabs[i] != null) familyTabs[i].onClick.AddListener(() => OpenFamily(family));
                }
            }

            if (cardButtons != null)
            {
                for (int i = 0; i < cardButtons.Length; i++)
                {
                    int tier = i;
                    if (cardButtons[i] != null) cardButtons[i].onClick.AddListener(() => PreviewShip(ShipCatalog.IndexOf(PreviewFamily, tier)));
                }
            }
        }

        private void OnEnable()
        {
            _preview = SaveManager.GetSelectedShip();
            Refresh();
            SaveManager.CrystalsChanged += OnCrystalsChanged;
            SaveManager.SelectedShipChanged += OnSelectedShipChanged;
            Localization.LanguageChanged += Refresh;
        }

        private void OnDisable()
        {
            SaveManager.CrystalsChanged -= OnCrystalsChanged;
            SaveManager.SelectedShipChanged -= OnSelectedShipChanged;
            Localization.LanguageChanged -= Refresh;
        }

        public int PreviewIndex => _preview < 0 ? SaveManager.GetSelectedShip() : _preview;
        private int PreviewFamily => ShipCatalog.FamilyOf(PreviewIndex);

        /// <summary>Shows a ship on the portrait without changing what is worn or spending anything.</summary>
        public void PreviewShip(int shipIndex)
        {
            if (shipIndex < 0 || shipIndex >= ShipCatalog.Count) return;
            _preview = shipIndex;
            Refresh();
        }

        private void OpenFamily(int family)
        {
            if (family == PreviewFamily) return;
            int worn = SaveManager.GetSelectedShip();
            PreviewShip(ShipCatalog.FamilyOf(worn) == family ? worn : ShipCatalog.IndexOf(family, 0));
        }

        private static string ShipName(int shipIndex) =>
            $"{Localization.Get("inventory.hull_" + ShipCatalog.FamilyOf(shipIndex))} {ShipCatalog.RomanTier(shipIndex)}";

        private void Refresh()
        {
            int worn = SaveManager.GetSelectedShip();
            int preview = PreviewIndex;
            ShipSpec spec = ShipCatalog.Get(preview);
            int family = spec.family;
            bool previewOwned = SaveManager.IsShipUnlocked(preview);

            // ---- portrait + the ship's own numbers
            if (shipImage != null && hulls != null && family < hulls.Length && hulls[family] != null)
            {
                Sprite[] sprites = hulls[family].levelSprites;
                shipImage.sprite = sprites[Mathf.Clamp(spec.tier, 0, sprites.Length - 1)];
                shipImage.color = previewOwned ? Color.white : LockedPortraitTint;
            }
            if (nameLabel != null) nameLabel.text = ShipName(preview);
            if (roleLabel != null) roleLabel.text = Localization.Get("inventory.role_" + family);

            int maxLevel = SaveManager.MaxShipLevel;
            float hpTop = (SaveManager.ShipBaseHp + (maxLevel - 1) * SaveManager.ShipHpPerLevel) * ShipCatalog.MaxHp;
            float dmgTop = (SaveManager.ShipBaseDamage + (maxLevel - 1) * SaveManager.ShipDamagePerLevel) * ShipCatalog.MaxDamage;
            int hp = SaveManager.GetShipMaxHp(preview);
            int dmg = SaveManager.GetShipDamage(preview);
            float shotsPerSecond = ShipCatalog.BaseShotsPerSecond * spec.rate;
            if (hpFill != null) hpFill.fillAmount = Mathf.Clamp01(hp / hpTop);
            if (dmgFill != null) dmgFill.fillAmount = Mathf.Clamp01(dmg / dmgTop);
            if (rateFill != null) rateFill.fillAmount = Mathf.Clamp01(spec.rate / ShipCatalog.MaxRate);
            if (hpLabel != null) hpLabel.text = hp.ToString();
            if (dmgLabel != null) dmgLabel.text = dmg.ToString();
            if (rateLabel != null) rateLabel.text = shotsPerSecond.ToString("0.0") + "/s";
            if (perksLabel != null) perksLabel.text = BuildPerkText(spec);

            RefreshHangar(worn, preview, family);
            RefreshAction(worn, preview, previewOwned);

            // ---- shared upgrade level
            int level = SaveManager.GetShipLevel();
            if (techLabel != null) techLabel.text = $"{Localization.Get("inventory.level")} {level}/{maxLevel}";
            int cost = SaveManager.GetShipUpgradeCost();
            if (cost < 0)
            {
                if (upgradeButtonLabel != null) upgradeButtonLabel.text = Localization.Get("inventory.max_level");
                if (upgradeButton != null) upgradeButton.interactable = false;
            }
            else
            {
                if (upgradeButtonLabel != null) upgradeButtonLabel.text = $"{Localization.Get("inventory.upgrade")} - {cost}";
                if (upgradeButton != null) upgradeButton.interactable = SaveManager.GetCrystals() >= cost;
            }

            if (crystalLabel != null) crystalLabel.text = SaveManager.GetCrystals().ToString();
        }

        /// <summary>One bullet line per ability the ship has, worded from its numbers.</summary>
        public static string BuildPerkText(ShipSpec spec)
        {
            StringBuilder text = new StringBuilder();
            void Add(string line)
            {
                if (text.Length > 0) text.Append('\n');
                text.Append("- ").Append(line);
            }

            if (spec.volley >= 3) Add(Localization.Get("inventory.perk_triple"));
            else if (spec.volley == 2) Add(Localization.Get("inventory.perk_twin"));
            if (spec.pierce > 0) Add(Localization.Format("inventory.perk_pierce_fmt", spec.pierce));
            if (spec.critChance > 0f) Add(Localization.Format("inventory.perk_crit_fmt", Mathf.RoundToInt(spec.critChance * 100f)));
            if (spec.damageTaken < 1f) Add(Localization.Format("inventory.perk_armor_fmt", Mathf.RoundToInt((1f - spec.damageTaken) * 100f)));
            if (spec.regenPerSecond > 0f) Add(Localization.Get("inventory.perk_regen"));
            if (spec.startBarrier > 0f) Add(Localization.Format("inventory.perk_barrier_fmt", Mathf.RoundToInt(spec.startBarrier)));
            if (spec.secondWind) Add(Localization.Get("inventory.perk_second_wind"));
            if (spec.magnetRadius > 0f) Add(Localization.Get("inventory.perk_magnet"));
            if (spec.buffDuration > 1f) Add(Localization.Format("inventory.perk_buff_fmt", Mathf.RoundToInt((spec.buffDuration - 1f) * 100f)));

            if (text.Length == 0) Add(Localization.Get("inventory.perk_none"));
            return text.ToString();
        }

        private void RefreshHangar(int worn, int preview, int family)
        {
            if (familyTabs != null)
            {
                for (int f = 0; f < familyTabs.Length; f++)
                {
                    int owned = 0;
                    for (int t = 0; t < ShipCatalog.TiersPerFamily; t++)
                        if (SaveManager.IsShipUnlocked(ShipCatalog.IndexOf(f, t))) owned++;

                    if (familyTabLabels != null && f < familyTabLabels.Length && familyTabLabels[f] != null)
                    {
                        familyTabLabels[f].text = $"{Localization.Get("inventory.hull_" + f)}  {owned}/{ShipCatalog.TiersPerFamily}";
                        familyTabLabels[f].color = f == family ? Color.white : OwnedColor;
                    }
                    if (familyTabBackgrounds != null && f < familyTabBackgrounds.Length && familyTabBackgrounds[f] != null)
                    {
                        familyTabBackgrounds[f].sprite = f == family ? selectedSprite : plainSprite;
                        familyTabBackgrounds[f].color = f == family ? Color.white : new Color(1f, 1f, 1f, 0.7f);
                    }
                }
            }

            if (cardButtons == null) return;
            for (int c = 0; c < cardButtons.Length; c++)
            {
                int ship = ShipCatalog.IndexOf(family, c);
                bool unlocked = SaveManager.IsShipUnlocked(ship);
                bool isWorn = ship == worn;
                bool blocked = !unlocked && SaveManager.GetShipRequirement(ship, out _) != SaveManager.ShipRequirement.Met;

                if (cardBackgrounds != null && c < cardBackgrounds.Length && cardBackgrounds[c] != null)
                {
                    cardBackgrounds[c].sprite = ship == preview ? selectedSprite : plainSprite;
                    cardBackgrounds[c].color = ship == preview ? Color.white : new Color(1f, 1f, 1f, 0.7f);
                }

                if (cardIcons != null && c < cardIcons.Length && cardIcons[c] != null)
                {
                    if (hulls != null && family < hulls.Length && hulls[family] != null)
                        cardIcons[c].sprite = hulls[family].levelSprites[Mathf.Clamp(c, 0, hulls[family].levelSprites.Length - 1)];
                    cardIcons[c].color = unlocked ? Color.white : LockedIconTint;
                }

                if (cardLocks != null && c < cardLocks.Length && cardLocks[c] != null) cardLocks[c].SetActive(!unlocked);
                if (cardNames != null && c < cardNames.Length && cardNames[c] != null) cardNames[c].text = ShipName(ship);

                if (cardStatusLabels != null && c < cardStatusLabels.Length && cardStatusLabels[c] != null)
                {
                    cardStatusLabels[c].gameObject.SetActive(unlocked);
                    cardStatusLabels[c].text = Localization.Get(isWorn ? "inventory.equipped" : "inventory.owned");
                    cardStatusLabels[c].color = isWorn ? EquippedColor : OwnedColor;
                }

                if (cardPriceRows != null && c < cardPriceRows.Length && cardPriceRows[c] != null) cardPriceRows[c].SetActive(!unlocked);
                if (cardPriceLabels != null && c < cardPriceLabels.Length && cardPriceLabels[c] != null)
                {
                    cardPriceLabels[c].text = SaveManager.GetShipUnlockCost(ship).ToString();
                    cardPriceLabels[c].color = blocked ? PriceBlockedColor : PriceColor;
                }
            }
        }

        private void RefreshAction(int worn, int preview, bool previewOwned)
        {
            if (hullActionButton == null) return;
            string label;
            bool interactable;

            if (previewOwned)
            {
                interactable = preview != worn;
                label = Localization.Get(preview == worn ? "inventory.equipped" : "inventory.equip");
            }
            else
            {
                switch (SaveManager.GetShipRequirement(preview, out int value))
                {
                    case SaveManager.ShipRequirement.PreviousShip:
                        interactable = false;
                        label = Localization.Format("inventory.req_ship_fmt", ShipName(value));
                        break;
                    case SaveManager.ShipRequirement.ClearedLevels:
                        interactable = false;
                        label = Localization.Format("inventory.req_level_fmt", value);
                        break;
                    default:
                        // Tappable even when short of Crystals: telling the player how many they
                        // still need beats a dead button.
                        interactable = true;
                        label = Localization.Format("inventory.unlock_fmt", SaveManager.GetShipUnlockCost(preview));
                        break;
                }
            }

            hullActionButton.interactable = interactable;
            if (hullActionLabel != null) hullActionLabel.text = label;
        }

        private void Upgrade()
        {
            if (SaveManager.TryUpgradeShip())
            {
                AudioManager.Play(GameAudio.Pickup, 0.55f);
                Refresh();
                AchievementNotifier.CheckAndToast(toastRoot != null ? toastRoot : transform);
            }
        }

        private void OnActionClicked()
        {
            int index = PreviewIndex;
            Transform toastParent = toastRoot != null ? toastRoot : transform;

            if (SaveManager.IsShipUnlocked(index))
            {
                // Bought before - wearing it again is free, any time.
                if (index != SaveManager.GetSelectedShip())
                {
                    SaveManager.SelectShip(index);
                    AudioManager.Play(GameAudio.Pickup, 0.45f);
                    ToastUI.ShowToast(toastParent, Localization.Format("inventory.equipped_toast_fmt", ShipName(index)));
                }
                Refresh();
                return;
            }

            if (SaveManager.GetShipRequirement(index, out _) != SaveManager.ShipRequirement.Met) return;

            if (SaveManager.TryUnlockShip(index))
            {
                SaveManager.SelectShip(index);
                AudioManager.Play(GameAudio.Pickup, 0.55f);
                ToastUI.ShowToast(toastParent, Localization.Get("inventory.ship_unlocked"));
                Refresh();
            }
            else
            {
                int cost = SaveManager.GetShipUnlockCost(index);
                ToastUI.ShowToast(toastParent, Localization.Format("inventory.not_enough_crystals_fmt", cost));
            }
        }

        private void OnSelectedShipChanged(int shipIndex)
        {
            Refresh();
        }

        private void OnCrystalsChanged(int value)
        {
            Refresh();
        }

        private void Close()
        {
            Destroy(gameObject);
        }
    }
}
