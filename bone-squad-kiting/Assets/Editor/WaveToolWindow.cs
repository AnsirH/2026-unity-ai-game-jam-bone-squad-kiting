using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BoneSquadKiting.Data;
using BoneSquadKiting.Game;
using BoneSquadKiting.Stage;

namespace BoneSquadKiting.EditorTools
{
    /// <summary>
    /// One window for a stage's wave versions: pick which version the open scene plays, duplicate one into
    /// the next version, edit it, read its numbers, compare two versions, and poke the waves while playing.
    /// Versions are WavePlan assets under Assets/Data/Waves/&lt;stage&gt;/.
    /// </summary>
    public class WaveToolWindow : EditorWindow
    {
        public const string Root = "Assets/Data/Waves";

        string _stage;
        WavePlan _selected, _compare;
        Editor _editor;
        Vector2 _scrollList, _scrollBody;
        readonly List<WaveEntry> _scratch = new();

        [MenuItem("BoneSquadKiting/Wave Tool")]
        public static void Open() => GetWindow<WaveToolWindow>("Wave Tool").minSize = new Vector2(760, 420);

        void OnEnable() { EditorApplication.playModeStateChanged += OnPlayMode; }
        void OnDisable() { EditorApplication.playModeStateChanged -= OnPlayMode; DestroyImmediate(_editor); }
        void OnPlayMode(PlayModeStateChange _) => Repaint();
        void OnInspectorUpdate() { if (EditorApplication.isPlaying) Repaint(); }

        // ---------- data ----------

        /// <summary>Stage folders under the wave root.</summary>
        public static List<string> Stages()
        {
            if (!AssetDatabase.IsValidFolder(Root)) return new List<string>();
            return AssetDatabase.GetSubFolders(Root).Select(Path.GetFileName).OrderBy(n => n).ToList();
        }

        /// <summary>Every version of a stage, by name.</summary>
        public static List<WavePlan> Versions(string stage)
        {
            string folder = $"{Root}/{stage}";
            if (!AssetDatabase.IsValidFolder(folder)) return new List<WavePlan>();
            return AssetDatabase.FindAssets("t:WavePlan", new[] { folder })
                .Select(g => AssetDatabase.LoadAssetAtPath<WavePlan>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null).OrderBy(p => p.name, System.StringComparer.Ordinal).ToList();
        }

