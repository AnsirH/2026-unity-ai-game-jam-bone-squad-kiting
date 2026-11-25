using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Core;
using BoneSquadKiting.Game;
using BoneSquadKiting.Stage;

namespace BoneSquadKiting.UI
{
    /// <summary>
    /// Player-facing HUD (uGUI): boss HP, squad count, DPS, formation buttons, the incoming pattern with
    /// its countdown, result toasts, and the start / win / lose overlay. Reads everything from GameDirector.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        static readonly Color Normal = new(0.045f, 0.055f, 0.065f, 0.96f);
        static readonly Color Selected = new(0.30f, 0.24f, 0.14f, 0.98f);
        static readonly Color Recommended = new(0.85f, 0.6f, 0.1f, 0.95f);
        static readonly Color Tracking = new(1f, 0.75f, 0.2f);
        static readonly Color Locked = new(1f, 0.2f, 0.15f);

        public GameDirector Director;

        [Header("Top")]
        public Image PortalGuide;
        public GameObject BossBar;          // shared by the current encounter
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

        [Header("Start / pause / result overlay")]
        public GameObject Overlay;
        public Text OverlayTitle, OverlayButtonText;
        public Button OverlayButton, PauseQuitButton;

        Text _overlayHint;
        Vector2 _resultButtonPosition, _resultButtonSize;
        Navigation _resultNavigation;
        ColorBlock _resultButtonColors;
        int _resultFontSize;
        bool _pauseLayout;
        float _toastUntil;
        bool _wasPlaying;

        void Start()
        {
            if (GetComponent<HitCounterHud>() == null) gameObject.AddComponent<HitCounterHud>();
            HudSkin.Apply(this);
            if (PauseLabel != null) PauseLabel.SetActive(false);   // older HUDs have a standalone pause label
            if (OverlayButton != null)
            {
                var rect = (RectTransform)OverlayButton.transform;
                _resultButtonPosition = rect.anchoredPosition;
                _resultButtonSize = rect.sizeDelta;
                _resultNavigation = OverlayButton.navigation;
                _resultButtonColors = OverlayButton.colors;
                _resultFontSize = OverlayButtonText.fontSize;
                _overlayHint = Overlay.transform.Find("Result Card/Hint")?.GetComponent<Text>();
            }
            if (Director == null) return;
            var formations = Director.Squad.Formations;
            for (int i = 0; i < FormationButtons.Length && i < formations.Count; i++)
            {
                string id = formations[i].Id;
                FormationButtons[i].onClick.AddListener(() => Director.ChangeFormation(id));
                if (i < FormationLabels.Length) FormationLabels[i].text = $"{formations[i].HotKey}  {formations[i].DisplayName}";
            }
            if (OverlayButton != null) OverlayButton.onClick.AddListener(OnOverlayButton);
            if (PauseQuitButton != null) PauseQuitButton.onClick.AddListener(OnQuitButton);
            Director.PatternResolved += OnResolved;
            Director.PhaseTwoStarted += OnPhaseTwo;
            Director.NearMissed += OnNearMiss;
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
            Director.NearMissed -= OnNearMiss;
            Director.WaveStarted -= OnWaveStarted;
            Director.CutsceneStarted -= OnCutscene;
            Director.MiniBossDefeated -= OnMiniBossDefeated;
        }

        void OnCutscene(Vector2 focus, float seconds, string title, GameObject fx) { if (!string.IsNullOrEmpty(title)) ShowToast(title, seconds); }
        void OnMiniBossDefeated(Elite elite)
        {
            bool gated = Director.ClearRule == Stage.StageClearRule.DefeatMiniBossThenReachEnd;
            ShowToast(gated && Director.EndOpen ? "끝 지점이 열렸습니다" : $"{elite.Def.DisplayName} 처치", 3f);
        }
        void OnOverlayButton()
        {
            bool advancingStage = Director.Phase == GamePhase.Win && !Director.IsFinalStage;
            if (Director.Phase == GamePhase.Play)
            {
                if (!Director.Paused) return;
                Director.TogglePause();
            }
            else if (Director.Phase == GamePhase.Win) Director.Proceed();
            else Director.Restart();
            if (!advancingStage) BoneSquadKiting.Audio.SoundManager.Current?.PlaySfx("ui.click");
        }

