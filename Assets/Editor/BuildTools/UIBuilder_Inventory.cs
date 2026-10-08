using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using SpaceHawk.UI;
using SpaceHawk.Data;
using static SpaceHawk.EditorTools.UIFactory;

namespace SpaceHawk.EditorTools
{
    /// <summary>Builds the Inventory overlay: the hangar of fifteen ships (three family tabs, five
    /// ship cards each) with a big preview of the ship in focus - its numbers, its special
    /// abilities and the equip / buy button - and the shared Crystal upgrade underneath.</summary>
    public static class UIBuilder_Inventory
    {
        private const string OutFolder = "Assets/Resources/Prefabs/UI";
        private static readonly Color BodyLight = new Color(0.85f, 0.95f, 0.97f, 1f);
        private static readonly Color Yellow = new Color(1f, 0.83f, 0.2f, 1f);
        private static readonly Color TitleTeal = new Color(0.63f, 0.93f, 0.93f, 1f);

        // The whole layout below was hand-placed in pixels assuming its bounding box (x=40 to
        // x=906) sits flush against the panel's left edge. Anchoring every element to the panel's
        // horizontal CENTER instead, offset by half that bounding box, keeps the whole block
        // centered at any panel width - instead of staying glued to the left while a wide phone
        // panel leaves a large empty gap on the right.
        private const float ContentLeft = 40f;
        private const float ContentRight = 906f;
        private const float ContentWidth = ContentRight - ContentLeft;
        private const float ContentCenterOffset = (ContentLeft + ContentRight) / 2f;

        private const float CardWidth = 160f;
        private const float CardHeight = 170f;
        private const float TabHeight = 52f;

