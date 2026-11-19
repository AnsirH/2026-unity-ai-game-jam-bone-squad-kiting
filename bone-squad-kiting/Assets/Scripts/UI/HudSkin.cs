using UnityEngine;
using UnityEngine.UI;

namespace BoneSquadKiting.UI
{
    public static class HudSkin
    {
        static readonly Color Ink = new(0.035f, 0.043f, 0.052f, 0.96f);
        static readonly Color Gold = new(0.61f, 0.49f, 0.30f, 0.85f);
        static readonly Color Paper = new(0.91f, 0.87f, 0.77f);

        static void Place(RectTransform r, Vector2 anchor, Vector2 position, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = position;
            r.sizeDelta = size;
        }

        static void Frame(GameObject go)
        {
            var image = go.GetComponent<Image>();
            if (image != null) image.color = Ink;
            var outline = go.GetComponent<Outline>();
            if (outline == null) outline = go.AddComponent<Outline>();
            outline.effectColor = Gold;
            outline.effectDistance = new Vector2(1, -1);
        }

        static Text Label(Transform parent, string name, string text, Font font, int size,
            Vector2 position, Vector2 dimensions, Color color)
        {
            var existing = parent.Find(name);
            var go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            var t = go.GetComponent<Text>();
            t.text = text; t.font = font; t.fontSize = size; t.color = color;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            Place(t.rectTransform, new Vector2(0.5f, 0.5f), position, dimensions);
            return t;
        }