        void OnQuitButton()
        {
            if (Director.Phase == GamePhase.Play && Director.Paused) MainMenu.QuitGame();
        }

        void SetPauseLayout(bool paused)
        {
            if (_pauseLayout == paused) return;
            _pauseLayout = paused;
            var primary = (RectTransform)OverlayButton.transform;
            PauseQuitButton.gameObject.SetActive(paused);
            var events = EventSystem.current;
            if (paused)
            {
                Overlay.transform.SetAsLastSibling();
                primary.anchoredPosition = new Vector2(-122, -68);
                primary.sizeDelta = HudSkin.PauseButtonSize;
                OverlayButtonText.fontSize = HudSkin.PauseActionFontSize;
                var colors = _resultButtonColors;
                colors.normalColor = new Color(.86f, .86f, .83f);
                colors.selectedColor = new Color(1f, .91f, .74f);
                colors.highlightedColor = Color.white;
                OverlayButton.colors = PauseQuitButton.colors = colors;
                OverlayButton.navigation = Toward(PauseQuitButton);
                PauseQuitButton.navigation = Toward(OverlayButton);
                events?.SetSelectedGameObject(OverlayButton.gameObject);
            }
            else
            {
                primary.anchoredPosition = _resultButtonPosition;
                primary.sizeDelta = _resultButtonSize;
                OverlayButtonText.fontSize = _resultFontSize;
                OverlayButton.colors = _resultButtonColors;
                OverlayButton.navigation = _resultNavigation;
                if (events != null && (events.currentSelectedGameObject == OverlayButton.gameObject ||
                    events.currentSelectedGameObject == PauseQuitButton.gameObject))
                    events.SetSelectedGameObject(null);
            }
            if (_overlayHint != null) _overlayHint.text = paused ? "ESC  /  RESUME" : "ENTER  /  CONTINUE";
        }

        static Navigation Toward(Button other) => new Navigation
        {
            mode = Navigation.Mode.Explicit, selectOnLeft = other, selectOnRight = other
        };

        void OnWaveStarted(int wave, int mobs) => ShowToast($"웨이브 {wave}  (+{mobs})");

        int _nearMissFrame = -1;
        int BuffPercent => Mathf.RoundToInt((Director.NearMissDamageMultiplier - 1f) * 100f);
        void OnNearMiss(int unitsNear)
        {
            _nearMissFrame = Time.frameCount;
            ShowToast($"NEAR MISS!  공격력 +{BuffPercent}%", 2.2f);
        }

        void OnResolved(int hit, int lost)
        {
            if (hit == 0 && _nearMissFrame == Time.frameCount) return;   // the near-miss message says it better
            ShowToast(hit == 0 ? "PERFECT DODGE!" : $"피격 {hit}명 · 전사 {lost}명");
        }
        void OnPhaseTwo() => ShowToast("PHASE 2");

