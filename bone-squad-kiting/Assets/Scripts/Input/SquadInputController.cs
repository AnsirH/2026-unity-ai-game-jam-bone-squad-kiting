using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Game;

namespace BoneSquadKiting.Input
{
    /// <summary>
    /// Mouse / keyboard to squad commands. Right click: enemy = attack it, ground = move.
    /// S: stop. F: revive. A then left click: attack-move. Mouse wheel: rotate the formation. Formation hotkeys come from the formation assets (Q/W/E/R).
    /// Reads the new Input System directly; the project has the legacy Input class disabled.
    /// </summary>
    public class SquadInputController : MonoBehaviour
    {
        static readonly Plane Ground = new(Vector3.up, Vector3.zero);

        public Camera Cam;
        public GameDirector Director;
        public LayerMask EnemyMask = ~0;

        /// <summary>True between pressing A and the click that places the attack-move.</summary>
        public bool AttackMovePending { get; private set; }

        void Update()
        {
            if (Director == null || Keyboard.current == null || Mouse.current == null) return;

            if (Director.Phase == GamePhase.Play && !Director.Paused)
            {
                if (Pressed(Key.A)) AttackMovePending = true;
                if (Pressed(Key.Escape)) AttackMovePending = false;
                if (Pressed(Key.S)) { AttackMovePending = false; Director.StopSquad(); }
                if (Pressed(Key.F)) Director.TryRevive();

                // A click that lands on a HUD button must not also be read as a squad command.
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

                if (overUi) { }
                else if (Mouse.current.leftButton.wasPressedThisFrame && AttackMovePending)
                {
                    AttackMovePending = false;
                    if (PickEnemy(out var enemy)) Director.IssueOrder(SquadOrder.Attack(enemy));
                    else if (PickGround(out var point)) Director.IssueOrder(SquadOrder.AttackMove(point));
                }
                else if (Mouse.current.rightButton.wasPressedThisFrame)
                {
                    if (AttackMovePending) AttackMovePending = false; // right click cancels a pending attack-move
                    else if (PickEnemy(out var enemy)) Director.IssueOrder(SquadOrder.Attack(enemy));
                    else if (PickGround(out var point)) Director.IssueOrder(SquadOrder.Move(point));
                }

                HandleFormationKeys();

                // Wheel down = clockwise, wheel up = counter-clockwise (seen from above). One notch per frame.
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (wheel != 0) Director.RotateFormation(wheel < 0 ? 1 : -1);
            }
            else AttackMovePending = false;

            // Pause has to stay reachable while paused; restart only outside a running match.
            if (Director.Phase == GamePhase.Play && Pressed(Key.P)) Director.TogglePause();
            if (Director.Phase != GamePhase.Play && Pressed(Key.Enter)) Director.Restart();
        }

        void HandleFormationKeys()
        {
            var formations = Director.Squad.Formations;
            for (int i = 0; i < formations.Count; i++)
            {
                var f = formations[i];
                if (f == null) continue;
                if (Pressed(f.HotKey)) Director.ChangeFormation(f.Id);
            }
        }

        bool PickEnemy(out IEnemy enemy)
        {
            enemy = null;
            if (Cam == null) return false;
            var ray = Cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out var hit, 500f, EnemyMask)
                && hit.collider.TryGetComponent<EnemyRef>(out var r) && r.Enemy is { Alive: true })
            {
                enemy = r.Enemy;
                return true;
            }
            return false;
        }

        bool PickGround(out Vector2 point)
        {
            point = default;
            if (Cam == null) return false;
            var ray = Cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Ground.Raycast(ray, out float enter)) return false;
            var w = ray.GetPoint(enter);
            point = new Vector2(w.x, w.z);
            return true;
        }

        static bool Pressed(Key k) => k != Key.None && Keyboard.current[k].wasPressedThisFrame;
    }
}
