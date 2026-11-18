using UnityEngine;
using UnityEngine.InputSystem;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Game;
using BoneSquadKiting.Input;

namespace BoneSquadKiting.UI
{
    /// <summary>
    /// Debug overlay for playtesting (IMGUI): the values the player HUD does not show — current order,
    /// formation rotation, live ranges, match time — plus a key reminder. F1 toggles it.
    /// </summary>
    public class TestHud : MonoBehaviour
    {
        const float RefHeight = 720f;

        public GameDirector Director;
        public SquadInputController Input; // optional: shows the attack-move cursor state
        public bool Visible = true;

        GUIStyle _label, _center, _box;

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current[Key.F1].wasPressedThisFrame) Visible = !Visible;
        }

        void OnGUI()
        {
            if (Director == null || !Visible) return;
            EnsureStyles();
            float scale = Screen.height / RefHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = RefHeight;

            var squad = Director.Squad;
            if (squad != null)
            {
                GUI.Box(new Rect(12, 84, 290, 180), GUIContent.none, _box);
                GUI.Label(new Rect(22, 88, 230, 24), "테스트 정보 (F1)", _label);
                GUI.Label(new Rect(22, 112, 230, 24), $"진형  {squad.Formation}  ({squad.RotationDegrees:0}°)", _label);
                GUI.Label(new Rect(22, 136, 230, 24), $"오더  {OrderText(squad.Order.Kind)}{(squad.IsMoving ? "  · 이동 중" : "")}", _label);
                GUI.Label(new Rect(22, 160, 230, 24), $"사거리  유닛 {Director.UnitFireRange:0.#} / 분대 {Director.SquadEngageRange:0.#}", _label);
                GUI.Label(new Rect(22, 184, 230, 24), $"시간  {Director.Elapsed:0.0}s   페이즈 {Director.BossPhase}", _label);
                if (Director.Mobs != null)
                {
                    string waves = Director.Waves != null
                        ? $"   웨이브 {Director.Waves.WavesSpawned}   진행 {Mathf.RoundToInt(Director.Waves.MaxProgress * 100)}%" : "";
                    GUI.Label(new Rect(22, 208, 240, 24), $"적  {Director.Mobs.AliveCount}{waves}", _label);
                    if (Director.ClearRule == Stage.StageClearRule.DefeatMiniBossThenReachEnd)
                        GUI.Label(new Rect(22, 232, 240, 24), Director.EndOpen ? "끝 지점: 열림" : "끝 지점: 닫힘 (미니 보스 처치 필요)", _label);
                }
            }

            string hint = "우클릭: 이동 / 적 공격   A+좌클릭: 어택땅   S: 정지   F: 부활   Q·W·E·R: 진형   휠: 회전   화면 가장자리: 카메라   P: 일시정지";
            if (Input != null && Input.AttackMovePending) hint = "어택땅 대기 — 좌클릭으로 지점 지정 (Esc/우클릭 취소)";
            GUI.Label(new Rect(0, h - 24, w, 22), hint, _center);
        }

        static string OrderText(OrderKind kind) => kind switch
        {
            OrderKind.AttackMove => "어택땅",
            OrderKind.AttackTarget => "대상 공격",
            _ => "이동",
        };

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            _box = new GUIStyle(GUI.skin.box);
        }
    }
}
