using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using BoneSquadKiting.Game;
using BoneSquadKiting.UI;

namespace BoneSquadKiting.EditorTools
{
    /// <summary>Builds the uGUI HUD hierarchy in code. Called by ArenaSceneBuilder.</summary>
    public static class HudBuilder
    {
        static Font _font;
        static Font UiFont => _font != null ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>Creates the canvas, event system and all widgets. Returns the HUD and the screen-flash image.</summary>
        public static Hud Build(GameDirector director, out Image flash)
        {
            // Legacy uGUI Text with the built-in dynamic font: it renders Korean through OS font fallback,
            // which the default TextMeshPro font asset cannot do.
            var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1f;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var root = canvasGo.GetComponent<RectTransform>();
            var hud = canvasGo.AddComponent<Hud>();
            hud.Director = director;

            // Screen flash sits under everything else and never blocks clicks.
            flash = Panel(root, "Screen Flash", new Color(1f, 0.1f, 0.05f, 0f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            flash.raycastTarget = false;

            // Boss bar, top center.
            var bossBg = Panel(root, "Boss Bar", new Color(0, 0, 0, 0.6f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -27), new Vector2(640, 26));
            var bossFill = Panel(bossBg.rectTransform, "Fill", new Color(0.85f, 0.2f, 0.2f, 0.95f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            hud.BossBar = bossBg.gameObject;
            hud.BossFill = bossFill.rectTransform;
            hud.BossText = Label(bossBg.rectTransform, "Text", 16, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Squad stats, top left.
            var stats = Panel(root, "Stats", new Color(0, 0, 0, 0.5f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(137, -44), new Vector2(250, 64));
            hud.AliveText = Label(stats.rectTransform, "Alive", 18, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(12, 0), new Vector2(-12, 0), stretch: true);
            hud.DpsText = Label(stats.rectTransform, "Dps", 18, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(12, 0), new Vector2(-12, 0), stretch: true);

            // Incoming pattern, under the boss bar.
            var warn = Panel(root, "Pattern Warning", new Color(0, 0, 0, 0.65f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(560, 84));
            hud.WarningPanel = warn.gameObject;
            hud.WarningTitle = Label(warn.rectTransform, "Title", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(540, 32), bold: true);
            hud.WarningDetail = Label(warn.rectTransform, "Detail", 16, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -48), new Vector2(540, 24));
            var warnBar = Panel(warn.rectTransform, "Countdown", new Color(0, 0, 0, 0.6f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(520, 10));
            hud.WarningFillImage = Panel(warnBar.rectTransform, "Fill", new Color(1f, 0.75f, 0.2f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            hud.WarningFill = hud.WarningFillImage.rectTransform;

            hud.Toast = Label(root, "Toast", 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(800, 44), bold: true);
            var pause = Label(root, "Pause Label", 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(600, 44), bold: true);
            pause.text = "일시정지 (P)";
            hud.PauseLabel = pause.gameObject;

            // Formation buttons, bottom center. One per formation, in FormationController order.
            int count = director.Squad.Formations.Count;
            hud.FormationButtons = new Button[count];
            hud.FormationLabels = new Text[count];
            const float bw = 130, gap = 10;
            float total = count * (bw + gap) - gap;
            for (int i = 0; i < count; i++)
            {
                float x = -total / 2f + bw / 2f + i * (bw + gap);
                hud.FormationButtons[i] = MakeButton(root, "Formation " + director.Squad.Formations[i].Id,
                    new Vector2(0.5f, 0), new Vector2(x, 58), new Vector2(bw, 40), out hud.FormationLabels[i]);
            }

            // Start / result overlay, on top of everything.
            var overlay = Panel(root, "Overlay", new Color(0, 0, 0, 0.55f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            hud.Overlay = overlay.gameObject;
            hud.OverlayTitle = Label(overlay.rectTransform, "Title", 44, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(900, 60), bold: true);
            hud.OverlayButton = MakeButton(overlay.rectTransform, "Start Button", new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(240, 48), out hud.OverlayButtonText);

            return hud;
        }

        internal static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 a, Vector2 b, bool stretch)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            if (stretch || anchorMin != anchorMax) { rt.offsetMin = a; rt.offsetMax = b; } // a/b = left-bottom / right-top insets
            else { rt.anchoredPosition = a; rt.sizeDelta = b; }                               // a/b = position / size
            return rt;
        }

        internal static Image Panel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 a, Vector2 b)
        {
            var rt = Rect(parent, name, anchorMin, anchorMax, a, b, false);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        internal static Text Label(Transform parent, string name, int size, TextAnchor align, Vector2 anchorMin, Vector2 anchorMax,
                          Vector2 a, Vector2 b, bool bold = false, bool stretch = false)
        {
            var rt = Rect(parent, name, anchorMin, anchorMax, a, b, stretch);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UiFont; t.fontSize = size; t.alignment = align; t.color = Color.white;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static Button MakeButton(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, out Text label)
        {
            var img = Panel(parent, name, new Color(0.12f, 0.16f, 0.22f, 0.9f), anchor, anchor, pos, size);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            // The HUD tints the image itself (selected / recommended), so keep the button's own tint neutral.
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f); colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav; // Enter must not "click" a focused button
            label = Label(img.rectTransform, "Text", 18, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }
    }
}
