using UnityEngine;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Input;

namespace BoneSquadKiting.UI
{
    /// <summary>
    /// Throwaway test UI drawn with IMGUI: boss HP, squad count, DPS, formation, current pattern and
    /// a start / restart button. Exists so the game can be played before the real uGUI HUD (M1 Task 10).
    /// </summary>
    public class TestHud : MonoBehaviour
    {
        const float RefHeight = 720f;

        public GameDirector Director;
        public SquadInputController Input; // optional: shows the attack-move cursor state

        string _toast;
        float _toastUntil;
        GUIStyle _label, _big, _center, _box;

        void OnEnable()
        {
            if (Director == null) return;
            Director.PatternResolved += OnResolved;
            Director.PhaseTwoStarted += OnPhaseTwo;
        }

        void OnDisable()
        {
            if (Director == null) return;
            Director.PatternResolved -= OnResolved;
            Director.PhaseTwoStarted -= OnPhaseTwo;
        }

        void OnResolved(int hit, int lost) => Toast(hit == 0 ? "PERFECT DODGE!" : $"피격 {hit}명 · 전사 {lost}명");
        void OnPhaseTwo() => Toast("PHASE 2");
        void Toast(string text) { _toast = text; _toastUntil = Time.unscaledTime + 1.6f; }

        void OnGUI()
        {
            if (Director == null) return;
            EnsureStyles();
            float scale = Screen.height / RefHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = RefHeight;

            DrawBossBar(w);
            DrawStats();
            DrawPattern(w);
            DrawFormations(w, h);
            DrawHints(w, h);

            if (_toast != null && Time.unscaledTime < _toastUntil)
                GUI.Label(new Rect(0, h * 0.34f, w, 40), _toast, _big);
            if (Director.Paused) GUI.Label(new Rect(0, h * 0.42f, w, 40), "일시정지 (P)", _big);
            if (Director.Phase != GamePhase.Play) DrawOverlay(w, h);
        }

        void DrawBossBar(float w)
        {
            float frac = Director.Boss != null ? Director.Boss.HpFrac : 1f;
            float hp = Director.Boss != null ? Director.Boss.Hp : GameConstants.BossMax;
            var bar = new Rect(w * 0.25f, 14, w * 0.5f, 22);
            Fill(bar, new Color(0, 0, 0, 0.6f));
            Fill(new Rect(bar.x, bar.y, bar.width * frac, bar.height), new Color(0.85f, 0.2f, 0.2f, 0.95f));
            GUI.Label(bar, $"BOSS  {Mathf.CeilToInt(frac * 100)}%   {Mathf.CeilToInt(hp)} / {GameConstants.BossMax:0}   (phase {Director.BossPhase})", _center);
        }

        void DrawStats()
        {
            var squad = Director.Squad;
            int alive = squad != null ? Director.AliveCount : 0;
            GUI.Box(new Rect(12, 12, 250, 156), GUIContent.none, _box);
            var prev = GUI.color;
            if (alive < 10) GUI.color = new Color(1f, 0.45f, 0.4f);
            GUI.Label(new Rect(22, 16, 220, 24), $"생존  {alive} / {GameConstants.SquadSize}", _label);
            GUI.color = prev;
            GUI.Label(new Rect(22, 40, 220, 24), $"DPS  {Mathf.RoundToInt(Director.LastDps)}", _label);
            if (squad == null) return;
            GUI.Label(new Rect(22, 64, 220, 24), $"진형  {squad.Formation}  ({squad.RotationDegrees:0}°)", _label);
            GUI.Label(new Rect(22, 88, 220, 24), $"오더  {OrderText(squad.Order.Kind)}{(squad.IsMoving ? "  · 이동 중" : "")}", _label);
            GUI.Label(new Rect(22, 112, 220, 24), $"시간  {Director.Elapsed:0.0}s", _label);
            GUI.Label(new Rect(22, 136, 220, 24), $"사거리  유닛 {Director.UnitFireRange:0.#} / 분대 {Director.SquadEngageRange:0.#}", _label);
        }

        void DrawPattern(float w)
        {
            if (Director.Attacks?.Current is not Bosses.BossAttackSystem.Active a || Director.Phase != GamePhase.Play) return;
            PatternDefinition p = a.Pattern;
            var box = new Rect(w * 0.3f, 48, w * 0.4f, 74);
            GUI.Box(box, GUIContent.none, _box);
            GUI.Label(new Rect(box.x, box.y + 2, box.width, 28), $"{p.DisplayName}   →   {p.Advice}", _big);
            GUI.Label(new Rect(box.x, box.y + 28, box.width, 22), $"{p.Detail}   {a.Remaining:0.0}s", _center);
            var bar = new Rect(box.x + 10, box.y + 56, box.width - 20, 10);
            Fill(bar, new Color(0, 0, 0, 0.6f));
            float t = p.Time > 0 ? a.Remaining / p.Time : 0;
            bool locked = a.Remaining <= GameConstants.CastTrackLockTime;
            Fill(new Rect(bar.x, bar.y, bar.width * t, bar.height), locked ? new Color(1f, 0.2f, 0.15f) : new Color(1f, 0.75f, 0.2f));
        }

        void DrawFormations(float w, float h)
        {
            var squad = Director.Squad;
            if (squad == null) return;
            string recommend = Director.Phase == GamePhase.Play && Director.Attacks?.Current is Bosses.BossAttackSystem.Active a
                ? a.Pattern.Recommend : null;
            float bw = 120, gap = 8, total = squad.Formations.Count * (bw + gap) - gap;
            float x = (w - total) / 2f, y = h - 62;
            for (int i = 0; i < squad.Formations.Count; i++)
            {
                var f = squad.Formations[i];
                if (f == null) continue;
                var prev = GUI.color;
                if (f.Id == recommend) GUI.color = new Color(1f, 0.85f, 0.3f);
                else if (f.Id == squad.Formation) GUI.color = new Color(0.4f, 1f, 0.9f);
                if (GUI.Button(new Rect(x + i * (bw + gap), y, bw, 34), $"{f.HotKey}  {f.DisplayName}"))
                    Director.ChangeFormation(f.Id);
                GUI.color = prev;
            }
        }

        void DrawHints(float w, float h)
        {
            string hint = "우클릭: 이동 / 적 공격   A+좌클릭: 어택땅   Q·W·E·R: 진형   휠: 회전   화면 가장자리: 카메라   P: 일시정지";
            if (Input != null && Input.AttackMovePending) hint = "어택땅 대기 — 좌클릭으로 지점 지정 (Esc/우클릭 취소)";
            GUI.Label(new Rect(0, h - 24, w, 22), hint, _center);
        }

        void DrawOverlay(float w, float h)
        {
            Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.55f));
            string title = Director.Phase switch
            {
                GamePhase.Win => "승리",
                GamePhase.Lose => "전멸",
                _ => "KITE SQUAD 25",
            };
            GUI.Label(new Rect(0, h * 0.36f, w, 44), title, _big);
            string button = Director.Phase == GamePhase.Menu ? "시작 (Enter)" : "다시 시작 (Enter)";
            if (GUI.Button(new Rect(w / 2f - 110, h * 0.48f, 220, 44), button)) Director.Restart();
        }

        static string OrderText(OrderKind kind) => kind switch
        {
            OrderKind.AttackMove => "어택땅",
            OrderKind.AttackTarget => "대상 공격",
            _ => "이동",
        };

        static void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            _big = new GUIStyle(_center) { fontSize = 24, fontStyle = FontStyle.Bold };
            _box = new GUIStyle(GUI.skin.box);
        }
    }
}
