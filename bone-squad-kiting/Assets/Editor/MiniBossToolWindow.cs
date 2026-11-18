using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BoneSquadKiting.Core;
using BoneSquadKiting.Data;
using BoneSquadKiting.Fx;
using BoneSquadKiting.Game;
using BoneSquadKiting.Stage;

namespace BoneSquadKiting.EditorTools
{
    /// <summary>
    /// Places a stage's mini bosses and sets how each one appears: its kind, where it stands, whether it is
    /// there from the start or comes when the squad is near / at a progress point / after others die, the
    /// route it walks in by, and the intro that plays. Everything is stored on MiniBossSpawn markers in the
    /// scene (under the stage's map, so they switch with it); this window only makes them quick to edit.
    /// </summary>
    public class MiniBossToolWindow : EditorWindow
    {
        const string ContainerName = "Mini Bosses";
        const string DatabasePath = "Assets/Data/Fx/PresentationDatabase.asset";

        int _stage;
        Vector2 _scroll;
        MiniBossSpawn _placingRouteFor;   // next Scene click adds a route point to this marker
        bool _placingMarker;              // next Scene click adds a marker
        EliteDefinition[] _kinds; string[] _kindNames;
        string[] _effectIds;

        [MenuItem("BoneSquadKiting/Mini Boss Tool")]
        public static void Open() => GetWindow<MiniBossToolWindow>("Mini Boss Tool").Show();

        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGui;
            Undo.undoRedoPerformed += Repaint;
            autoRepaintOnSceneChange = true;   // a marker moved in the Scene view must refresh this window's warnings
            LoadLists();
        }
        void OnDisable() { SceneView.duringSceneGui -= OnSceneGui; Undo.undoRedoPerformed -= Repaint; }

        // Where every marker and outline point stood when the window was last drawn. The window is redrawn as
        // soon as any of them moves, however it was moved (handle, gizmo, Inspector).
        int _layoutHash;
        static int LayoutHash(Transform root, List<MiniBossSpawn> markers)
        {
            int h = 17;
            foreach (var m in markers) if (m != null) h = h * 31 + m.transform.position.GetHashCode();
            var outlines = root != null ? root.GetComponentsInChildren<WalkOutline>(true) : FindObjectsByType<WalkOutline>(FindObjectsSortMode.None);
            foreach (var o in outlines) foreach (var p in o.Points()) h = h * 31 + p.GetHashCode();
            return h;
        }
        void OnFocus() => LoadLists();
        void OnHierarchyChange() => Repaint();
        void OnInspectorUpdate() { if (EditorApplication.isPlaying) Repaint(); }

        void LoadLists()
        {
            var kinds = new List<EliteDefinition>();
            foreach (var guid in AssetDatabase.FindAssets("t:EliteDefinition"))
            {
                var def = AssetDatabase.LoadAssetAtPath<EliteDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) kinds.Add(def);
            }
            kinds.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            _kinds = kinds.ToArray();
            _kindNames = System.Array.ConvertAll(_kinds, k => string.IsNullOrEmpty(k.DisplayName) ? k.name : $"{k.DisplayName}  ({k.name})");

