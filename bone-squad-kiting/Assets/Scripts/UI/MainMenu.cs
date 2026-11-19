using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using BoneSquadKiting.Audio;
using BoneSquadKiting.Core;
using BoneSquadKiting.Game;
using BoneSquadKiting.Stage;

namespace BoneSquadKiting.UI
{
    /// <summary>
    /// The main menu, in a scene of its own: new game, continue, quit. No map and no game camera are
    /// loaded here. A button loads the game scene with only the chosen stage's map set up, and play
    /// starts at once from that stage's start point with a full squad.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [Tooltip("The scene that holds the stage maps.")]
        public string GameScene = "Game";
        public string Title = "KITE SQUAD 25";
        [Tooltip("The hand that bursts out of the ground when a game is started. Empty = start at once.")]
        public CommanderHandBurst Burst;
        [Tooltip("Seconds the burst is shown before the game loads.")]
        public float BurstSeconds = 2f;
        [Tooltip("Seconds at the end of the burst over which the screen goes dark.")]
        public float FadeSeconds = 0.35f;

        /// <summary>True from the click until the game scene loads.</summary>
        public bool Starting { get; private set; }
        [SerializeField] Image _fade;
        [SerializeField] GameObject _widgets;

        [field: SerializeField] public GameObject Root { get; private set; }
        [field: SerializeField] public Button NewGameButton { get; private set; }
        [field: SerializeField] public Button ContinueButton { get; private set; }
        [field: SerializeField] public Button QuitButton { get; private set; }
        [field: SerializeField] public Text ContinueLabel { get; private set; }

        void Start()
        {
            Time.timeScale = 1f;
            SoundManager.Current?.PlayBgm("bgm.stage3");
            CreateEditableCanvas();
            _widgets.SetActive(true);
            SetFade(0f);
            Bind(NewGameButton, NewGame);
            Bind(ContinueButton, Continue);
            Bind(QuitButton, Quit);
            Refresh();
        }

        // Persistent Inspector callbacks are preserved; old/generated scenes get runtime bindings.
        void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            button.onClick.RemoveListener(action);
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentTarget(i) == this &&
                    button.onClick.GetPersistentMethodName(i) == action.Method.Name &&
                    button.onClick.GetPersistentListenerState(i) != UnityEngine.Events.UnityEventCallState.Off)
                    return;
            button.onClick.AddListener(action);
        }

        /// <summary>Create only when absent; authored layout, text and artwork survive Play.</summary>
        [ContextMenu("Create Editable Canvas")]
        public void CreateEditableCanvas()
        {
            if (Root != null) return;
            Build();
        }

        /// <summary>Enable "continue" only when there is a save.</summary>
        public void Refresh()
        {
            if (Root == null || Starting) return;
            bool save = GameProgress.HasSave;
            ContinueButton.interactable = save;
            ContinueLabel.color = save ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        }

        /// <summary>Stage 1, from its start point. The old save is forgotten.</summary>
        public void NewGame()
        {
            if (Starting) return;
            GameProgress.Clear();
            StartCoroutine(BurstThenLoad(GameScene, 0));
        }

        /// <summary>The stage after the last one cleared, from its start point.</summary>
        public void Continue()
        {
            if (Starting || !GameProgress.HasSave) return;
            if (!string.IsNullOrEmpty(GameProgress.NextScene)) StartCoroutine(BurstThenLoad(GameProgress.NextScene, -1));
            else StartCoroutine(BurstThenLoad(GameScene, GameProgress.NextStage));
        }

        // The buttons go away, the hand bursts out, the screen goes dark at the end, and the game loads.
        IEnumerator BurstThenLoad(string scene, int stage)
        {
            Starting = true;
            _widgets.SetActive(false);
            if (Burst != null)
            {
                Burst.PlayBurst();
                float fadeFrom = Mathf.Max(0f, BurstSeconds - FadeSeconds);
                for (float t = 0f; t < BurstSeconds; t += Time.unscaledDeltaTime)
                {
                    if (t > fadeFrom && FadeSeconds > 0f) SetFade((t - fadeFrom) / FadeSeconds);
                    yield return null;
                }
            }
            SetFade(1f);
            StageRun.RequestedStage = stage;
            GameDirector.LoadStageScene(scene);
        }

        void SetFade(float a) { var c = _fade.color; c.a = Mathf.Clamp01(a); _fade.color = c; }

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
            var art = Resources.Load<HudArtwork>("HudArtwork");
            var font = art != null && art.KoreanFont != null ? art.KoreanFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Root = new GameObject("Main Menu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Root.transform.SetParent(transform, false);
            var canvas = Root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        var scaler = Root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Title and buttons sit to the left, so the middle of the screen is free for the hand.
            _widgets = new GameObject("Widgets", typeof(RectTransform));
            _widgets.transform.SetParent(Root.transform, false);
            var wr = (RectTransform)_widgets.transform;
            wr.anchorMin = Vector2.zero; wr.anchorMax = Vector2.one; wr.offsetMin = wr.offsetMax = Vector2.zero;

            var title = MakeText(_widgets.transform, "Title", font, 72, Title);
            if (art != null && art.TitleFont != null) title.font = art.TitleFont;
            title.fontStyle = FontStyle.Bold;
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -130f); title.rectTransform.sizeDelta = new Vector2(1200f, 120f);

            NewGameButton = MakeButton("New Game", font, "새 게임", 120f, out _);
            ContinueButton = MakeButton("Continue", font, "이어하기", 0f, out var continueLabel);
            QuitButton = MakeButton("Quit", font, "게임 종료", -120f, out _);
            ContinueLabel = continueLabel;
            if (art != null && art.StartButton != null)
                foreach (var button in new[] { NewGameButton, ContinueButton, QuitButton })
                {
                    button.image.sprite = art.StartButton;
                    button.image.color = art.ArtworkTint;
                    ((RectTransform)button.transform).sizeDelta = new Vector2(650f, 160f);
                    var colors = button.colors;
                    colors.highlightedColor = new Color(1f, .92f, .75f);
                    colors.pressedColor = new Color(.65f, .58f, .45f);
                    button.colors = colors;
                }

            _fade = MakeImage(Root.transform, "Fade", new Color(0f, 0f, 0f, 0f));
            _fade.raycastTarget = false;
            _fade.rectTransform.anchorMin = Vector2.zero; _fade.rectTransform.anchorMax = Vector2.one;
            _fade.rectTransform.offsetMin = _fade.rectTransform.offsetMax = Vector2.zero;
        }

        Button MakeButton(string name, Font font, string text, float y, out Text label)
        {
            var img = MakeImage(_widgets.transform, name, new Color(0.12f, 0.16f, 0.22f, 0.95f));
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            img.rectTransform.anchoredPosition = new Vector2(390f, y); img.rectTransform.sizeDelta = new Vector2(550f, 96f);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.55f, 0.85f, 0.8f, 1f);
            colors.pressedColor = new Color(0.4f, 0.65f, 0.6f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
            var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
            label = MakeText(img.transform, "Text", font, 28, text);
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