        /// <summary>Copy a version into the next free number of its folder (Stage1_v02 → Stage1_v03).</summary>
        public static WavePlan DuplicateAsNextVersion(WavePlan source)
        {
            string path = AssetDatabase.GetAssetPath(source);
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            string stem = Regex.Replace(source.name, @"_v\d+$", "");
            int next = 1;
            foreach (var g in AssetDatabase.FindAssets("t:WavePlan", new[] { folder }))
            {
                var m = Regex.Match(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)), @"_v(\d+)$");
                if (m.Success) next = Mathf.Max(next, int.Parse(m.Groups[1].Value) + 1);
            }
            string target = $"{folder}/{stem}_v{next:00}.asset";
            if (!AssetDatabase.CopyAsset(path, target)) return null;
            var copy = AssetDatabase.LoadAssetAtPath<WavePlan>(target);
            copy.Note = $"v{next:00}: ({source.name}에서 복제) ";
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssets();
            return copy;
        }

        // What the open scene plays: a generated stage's WaveSpawner, or a map scene's Stage Start.
        static WaveSpawner SceneSpawner() => FindAnyObjectByType<WaveSpawner>();
        static StageStart SceneStart() => FindAnyObjectByType<StageStart>();

        // A game scene holds several stages: its StageRun list says which version each one plays.
        static StageRun SceneRun() => FindAnyObjectByType<StageRun>();

        /// <summary>Which entry of a StageRun list a wave folder belongs to: "Stage2" is entry 1. −1 if the
        /// name has no number or the list is shorter.</summary>
        public static int RunIndexOf(StageRun run, string stage)
        {
            var m = Regex.Match(stage ?? "", @"(\d+)$");
            if (run == null || !m.Success) return -1;
            int i = int.Parse(m.Groups[1].Value) - 1;
            return i >= 0 && i < run.Stages.Count ? i : -1;
        }

        static string StageOf(WavePlan plan) =>
            plan == null ? null : Path.GetFileName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(plan)));

        /// <summary>The version this stage uses in the open scene. In a game scene with a stage list that is
        /// the list's entry for the stage; in a single-stage scene it is whatever the scene plays.</summary>
        public static WavePlan InUse(string stage)
        {
            var run = SceneRun();
            if (run == null) return InUse();
            int i = RunIndexOf(run, stage);
            return i >= 0 ? run.Stages[i].WavePlan : null;
        }

        /// <summary>The version the open scene (or the running game) uses now.</summary>
        public static WavePlan InUse()
        {
            var run = SceneRun();
            if (run != null && run.Stages.Count > 0)
            {
                int i = EditorApplication.isPlaying ? run.Current : Mathf.Clamp(run.StartStage, 0, run.Stages.Count - 1);
                return run.Stages[i].WavePlan;
            }
            var spawner = SceneSpawner();
            if (spawner != null && (spawner.Plan != null || EditorApplication.isPlaying)) return spawner.Plan;
            var start = SceneStart();
            return start != null ? start.WavePlan : null;
        }

        /// <summary>Make the open scene use this version. While playing it also switches the running game
        /// (from the next wave); that switch lasts only for this play session.</summary>
        public static bool Use(WavePlan plan)
        {
            var run = SceneRun();
            if (run != null)
            {
                // The stage is told by the version's folder (Stage2 → the list's second entry).
                int i = RunIndexOf(run, StageOf(plan));
                if (i < 0) return false;
                if (!EditorApplication.isPlaying) Undo.RecordObject(run, "Use wave plan");
                run.SetWavePlan(i, plan);      // while playing that stage, its waves follow from the next one
                if (!EditorApplication.isPlaying) { EditorUtility.SetDirty(run); EditorSceneManager.MarkSceneDirty(run.gameObject.scene); }
                return true;
            }
            bool done = false;
            var spawner = SceneSpawner();
            if (spawner != null)
            {
                if (!EditorApplication.isPlaying) Undo.RecordObject(spawner, "Use wave plan");
                spawner.Plan = plan; done = true;
                if (!EditorApplication.isPlaying) { EditorUtility.SetDirty(spawner); EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene); }
            }
            var start = SceneStart();
            if (start != null && !EditorApplication.isPlaying)
            {
                Undo.RecordObject(start, "Use wave plan");
                start.WavePlan = plan; done = true;
                EditorUtility.SetDirty(start); EditorSceneManager.MarkSceneDirty(start.gameObject.scene);
            }
            return done;
        }

        // ---------- window ----------

        void OnGUI()
        {
            var stages = Stages();
            if (stages.Count == 0)
            {
                EditorGUILayout.HelpBox($"{Root} 아래에 스테이지 폴더가 없습니다. 폴더를 만들고 Create > BoneSquadKiting > Wave Plan으로 첫 버전을 만드세요.", MessageType.Info);
                return;
            }
            if (_stage == null || !stages.Contains(_stage))
            {
                var used = InUse();
                string usedStage = used != null ? Path.GetFileName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(used))) : null;
                _stage = usedStage != null && stages.Contains(usedStage) ? usedStage : stages[0];
                _selected = null;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("스테이지", GUILayout.Width(56));
                int index = EditorGUILayout.Popup(stages.IndexOf(_stage), stages.ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(140));
                if (stages[index] != _stage) { _stage = stages[index]; _selected = null; _compare = null; }
                GUILayout.FlexibleSpace();
                var inUse = InUse(_stage);
                var run = SceneRun();
                if (run != null && RunIndexOf(run, _stage) < 0) GUILayout.Label($"이 씬의 스테이지 목록에 {_stage}이(가) 없음");
                else if (run != null) GUILayout.Label(inUse != null ? $"스테이지 목록 {RunIndexOf(run, _stage) + 1}번이 쓰는 버전: {inUse.name}" : $"스테이지 목록 {RunIndexOf(run, _stage) + 1}번에 웨이브 설정 없음");
                else GUILayout.Label(inUse != null ? $"열린 씬이 쓰는 버전: {inUse.name}" : "열린 씬에 웨이브 설정이 연결되어 있지 않음");
            }

            var versions = Versions(_stage);
            if (_selected == null || !versions.Contains(_selected)) _selected = versions.Contains(InUse(_stage)) ? InUse(_stage) : versions.FirstOrDefault();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawVersionList(versions);
                using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollBody))
                {
                    _scrollBody = scroll.scrollPosition;
                    if (_selected == null) { EditorGUILayout.HelpBox("이 스테이지에 버전이 없습니다.", MessageType.Info); }
                    else
                    {
                        DrawPlayControls();
                        DrawSummary(versions);
                        EditorGUILayout.Space(8);
                        EditorGUILayout.LabelField($"편집: {_selected.name}", EditorStyles.boldLabel);
                        Editor.CreateCachedEditor(_selected, null, ref _editor);
                        _editor.OnInspectorGUI();
                    }
                }
            }
        }

        void DrawVersionList(List<WavePlan> versions)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(230)))
            {
                using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollList, GUILayout.ExpandHeight(true)))
                {
                    _scrollList = scroll.scrollPosition;
                    var inUse = InUse(_stage);
                    foreach (var v in versions)
                    {
                        using (new EditorGUILayout.VerticalScope(v == _selected ? EditorStyles.helpBox : GUIStyle.none))
                        {
                            string mark = v == inUse ? "▶ " : "    ";
                            if (GUILayout.Button(mark + v.name, EditorStyles.label)) { _selected = v; GUI.FocusControl(null); }
                            if (!string.IsNullOrEmpty(v.Note)) EditorGUILayout.LabelField(v.Note, EditorStyles.wordWrappedMiniLabel);
                        }
                    }
                }
                using (new EditorGUI.DisabledScope(_selected == null))
                {
                    if (GUILayout.Button("이 버전으로 플레이", GUILayout.Height(26)))
                    {
                        if (!Use(_selected)) ShowNotification(new GUIContent(SceneRun() != null ? "이 씬의 스테이지 목록에 이 스테이지가 없습니다" : "열린 씬에 Wave Spawner나 Stage Start가 없습니다"));
                        else if (EditorApplication.isPlaying) ShowNotification(new GUIContent("다음 웨이브부터 적용 (이번 플레이에만)"));
                    }
                    if (GUILayout.Button("복제해서 새 버전"))
                    {
                        var copy = DuplicateAsNextVersion(_selected);
                        if (copy != null) { _selected = copy; EditorGUIUtility.PingObject(copy); }
                    }
                    if (GUILayout.Button("Project 창에서 보기")) EditorGUIUtility.PingObject(_selected);
                }
            }
        }

        void DrawPlayControls()
        {
            if (!EditorApplication.isPlaying) return;
            var director = FindAnyObjectByType<GameDirector>();
            var waves = director != null ? director.Waves : null;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("플레이 중", EditorStyles.boldLabel);
                if (waves == null) { EditorGUILayout.LabelField("이 씬에는 웨이브가 없습니다."); return; }
                int alive = director.Mobs != null ? director.Mobs.AliveCount : 0;
                EditorGUILayout.LabelField(
                    $"{(waves.Plan != null ? waves.Plan.name : "(곡선 설정)")}   진행 {waves.MaxProgress * 100f:0}%   구간 {waves.SectionIndex + 1}   " +
                    $"웨이브 {waves.WavesSpawned}   다음 {Mathf.Max(0f, waves.TimeToNextWave):0.0}s   살아 있음 {alive}   누적 등장 {waves.MobsSpawned}");
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("지금 웨이브 한 번")) waves.SpawnNow();
                    waves.Hold = GUILayout.Toggle(waves.Hold, "웨이브 멈춤", "Button");
                    if (GUILayout.Button("잡몹 전부 제거") && director.Mobs != null) director.Mobs.Clear(director.Enemies);
                    GUILayout.Space(12);
                    GUILayout.Label("배속", GUILayout.Width(30));
                    foreach (float scale in new[] { 0.5f, 1f, 2f, 3f })
                        if (GUILayout.Toggle(Mathf.Approximately(Time.timeScale, scale), $"{scale:0.#}×", "Button", GUILayout.Width(40))) Time.timeScale = scale;
                }
            }
        }

        // The progress points worth a row: where any row of either version starts.
        List<float> SamplePoints(WavePlan a, WavePlan b)
        {
            var points = new SortedSet<float> { 0f };
            foreach (var plan in new[] { a, b })
                if (plan != null) foreach (var s in plan.Sections) if (s != null) points.Add(Mathf.Round(s.FromProgress * 1000f) / 1000f);
            return points.ToList();
        }

        float HpPerMinute(WavePlan plan, float progress)
        {
            float interval = plan.IntervalAt(progress);
            if (interval <= 0f) return 0f;
            plan.CompositionAt(progress, _scratch);
            float hp = 0f;
            foreach (var e in _scratch) hp += e.Count * e.Mob.Hp;
            return hp / interval * 60f;
        }

        string Composition(WavePlan plan, float progress)
        {
            plan.CompositionAt(progress, _scratch);
            return _scratch.Count == 0 ? "-" : string.Join(", ", _scratch.Select(e => $"{e.Mob.DisplayName} {e.Count}"));
        }

        void DrawSummary(List<WavePlan> versions)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"요약: {_selected.name}", EditorStyles.boldLabel, GUILayout.Width(160));
                GUILayout.FlexibleSpace();
                var others = versions.Where(v => v != _selected).ToList();
                var names = new List<string> { "(비교 안 함)" }; names.AddRange(others.Select(v => v.name));
                int pick = EditorGUILayout.Popup("비교할 버전", _compare != null ? others.IndexOf(_compare) + 1 : 0, names.ToArray(), GUILayout.Width(280));
                _compare = pick > 0 ? others[pick - 1] : null;
            }

            var changed = new GUIStyle(EditorStyles.label); changed.normal.textColor = new Color(1f, 0.72f, 0.2f);
            float[] widths = _compare == null ? new[] { 50f, 60f, 200f, 70f, 90f } : new[] { 50f, 90f, 260f, 110f, 130f };
            Row(widths, null, "진행도", "주기(초)", "한 웨이브 구성", "분당 마리", "분당 체력 합");
            foreach (float p in SamplePoints(_selected, _compare))
            {
                float q = p + 0.0005f;   // just inside the row that starts here
                if (_compare == null)
                {
                    Row(widths, null, $"{p:0.##}", Num(_selected.IntervalAt(q)), Composition(_selected, q),
                        Num(_selected.MobsPerMinuteAt(q)), Num(HpPerMinute(_selected, q)));
                }
                else
                {
                    bool same = Composition(_selected, q) == Composition(_compare, q) && Mathf.Approximately(_selected.IntervalAt(q), _compare.IntervalAt(q));
                    Row(widths, same ? null : changed, $"{p:0.##}",
                        Pair(_selected.IntervalAt(q), _compare.IntervalAt(q)),
                        same ? Composition(_selected, q) : $"{Composition(_selected, q)}   ←   {Composition(_compare, q)}",
                        Pair(_selected.MobsPerMinuteAt(q), _compare.MobsPerMinuteAt(q)),
                        Pair(HpPerMinute(_selected, q), HpPerMinute(_compare, q)));
                }
            }
            if (_compare != null)
                EditorGUILayout.LabelField($"각 칸은 '{_selected.name} ← {_compare.name}'. 달라진 줄은 주황색.", EditorStyles.miniLabel);
            if (_selected.Bursts.Count > 0)
                EditorGUILayout.LabelField("돌발: " + string.Join("   ", _selected.Bursts.Where(b => b != null).OrderBy(b => b.AtProgress)
                    .Select(b => $"진행도 {b.AtProgress:0.##}에서 {string.Join(", ", b.Mobs.Where(e => e != null && e.Mob != null).Select(e => $"{e.Mob.DisplayName} {e.Count}"))}")),
                    EditorStyles.wordWrappedLabel);
        }

        static string Num(float v) => v <= 0f ? "-" : v.ToString(v >= 100f ? "0" : "0.#");
        static string Pair(float a, float b) => Mathf.Approximately(a, b) ? Num(a) : $"{Num(a)} ← {Num(b)}";

        static void Row(float[] widths, GUIStyle style, params string[] cells)
        {
            using (new EditorGUILayout.HorizontalScope())
                for (int i = 0; i < cells.Length; i++)
                    GUILayout.Label(cells[i], style ?? (i == 0 ? EditorStyles.miniBoldLabel : EditorStyles.label), GUILayout.Width(widths[i]));
        }
    }
}