        public static void Apply(Hud hud)
        {
            if (hud == null || hud.AliveText == null || hud.Overlay == null) return;
            var scaler = hud.GetComponent<CanvasScaler>();
            if (scaler != null) { scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 0.5f; }
            foreach (var text in hud.GetComponentsInChildren<Text>(true))
            {
                text.color = Paper;
                text.raycastTarget = false;
            }
            var stats = hud.AliveText.transform.parent.GetComponent<RectTransform>();
            Place(stats, new Vector2(0, 1), new Vector2(165, -58), new Vector2(280, 76));
            Frame(stats.gameObject);
            hud.AliveText.fontSize = 17;
            hud.DpsText.fontSize = 14;
            if (hud.BossBar != null)
            {
                Place((RectTransform)hud.BossBar.transform, new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(490, 26));
                Frame(hud.BossBar);
                hud.BossFill.GetComponent<Image>().color = new Color(0.46f, 0.09f, 0.1f);
                hud.BossText.fontSize = 14;
            }
            if (hud.WarningPanel != null) Frame(hud.WarningPanel);
            int count = hud.FormationButtons.Length;
            for (int i = 0; i < count; i++)
            {
                var button = hud.FormationButtons[i];
                if (button == null) continue;
                Place((RectTransform)button.transform, new Vector2(0.5f, 0), new Vector2((i - (count - 1) * 0.5f) * 144, 60), new Vector2(134, 54));
                Frame(button.gameObject);
                var colors = button.colors;
                colors.highlightedColor = new Color(1.15f, 1.12f, 1.05f);
                colors.pressedColor = new Color(0.75f, 0.7f, 0.6f);
                button.colors = colors;
                if (i < hud.FormationLabels.Length) hud.FormationLabels[i].fontSize = 17;
            }
            var overlay = hud.Overlay.GetComponent<Image>();
            if (overlay != null) overlay.color = new Color(0.012f, 0.018f, 0.025f, 0.82f);
            var panel = hud.Overlay.transform.Find("Result Card");
            if (panel == null)
            {
                var go = new GameObject("Result Card", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(hud.Overlay.transform, false);
                go.layer = hud.Overlay.layer;
                panel = go.transform;
            }
            Place((RectTransform)panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580, 300));
            panel.SetAsFirstSibling();
            Frame(panel.gameObject);
            Label(panel, "Eyebrow", "—  B O N E   S Q U A D  —", hud.OverlayTitle.font, 13,
                new Vector2(0, 106), new Vector2(520, 25), Gold);
            Label(panel, "Rule", "━━━━━━━━  ◆  ━━━━━━━━", hud.OverlayTitle.font, 14,
                new Vector2(0, -4), new Vector2(480, 25), Gold);
            Label(panel, "Hint", "ENTER  /  CONTINUE", hud.OverlayTitle.font, 11,
                new Vector2(0, -120), new Vector2(400, 24), new Color(0.6f, 0.59f, 0.54f));
            Place(hud.OverlayTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 45), new Vector2(540, 70));
            hud.OverlayTitle.fontSize = 34;
            hud.OverlayTitle.fontStyle = FontStyle.Bold;
            hud.OverlayTitle.resizeTextForBestFit = true;
            hud.OverlayTitle.resizeTextMinSize = 22;
            hud.OverlayTitle.resizeTextMaxSize = 34;
            Place((RectTransform)hud.OverlayButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -67), new Vector2(290, 48));
            Frame(hud.OverlayButton.gameObject);
            hud.OverlayButton.image.color = new Color(0.24f, 0.18f, 0.11f, 1f);
            hud.OverlayButtonText.fontSize = 18;
            if (hud.Toast != null) { hud.Toast.fontSize = 23; hud.Toast.color = Paper; }
            ApplyArtwork(hud, panel, stats);
        }

        static void Art(Image image, Sprite sprite)
        {
            if (image == null || sprite == null) return;
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            var outline = image.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
        }

        static void ApplyArtwork(Hud hud, Transform panel, RectTransform stats)
        {
            var art = Resources.Load<HudArtwork>("HudArtwork");
            if (art == null) return;
            if (art.KoreanFont != null)
                foreach (var text in hud.GetComponentsInChildren<Text>(true))
                { text.font = art.KoreanFont; text.fontStyle = FontStyle.Normal; }
            Art(panel.GetComponent<Image>(), art.Menu);
            Art(stats.GetComponent<Image>(), art.Stats);
            Art(hud.OverlayButton.image, art.StartButton);
            Place((RectTransform)panel, new Vector2(.5f,.5f), Vector2.zero, new Vector2(660,350));
            Place(stats, new Vector2(0,1), new Vector2(166,-59), new Vector2(304,108));
            Place(hud.AliveText.rectTransform, new Vector2(.5f,.5f), new Vector2(0,18), new Vector2(244,27));
            Place(hud.DpsText.rectTransform, new Vector2(.5f,.5f), new Vector2(0,-19), new Vector2(244,26));
            hud.AliveText.fontSize = 16;
            hud.DpsText.fontSize = 14;
            Place((RectTransform)hud.OverlayButton.transform, new Vector2(.5f,.5f), new Vector2(0,-72), new Vector2(340,110));
            hud.OverlayButtonText.fontSize = 18;
            var title = hud.OverlayTitle;
            if (art.TitleFont != null && IsLatin(title.text)) title.font = art.TitleFont;
            var eyebrow = panel.Find("Eyebrow").GetComponent<Text>();
            eyebrow.text = "— BONE SQUAD —";
            if (art.TitleFont != null) eyebrow.font = art.TitleFont;
            var hint = panel.Find("Hint").GetComponent<Text>();
            if (art.TitleFont != null) hint.font = art.TitleFont;
            panel.Find("Rule").GetComponent<Text>().text = "";
            var divider = panel.Find("Divider Art");
            if (divider == null)
            {
                var go = new GameObject("Divider Art", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(panel, false);
                go.layer = panel.gameObject.layer;
                divider = go.transform;
            }
            Place((RectTransform)divider, new Vector2(.5f,.5f), new Vector2(0,-3), new Vector2(350,100));
            Art(divider.GetComponent<Image>(), art.Divider);
            divider.GetComponent<Image>().raycastTarget = false;
            for (int i=0;i<hud.FormationButtons.Length;i++)
            {
                var button=hud.FormationButtons[i];
                if (i < art.Formations.Length) Art(button.image, art.Formations[i]);
                Place((RectTransform)button.transform,new Vector2(.5f,0),new Vector2((i-(hud.FormationButtons.Length-1)*.5f)*144,60),new Vector2(166,82));
                if (i >= hud.FormationLabels.Length) continue;
                Place(hud.FormationLabels[i].rectTransform,new Vector2(.5f,.5f),new Vector2(24,0),new Vector2(84,32));
                hud.FormationLabels[i].fontSize=15;
            }
        }

        public static void RefreshTitleFont(Hud hud)
        {
            var art = Resources.Load<HudArtwork>("HudArtwork");
            if (art != null) hud.OverlayTitle.font = IsLatin(hud.OverlayTitle.text) && art.TitleFont != null ? art.TitleFont : art.KoreanFont;
        }

        static bool IsLatin(string text)
        {
            foreach (char c in text) if (c > 127) return false;
            return true;
        }
    }
}
