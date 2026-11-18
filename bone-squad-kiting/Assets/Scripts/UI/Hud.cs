using UnityEngine;
using UnityEngine.UI;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Core;
using BoneSquadKiting.Game;

namespace BoneSquadKiting.UI
{
    /// <summary>
    /// Player-facing HUD (uGUI): boss HP, squad count, DPS, formation buttons, the incoming pattern with
    /// its countdown, result toasts, and the start / win / lose overlay. Reads everything from GameDirector.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        static readonly Color Normal = new(0.12f, 0.16f, 0.22f, 0.9f);
        static readonly Color Selected = new(0.1f, 0.55f, 0.5f, 0.95f);
        static readonly Color Recommended = new(0.85f, 0.6f, 0.1f, 0.95f);
        static readonly Color Tracking = new(1f, 0.75f, 0.2f);
        static readonly Color Locked = new(1f, 0.2f, 0.15f);

        public GameDirector Director;

        [Header("Top")]
        public GameObject BossBar;          // hidden on stages without a boss
        public RectTransform BossFill;
        public Text BossText, AliveText, DpsText;

        [Header("Pattern warning")]
        public GameObject WarningPanel;
        public Text WarningTitle, WarningDetail;
        public RectTransform WarningFill;
        public Image WarningFillImage;

        [Header("Formations (same order as FormationController.Formations)")]
        public Button[] FormationButtons;
        public Text[] FormationLabels;

        [Header("Messages")]
        public Text Toast;
        public GameObject PauseLabel;
        public float ToastSeconds = 1.6f;

        [Header("Start / result overlay")]
        public GameObject Overlay;
        public Text OverlayTitle, OverlayButtonText;
        public Button OverlayButton;

        float _toastUntil;
        bool _wasPlaying;

        void Start()
        {
            if (Director == null) return;
            var formations = Director.Squad.Formations;
            for (int i = 0; i < FormationButtons.Length && i < formations.Count; i++)
            {
                string id = formations[i].Id;
                FormationButtons[i].onClick.AddListener(() => Director.ChangeFormation(id));
                if (i < FormationLabels.Length) FormationLabels[i].text = $"{formations[i].HotKey}  {formations[i].DisplayName}";
            }
            if (OverlayButton != null) OverlayButton.onClick.AddListener(OnOverlayButton);
            Director.PatternResolved += OnResolved;
            Director.PhaseTwoStarted += OnPhaseTwo;
            Director.WaveStarted += OnWaveStarted;
            Director.CutsceneStarted += OnCutscene;
            Director.MiniBossDefeated += OnMiniBossDefeated;
            if (Toast != null) Toast.text = "";
        }

        void OnDestroy()
        {
            if (Director == null) return;
            Director.PatternResolved -= OnResolved;
            Director.PhaseTwoStarted -= OnPhaseTwo;
            Director.WaveStarted -= OnWaveStarted;
            Director.CutsceneStarted -= OnCutscene;
            Director.MiniBossDefeated -= OnMiniBossDefeated;
        }

        void OnCutscene(Vector2 focus, float seconds, string title, GameObject fx) { if (!string.IsNullOrEmpty(title)) ShowToast(title, seconds); }
        void OnMiniBossDefeated(Elite elite) => ShowToast(Director.EndOpen ? "끝 지점이 열렸습니다" : $"{elite.Def.DisplayName} 처치", 3f);
        void OnOverlayButton()
        {
            if (Director.Phase == GamePhase.Win) Director.Proceed();
            else Director.Restart();
        }

        void OnWaveStarted(int wave, int mobs) => ShowToast($"웨이브 {wave}  (+{mobs})");

        void OnResolved(int hit, int lost) => ShowToast(hit == 0 ? "PERFECT DODGE!" : $"피격 {hit}명 · 전사 {lost}명");
        void OnPhaseTwo() => ShowToast("PHASE 2");

        public void ShowToast(string text, float seconds = -1f)
        {
            if (Toast == null) return;
            Toast.text = text;
            _toastUntil = Time.unscaledTime + (seconds > 0f ? seconds : ToastSeconds);
        }

        void Update()
        {
            if (Director == null) return;
            Refresh();
        }