        public void ShowToast(string text, float seconds = -1f)
        {
            if (Toast == null) return;
            Toast.text = text;
            _toastUntil = Time.time + (seconds > 0f ? seconds : ToastSeconds);
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

            var target = playing ? EncounterHealth() : null;
            if (BossBar != null && BossBar.activeSelf != target.HasValue) BossBar.SetActive(target.HasValue);
            if (target is { } health)
            {
                float fraction = health.Max > 0f ? Mathf.Clamp01(health.Hp / health.Max) : 0f;
                SetFill(BossFill, fraction);
                BossText.text = $"{health.Name}  {Mathf.CeilToInt(fraction * 100)}%   {Mathf.CeilToInt(health.Hp)} / {health.Max:0}";
            }

            int alive = Director.AliveCount;
            int dead = Director.DeadCount, reviving = Director.Revives != null ? Director.Revives.Active.Count : 0;
            AliveText.text = dead > 0
                ? $"생존  {alive} / {GameConstants.SquadSize}   전사 {dead}" + (reviving > 0 ? $" (부활 중 {reviving})" : "")
                : $"생존  {alive} / {GameConstants.SquadSize}";
            AliveText.color = alive < 10 ? new Color(1f, 0.45f, 0.4f) : Color.white;
            DpsText.text = Director.NearMissBuffLeft > 0f
                ? $"DPS  {Mathf.RoundToInt(Director.LastDps)}   공격력 +{BuffPercent}%  {Director.NearMissBuffLeft:0.0}s"
                : $"DPS  {Mathf.RoundToInt(Director.LastDps)}";
            DpsText.color = Director.NearMissBuffLeft > 0f ? new Color(1f, 0.85f, 0.3f) : Color.white;

            BossAttackSystem.Active? current = playing ? Director.NextTelegraph : null;
            WarningPanel.SetActive(current.HasValue);
            string recommend = null;
            if (current is BossAttackSystem.Active a)
            {
                recommend = a.Pattern.Recommend;
                WarningTitle.text = $"{a.Pattern.DisplayName}   →   {a.Pattern.Advice}";
                if (a.Sustaining)
                {
                    WarningDetail.text = $"지속 중 — 범위 밖에 머무르세요   {Mathf.Max(0f, a.SustainRemaining):0.0}s";
                    SetFill(WarningFill, a.Pattern.SustainSeconds > 0 ? a.SustainRemaining / a.Pattern.SustainSeconds : 0);
                    WarningFillImage.color = Locked;
                }
                else
                {
                    WarningDetail.text = $"{a.Pattern.Detail}   {a.Remaining:0.0}s";
                    SetFill(WarningFill, a.Pattern.Time > 0 ? a.Remaining / a.Pattern.Time : 0);
                    WarningFillImage.color = a.Remaining <= GameConstants.CastTrackLockTime ? Locked : Tracking;
                }
            }

            var formations = Director.Squad.Formations;
            for (int i = 0; i < FormationButtons.Length && i < formations.Count; i++)
            {
                string id = formations[i].Id;
                bool artwork = FormationButtons[i].image.sprite != null;
                FormationButtons[i].image.color = artwork
                    ? (id == recommend ? new Color(.7f, .7f, .67f) : id == Director.Squad.Formation ? new Color(.56f,.56f,.54f) : new Color(.4f,.4f,.39f))
                    : id == recommend ? Recommended : id == Director.Squad.Formation ? Selected : Normal;
            }

            if (Toast != null && Toast.text.Length > 0 && Time.time >= _toastUntil) Toast.text = "";
            bool paused = playing && Director.Paused;
            Overlay.SetActive(!playing || paused);
            SetPauseLayout(paused);
            if (paused)
            {
                if (OverlayTitle.text != "일시정지")
                {
                    OverlayTitle.text = "일시정지";
                    HudSkin.RefreshTitleFont(this);
                }
                OverlayButtonText.text = "재개";
            }
            else if (!playing)
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
                HudSkin.RefreshTitleFont(this);
            }
        }

        (string Name, float Hp, float Max)? EncounterHealth()
        {
            var fight = Director.Fight;
            if (fight != null || Director.HasBoss)
            {
                var boss = Director.Boss;
                if (boss == null || !boss.Alive || fight?.State == FrostKnightState.Waiting) return null;
                return (fight?.BossName ?? "BOSS", boss.Hp, boss.MaxHp);
            }

            var wall = BreakableWall.Shown;
            if (wall != null && wall.Director == Director && wall.Alive)
                return (wall.DisplayName, wall.Health, wall.MaxHp);

            var elites = Director.Elites?.Elites;
            if (elites == null) return null;
            Elite nearest = null;
            float nearestSqr = float.PositiveInfinity;
            for (int i = elites.Count - 1; i >= 0; i--)
            {
                var elite = elites[i];
                if (!elite.Alive) continue;
                // A newly arrived mini boss takes the bar even if an emplacement is still nearby.
                if (elite.HealthBarApproachRadius is not float radius)
                    return (elite.Def.DisplayName, elite.Health, elite.Def.MaxHp);
                float distanceSqr = (elite.Pos - Director.Squad.Anchor).sqrMagnitude;
                if (distanceSqr <= radius * radius && distanceSqr < nearestSqr)
                {
                    nearest = elite;
                    nearestSqr = distanceSqr;
                }
            }
            if (nearest == null) return null;
            return (nearest.Def.DisplayName, nearest.Health, nearest.Def.MaxHp);
        }

        static void SetFill(RectTransform fill, float frac)
        {
            if (fill == null) return;
            fill.anchorMax = new Vector2(Mathf.Clamp01(frac), 1f);
        }
    }
}
