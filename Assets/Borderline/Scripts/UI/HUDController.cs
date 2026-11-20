using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Borderline
{
    /// <summary>
    /// Builds the whole HUD in code (no prefab wiring needed):
    ///  top bar     - Energy count + bar, Territory % for both sides
    ///  status line - your location hex, dwell time and wall growth rate
    ///  bottom      - build selector (Troops / Vehicle / Turret), attack strength (25/50/100%), Set Location
    ///  footer      - "Created by OG Software v0.1.0" + Achievements / Leaderboards / Privacy / Support links
    ///  overlays    - toast messages, victory / defeat panel with Restart
    /// Respects the device safe area (notches, home indicator).
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [Tooltip("Optional. Leave empty to use the TMP default font.")]
        [SerializeField] TMP_FontAsset font;

        static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.09f, 0.85f);
        static readonly Color ButtonColor = new Color(0.18f, 0.20f, 0.24f, 1f);
        static readonly Color SelectedColor = new Color(0.20f, 0.47f, 0.96f, 1f);
        static readonly Color PresenceColor = new Color(1f, 0.78f, 0.15f, 1f);
        static readonly Color MutedText = new Color(1f, 1f, 1f, 0.55f);

        RectTransform safeArea;
        Rect lastSafeArea;
        Vector2Int lastScreenSize;

        TextMeshProUGUI energyText, territoryText, presenceText, toastText, gameOverText, presenceButtonLabel;
        RectTransform energyFill;
        CanvasGroup toastGroup;
        GameObject gameOverPanel;
        Image presenceButtonImage;
        readonly Dictionary<UnitType, Image> unitButtons = new Dictionary<UnitType, Image>();
        readonly List<KeyValuePair<float, Image>> attackButtons = new List<KeyValuePair<float, Image>>();

        float toastTimer;
        float refreshTimer;

        // ------------------------------------------------------------------ lifecycle

        void Awake()
        {
            if (game == null) game = FindFirstObjectByType<GameManager>();
            EnsureEventSystem();
            BuildUI();
        }

        void OnEnable()
        {
            if (game == null) return;
            game.Toast += ShowToast;
            game.GameEnded += OnGameEnded;
        }

        void OnDisable()
        {
            if (game == null) return;
            game.Toast -= ShowToast;
            game.GameEnded -= OnGameEnded;
        }

        void Update()
        {
            ApplySafeArea();

            if (toastTimer > 0f)
            {
                toastTimer -= Time.unscaledDeltaTime;
                toastGroup.alpha = Mathf.Clamp01(toastTimer / 0.4f);
            }

            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f && game != null && game.Grid != null && game.Grid.TileCount > 0)
            {
                refreshTimer = 0.1f;
                RefreshStats();
            }
        }

        // ------------------------------------------------------------------ refresh

        void RefreshStats()
        {
            EnergyPool e = game.PlayerEnergy;
            energyText.text = $"ENERGY  <b>{e.Whole}</b> / {e.max:0}";
            energyFill.anchorMax = new Vector2(Mathf.Clamp01(e.Fraction), 1f);

            HexGridManager grid = game.Grid;
            int mine = grid.CountOwned(Owner.Player);
            int theirs = grid.CountOwned(Owner.Rival);
            float total = Mathf.Max(1, grid.TileCount);
            territoryText.text =
                $"<color=#5B8FFF>YOU {100f * mine / total:0}%</color>   <color=#FF6A5E>RIVAL {100f * theirs / total:0}%</color>\n" +
                $"<size=70%>{mine} of {grid.TileCount} hexes controlled</size>";

            HexTile here = game.PlayerPresence;
            if (here == null)
            {
                presenceText.text = "LOCATION: not set";
            }
            else if (here.OwnerID != Owner.Player)
            {
                presenceText.text = $"LOCATION hex {here.Coord}: not your territory, so no fortifying";
            }
            else
            {
                int seconds = Mathf.FloorToInt(here.DwellSeconds);
                float rate = game.Dwell.FortificationPerSecond(here.DwellSeconds);
                presenceText.text = $"LOCATION hex {here.Coord}  ·  here {seconds / 60}:{seconds % 60:00}  ·  walls +{rate:0.00}/s";
            }

            foreach (var pair in unitButtons)
                pair.Value.color = pair.Key == game.SelectedUnit ? SelectedColor : ButtonColor;
            foreach (var pair in attackButtons)
                pair.Value.color = Mathf.Approximately(pair.Key, game.AttackFraction) ? SelectedColor : ButtonColor;

            presenceButtonImage.color = game.PlacingPresence ? PresenceColor : ButtonColor;
            presenceButtonLabel.text = game.PlacingPresence ? "Tap a hex..." : "Set Location";
            presenceButtonLabel.color = game.PlacingPresence ? Color.black : Color.white;
        }

        void ShowToast(string message)
        {
            if (toastText == null) return;
            toastText.text = message;
            toastGroup.alpha = 1f;
            toastTimer = 2.6f;
        }

        void OnGameEnded(bool playerWon)
        {
            gameOverText.text = playerWon
                ? "VICTORY\n<size=45%>The rival has been wiped off the map.</size>"
                : "DEFEATED\n<size=45%>Your last hex has fallen.</size>";
            gameOverPanel.SetActive(true);
        }

        // ------------------------------------------------------------------ build

        void BuildUI()
        {
            var canvasGO = new GameObject("HUD Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            safeArea = NewRect("SafeArea", canvasGO.transform);
            Stretch(safeArea);

            BuildTopBar();
            BuildFooter();
            BuildControls();
            BuildToast();
            BuildGameOver();
            ApplySafeArea();
        }

        void BuildTopBar()
        {
            RectTransform top = NewImage(safeArea, "TopBar", PanelColor, true).rectTransform;
            SetAnchors(top, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            top.sizeDelta = new Vector2(0f, 190f);
            top.anchoredPosition = Vector2.zero;

            energyText = NewText(top, "Energy", "", 44, TextAlignmentOptions.Left, Color.white);
            SetAnchors(energyText.rectTransform, new Vector2(0f, 0.45f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));
            SetOffsets(energyText.rectTransform, 36f, 0f, -10f, -14f);

            Image barBg = NewImage(top, "EnergyBar", new Color(1f, 1f, 1f, 0.12f), false);
            SetAnchors(barBg.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.5f));
            SetOffsets(barBg.rectTransform, 36f, 34f, -10f, -14f);

            Image fill = NewImage(barBg.rectTransform, "Fill", PresenceColor, false);
            energyFill = fill.rectTransform;
            SetAnchors(energyFill, Vector2.zero, new Vector2(0.4f, 1f), new Vector2(0f, 0.5f));
            SetOffsets(energyFill, 0f, 0f, 0f, 0f);

            territoryText = NewText(top, "Territory", "", 40, TextAlignmentOptions.Right, Color.white);
            SetAnchors(territoryText.rectTransform, new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
            SetOffsets(territoryText.rectTransform, 10f, 20f, -36f, -14f);

            Image strip = NewImage(safeArea, "PresenceStrip", new Color(0f, 0f, 0f, 0.55f), false);
            SetAnchors(strip.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            strip.rectTransform.sizeDelta = new Vector2(0f, 58f);
            strip.rectTransform.anchoredPosition = new Vector2(0f, -190f);
            presenceText = NewText(strip.rectTransform, "Presence", "", 28, TextAlignmentOptions.Center, PresenceColor);
            Stretch(presenceText.rectTransform);
        }

        void BuildControls()
        {
            Image panel = NewImage(safeArea, "Controls", PanelColor, true);
            RectTransform rt = panel.rectTransform;
            SetAnchors(rt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            rt.sizeDelta = new Vector2(0f, 300f);
            rt.anchoredPosition = new Vector2(0f, 64f);

            var column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(24, 24, 20, 20);
            column.spacing = 16f;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = column.childForceExpandHeight = true;

            RectTransform buildRow = NewRow(rt, "BuildRow");
            foreach (UnitType type in new[] { UnitType.Troops, UnitType.Vehicle, UnitType.Turret })
            {
                UnitType captured = type;
                string amount = type == UnitType.Troops ? " +3" : "";
                string label = $"{UnitStats.DisplayName(type)}{amount}\n<size=65%>{UnitStats.Cost(type)} energy</size>";
                Image img = NewButton(buildRow, label, 36, () => game.SelectedUnit = captured, out _);
                unitButtons[type] = img;
            }

            RectTransform attackRow = NewRow(rt, "AttackRow");
            foreach (float fraction in new[] { 0.25f, 0.5f, 1f })
            {
                float captured = fraction;
                Image img = NewButton(attackRow, $"Attack\n<size=65%>{fraction * 100f:0}% energy</size>", 34,
                    () => game.AttackFraction = captured, out _);
                attackButtons.Add(new KeyValuePair<float, Image>(fraction, img));
            }
            presenceButtonImage = NewButton(attackRow, "Set Location", 34,
                () => game.PlacingPresence = !game.PlacingPresence, out presenceButtonLabel);
        }

        void BuildFooter()
        {
            RectTransform footer = NewRect("Footer", safeArea);
            SetAnchors(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            footer.sizeDelta = new Vector2(0f, 64f);
            footer.anchoredPosition = Vector2.zero;

            TextMeshProUGUI credit = NewText(footer, "Credit", AppInfo.CreditLine, 24, TextAlignmentOptions.Left, MutedText);
            Stretch(credit.rectTransform);
            SetOffsets(credit.rectTransform, 30f, 0f, -650f, 0f);

            // Game Center / Google Play Games (sign in first if needed)
            NewLink(footer, "Achievements", () => OGSoftware.Platform.OGPlatform.ShowAchievements(), -470f, 170f);
            NewLink(footer, "Leaderboards", () => OGSoftware.Platform.OGPlatform.ShowLeaderboard(), -285f, 170f);
            NewLink(footer, "Privacy", AppInfo.PrivacyPolicyUrl, -150f);
            NewLink(footer, "Support", AppInfo.SupportUrl, -20f);
        }

        void BuildToast()
        {
            Image bg = NewImage(safeArea, "Toast", new Color(0f, 0f, 0f, 0.78f), false);
            RectTransform rt = bg.rectTransform;
            SetAnchors(rt, new Vector2(0.05f, 0f), new Vector2(0.95f, 0f), new Vector2(0.5f, 0f));
            rt.sizeDelta = new Vector2(0f, 120f);
            rt.anchoredPosition = new Vector2(0f, 390f);
            toastGroup = bg.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
            toastGroup.interactable = false;

            toastText = NewText(rt, "Text", "", 32, TextAlignmentOptions.Center, Color.white);
            Stretch(toastText.rectTransform);
            SetOffsets(toastText.rectTransform, 24f, 10f, -24f, -10f);
        }

        void BuildGameOver()
        {
            Image dim = NewImage(safeArea.parent, "GameOver", new Color(0f, 0f, 0f, 0.8f), true);
            Stretch(dim.rectTransform);
            gameOverPanel = dim.gameObject;

            gameOverText = NewText(dim.rectTransform, "Title", "", 110, TextAlignmentOptions.Center, Color.white);
            SetAnchors(gameOverText.rectTransform, new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.75f), new Vector2(0.5f, 0.5f));
            SetOffsets(gameOverText.rectTransform, 0f, 0f, 0f, 0f);

            RectTransform holder = NewRect("RestartHolder", dim.rectTransform);
            SetAnchors(holder, new Vector2(0.3f, 0.36f), new Vector2(0.7f, 0.44f), new Vector2(0.5f, 0.5f));
            SetOffsets(holder, 0f, 0f, 0f, 0f);
            var layout = holder.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            Image restart = NewButton(holder, "Play again", 44, () => game.Restart(), out _);
            restart.color = SelectedColor;

            gameOverPanel.SetActive(false);
        }

        // ------------------------------------------------------------------ safe area / event system

        void ApplySafeArea()
        {
            Rect area = Screen.safeArea;
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (area == lastSafeArea && screenSize == lastScreenSize) return;
            lastSafeArea = area;
            lastScreenSize = screenSize;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            Vector2 min = area.position;
            Vector2 max = area.position + area.size;
            min.x /= Screen.width; max.x /= Screen.width;
            min.y /= Screen.height; max.y /= Screen.height;
            safeArea.anchorMin = min;
            safeArea.anchorMax = max;
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        // ------------------------------------------------------------------ UI helpers

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = pivot;
        }

        static void SetOffsets(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
        }

        Image NewImage(Transform parent, string name, Color color, bool raycast)
        {
            RectTransform rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        TextMeshProUGUI NewText(Transform parent, string name, string text, float size, TextAlignmentOptions align, Color color)
        {
            RectTransform rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.richText = true;
            t.raycastTarget = false;
            return t;
        }

        RectTransform NewRow(Transform parent, string name)
        {
            RectTransform row = NewRect(name, parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            return row;
        }

        Image NewButton(Transform parent, string label, float fontSize, UnityAction onClick, out TextMeshProUGUI labelText)
        {
            Image img = NewImage(parent, "Button", ButtonColor, true);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(onClick);

            labelText = NewText(img.rectTransform, "Label", label, fontSize, TextAlignmentOptions.Center, Color.white);
            Stretch(labelText.rectTransform);
            SetOffsets(labelText.rectTransform, 8f, 4f, -8f, -4f);
            return img;
        }

        void NewLink(Transform parent, string label, string url, float rightEdge)
        {
            NewLink(parent, label, () => Application.OpenURL(url), rightEdge, 120f);
        }

        void NewLink(Transform parent, string label, UnityAction onClick, float rightEdge, float width)
        {
            RectTransform rt = NewRect(label, parent);
            SetAnchors(rt, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f));
            rt.sizeDelta = new Vector2(width, 0f);
            rt.anchoredPosition = new Vector2(rightEdge, 0f);

            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f); // invisible, but catches taps
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.onClick.AddListener(onClick);

            TextMeshProUGUI t = NewText(rt, "Label", $"<u>{label}</u>", 24, TextAlignmentOptions.Right, MutedText);
            Stretch(t.rectTransform);
        }
    }
}