        /// <summary>Push the director's current state into the widgets. Public so tests can call it.</summary>
        public void Refresh()
        {
            bool playing = Director.Phase == GamePhase.Play;
            if (playing && !_wasPlaying && !string.IsNullOrEmpty(Director.StageTitle)) ShowToast(Director.StageTitle, 2f);
            _wasPlaying = playing;

            // The bar shows the boss, or on a stage without one, the mini boss currently in play.
            Elite elite = Director.Elites != null && Director.Elites.Elites.Count > 0 ? Director.Elites.Elites[0] : null;
            bool showBar = Director.HasBoss || elite != null;
            if (BossBar != null && BossBar.activeSelf != showBar) BossBar.SetActive(showBar);
            if (elite != null && !Director.HasBoss)
            {
                SetFill(BossFill, elite.HpFrac);
                BossText.text = $"{elite.Def.DisplayName}  {Mathf.CeilToInt(elite.HpFrac * 100)}%   {Mathf.CeilToInt(elite.Health)} / {elite.Def.MaxHp:0}";
            }
            else
            {
                float frac = Director.Boss != null ? Director.Boss.HpFrac : 1f;
                float hp = Director.Boss != null ? Director.Boss.Hp : GameConstants.BossMax;
                SetFill(BossFill, frac);
                BossText.text = $"BOSS  {Mathf.CeilToInt(frac * 100)}%   {Mathf.CeilToInt(hp)} / {GameConstants.BossMax:0}";
            }

            int alive = Director.AliveCount;
            int dead = Director.DeadCount, reviving = Director.Revives != null ? Director.Revives.Active.Count : 0;
            AliveText.text = dead > 0
                ? $"생존  {alive} / {GameConstants.SquadSize}   전사 {dead}" + (reviving > 0 ? $" (부활 중 {reviving})" : "")
                : $"생존  {alive} / {GameConstants.SquadSize}";
            AliveText.color = alive < 10 ? new Color(1f, 0.45f, 0.4f) : Color.white;
            DpsText.text = $"DPS  {Mathf.RoundToInt(Director.LastDps)}";

            BossAttackSystem.Active? current = playing ? Director.NextTelegraph : null;
            WarningPanel.SetActive(current.HasValue);
            string recommend = null;
            if (current is BossAttackSystem.Active a)
            {
                recommend = a.Pattern.Recommend;
                WarningTitle.text = $"{a.Pattern.DisplayName}   →   {a.Pattern.Advice}";
                WarningDetail.text = $"{a.Pattern.Detail}   {a.Remaining:0.0}s";
                SetFill(WarningFill, a.Pattern.Time > 0 ? a.Remaining / a.Pattern.Time : 0);
                WarningFillImage.color = a.Remaining <= GameConstants.CastTrackLockTime ? Locked : Tracking;
            }

            var formations = Director.Squad.Formations;
            for (int i = 0; i < FormationButtons.Length && i < formations.Count; i++)
            {
                string id = formations[i].Id;
                FormationButtons[i].image.color = id == recommend ? Recommended
                    : id == Director.Squad.Formation ? Selected : Normal;
            }

            if (Toast != null && Toast.text.Length > 0 && Time.unscaledTime >= _toastUntil) Toast.text = "";
            PauseLabel.SetActive(playing && Director.Paused);

            Overlay.SetActive(!playing);
            if (!playing)
            {
                string stage = string.IsNullOrEmpty(Director.StageTitle) ? "" : Director.StageTitle + "  ";
                OverlayTitle.text = Director.Phase switch
                {
                    GamePhase.Win => Director.IsFinalStage ? "게임 클리어" : stage + "클리어",
                    GamePhase.Lose => stage + "전멸",
                    _ => "KITE SQUAD 25",
                };
                OverlayButtonText.text = Director.Phase switch
                {
                    GamePhase.Menu => "시작 (Enter)",
                    GamePhase.Win => Director.IsFinalStage ? "처음으로 (Enter)" : "다음 스테이지 (Enter)",
                    _ => "다시 시작 (Enter)",
                };
            }
        }

        static void SetFill(RectTransform fill, float frac)
        {
            if (fill == null) return;
            fill.anchorMax = new Vector2(Mathf.Clamp01(frac), 1f);
        }
    }
}
