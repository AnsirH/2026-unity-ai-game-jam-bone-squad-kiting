using UnityEngine;
using BoneSquadKiting.Bosses;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;

namespace BoneSquadKiting.UI
{
    /// <summary>
    /// Test panel: one button per kind of enemy. Pressing a button brings that enemy in at
    /// <see cref="SpawnPoint"/>, which is marked on the ground. Also a switch that makes the squad take no
    /// damage and a slider for how many soldiers it has. It works only in the scene named
    /// <see cref="OnlyInScene"/> (the move tuning scene); anywhere else it switches itself off.
    /// </summary>
    public class MonsterSpawnPanel : MonoBehaviour
    {
        [Tooltip("Where summoned enemies appear. This object is the visible mark on the ground.")]
        public Transform SpawnPoint;
        public MobDefinition[] Mobs;
        public EliteDefinition[] Elites;
        [Min(1)] public int MobCount = 8;        // how many small mobs one press brings
        public float MobSpread = 1.5f;           // radius they are scattered over
        public bool Visible = true;
        [Tooltip("This panel only works in the scene with this name.")]
        public string OnlyInScene = "MoveTuning";

        /// <summary>The squad takes no damage while this is on.</summary>
        public bool Invincible
        {
            get => BoneSquadKiting.Squad.SquadDamage.Invincible;
            set => BoneSquadKiting.Squad.SquadDamage.Invincible = value;
        }

        /// <summary>How many soldiers the squad may have at most (the leader is not counted).</summary>
        public static int MaxSoldiers => BoneSquadKiting.Core.GameConstants.SquadSize - 1;

        void Awake()
        {
            if (gameObject.scene.name == OnlyInScene) return;
            Debug.LogWarning($"MonsterSpawnPanel only works in the '{OnlyInScene}' scene; switched off in '{gameObject.scene.name}'.", this);
            enabled = false;
        }

        void OnDisable() => Invincible = false;   // never leaks into another scene

        /// <summary>Soldiers alive right now, not counting the leader.</summary>
        public int SoldierCount
        {
            get
            {
                var d = Director; int n = 0;
                if (d == null || d.Squad == null) return 0;
                foreach (var u in d.Squad.Units) if (!u.IsLeader && !u.Dead) n++;
                return n;
            }
        }

        /// <summary>
        /// Make the squad have this many soldiers (0..MaxSoldiers) besides the leader: the last ones leave,
        /// or missing ones come back at full health next to the squad. The formation closes up to fit.
        /// </summary>
        public void SetSoldierCount(int count)
        {
            var d = Director;
            if (!enabled || d == null || d.Squad == null) return;
            var units = d.Squad.Units;
            count = Mathf.Clamp(count, 0, MaxSoldiers);
            int have = SoldierCount;
            if (have == count) return;
            d.Revives?.Clear();                                   // a revive in progress would undo or double this
            foreach (var u in units) u.Reviving = false;
            for (int i = units.Count - 1; i >= 0 && have > count; i--)
            {
                var u = units[i];
                if (u.IsLeader || u.Dead) continue;
                u.Dead = true; have--;
            }
            for (int i = 0; i < units.Count && have < count; i++)
            {
                var u = units[i];
                if (u.IsLeader || !u.Dead) continue;
                u.Dead = false; u.Hp = BoneSquadKiting.Core.GameConstants.UnitMax; u.Flash = 1f;
                u.X = d.Squad.Anchor.x; u.Z = d.Squad.Anchor.y;   // it walks to its seat from the center
                have++;
            }
            d.Squad.Reform();
        }

        const float RefHeight = 720f;
        GameDirector _director;
        GUIStyle _box, _label, _button, _toggle;

        /// <summary>The game this panel summons into. Found in the scene (it may arrive after this object).</summary>
        public GameDirector Director
        {
            get
            {
                if (_director == null) _director = FindAnyObjectByType<GameDirector>();
                return _director;
            }
        }

        public Vector2 Point => SpawnPoint != null
            ? new Vector2(SpawnPoint.position.x, SpawnPoint.position.z)
            : new Vector2(transform.position.x, transform.position.z);

