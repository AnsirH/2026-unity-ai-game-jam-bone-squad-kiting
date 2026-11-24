using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using BoneSquadKiting.Combat;
using BoneSquadKiting.Fx;
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
        CommandPulse _pulse;

        public Camera Cam;
        public GameDirector Director;
        public LayerMask EnemyMask = ~0;

        /// <summary>True between pressing A and the click that places the attack-move.</summary>
        public bool AttackMovePending { get; private set; }

        void Update()
        {
            if (Director == null || Keyboard.current == null) return;
            if (Director.Phase == GamePhase.Play && (Pressed(Key.Escape) || Pressed(Key.P)))
            {
                AttackMovePending = false;
                Director.TogglePause();
                return;
            }

            if (Director.Phase == GamePhase.Play && !Director.Paused && Mouse.current != null)
            {
                if (Pressed(Key.A)) AttackMovePending = true;
                if (Pressed(Key.S)) { AttackMovePending = false; Director.StopSquad(); }
                if (Pressed(Key.F)) Director.TryRevive();

                // A click that lands on a HUD button must not also be read as a squad command.
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

                if (overUi) { }
                else if (Mouse.current.leftButton.wasPressedThisFrame && AttackMovePending)
                {
                    AttackMovePending = false;
                    IssueClickOrder(true);
                }
                else if (Mouse.current.rightButton.wasPressedThisFrame)
                {
                    bool cancelOnGround = AttackMovePending;
                    AttackMovePending = false;
                    IssueClickOrder(false, cancelOnGround);
                }

                HandleFormationKeys();

                // Wheel down = clockwise, wheel up = counter-clockwise (seen from above). One notch per frame.
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (wheel != 0) Director.RotateFormation(wheel < 0 ? 1 : -1);
            }
            else AttackMovePending = false;

            if (Director.Phase != GamePhase.Play && Pressed(Key.Enter))
            {
                if (Director.Phase == GamePhase.Win) Director.Proceed();   // next stage (or back to the title after the last)
                else Director.Restart();                                   // start, or retry this stage after a wipe
            }
        }

        void IssueClickOrder(bool attackMove, bool cancelOnGround = false)
        {
            if (Cam == null) return;
            var ray = Cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            var enemy = PickEnemy(ray);
            SquadOrder order;
            if (enemy != null) order = SquadOrder.Attack(enemy);
            else
            {
                if (cancelOnGround || !Ground.Raycast(ray, out float distance)) return;
                var world = ray.GetPoint(distance);
                var point = new Vector2(world.x, world.z);
                order = attackMove ? SquadOrder.AttackMove(point) : SquadOrder.Move(point);
            }

            if (!Director.IssueOrder(order)) return;
            if (_pulse == null) _pulse = CommandPulse.Create(transform);
            _pulse.Show(order.Target != null ? order.Target.Pos : order.Point, order.Kind != OrderKind.Move);
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

        IEnemy PickEnemy(Ray ray)
        {
            // The actor views move in LateUpdate, between physics steps.
            Physics.SyncTransforms();
            IEnemy closest = null;
            float nearest = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(ray, 500f, EnemyMask, QueryTriggerInteraction.Collide))
            {
                if (hit.distance >= nearest) continue;
                var pick = hit.collider.GetComponentInParent<EnemyRef>();
                if (pick == null || !Director.Enemies.CanTarget(pick.Enemy)) continue;
                closest = pick.Enemy;
                nearest = hit.distance;
            }
            return closest;
        }

        static bool Pressed(Key k) => k != Key.None && Keyboard.current[k].wasPressedThisFrame;
    }
}
