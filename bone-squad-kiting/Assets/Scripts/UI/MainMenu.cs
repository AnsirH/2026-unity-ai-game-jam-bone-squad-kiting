using UnityEngine;
using UnityEngine.UI;
using BoneSquadKiting.Core;
using BoneSquadKiting.Game;
using BoneSquadKiting.Stage;

namespace BoneSquadKiting.UI
{
    /// <summary>
    /// The title screen of the game scene: new game, continue, quit. It builds its own widgets and shows
    /// while the game is on its title (GamePhase.Menu). New game starts stage 1 from its start point;
    /// continue starts the stage after the last one cleared, from that stage's start point.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public StageRun Run;
        public string Title = "KITE SQUAD 25";

        public GameObject Root { get; private set; }
        public Button NewGameButton { get; private set; }
        public Button ContinueButton { get; private set; }
        public Button QuitButton { get; private set; }
        public Text ContinueLabel { get; private set; }

        /// <summary>True while the menu is on screen.</summary>
        public bool Showing => Root != null && Root.activeSelf;

        void Start()
        {
            Build();
            Refresh();
        }

        void Update() => Refresh();

        /// <summary>Show or hide to match the game, and enable "continue" only when there is a save.</summary>
        public void Refresh()
        {
            if (Root == null || Run == null || Run.Kit == null) return;
            bool show = Run.Kit.Director.Phase == GamePhase.Menu && !Run.Switching;
            if (Root.activeSelf != show) Root.SetActive(show);
            if (!show) return;
            bool save = GameProgress.HasSave;
            ContinueButton.interactable = save;
            ContinueLabel.color = save ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            ContinueLabel.text = save ? $"이어하기  ({StageName(GameProgress.NextStage)})" : "이어하기";
        }

        string StageName(int index)
        {
            if (index < Run.Stages.Count) return Run.Stages[index].Title;
            return "보스";
        }

        public void NewGame()
        {
            GameProgress.Clear();
            Run.StartAt(0);
        }

        public void Continue()
        {
            if (!GameProgress.HasSave) return;
            Run.StartAt(GameProgress.NextStage);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Build()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Root = new GameObject("Main Menu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Root.transform.SetParent(transform, false);
            var canvas = Root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;                       // over the game HUD, under the stage fade
            var scaler = Root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var dim = MakeImage(Root.transform, "Dim", new Color(0f, 0f, 0f, 0.55f));
            dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;

            var title = MakeText(Root.transform, "Title", font, 72, Title);
            title.fontStyle = FontStyle.Bold;
            Place(title.rectTransform, new Vector2(0f, 230f), new Vector2(1200f, 120f));

            NewGameButton = MakeButton("New Game", font, "새 게임", 60f, out _);
            ContinueButton = MakeButton("Continue", font, "이어하기", -30f, out var continueLabel);
            QuitButton = MakeButton("Quit", font, "게임 종료", -120f, out _);
            ContinueLabel = continueLabel;
            NewGameButton.onClick.AddListener(NewGame);
            ContinueButton.onClick.AddListener(Continue);
            QuitButton.onClick.AddListener(Quit);
        }

        Button MakeButton(string name, Font font, string text, float y, out Text label)
        {
            var img = MakeImage(Root.transform, name, new Color(0.12f, 0.16f, 0.22f, 0.95f));
            Place(img.rectTransform, new Vector2(0f, y), new Vector2(420f, 72f));
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.55f, 0.85f, 0.8f, 1f);
            colors.pressedColor = new Color(0.4f, 0.65f, 0.6f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
            var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
            label = MakeText(img.transform, "Text", font, 30, text);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        static Image MakeImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        static Text MakeText(Transform parent, string name, Font font, int size, string text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.text = text; t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }
    }
}
