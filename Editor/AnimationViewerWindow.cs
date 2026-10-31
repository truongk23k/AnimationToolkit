using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnimationToolkit.EditorTools
{
    /// <summary>
    /// Cửa sổ xem kỹ animation: kéo một GameObject vào (hoặc bật Follow selection) → thấy mọi animator của nó,
    /// theo từng trạng thái (Idle/Fly...) nếu có AnimStateSet. Timeline cho thấy chu kỳ lặp từng animator,
    /// playhead kéo để tua, Play/Pause/Stop để xem thử, đồ thị đường cong chuyển động của animator đang chọn.
    /// Tạo animation thì gọi skill /unity-animation-toolkit; cửa sổ này chỉ để xem và tinh chỉnh.
    /// </summary>
    public class AnimationViewerWindow : EditorWindow
    {
        [MenuItem("Tools/Animation Toolkit/Animation Viewer")]
        static void Open()
        {
            AnimationViewerWindow w = GetWindow<AnimationViewerWindow>("Animation Viewer");
            w.minSize = new Vector2(560, 460);
        }

        enum PlayState { Stopped, Playing, Paused }

        // ── trạng thái chung ──
        GameObject _target;
        bool _follow = true;
        int _stateIndex = -1;                   // -1 = tất cả animator
        ToolkitAnimator _selected;
        Vector2 _scroll;
        float _speed = 1f;
        float _clock;
        PlayState _play = PlayState.Stopped;
        double _lastTime;

        // ── dữ liệu thu thập ──
        ToolkitAnimator[] _all = new ToolkitAnimator[0];
        AnimStateSet _set;
        readonly List<ToolkitAnimator> _group = new List<ToolkitAnimator>();

        // ── snapshot để khôi phục sau preview ──
        readonly Dictionary<ToolkitAnimator, bool> _savedEnabled = new Dictionary<ToolkitAnimator, bool>();
        readonly Dictionary<ToolkitAnimator, float> _savedWeight = new Dictionary<ToolkitAnimator, float>();
        readonly Dictionary<Transform, Vector3[]> _savedPose = new Dictionary<Transform, Vector3[]>();
        readonly Dictionary<Transform, Quaternion> _savedRot = new Dictionary<Transform, Quaternion>();

        // ── đồ thị ──
        sealed class Plot { public string label; public float[] v; public Color color; public float min, max; }
        readonly List<Plot> _plots = new List<Plot>();
        ToolkitAnimator _plotFor;
        float _plotSpan;
        bool _plotDirty = true;
        bool _showInspector = true;
        UnityEditor.Editor _editor;

        // ═════════ lifecycle ═════════
        void OnEnable()
        {
            Selection.selectionChanged += OnSelection;
            EditorApplication.playModeStateChanged += OnPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            Undo.undoRedoPerformed += OnUndo;
            AnimPreviewRunner.Starting += StopPreview; // Inspector preview và Viewer preview không chạy chồng nhau
            if (_target == null) OnSelection();
        }

        void OnDisable()
        {
            Selection.selectionChanged -= OnSelection;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            Undo.undoRedoPerformed -= OnUndo;
            AnimPreviewRunner.Starting -= StopPreview;
            StopPreview();
            DropEditor();
        }

        void OnPlayMode(PlayModeStateChange s) { if (s == PlayModeStateChange.ExitingEditMode) StopPreview(); }
        void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path) { StopPreview(); }
        void OnUndo() { _plotDirty = true; Repaint(); }

        void OnSelection()
        {
            if (!_follow) return;
            GameObject go = Selection.activeGameObject;
            if (go == null || EditorUtility.IsPersistent(go) || go == _target) return;
            if (go.GetComponentsInChildren<ToolkitAnimator>(true).Length == 0 && go.GetComponentInParent<AnimStateSet>() == null) return;
            SetTarget(go);
            Repaint();
        }

        void SetTarget(GameObject go)
        {
            StopPreview();
            _target = go;
            _stateIndex = -1;
            _selected = null;
            _clock = 0f;
            _plotDirty = true;
            DropEditor();
            Collect();
            // Có AnimStateSet thì mở sẵn state đầu tiên
            if (_set != null && _set.Count > 0) _stateIndex = 0;
            RebuildGroup();
        }

        void Collect()
        {
            _all = _target != null ? _target.GetComponentsInChildren<ToolkitAnimator>(true) : new ToolkitAnimator[0];
            _set = _target != null ? _target.GetComponentInChildren<AnimStateSet>(true) : null;
        }

        void RebuildGroup()
        {
            _group.Clear();
            if (_set != null && _stateIndex >= 0 && _stateIndex < _set.Count)
            {
                ToolkitAnimator[] st = _set.StateAnimators(_stateIndex);
                for (int i = 0; i < st.Length; i++) if (st[i] != null) _group.Add(st[i]);
            }
            else _group.AddRange(_all);
            if (_selected == null || !_group.Contains(_selected)) _selected = _group.Count > 0 ? _group[0] : null;
            _plotDirty = true;
            DropEditor();
        }

        void DropEditor() { if (_editor != null) { DestroyImmediate(_editor); _editor = null; } }

        // ═════════ OnGUI ═════════
        void OnGUI()
        {
            DrawToolbar();
            if (_target == null) { DrawDropZone(); return; }
            if (_all.Length == 0 || _all[0] == null) Collect();

            DrawHeader();
            DrawStateTabs();
            DrawTransport();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawTimeline();
            DrawDetails();
            EditorGUILayout.EndScrollView();

            if (_play == PlayState.Playing) Repaint();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.BeginChangeCheck();
            GameObject go = (GameObject)EditorGUILayout.ObjectField(_target, typeof(GameObject), true, GUILayout.Width(240));
            if (EditorGUI.EndChangeCheck() && go != _target) SetTarget(go);
            _follow = GUILayout.Toggle(_follow, "Follow selection", EditorStyles.toolbarButton, GUILayout.Width(110));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cài skill cho Claude Code", EditorStyles.toolbarButton, GUILayout.Width(170))) SkillInstaller.Install();
            EditorGUILayout.EndHorizontal();
        }

        void DrawDropZone()
        {
            GUILayout.Space(30);
            Rect r = GUILayoutUtility.GetRect(0, 140, GUILayout.ExpandWidth(true));
            r.x += 30; r.width -= 60;
            GUI.Box(r, GUIContent.none, EditorStyles.helpBox);
            GUI.Label(r, "Kéo một GameObject từ Hierarchy vào đây\n(hoặc chọn nó khi 'Follow selection' đang bật)\n\nTạo animation mới: gọi skill  /unity-animation-toolkit",
                new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 12, wordWrap = true });

            Event e = Event.current;
            if (r.Contains(e.mousePosition) && (e.type == EventType.DragUpdated || e.type == EventType.DragPerform))
            {
                bool ok = DragAndDrop.objectReferences.Length > 0 && DragAndDrop.objectReferences[0] is GameObject;
                DragAndDrop.visualMode = ok ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;
                if (e.type == EventType.DragPerform && ok) { DragAndDrop.AcceptDrag(); SetTarget((GameObject)DragAndDrop.objectReferences[0]); }
                e.Use();
            }
        }

        void DrawHeader()
        {
            int loops = 0, once = 0, cont = 0;
            for (int i = 0; i < _group.Count; i++)
            {
                if (_group[i] == null) continue;
                if (!_group[i].IsLooping) once++; else if (_group[i].CycleDuration > 0f) loops++; else cont++;
            }
            string stateName = (_set != null && _stateIndex >= 0) ? _set.StateName(_stateIndex) : "Tất cả";
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(_target.name + "   ·   " + stateName, EditorStyles.boldLabel);
            GUILayout.Label(_group.Count + " animator   ·   loop " + loops + "   ·   once " + once + "   ·   continuous " + cont, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        void DrawStateTabs()
        {
            if (_set == null || _set.Count == 0) return;
            string[] names = new string[_set.Count + 1];
            for (int i = 0; i < _set.Count; i++) names[i] = _set.StateName(i);
            names[_set.Count] = "(Tất cả)";
            int cur = _stateIndex < 0 ? _set.Count : _stateIndex;
            // Ô "Animation" sổ xuống chọn, giống Spine
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Animation", EditorStyles.boldLabel, GUILayout.Width(70));
            int sel = EditorGUILayout.Popup(cur, names, GUILayout.Height(22));
            if (GUILayout.Button("◀", EditorStyles.miniButtonLeft, GUILayout.Width(26))) sel = (cur - 1 + names.Length) % names.Length;
            if (GUILayout.Button("▶", EditorStyles.miniButtonRight, GUILayout.Width(26))) sel = (cur + 1) % names.Length;
            EditorGUILayout.EndHorizontal();
            if (sel != cur)
            {
                bool resume = _play == PlayState.Playing;
                StopPreview();
                _stateIndex = sel == _set.Count ? -1 : sel;
                _clock = 0f;
                RebuildGroup();
                if (resume) StartPreview(true);
            }
        }

        void DrawTransport()
        {
            float span = Span();
            EditorGUILayout.BeginHorizontal();
            if (_play != PlayState.Playing)
            {
                if (GUILayout.Button("▶  Play", GUILayout.Width(80), GUILayout.Height(24))) { if (_play == PlayState.Paused) _play = PlayState.Playing; else StartPreview(true); _lastTime = EditorApplication.timeSinceStartup; }
            }
            else if (GUILayout.Button("⏸  Pause", GUILayout.Width(80), GUILayout.Height(24))) _play = PlayState.Paused;
            if (GUILayout.Button("■  Stop", GUILayout.Width(70), GUILayout.Height(24))) StopPreview();
            GUILayout.Space(8);
            GUILayout.Label("Speed", GUILayout.Width(42));
            _speed = GUILayout.HorizontalSlider(_speed, 0.1f, 3f, GUILayout.Width(110));
            GUILayout.Label(_speed.ToString("0.0") + "x", GUILayout.Width(34));
            GUILayout.FlexibleSpace();
            GUILayout.Label("t = " + (span > 0f ? Mathf.Repeat(_clock, span) : 0f).ToString("0.00") + "s / " + span.ToString("0.0") + "s", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            if (_play == PlayState.Stopped)
                EditorGUILayout.LabelField("Kéo playhead trên timeline hoặc bấm Play để xem trực tiếp trên object trong scene (tự trả về pose gốc khi Stop).", EditorStyles.miniLabel);
        }

        // ═════════ Timeline ═════════
        float Span()
        {
            float m = 3f;
            for (int i = 0; i < _group.Count; i++)
            {
                ToolkitAnimator a = _group[i];
                if (a == null) continue;
                float len = a.EffectiveCycle > 0f ? a.EffectiveCycle : (a.IsLooping ? 4f : 1.5f);
                m = Mathf.Max(m, len * 1.25f);
            }
            return Mathf.Min(m, 20f);
        }

        void DrawTimeline()
        {
            float span = Span();
            const float labelW = 210f, rowH = 24f;
            EditorGUILayout.Space(4);
            Rect header = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
            Rect bar = new Rect(header.x + labelW, header.y, header.width - labelW - 6, header.height);

            // thước thời gian
            EditorGUI.DrawRect(bar, new Color(0, 0, 0, 0.15f));
            float step = span <= 6f ? 1f : (span <= 12f ? 2f : 5f);
            for (float t = 0f; t <= span + 0.001f; t += step)
            {
                float x = bar.x + t / span * bar.width;
                EditorGUI.DrawRect(new Rect(x, bar.y + 10, 1, 8), new Color(1, 1, 1, 0.5f));
                GUI.Label(new Rect(x + 2, bar.y - 1, 40, 14), t.ToString("0") + "s", EditorStyles.miniLabel);
            }

            Rect rowsArea = new Rect(header.x, header.yMax + 2, header.width, 0);
            for (int i = 0; i < _group.Count; i++)
            {
                ToolkitAnimator a = _group[i];
                if (a == null) continue;
                Rect row = GUILayoutUtility.GetRect(0, rowH, GUILayout.ExpandWidth(true));
                if (a == _selected) EditorGUI.DrawRect(row, new Color(0.24f, 0.48f, 0.9f, 0.22f));

                // nhãn
                Color badgeColor; string badge = Badge(a, out badgeColor);
                Rect chip = new Rect(row.x + 2, row.y + 4, 74, 16);
                AnimUiStyle.Chip(chip, badge, badgeColor);
                GUI.Label(new Rect(row.x + 80, row.y + 2, labelW - 82, 20), Short(a.Describe(), 30), EditorStyles.miniLabel);

                // thanh chu kỳ
                Rect b = new Rect(row.x + labelW, row.y + 3, row.width - labelW - 6, rowH - 6);
                DrawBars(b, a, span);
                rowsArea.height = row.yMax - rowsArea.y;

                Event e = Event.current;
                if (e.type == EventType.MouseDown && new Rect(row.x, row.y, labelW, row.height).Contains(e.mousePosition))
                { _selected = a; _plotDirty = true; DropEditor(); e.Use(); Repaint(); }
            }

            // playhead + kéo để tua
            Rect scrubArea = new Rect(bar.x, header.y, bar.width, rowsArea.yMax - header.y);
            float clockMod = span > 0f ? Mathf.Repeat(_clock, span) : 0f;
            float px = bar.x + clockMod / span * bar.width;
            EditorGUI.DrawRect(new Rect(px - 0.5f, header.y, 2f, scrubArea.height), new Color(1f, 0.3f, 0.25f, 0.95f));

            Event ev = Event.current;
            if ((ev.type == EventType.MouseDown || ev.type == EventType.MouseDrag) && ev.button == 0 && scrubArea.Contains(ev.mousePosition) && ev.mousePosition.x >= bar.x)
            {
                float t = Mathf.Clamp01((ev.mousePosition.x - bar.x) / bar.width) * span;
                Scrub(t);
                ev.Use();
            }
        }

        static void DrawBars(Rect b, ToolkitAnimator a, float span)
        {
            EditorGUI.DrawRect(b, new Color(0, 0, 0, 0.12f));
            float cycle = a.EffectiveCycle;
            if (!a.IsLooping)
            {
                float len = cycle > 0f ? cycle : 1f;
                float w = Mathf.Clamp01(len / span) * b.width;
                EditorGUI.DrawRect(new Rect(b.x, b.y, w, b.height), AnimUiStyle.Once);
                EditorGUI.DrawRect(new Rect(b.x + w, b.y + b.height * 0.5f - 1, b.width - w, 2), new Color(AnimUiStyle.Once.r, AnimUiStyle.Once.g, AnimUiStyle.Once.b, 0.5f));
                return;
            }
            if (cycle <= 0.0001f)
            {
                // liên tục / ngẫu nhiên: vạch thưa
                for (float x = 0; x < b.width; x += 14f) EditorGUI.DrawRect(new Rect(b.x + x, b.y + 4, 6, b.height - 8), new Color(AnimUiStyle.Cont.r, AnimUiStyle.Cont.g, AnimUiStyle.Cont.b, 0.7f));
                return;
            }
            int n = Mathf.CeilToInt(span / cycle);
            for (int k = 0; k < n && k < 400; k++)
            {
                float x0 = b.x + k * cycle / span * b.width;
                float x1 = Mathf.Min(b.xMax, b.x + (k + 1) * cycle / span * b.width);
                Color c = AnimUiStyle.Loop; if ((k & 1) == 1) c = Color.Lerp(c, Color.black, 0.18f);
                EditorGUI.DrawRect(new Rect(x0, b.y, Mathf.Max(1f, x1 - x0 - 1f), b.height), c);
            }
        }

        static string Badge(ToolkitAnimator a, out Color c)
        {
            if (!a.IsLooping) { c = AnimUiStyle.Once; return "ONCE"; }
            if (a.EffectiveCycle > 0f) { c = AnimUiStyle.Loop; return "LOOP " + a.EffectiveCycle.ToString("0.##") + "s"; }
            c = AnimUiStyle.Cont; return "CONTINUOUS";
        }

        static string Short(string s, int n) { return s.Length <= n ? s : s.Substring(0, n - 1) + "…"; }

        // ═════════ Chi tiết animator đang chọn ═════════
        void DrawDetails()
        {
            if (_selected == null) return;
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Color c; string badge = Badge(_selected, out c);
            EditorGUILayout.BeginHorizontal();
            AnimUiStyle.Chip(GUILayoutUtility.GetRect(100, 18, GUILayout.Width(100)), badge, c);
            GUILayout.Label(_selected.Describe(), EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            OffsetAnimator oa = _selected as OffsetAnimator;
            GUILayout.Label((oa != null ? "moves: " + oa.Target.name + "   ·   " : "") + "weight " + (_selected.Weight * 100f).ToString("0") + "%   ·   speed ×" + _selected.Speed.ToString("0.##")
                + (_selected.IsBakeable ? "" : "   ·   live only (không bake)"), EditorStyles.miniLabel);

            DrawPlots();

            _showInspector = EditorGUILayout.Foldout(_showInspector, "Thông số (chỉnh trực tiếp)", true);
            if (_showInspector)
            {
                if (_editor == null || _editor.target != _selected) { DropEditor(); _editor = UnityEditor.Editor.CreateEditor(_selected); }
                EditorGUI.BeginChangeCheck();
                _editor.OnInspectorGUI();
                if (EditorGUI.EndChangeCheck()) { _plotDirty = true; Repaint(); }
            }
            EditorGUILayout.EndVertical();
        }

        // ═════════ Đồ thị chuyển động ═════════
        void DrawPlots()
        {
            float span = Span();
            if (_plotDirty || _plotFor != _selected || !Mathf.Approximately(_plotSpan, span)) BuildPlots(span);
            if (_plots.Count == 0)
            {
                GUILayout.Label("Không có đồ thị (animator này ngẫu nhiên / phản ứng với chuyển động nên không vẽ trước được).", EditorStyles.miniLabel);
                return;
            }
            GUILayout.Label("Chuyển động theo thời gian (so với pose gốc)", EditorStyles.miniBoldLabel);
            for (int i = 0; i < _plots.Count; i++)
            {
                Plot p = _plots[i];
                Rect r = GUILayoutUtility.GetRect(0, 46, GUILayout.ExpandWidth(true));
                Rect g = new Rect(r.x + 120, r.y + 2, r.width - 126, r.height - 4);
                EditorGUI.DrawRect(g, new Color(0, 0, 0, 0.18f));
                GUI.Label(new Rect(r.x, r.y + 2, 118, 16), p.label, EditorStyles.miniBoldLabel);
                GUI.Label(new Rect(r.x, r.y + 16, 118, 14), p.min.ToString("0.##") + " … " + p.max.ToString("0.##"), EditorStyles.miniLabel);
                float range = Mathf.Max(0.0001f, p.max - p.min);
                // đường 0
                float zeroY = g.yMax - Mathf.Clamp01((0f - p.min) / range) * g.height;
                if (p.min < 0f && p.max > 0f) EditorGUI.DrawRect(new Rect(g.x, zeroY, g.width, 1), new Color(1, 1, 1, 0.25f));
                Vector3[] pts = new Vector3[p.v.Length];
                for (int k = 0; k < p.v.Length; k++)
                    pts[k] = new Vector3(g.x + (float)k / (p.v.Length - 1) * g.width, g.yMax - Mathf.Clamp01((p.v[k] - p.min) / range) * g.height, 0f);
                Handles.color = p.color;
                Handles.DrawAAPolyLine(2f, pts);
                // playhead
                float x = g.x + Mathf.Repeat(_clock, span) / span * g.width;
                EditorGUI.DrawRect(new Rect(x, g.y, 1.5f, g.height), new Color(1f, 0.3f, 0.25f, 0.9f));
            }
        }

        /// <summary>Vẽ đồ thị trên BẢN SAO ẩn của object: không đụng vào scene thật.</summary>
        void BuildPlots(float span)
        {
            _plots.Clear();
            _plotFor = _selected; _plotSpan = span; _plotDirty = false;
            if (_selected == null || !_selected.IsBakeable || _selected is SecondaryMotionAnimator) return;

            int index = System.Array.IndexOf(_all, _selected);
            if (index < 0) return;

            GameObject clone = Instantiate(_target);
            clone.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                ToolkitAnimator[] ca = clone.GetComponentsInChildren<ToolkitAnimator>(true);
                if (index >= ca.Length) return;
                ToolkitAnimator a = ca[index];
                for (int i = 0; i < ca.Length; i++) { ca[i].ClearOffset(); }
                List<Transform> tr = new List<Transform>();
                a.GetAffectedTransforms(tr);
                if (tr.Count == 0 || tr[0] == null) return;
                Transform t0 = tr[0];

                a.Weight = 1f;
                a.ResetState();
                const int N = 160;
                float[] rot = new float[N], px = new float[N], py = new float[N], sy = new float[N];
                Vector3 p0 = t0.localPosition, s0 = t0.localScale; float r0 = t0.localEulerAngles.z;
                for (int k = 0; k < N; k++)
                {
                    float t = (float)k / (N - 1) * span;
                    a.ClearOffset();
                    a.SetTime(t * a.Speed - a.TimeOffset);
                    a.Tick(0f);
                    rot[k] = Mathf.DeltaAngle(r0, t0.localEulerAngles.z);
                    px[k] = t0.localPosition.x - p0.x;
                    py[k] = t0.localPosition.y - p0.y;
                    sy[k] = s0.y > 0.0001f ? (t0.localScale.y / s0.y - 1f) * 100f : 0f;
                }
                AddPlot("Xoay Z (độ) · " + t0.name, rot, new Color(1f, 0.7f, 0.2f));
                AddPlot("Vị trí X · " + t0.name, px, new Color(0.4f, 0.85f, 1f));
                AddPlot("Vị trí Y · " + t0.name, py, new Color(0.5f, 1f, 0.55f));
                AddPlot("Scale Y (%) · " + t0.name, sy, new Color(1f, 0.55f, 0.85f));
            }
            finally { DestroyImmediate(clone); }
        }

        void AddPlot(string label, float[] v, Color color)
        {
            float mn = float.MaxValue, mx = float.MinValue;
            for (int i = 0; i < v.Length; i++) { if (v[i] < mn) mn = v[i]; if (v[i] > mx) mx = v[i]; }
            if (mx - mn < 0.0005f) return; // kênh không đổi → bỏ
            float pad = (mx - mn) * 0.08f;
            _plots.Add(new Plot { label = label, v = v, color = color, min = mn - pad, max = mx + pad });
        }

        // ═════════ Preview (trên object thật, khôi phục khi Stop) ═════════
        void StartPreview(bool playing)
        {
            StopPreview();
            AnimPreviewRunner.Stop();
            if (_group.Count == 0) return;
            _savedEnabled.Clear(); _savedWeight.Clear(); _savedPose.Clear(); _savedRot.Clear();

            List<Transform> tmp = new List<Transform>();
            for (int i = 0; i < _all.Length; i++)
            {
                if (_all[i] == null) continue;
                _savedEnabled[_all[i]] = _all[i].enabled;
                _savedWeight[_all[i]] = _all[i].Weight;
                tmp.Clear();
                _all[i].GetAffectedTransforms(tmp);
                for (int k = 0; k < tmp.Count; k++)
                    if (tmp[k] != null && !_savedPose.ContainsKey(tmp[k]))
                    {
                        _savedPose[tmp[k]] = new Vector3[] { tmp[k].localPosition, tmp[k].localScale };
                        _savedRot[tmp[k]] = tmp[k].localRotation;
                    }
            }
            // Chỉ nhóm đang xem chạy; animator ngoài nhóm tắt (trả pose gốc) trong lúc preview
            for (int i = 0; i < _all.Length; i++)
            {
                if (_all[i] == null) continue;
                bool inGroup = _group.Contains(_all[i]);
                _all[i].enabled = false;
                if (inGroup) { _all[i].Weight = 1f; _all[i].ResetState(); }
            }
            for (int i = 0; i < _group.Count; i++) if (_group[i] != null) _group[i].enabled = true;

            _play = playing ? PlayState.Playing : PlayState.Paused;
            _lastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += PreviewTick;
        }

        void PreviewTick()
        {
            double now = EditorApplication.timeSinceStartup;
            float real = Mathf.Min((float)(now - _lastTime), 0.1f);
            _lastTime = now;
            if (_play != PlayState.Playing) return;
            float dt = real * _speed;
            _clock += dt;
            for (int i = 0; i < _group.Count; i++)
                if (_group[i] != null && _group[i].enabled) _group[i].Tick(dt);
            SceneView.RepaintAll();
            Repaint();
        }

        void Scrub(float t)
        {
            if (_play == PlayState.Stopped) StartPreview(false);
            if (_play == PlayState.Playing) _play = PlayState.Paused;
            _clock = t;
            for (int i = 0; i < _group.Count; i++) if (_group[i] != null) _group[i].ClearOffset();
            for (int i = 0; i < _group.Count; i++)
            {
                ToolkitAnimator a = _group[i];
                if (a == null) continue;
                a.SetTime(t * a.Speed - a.TimeOffset); // thời gian nội bộ = giây thực × speed; Evaluate dùng time + timeOffset
                a.Tick(0f);
            }
            SceneView.RepaintAll();
            Repaint();
        }

        void StopPreview()
        {
            if (_play == PlayState.Stopped && _savedPose.Count == 0) return;
            EditorApplication.update -= PreviewTick;
            for (int i = 0; i < _all.Length; i++) if (_all[i] != null) _all[i].ClearOffset();
            foreach (KeyValuePair<Transform, Vector3[]> kv in _savedPose)
            {
                if (kv.Key == null) continue;
                kv.Key.localPosition = kv.Value[0];
                kv.Key.localScale = kv.Value[1];
                kv.Key.localRotation = _savedRot[kv.Key];
            }
            foreach (KeyValuePair<ToolkitAnimator, bool> kv in _savedEnabled) if (kv.Key != null) kv.Key.enabled = kv.Value;
            foreach (KeyValuePair<ToolkitAnimator, float> kv in _savedWeight) if (kv.Key != null) kv.Key.Weight = kv.Value;
            _savedEnabled.Clear(); _savedWeight.Clear(); _savedPose.Clear(); _savedRot.Clear();
            _play = PlayState.Stopped;
            SceneView.RepaintAll();
            Repaint();
        }
    }
}