        /// <summary>Bring a group of one kind of small mob in at the spawn point. Returns how many appeared.</summary>
        public int SummonMobs(MobDefinition def)
        {
            var d = Director;
            if (!enabled || def == null || d == null || d.Mobs == null) return 0;
            int made = 0;
            for (int i = 0; i < MobCount; i++)
            {
                var pos = Point + (MobCount > 1 ? Random.insideUnitCircle * MobSpread : Vector2.zero);
                if (d.Mobs.Spawn(def, pos, d.Enemies) != null) made++;
            }
            return made;
        }

        /// <summary>Bring one mini boss in at the spawn point.</summary>
        public Elite SummonElite(EliteDefinition def)
        {
            var d = Director;
            if (!enabled || def == null || d == null || d.Elites == null) return null;
            return d.Elites.Spawn(def, Point, d.Enemies);
        }

        /// <summary>Remove every summoned enemy and every shot in the air.</summary>
        public void ClearAll()
        {
            var d = Director;
            if (d == null) return;
            d.Mobs?.Clear(d.Enemies);
            d.Elites?.Clear(d.Enemies);
        }

        void OnGUI()
        {
            if (!Visible || Director == null || Director.Mobs == null) return;
            if (_box == null)
            {
                var tex = new Texture2D(1, 1); tex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f)); tex.Apply();
                _box = new GUIStyle(GUI.skin.box) { normal = { background = tex } };
                _label = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = Color.white } };
                _toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 15, normal = { textColor = Color.white }, onNormal = { textColor = Color.white } };
                _button = new GUIStyle(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 6, 3, 3) };
            }
            float scale = Screen.height / RefHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;

            int rows = (Mobs?.Length ?? 0) + (Elites?.Length ?? 0);
            const float bw = 300f, bh = 26f, pad = 10f;
            float height = 30f + 24f * 2f + rows * (bh + 3f) + 38f + 96f + pad;
            float x = w - bw - pad * 2f - 12f, y = 84f;
            GUI.Box(new Rect(x, y, bw + pad * 2f, height), GUIContent.none, _box);
            x += pad; y += 6f;
            GUI.Label(new Rect(x, y, bw, 24f), "적 소환 (표시된 지점에 등장)", _label); y += 28f;

            GUI.Label(new Rect(x, y, bw, 22f), $"잡몹  (한 번에 {MobCount}마리)", _label); y += 24f;
            if (Mobs != null)
                foreach (var m in Mobs)
                {
                    if (m == null) continue;
                    if (GUI.Button(new Rect(x, y, bw, bh), $"{m.DisplayName}  ·  {m.name}{(m.IsRanged ? "  (원거리)" : "")}", _button)) SummonMobs(m);
                    y += bh + 3f;
                }

            GUI.Label(new Rect(x, y, bw, 22f), "미니 보스", _label); y += 24f;
            if (Elites != null)
                foreach (var e in Elites)
                {
                    if (e == null) continue;
                    if (GUI.Button(new Rect(x, y, bw, bh), $"{e.DisplayName}  ·  {e.name}", _button)) SummonElite(e);
                    y += bh + 3f;
                }

            y += 6f;
            if (GUI.Button(new Rect(x, y, bw, bh + 2f), $"전부 제거   (잡몹 {Director.Mobs.AliveCount} · 미니 보스 {AliveElites()})")) ClearAll();
            y += bh + 12f;

            GUI.Label(new Rect(x, y, bw, 22f), "분대", _label); y += 24f;
            bool inv = GUI.Toggle(new Rect(x, y, bw, 22f), Invincible, "  무적 (피해를 받지 않음)", _toggle);
            if (inv != Invincible) Invincible = inv;
            y += 26f;
            int soldiers = SoldierCount;
            GUI.Label(new Rect(x, y, 96f, 22f), $"병사 {soldiers}명", _label);
            int want = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(x + 100f, y + 6f, bw - 100f, 16f), soldiers, 0f, MaxSoldiers));
            if (want != soldiers) SetSoldierCount(want);
        }

        int AliveElites()
        {
            int n = 0;
            if (Director.Elites != null) foreach (var e in Director.Elites.Elites) if (e.Alive) n++;
            return n;
        }
    }
}
