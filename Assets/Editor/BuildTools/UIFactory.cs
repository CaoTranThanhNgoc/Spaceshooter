using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using SpaceHawk.Data;

namespace SpaceHawk.EditorTools
{
    /// <summary>
    /// Code-only UI construction helpers used by the screen builders. Everything here runs
    /// once inside the Unity Editor (via BuildPhase1MenuItem) to assemble real prefabs/scenes
    /// out of the sprites in Assets/Material — nothing here runs at game runtime.
    /// </summary>
    public static class UIFactory
    {
        public const string UIPath = "Assets/Material/UI/";

        public static Sprite LoadUI(string fileName)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(UIPath + fileName + ".png");
            if (s == null) Debug.LogWarning($"[UIFactory] Missing UI sprite: {fileName}");
            return s;
        }

        public static Sprite LoadShip(string shipFolder, string fileName)
        {
            string path = $"Assets/Material/{shipFolder}/{fileName}.png";
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) Debug.LogWarning($"[UIFactory] Missing ship sprite: {path}");
            return s;
        }

        /// <summary>Loads a numbered frame sequence such as Ship_01/Exhaust/Exhaust_1_1_000..009.</summary>
        public static Sprite[] LoadSequence(string folderRelativeToMaterial, string filePrefix, int count, int startIndex = 0, int digits = 3)
        {
            List<Sprite> list = new List<Sprite>();
            for (int i = startIndex; i < startIndex + count; i++)
            {
                string idx = i.ToString().PadLeft(digits, '0');
                string path = $"Assets/Material/{folderRelativeToMaterial}/{filePrefix}{idx}.png";
                Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null) list.Add(s);
            }
            return list.ToArray();
        }

        public static readonly string[] ShipHullFolders = { "Ship_01", "Ship_02", "Ship_03" };

        /// <summary>One entry per selectable hull (Ship_01/02/03), same order SaveManager indexes
        /// them by - shared between the Player gameplay prefab and the Inventory picker UI so both
        /// stay in sync with whatever ships actually exist as assets.</summary>
        public static ShipHullSprites[] LoadAllShipHulls()
        {
            ShipHullSprites[] hulls = new ShipHullSprites[ShipHullFolders.Length];
            for (int i = 0; i < ShipHullFolders.Length; i++)
            {
                string folder = ShipHullFolders[i];
                hulls[i] = new ShipHullSprites
                {
                    levelSprites = new[]
                    {
                        LoadShip(folder, "Ship_LVL_1"),
                        LoadShip(folder, "Ship_LVL_2"),
                        LoadShip(folder, "Ship_LVL_3"),
                        LoadShip(folder, "Ship_LVL_4"),
                        LoadShip(folder, "Ship_LVL_5"),
                    },
                    exhaustFrames = LoadSequence($"{folder}/Exhaust", "Exhaust_1_1_", 10, 0, 3),
                };
            }
            return hulls;
        }

        public static GameObject NewUI(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static RectTransform RT(GameObject go) => go.GetComponent<RectTransform>();

        public static void Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static void Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        /// <summary>Pass Vector2.zero for size to use the sprite's native pixel size.</summary>
        public static Image CreateImage(Transform parent, string name, Sprite sprite,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size,
            Image.Type type = Image.Type.Simple, bool raycastTarget = false)
        {
            GameObject go = NewUI(name, parent);
            Anchor(RT(go), anchorMin, anchorMax, pivot, anchoredPos, size);
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = type;
            img.raycastTarget = raycastTarget;
            if (sprite != null && size == Vector2.zero) img.SetNativeSize();
            return img;
        }

        public static Image CreateStretchImage(Transform parent, string name, Sprite sprite, Image.Type type = Image.Type.Simple, bool raycastTarget = false)
        {
            GameObject go = NewUI(name, parent);
            Stretch(RT(go));
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = type;
            img.raycastTarget = raycastTarget;
            return img;
        }

        /// <summary>Pass locKey to have this text driven by Localization at runtime (re-applied
        /// live on language change) instead of staying fixed to the literal `text` passed here -
        /// that literal is still used as the build-time/editor preview until then.</summary>
        public static TMP_Text CreateText(Transform parent, string name, string text, float fontSize, TextAlignmentOptions align, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size, string locKey = null)
        {
            GameObject go = NewUI(name, parent);
            Anchor(RT(go), anchorMin, anchorMax, pivot, anchoredPos, size);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = color;
            tmp.raycastTarget = false;
            if (!string.IsNullOrEmpty(locKey))
            {
                SpaceHawk.UI.LocalizedText loc = go.AddComponent<SpaceHawk.UI.LocalizedText>();
                loc.key = locKey;
            }
            return tmp;
        }

        /// <summary>For a notice / explanation whose text length varies (translations, server answers): it
        /// wraps, shrinks to fit its box (never below `minSize`) and - if it still cannot fit - is cut
        /// with an ellipsis rather than spilling over the elements around it.</summary>
        public static TMP_Text Flexible(this TMP_Text label, float minSize = 11f)
        {
            label.enableAutoSizing = true;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = Mathf.Min(minSize, label.fontSize);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        /// <summary>Minimal correct TMP_InputField hierarchy (root + masked TextArea + Placeholder
        /// + Text) - the first text-entry field this project has needed, everything before this
        /// was buttons/sliders/toggles.</summary>
        public static TMP_InputField CreateInputField(Transform parent, string name, string placeholderText,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size, int characterLimit = 16, string placeholderLocKey = null)
        {
            GameObject go = NewUI(name, parent);
            Anchor(RT(go), anchorMin, anchorMax, pivot, anchoredPos, size);
            Image bg = go.AddComponent<Image>();
            bg.sprite = LoadUI("BgDropdownContent");
            bg.type = Image.Type.Sliced;

            GameObject textArea = NewUI("TextArea", go.transform);
            Stretch(RT(textArea), 18, 18, 8, 8);
            textArea.AddComponent<RectMask2D>();

            TMP_Text placeholder = CreateText(textArea.transform, "Placeholder", placeholderText, 24, TextAlignmentOptions.MidlineLeft,
                new Color(0.55f, 0.68f, 0.74f, 0.9f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, placeholderLocKey);
            placeholder.fontStyle = FontStyles.Italic;

            TMP_Text valueText = CreateText(textArea.transform, "Text", "", 24, TextAlignmentOptions.MidlineLeft,
                new Color(0.92f, 0.97f, 1f, 1f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            TMP_InputField field = go.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = RT(textArea);
            field.textComponent = valueText;
            field.placeholder = placeholder;
            field.characterLimit = characterLimit;
            return field;
        }

        public static Button CreateButton(Transform parent, string name, Sprite normal, Sprite hover, Sprite disabled,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size,
            Image.Type type = Image.Type.Simple)
        {
            GameObject go = NewUI(name, parent);
            Anchor(RT(go), anchorMin, anchorMax, pivot, anchoredPos, size);
            Image img = go.AddComponent<Image>();
            img.sprite = normal;
            img.type = type;
            img.raycastTarget = true;

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            if (hover != null || disabled != null)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                SpriteState state = btn.spriteState;
                if (hover != null) { state.highlightedSprite = hover; state.pressedSprite = hover; }
                if (disabled != null) state.disabledSprite = disabled;
                btn.spriteState = state;
            }
            else
            {
                btn.transition = Selectable.Transition.ColorTint;
            }

            go.AddComponent<SpaceHawk.UI.ButtonClickSound>();
            return btn;
        }

        public static Button CreateTextButton(Transform parent, string name, Sprite normal, Sprite hover, Sprite disabled,
            string label, float fontSize, Color textColor,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size,
            Image.Type type = Image.Type.Sliced, string locKey = null)
        {
            Button btn = CreateButton(parent, name, normal, hover, disabled, anchorMin, anchorMax, pivot, anchoredPos, size, type);
            CreateText(btn.transform, "Label", label, fontSize, TextAlignmentOptions.Center, textColor,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, locKey);
            return btn;
        }

        public static Slider CreateSlider(Transform parent, string name, Sprite background, Sprite fill, Sprite handle,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            GameObject root = NewUI(name, parent);
            Anchor(RT(root), anchorMin, anchorMax, pivot, anchoredPos, size);
            Slider slider = root.AddComponent<Slider>();

            CreateStretchImage(root.transform, "Background", background, Image.Type.Sliced, false);

            GameObject fillArea = NewUI("Fill Area", root.transform);
            Stretch(RT(fillArea));
            GameObject fillGO = NewUI("Fill", fillArea.transform);
            Stretch(RT(fillGO));
            Image fillImg = fillGO.AddComponent<Image>();
            fillImg.sprite = fill;
            fillImg.type = Image.Type.Sliced;
            fillImg.raycastTarget = false;

            GameObject handleArea = NewUI("Handle Slide Area", root.transform);
            Stretch(RT(handleArea));
            Vector2 handleSize = handle != null ? new Vector2(handle.rect.width, handle.rect.height) : new Vector2(20, 40);
            GameObject handleGO = NewUI("Handle", handleArea.transform);
            Anchor(RT(handleGO), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, handleSize);
            Image handleImg = handleGO.AddComponent<Image>();
            handleImg.sprite = handle;
            handleImg.raycastTarget = true;

            slider.targetGraphic = handleImg;
            slider.fillRect = fillImg.rectTransform;
            slider.handleRect = handleImg.rectTransform;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        public static Toggle CreateToggle(Transform parent, string name, Sprite background, Sprite checkmark,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            GameObject root = NewUI(name, parent);
            Anchor(RT(root), anchorMin, anchorMax, pivot, anchoredPos, size);

            Image bgImg = root.AddComponent<Image>();
            bgImg.sprite = background;
            bgImg.raycastTarget = true;

            GameObject checkGO = NewUI("Checkmark", root.transform);
            Stretch(RT(checkGO), 6, 6, 6, 6);
            Image checkImg = checkGO.AddComponent<Image>();
            checkImg.sprite = checkmark;
            checkImg.raycastTarget = false;

            Toggle toggle = root.AddComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            toggle.transition = Selectable.Transition.None;
            return toggle;
        }

        public static ScrollRect CreateHorizontalScroll(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size,
            out RectTransform content)
        {
            GameObject root = NewUI(name, parent);
            Anchor(RT(root), anchorMin, anchorMax, pivot, anchoredPos, size);
            Image bg = root.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.001f); // invisible but still eats scroll drags
            ScrollRect scroll = root.AddComponent<ScrollRect>();

            GameObject viewport = NewUI("Viewport", root.transform);
            Stretch(RT(viewport));
            viewport.AddComponent<RectMask2D>();

            GameObject contentGO = NewUI("Content", viewport.transform);
            RectTransform contentRT = RT(contentGO);
            contentRT.anchorMin = new Vector2(0f, 0f);
            contentRT.anchorMax = new Vector2(0f, 1f);
            contentRT.pivot = new Vector2(0f, 0.5f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0f, 0f);

            HorizontalLayoutGroup layout = contentGO.AddComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 24f;
            layout.padding = new RectOffset(40, 40, 0, 0);

            ContentSizeFitter fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.viewport = RT(viewport);
            scroll.content = contentRT;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            content = contentRT;
            return scroll;
        }

        public static ScrollRect CreateVerticalScroll(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size,
            out RectTransform content)
        {
            GameObject root = NewUI(name, parent);
            Anchor(RT(root), anchorMin, anchorMax, pivot, anchoredPos, size);
            Image bg = root.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.001f); // invisible but still eats scroll drags
            ScrollRect scroll = root.AddComponent<ScrollRect>();

            GameObject viewport = NewUI("Viewport", root.transform);
            Stretch(RT(viewport));
            viewport.AddComponent<RectMask2D>();

            GameObject contentGO = NewUI("Content", viewport.transform);
            RectTransform contentRT = RT(contentGO);
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = contentGO.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 18f;
            layout.padding = new RectOffset(0, 0, 10, 10);

            ContentSizeFitter fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.viewport = RT(viewport);
            scroll.content = contentRT;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            content = contentRT;
            return scroll;
        }

        public static void MarkDirty(UnityEngine.Object obj)
        {
            EditorUtility.SetDirty(obj);
        }
    }
}