        [MenuItem("Tools/Space Hawk/10. Build Inventory Panel")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(OutFolder);

            GameObject root = NewUI("InventoryPanel", null);
            Stretch(RT(root));
            Image dim = root.AddComponent<Image>();
            dim.sprite = LoadUI("DarkBackground");
            dim.type = Image.Type.Sliced;
            dim.raycastTarget = true;

            UIBuilder_Overlays.BuildDimTitle(root.transform, "INVENTORY", "inventory.title");

            GameObject panel = NewUI("Panel", root.transform);
            Anchor(RT(panel), new Vector2(0.14f, 0.5f), new Vector2(0.86f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(0, 740));
            Image panelImg = panel.AddComponent<Image>();
            panelImg.sprite = LoadUI("BoxMenu");
            panelImg.type = Image.Type.Sliced;
            panelImg.raycastTarget = true;

            Button closeBtn = UIBuilder_Overlays.BuildHeaderCloseButton(root.transform);

            Transform p = panel.transform;
            Vector2 top = new Vector2(0.5f, 1);
            Vector2 topLeft = new Vector2(0, 1);
            float X(float x) => x - ContentCenterOffset;

            // ---- Left column: the ship in focus - name, role, portrait, equip / buy button
            TMP_Text nameLabel = CreateText(p, "ShipName", "HAWK I", 32, TextAlignmentOptions.Center, Color.white,
                top, top, topLeft, new Vector2(X(40), -22), new Vector2(380, 42));
            nameLabel.fontStyle = FontStyles.Bold;

            TMP_Text roleLabel = CreateText(p, "ShipRole", "INTERCEPTOR", 17, TextAlignmentOptions.Center, TitleTeal,
                top, top, topLeft, new Vector2(X(40), -62), new Vector2(380, 26));
            roleLabel.enableAutoSizing = true;
            roleLabel.fontSizeMin = 12;
            roleLabel.fontSizeMax = 17;

            Image shipImage = CreateImage(p, "ShipImage", LoadShip("Ship_01", "Ship_LVL_1"),
                top, top, topLeft, new Vector2(X(40), -92), new Vector2(380, 200));
            shipImage.preserveAspect = true;

            Button hullAction = CreateTextButton(p, "HullActionButton", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "EQUIP", 26, BodyLight, top, top, topLeft, new Vector2(X(40), -300), new Vector2(380, 64), Image.Type.Sliced);
            TMP_Text hullActionLabel = hullAction.GetComponentInChildren<TMP_Text>();
            hullActionLabel.enableAutoSizing = true;
            hullActionLabel.fontSizeMin = 15;
            hullActionLabel.fontSizeMax = 26;

            // ---- Right column: the ship's numbers and abilities
            float colX = 460f;
            MakeStat(p, "Hp", "HEALTH POINTS", "inventory.hp", colX, -22, new Color(0.25f, 0.85f, 0.5f), out Image hpFill, out TMP_Text hpLabel, X);
            MakeStat(p, "Dmg", "DAMAGE POINTS", "inventory.dmg", colX, -92, new Color(1f, 0.55f, 0.3f), out Image dmgFill, out TMP_Text dmgLabel, X);
            MakeStat(p, "Rate", "FIRE RATE", "inventory.stat_rate", colX, -162, new Color(0.35f, 0.75f, 1f), out Image rateFill, out TMP_Text rateLabel, X);

            Image perkBox = CreateImage(p, "PerkBox", LoadUI("BgDropdownContent"),
                top, top, topLeft, new Vector2(X(colX), -240), new Vector2(446, 124), Image.Type.Sliced);
            CreateText(perkBox.transform, "PerksHeader", "SPECIAL ABILITIES", 16, TextAlignmentOptions.MidlineLeft, Yellow,
                topLeft, topLeft, topLeft, new Vector2(14, -6), new Vector2(418, 24), "inventory.perks");
            TMP_Text perksLabel = CreateText(perkBox.transform, "Perks", "- Twin cannons", 17, TextAlignmentOptions.TopLeft, BodyLight,
                topLeft, topLeft, topLeft, new Vector2(14, -32), new Vector2(418, 88));
            perksLabel.enableAutoSizing = true;
            perksLabel.fontSizeMin = 12;
            perksLabel.fontSizeMax = 17;

            // ---- Row 2: Crystal balance, upgrade level, upgrade button
            CreateImage(p, "CrystalIcon", LoadUI("CrystalIcon"),
                top, top, topLeft, new Vector2(X(40), -380), new Vector2(46, 46));
            TMP_Text crystalLabel = CreateText(p, "CrystalLabel", "0", 26, TextAlignmentOptions.MidlineLeft, Yellow,
                top, top, topLeft, new Vector2(X(94), -380), new Vector2(150, 46));
            TMP_Text techLabel = CreateText(p, "TechLabel", "LEVEL 1/5", 20, TextAlignmentOptions.MidlineRight, TitleTeal,
                top, top, topLeft, new Vector2(X(250), -380), new Vector2(170, 46));

            Button upgradeBtn = CreateTextButton(p, "UpgradeButton", LoadUI("Normal_LongBtn"), LoadUI("Hover_LongBtn"), LoadUI("Disable_LongBtn"),
                "UPGRADE", 26, BodyLight, top, top, topLeft, new Vector2(X(colX), -372), new Vector2(446, 64));
            TMP_Text upgradeLabel = upgradeBtn.GetComponentInChildren<TMP_Text>();

            // ---- Hangar: family tabs, then the five ships of the open family
            int families = ShipCatalog.FamilyCount;
            float tabGap = 13f;
            float tabWidth = (ContentWidth - (families - 1) * tabGap) / families;
            Button[] tabs = new Button[families];
            Image[] tabBackgrounds = new Image[families];
            TMP_Text[] tabLabels = new TMP_Text[families];
            for (int f = 0; f < families; f++)
            {
                Button tab = CreateButton(p, $"FamilyTab{f}", LoadUI("BgDropdownContent"), null, null,
                    top, top, topLeft, new Vector2(X(ContentLeft + f * (tabWidth + tabGap)), -456), new Vector2(tabWidth, TabHeight), Image.Type.Sliced);
                tabs[f] = tab;
                tabBackgrounds[f] = tab.GetComponent<Image>();
                tabLabels[f] = CreateText(tab.transform, "Label", "HAWK 1/5", 22, TextAlignmentOptions.Center, BodyLight,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                tabLabels[f].fontStyle = FontStyles.Bold;
            }

            int tiers = ShipCatalog.TiersPerFamily;
            float cardGap = (ContentWidth - tiers * CardWidth) / (tiers - 1);
            Button[] cards = new Button[tiers];
            Image[] cardBackgrounds = new Image[tiers];
            Image[] cardIcons = new Image[tiers];
            GameObject[] cardLocks = new GameObject[tiers];
            TMP_Text[] cardNames = new TMP_Text[tiers];
            TMP_Text[] cardStatus = new TMP_Text[tiers];
            GameObject[] cardPriceRows = new GameObject[tiers];
            TMP_Text[] cardPriceLabels = new TMP_Text[tiers];

            ShipHullSprites[] hulls = LoadAllShipHulls();
            Vector2 centre = new Vector2(0.5f, 0.5f);
            for (int t = 0; t < tiers; t++)
            {
                Button card = CreateButton(p, $"ShipCard{t}", LoadUI("BgDropdownContent"), null, null,
                    top, top, topLeft, new Vector2(X(ContentLeft + t * (CardWidth + cardGap)), -520), new Vector2(CardWidth, CardHeight), Image.Type.Sliced);
                cards[t] = card;
                cardBackgrounds[t] = card.GetComponent<Image>();

                Image icon = CreateImage(card.transform, "Icon", hulls[0].levelSprites[t],
                    top, top, top, new Vector2(0, -10), new Vector2(140, 92));
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                cardIcons[t] = icon;

                Image lock_ = CreateImage(card.transform, "Lock", LoadUI("LvlLock"),
                    top, top, centre, new Vector2(48, -82), new Vector2(30, 30));
                lock_.raycastTarget = false;
                cardLocks[t] = lock_.gameObject;

                TMP_Text name = CreateText(card.transform, "Name", "HAWK I", 19, TextAlignmentOptions.Center, BodyLight,
                    top, top, top, new Vector2(0, -108), new Vector2(150, 28));
                name.fontStyle = FontStyles.Bold;
                name.enableAutoSizing = true;
                name.fontSizeMin = 12;
                name.fontSizeMax = 19;
                cardNames[t] = name;

                TMP_Text status = CreateText(card.transform, "Status", "OWNED", 16, TextAlignmentOptions.Center, TitleTeal,
                    top, top, top, new Vector2(0, -138), new Vector2(150, 26));
                status.enableAutoSizing = true;
                status.fontSizeMin = 11;
                status.fontSizeMax = 16;
                cardStatus[t] = status;

                GameObject priceRow = NewUI("Price", card.transform);
                Anchor(RT(priceRow), top, top, top, new Vector2(0, -136), new Vector2(150, 30));
                CreateImage(priceRow.transform, "CrystalIcon", LoadUI("CrystalIcon"),
                    centre, centre, centre, new Vector2(-40, 0), new Vector2(26, 26));
                cardPriceLabels[t] = CreateText(priceRow.transform, "Cost", "12000", 19, TextAlignmentOptions.MidlineLeft, Yellow,
                    centre, centre, centre, new Vector2(24, 0), new Vector2(92, 30));
                cardPriceRows[t] = priceRow;
            }

            InventoryPanel comp = root.AddComponent<InventoryPanel>();
            comp.closeButton = closeBtn;
            comp.hulls = hulls;
            comp.shipImage = shipImage;
            comp.nameLabel = nameLabel;
            comp.roleLabel = roleLabel;
            comp.hpFill = hpFill;
            comp.hpLabel = hpLabel;
            comp.dmgFill = dmgFill;
            comp.dmgLabel = dmgLabel;
            comp.rateFill = rateFill;
            comp.rateLabel = rateLabel;
            comp.perksLabel = perksLabel;
            comp.hullActionButton = hullAction;
            comp.hullActionLabel = hullActionLabel;
            comp.upgradeButton = upgradeBtn;
            comp.upgradeButtonLabel = upgradeLabel;
            comp.techLabel = techLabel;
            comp.crystalLabel = crystalLabel;
            comp.familyTabs = tabs;
            comp.familyTabBackgrounds = tabBackgrounds;
            comp.familyTabLabels = tabLabels;
            comp.cardButtons = cards;
            comp.cardBackgrounds = cardBackgrounds;
            comp.cardIcons = cardIcons;
            comp.cardLocks = cardLocks;
            comp.cardNames = cardNames;
            comp.cardStatusLabels = cardStatus;
            comp.cardPriceRows = cardPriceRows;
            comp.cardPriceLabels = cardPriceLabels;
            comp.plainSprite = LoadUI("BgDropdownContent");
            comp.selectedSprite = LoadUI("Normal_Btn");

            SceneBuilderUtil.SaveAsPrefab(root, $"{OutFolder}/InventoryPanel.prefab");
            Debug.Log("[UIBuilder_Inventory] Inventory panel built.");
        }

        /// <summary>A caption, a filled bar in the given colour and its value, one under the other.</summary>
        private static void MakeStat(Transform p, string id, string caption, string locKey, float x, float y, Color barColor,
            out Image fill, out TMP_Text valueLabel, System.Func<float, float> X)
        {
            Vector2 top = new Vector2(0.5f, 1);
            Vector2 topLeft = new Vector2(0, 1);
            CreateText(p, $"{id}Caption", caption, 19, TextAlignmentOptions.MidlineLeft, BodyLight,
                top, top, topLeft, new Vector2(X(x), y), new Vector2(300, 26), locKey);
            Image frame = CreateImage(p, $"{id}Frame", LoadUI("BgHudBar"),
                top, top, topLeft, new Vector2(X(x), y - 26), new Vector2(330, 40), Image.Type.Sliced);
            fill = CreateImage(frame.transform, $"{id}Fill", LoadUI("HealthBar"),
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(296, 22));
            fill.color = barColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            valueLabel = CreateText(p, $"{id}Label", "100", 22, TextAlignmentOptions.MidlineLeft, BodyLight,
                top, top, topLeft, new Vector2(X(x + 346), y - 26), new Vector2(100, 40));
        }
    }
}