            var ids = new List<string> { "(없음)" };
            var db = AssetDatabase.LoadAssetAtPath<PresentationDatabase>(DatabasePath);
            if (db != null) foreach (var e in db.Effects) if (e != null && !string.IsNullOrEmpty(e.Id)) ids.Add(e.Id);
            _effectIds = ids.ToArray();
        }

        // ---------------------------------------------------------------- what is being edited

        static StageRun Run() => FindAnyObjectByType<StageRun>(FindObjectsInactive.Include);

        /// <summary>The object a stage's markers live under: the stage's map with a StageRun, the scene otherwise.</summary>
        public static Transform StageRoot(StageRun run, int stage)
        {
            if (run == null || stage < 0 || stage >= run.Stages.Count || run.Stages[stage].Map == null) return null;
            return run.Stages[stage].Map.transform;
        }

        /// <summary>The markers of one stage (inactive ones too), in hierarchy order.</summary>
        public static List<MiniBossSpawn> Markers(Transform stageRoot)
        {
            var list = new List<MiniBossSpawn>();
            if (stageRoot != null) list.AddRange(stageRoot.GetComponentsInChildren<MiniBossSpawn>(true));
            else list.AddRange(FindObjectsByType<MiniBossSpawn>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            list.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            return list;
        }

        /// <summary>Add a marker at a ground point. It goes under a "Mini Bosses" object inside the stage's map.</summary>
        public static MiniBossSpawn AddMarker(Transform stageRoot, Vector3 position, EliteDefinition kind)
        {
            Transform container = stageRoot != null ? stageRoot.Find(ContainerName) : null;
            if (container == null)
            {
                var existing = stageRoot == null ? GameObject.Find(ContainerName) : null;
                if (existing != null) container = existing.transform;
                else
                {
                    var c = new GameObject(ContainerName);
                    Undo.RegisterCreatedObjectUndo(c, "Add mini boss");
                    if (stageRoot != null) c.transform.SetParent(stageRoot, false);
                    container = c.transform;
                }
            }
            var go = new GameObject("Mini Boss");
            Undo.RegisterCreatedObjectUndo(go, "Add mini boss");
            go.transform.SetParent(container, false);
            go.transform.position = position;
            var marker = go.AddComponent<MiniBossSpawn>();
            marker.Definition = kind;
            Rename(marker);
            EditorSceneManager.MarkSceneDirty(go.scene);
            return marker;
        }

        /// <summary>Add a point to the end of a marker's entry route (the route's first point is where it appears).</summary>
        public static Transform AddRoutePoint(MiniBossSpawn marker, Vector3 position)
        {
            var route = new List<Transform>();
            if (marker.Route != null) foreach (var t in marker.Route) if (t != null) route.Add(t);
            var p = new GameObject("Route " + (route.Count + 1));
            Undo.RegisterCreatedObjectUndo(p, "Add route point");
            p.transform.SetParent(marker.transform, false);
            p.transform.position = position;
            route.Add(p.transform);
            Undo.RecordObject(marker, "Add route point");
            marker.Route = route.ToArray();
            EditorUtility.SetDirty(marker);
            EditorSceneManager.MarkSceneDirty(marker.gameObject.scene);
            return p.transform;
        }

        public static void ClearRoute(MiniBossSpawn marker)
        {
            if (marker.Route != null)
                foreach (var t in marker.Route) if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
            Undo.RecordObject(marker, "Clear route");
            marker.Route = null;
            EditorUtility.SetDirty(marker);
            EditorSceneManager.MarkSceneDirty(marker.gameObject.scene);
        }

        static void Rename(MiniBossSpawn marker)
        {
            string kind = marker.Definition != null ? marker.Definition.name : "?";
            marker.gameObject.name = $"Mini Boss ({kind})";
        }

        /// <summary>
        /// Whether a ground point is inside the stage's drawn walk area (Include outlines minus Exclude ones).
        /// True when the stage has no outlines to judge by.
        /// </summary>
        public static bool InWalkArea(Transform stageRoot, Vector3 position)
        {
            var outlines = stageRoot != null ? stageRoot.GetComponentsInChildren<WalkOutline>(true) : FindObjectsByType<WalkOutline>(FindObjectsSortMode.None);
            var p = new Vector2(position.x, position.z);
            bool anyInclude = false, inside = false;
            foreach (var o in outlines)
            {
                var points = o.Points();
                if (points.Length < 3) continue;
                if (o.Mode == WalkOutlineMode.Exclude) { if (Polygon.Contains(points, p)) return false; }
                else { anyInclude = true; if (Polygon.Contains(points, p)) inside = true; }
            }
            return !anyInclude || inside;
        }

        // ---------------------------------------------------------------- window

        void OnGUI()
        {
            var run = Run();
            Transform root = null;
            if (run != null && run.Stages.Count > 0)
            {
                if (EditorApplication.isPlaying) _stage = run.Current;
                _stage = Mathf.Clamp(_stage, 0, run.Stages.Count - 1);
                var names = new string[run.Stages.Count];
                for (int i = 0; i < names.Length; i++) names[i] = $"{i + 1}. {run.Stages[i].Title}";
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    _stage = GUILayout.Toolbar(_stage, names);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    if (GUILayout.Button("이 맵만 보기", GUILayout.Width(90)))
                    {
                        foreach (var e in run.Stages) if (e.Map != null) Undo.RecordObject(e.Map, "Show one map");
                        run.ShowOnly(_stage);
                        EditorSceneManager.MarkSceneDirty(run.gameObject.scene);
                    }
                EditorGUILayout.EndHorizontal();
                root = StageRoot(run, _stage);

                var entry = run.Stages[_stage];
                EditorGUI.BeginChangeCheck();
                var rule = (StageClearRule)EditorGUILayout.EnumPopup(new GUIContent("클리어 조건", "ReachEnd: 끝 지점 도달 / DefeatMiniBossThenReachEnd: 미니 보스를 모두 잡아야 끝 지점이 열림"), entry.ClearRule);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(run, "Clear rule");
                    entry.ClearRule = rule;
                    EditorUtility.SetDirty(run); EditorSceneManager.MarkSceneDirty(run.gameObject.scene);
                }
            }
            else EditorGUILayout.HelpBox("이 씬에는 Stage Run이 없습니다. 씬 전체의 미니 보스 표식을 편집합니다.", MessageType.Info);

            var markers = Markers(root);
            if (markers.Count > 0 && run != null && run.Stages.Count > 0 && run.Stages[_stage].ClearRule == StageClearRule.ReachEnd)
                EditorGUILayout.HelpBox("클리어 조건이 '끝 지점 도달'이라 미니 보스를 잡지 않아도 클리어됩니다.", MessageType.None);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = _placingMarker ? new Color(1f, 0.8f, 0.3f) : Color.white;
            if (GUILayout.Button(_placingMarker ? "Scene 뷰에서 놓을 곳을 클릭… (취소: Esc)" : "+ 미니 보스 추가 (Scene 뷰 클릭)", GUILayout.Height(26)))
            { _placingMarker = !_placingMarker; _placingRouteFor = null; SceneView.RepaintAll(); }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < markers.Count; i++) DrawMarker(markers[i], i, markers, root);
            if (markers.Count == 0) EditorGUILayout.HelpBox("아직 미니 보스가 없습니다. 위 버튼을 누르고 Scene 뷰에서 바닥을 클릭하세요.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        static readonly string[] AppearNames = { "처음부터 맵에 있음", "분대가 가까이 오면 등장", "진행도에 도달하면 등장" };

        void DrawMarker(MiniBossSpawn m, int index, List<MiniBossSpawn> all, Transform root)
        {
            if (m == null) return;
            var so = new SerializedObject(m);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{index + 1}", EditorStyles.boldLabel, GUILayout.Width(18));
            int kind = System.Array.IndexOf(_kinds, m.Definition);
            EditorGUI.BeginChangeCheck();
            kind = EditorGUILayout.Popup(kind, _kindNames);
            if (EditorGUI.EndChangeCheck() && kind >= 0)
            {
                Undo.RecordObjects(new Object[] { m, m.gameObject }, "Mini boss kind");
                m.Definition = _kinds[kind]; Rename(m);
                EditorUtility.SetDirty(m); so.Update();
            }
            if (GUILayout.Button("선택", GUILayout.Width(44))) { Selection.activeGameObject = m.gameObject; SceneView.FrameLastActiveSceneView(); }
            if (GUILayout.Button("삭제", GUILayout.Width(44)))
            {
                Undo.DestroyObjectImmediate(m.gameObject);
                EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();

            if (!InWalkArea(root, m.transform.position))
            {
                if (m.Definition != null && m.Definition.Fixed)
                    EditorGUILayout.HelpBox("이동 가능 영역 밖에 있습니다. 움직이지 않는 미니 보스라 이 자리에 그대로 섭니다. 분대가 다가갈 수 없으니 사거리 안에서 쏠 수 있는지 확인하세요.", MessageType.Info);
                else
                    EditorGUILayout.HelpBox("이동 가능 영역 밖에 있습니다. 등장할 때 가장 가까운 이동 가능 지점으로 옮겨집니다.", MessageType.Warning);
            }

            // How it appears.
            int appear = m.PresentFromStart ? 0 : m.AtProgress >= 0f ? 2 : 1;
            EditorGUI.BeginChangeCheck();
            appear = EditorGUILayout.Popup("등장 방식", appear, AppearNames);
            if (EditorGUI.EndChangeCheck())
            {
                so.FindProperty("PresentFromStart").boolValue = appear == 0;
                so.FindProperty("AtProgress").floatValue = appear == 2 ? Mathf.Max(0f, m.AtProgress < 0f ? 0.5f : m.AtProgress) : -1f;
            }
            if (appear == 1) EditorGUILayout.PropertyField(so.FindProperty("TriggerRadius"), new GUIContent("  감지 반경", "분대 중심이 이 거리 안에 들어오면 등장"));
            if (appear == 2) EditorGUILayout.Slider(so.FindProperty("AtProgress"), 0f, 1f, new GUIContent("  진행도", "0 = 시작 지점, 1 = 끝 지점. 웨이브와 끝 지점이 있는 스테이지에서만 동작"));

            // Which others must die first.
            if (all.Count > 1)
            {
                var others = new List<MiniBossSpawn>(); var labels = new List<string>();
                for (int k = 0; k < all.Count; k++) if (all[k] != m) { others.Add(all[k]); labels.Add($"{k + 1}. {(all[k].Definition != null ? all[k].Definition.name : "?")}"); }
                int mask = 0;
                if (m.ActivateAfter != null) for (int k = 0; k < others.Count; k++) if (System.Array.IndexOf(m.ActivateAfter, others[k]) >= 0) mask |= 1 << k;
                EditorGUI.BeginChangeCheck();
                mask = EditorGUILayout.MaskField(new GUIContent("먼저 처치할 대상", "여기서 고른 미니 보스가 모두 죽어야 이 미니 보스가 등장 조건을 따집니다"), mask, labels.ToArray());
                if (EditorGUI.EndChangeCheck())
                {
                    var picked = new List<MiniBossSpawn>();
                    for (int k = 0; k < others.Count; k++) if ((mask & (1 << k)) != 0) picked.Add(others[k]);
                    var prop = so.FindProperty("ActivateAfter");
                    prop.arraySize = picked.Count;
                    for (int k = 0; k < picked.Count; k++) prop.GetArrayElementAtIndex(k).objectReferenceValue = picked[k];
                }
            }

            if (appear != 0)
            {
                // Entry route.
                int points = 0; if (m.Route != null) foreach (var t in m.Route) if (t != null) points++;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("등장 루트", "첫 점에서 나타나 순서대로 걸어 들어와 표식 위치에 섭니다. 점이 없으면 표식 위치에 바로 나타납니다."));
                EditorGUILayout.LabelField(points == 0 ? "없음 (제자리 등장)" : $"점 {points}개", GUILayout.Width(110));
                GUI.backgroundColor = _placingRouteFor == m ? new Color(1f, 0.8f, 0.3f) : Color.white;
                if (GUILayout.Button(_placingRouteFor == m ? "클릭 중… (끝: Esc)" : "점 추가")) { _placingRouteFor = _placingRouteFor == m ? null : m; _placingMarker = false; SceneView.RepaintAll(); }
                GUI.backgroundColor = Color.white;
                using (new EditorGUI.DisabledScope(points == 0))
                    if (GUILayout.Button("지우기", GUILayout.Width(52))) { ClearRoute(m); so.Update(); }
                EditorGUILayout.EndHorizontal();
                if (points > 0) EditorGUILayout.PropertyField(so.FindProperty("EntrySpeed"), new GUIContent("  걸어 들어오는 속도"));

                // Intro.
                EditorGUILayout.PropertyField(so.FindProperty("PlayIntro"), new GUIContent("등장 연출", "게임을 잠깐 멈추고 카메라가 미니 보스를 비춥니다"));
                if (m.PlayIntro)
                {
                    if (points > 0)
                    {
                        var when = so.FindProperty("IntroWhen");
                        when.enumValueIndex = EditorGUILayout.Popup("  연출 시점", when.enumValueIndex, new[] { "나타날 때", "자리에 도착했을 때" });
                    }
                    var id = so.FindProperty("IntroEffectId");
                    int current = Mathf.Max(0, System.Array.IndexOf(_effectIds, id.stringValue));
                    if (!string.IsNullOrEmpty(id.stringValue) && System.Array.IndexOf(_effectIds, id.stringValue) < 0)
                        EditorGUILayout.HelpBox($"연출 효과 '{id.stringValue}'가 PresentationDatabase에 없습니다.", MessageType.Warning);
                    EditorGUI.BeginChangeCheck();
                    current = EditorGUILayout.Popup(new GUIContent("  화면 효과", "PresentationDatabase의 Effects에 등록된 효과(흔들림·화면색)"), current, _effectIds);
                    if (EditorGUI.EndChangeCheck()) id.stringValue = current == 0 ? "" : _effectIds[current];
                    EditorGUILayout.PropertyField(so.FindProperty("IntroSeconds"), new GUIContent("  멈추는 시간(초)", "0이면 미니 보스 종류에 정해진 시간"));
                }
            }

            if (so.ApplyModifiedProperties()) EditorSceneManager.MarkSceneDirty(m.gameObject.scene);

            if (EditorApplication.isPlaying)
            {
                var director = FindAnyObjectByType<GameDirector>();
                string state = m.Defeated ? "처치됨" : m.Triggered ? (m.Elite != null && m.Elite.Entering ? "걸어 들어오는 중" : "등장함") : m.Unlocked ? "대기 중" : "잠김 (선행 대상 생존)";
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("상태: " + state);
                using (new EditorGUI.DisabledScope(m.Triggered || director == null || director.Phase != GamePhase.Play))
                    if (GUILayout.Button("지금 등장", GUILayout.Width(80))) director.SpawnMiniBoss(m);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        // ---------------------------------------------------------------- scene view

        void OnSceneGui(SceneView view)
        {
            var run = Run();
            var root = run != null ? StageRoot(run, Mathf.Clamp(_stage, 0, Mathf.Max(0, run.Stages.Count - 1))) : null;
            var markers = Markers(root);
            int hash = LayoutHash(root, markers);
            if (hash != _layoutHash) { _layoutHash = hash; Repaint(); }

            for (int i = 0; i < markers.Count; i++)
            {
                var m = markers[i];
                if (m == null || !m.gameObject.activeInHierarchy) continue;
                var pos = m.transform.position;
                Handles.color = m.PresentFromStart ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.6f, 0.15f);
                Handles.Label(pos + Vector3.up * 2.2f, $"{i + 1}. {(m.Definition != null ? (string.IsNullOrEmpty(m.Definition.DisplayName) ? m.Definition.name : m.Definition.DisplayName) : "?")}", EditorStyles.whiteBoldLabel);

                EditorGUI.BeginChangeCheck();
                var moved = Handles.Slider2D(pos, Vector3.up, Vector3.right, Vector3.forward, HandleUtility.GetHandleSize(pos) * 0.12f, Handles.CircleHandleCap, 0f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m.transform, "Move mini boss");
                    // Route points are children: keep them where they are while the marker moves.
                    var keep = new List<(Transform t, Vector3 p)>();
                    if (m.Route != null) foreach (var t in m.Route) if (t != null) { Undo.RecordObject(t, "Move mini boss"); keep.Add((t, t.position)); }
                    m.transform.position = new Vector3(moved.x, pos.y, moved.z);
                    foreach (var (t, p) in keep) t.position = p;
                }

                if (m.Route == null) continue;
                Vector3? last = null;
                for (int k = 0; k < m.Route.Length; k++)
                {
                    var t = m.Route[k];
                    if (t == null) continue;
                    Handles.Label(t.position + Vector3.up * 0.8f, k == 0 ? $"{i + 1} 등장" : $"{i + 1}-{k + 1}");
                    EditorGUI.BeginChangeCheck();
                    var rp = Handles.Slider2D(t.position, Vector3.up, Vector3.right, Vector3.forward, HandleUtility.GetHandleSize(t.position) * 0.09f, Handles.RectangleHandleCap, 0f);
                    if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(t, "Move route point"); t.position = new Vector3(rp.x, t.position.y, rp.z); }
                    if (last.HasValue) Handles.DrawDottedLine(last.Value, t.position, 4f);
                    last = t.position;
                }
                if (last.HasValue) Handles.DrawDottedLine(last.Value, pos, 4f);
            }

            if (!_placingMarker && _placingRouteFor == null) return;
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { _placingMarker = false; _placingRouteFor = null; e.Use(); Repaint(); return; }
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));   // clicks come to us, not to selection
            if (!GroundPoint(e.mousePosition, out var point)) return;
            Handles.color = Color.yellow;
            Handles.DrawWireDisc(point, Vector3.up, 0.8f);
            view.Repaint();
            if (e.type != EventType.MouseDown || e.button != 0 || e.alt) return;
            if (_placingMarker)
            {
                var marker = AddMarker(root, point, _kinds.Length > 0 ? _kinds[0] : null);
                Selection.activeGameObject = marker.gameObject;
                _placingMarker = false;
            }
            else if (_placingRouteFor != null) AddRoutePoint(_placingRouteFor, point);
            e.Use();
            Repaint();
        }

        // Where a Scene-view mouse position lands on the ground: a collider if there is one, else height 0.
        static bool GroundPoint(Vector2 mouse, out Vector3 point)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            if (Physics.Raycast(ray, out var hit, 2000f, ~0, QueryTriggerInteraction.Ignore)) { point = hit.point; return true; }
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float enter)) { point = ray.GetPoint(enter); return true; }
            point = default; return false;
        }
    }
}
