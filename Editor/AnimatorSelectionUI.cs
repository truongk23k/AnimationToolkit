using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnimationToolkit.EditorTools
{
    static class AnimUiStyle
    {
        public static readonly Color Loop = new Color(0.25f, 0.65f, 0.35f);
        public static readonly Color Once = new Color(0.85f, 0.55f, 0.15f);
        public static readonly Color Cont = new Color(0.3f, 0.5f, 0.85f);
        public static readonly Color Off = new Color(0.5f, 0.5f, 0.5f);

        /// <summary>Loại hiển thị: LOOP (có chu kỳ), CONT (liên tục/ngẫu nhiên, không có chu kỳ cố định), ONCE (một lần / theo lệnh).</summary>
        public static string Badge(ToolkitAnimator a, out Color color)
        {
            if (!a.enabled) { color = Off; return "OFF"; }
            if (!a.IsLooping) { color = Once; return "ONCE"; }
            float c = a.EffectiveCycle;
            if (c > 0f) { color = Loop; return "LOOP " + c.ToString("0.##") + "s"; }
            color = Cont; return "CONTINUOUS";
        }

        public static void Chip(Rect r, string text, Color c)
        {
            EditorGUI.DrawRect(r, c);
            GUI.Label(r, text, new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } });
        }
    }

    /// <summary>Header ở Inspector của mọi animator: loại, có loop không, chu kỳ, target, weight.</summary>
    [CustomEditor(typeof(ToolkitAnimator), true)]
    [CanEditMultipleObjects]
    public class ToolkitAnimatorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (targets.Length == 1)
            {
                ToolkitAnimator a = (ToolkitAnimator)target;
                Color c;
                string badge = AnimUiStyle.Badge(a, out c);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                Rect chip = GUILayoutUtility.GetRect(110, 18, GUILayout.Width(110));
                AnimUiStyle.Chip(chip, badge, c);
                GUILayout.Label(a.Describe(), EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                string where = "";
                OffsetAnimator oa = a as OffsetAnimator;
                if (oa != null) where = "moves: " + oa.Target.name + "   ·   ";
                EditorGUILayout.LabelField(where + "weight " + (a.Weight * 100f).ToString("0") + "%   ·   speed ×" + a.Speed.ToString("0.##")
                    + (a.IsBakeable ? "" : "   ·   live only (not bakeable)"), EditorStyles.miniLabel);

                if (!a.IsLooping)
                    EditorGUILayout.LabelField("Plays once and holds the final pose (or runs only when triggered from code).", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }
            DrawDefaultInspector();
        }
    }

    /// <summary>Bảng nổi trong Scene view + nhãn Hierarchy: chọn GameObject là thấy nó đang chạy anim nào, loop hay không.</summary>
    [InitializeOnLoad]
    public static class AnimatorSelectionOverlay
    {
        static bool _previewing;
        static double _last;
        static readonly List<ToolkitAnimator> PreviewAnims = new List<ToolkitAnimator>();
        static readonly Dictionary<Transform, Vector3[]> Snapshot = new Dictionary<Transform, Vector3[]>();
        static readonly Dictionary<Transform, Quaternion> SnapshotRot = new Dictionary<Transform, Quaternion>();

        static AnimatorSelectionOverlay()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyItem;
            Selection.selectionChanged += SceneView.RepaintAll;
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingEditMode) StopPreview(); };
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;   // không để pose lệch sót lại sau khi compile
            EditorSceneManager.sceneSaving += (scene, path) => StopPreview(); // và không lưu pose đang preview vào scene
        }


        static void StopMenu() { StopPreview(); }

        // ───── Scene view panel ─────
        static void OnSceneGUI(SceneView sv)
        {
            GameObject go = Selection.activeGameObject;
            if (go == null || EditorUtility.IsPersistent(go)) return;

            ToolkitAnimator[] own = go.GetComponents<ToolkitAnimator>();
            ToolkitAnimator[] all = go.GetComponentsInChildren<ToolkitAnimator>(true);
            if (all.Length == 0 && !_previewing) return;

            const float width = 380f;
            int rows = Mathf.Max(own.Length, 1);
            float height = 54f + rows * 36f + (all.Length > own.Length ? 18f : 0f);

            Handles.BeginGUI();
            // Lùi sang phải để không bị thanh công cụ của Scene view (góc trên-trái) che
            GUILayout.BeginArea(new Rect(60, 10, width, height), EditorStyles.helpBox);

            GUILayout.BeginHorizontal();
            GUILayout.Label(go.name, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(own.Length + " animation" + (own.Length == 1 ? "" : "s"), EditorStyles.miniLabel);
            GUILayout.EndHorizontal();

            if (own.Length == 0)
            {
                GUILayout.Label("No animator on this object.", EditorStyles.miniLabel);
            }
            for (int i = 0; i < own.Length; i++) DrawRow(own[i]);
            if (all.Length > own.Length)
                GUILayout.Label((all.Length - own.Length) + " more on children (select a child to see them)", EditorStyles.miniLabel);

            GUILayout.BeginHorizontal();
            if (!_previewing)
            {
                GUI.enabled = all.Length > 0;
                if (GUILayout.Button("Play preview", EditorStyles.miniButtonLeft)) StartPreview(all);
                GUI.enabled = true;
            }
            else if (GUILayout.Button("Stop preview (restores pose)", EditorStyles.miniButtonLeft)) StopPreview();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
            Handles.EndGUI();
        }

        static void DrawRow(ToolkitAnimator a)
        {
            if (a == null) return;
            Color c;
            string badge = AnimUiStyle.Badge(a, out c);
            EditorGUILayout.BeginHorizontal(GUILayout.Height(32));
            Rect chip = GUILayoutUtility.GetRect(92, 18, GUILayout.Width(92));
            chip.y += 7;
            AnimUiStyle.Chip(chip, badge, c);
            EditorGUILayout.BeginVertical();
            GUILayout.Label(a.Describe(), a.enabled ? EditorStyles.boldLabel : EditorStyles.miniLabel);
            OffsetAnimator oa = a as OffsetAnimator;
            GUILayout.Label((oa != null ? "moves " + oa.Target.name + " · " : "") + "weight " + (a.Weight * 100f).ToString("0") + "% · ×" + a.Speed.ToString("0.##"), EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        // ───── Hierarchy label ─────
        static void OnHierarchyItem(int instanceId, Rect rect)
        {
            GameObject go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (go == null) return;
            ToolkitAnimator[] own = go.GetComponents<ToolkitAnimator>();
            if (own.Length == 0) return;

            int loops = 0, once = 0;
            for (int i = 0; i < own.Length; i++) { if (own[i].IsLooping) loops++; else once++; }
            string text = (loops > 0 ? "loop " + loops : "") + (loops > 0 && once > 0 ? " · " : "") + (once > 0 ? "once " + once : "");
            GUIStyle st = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, normal = { textColor = loops > 0 ? AnimUiStyle.Loop : AnimUiStyle.Once } };
            GUI.Label(rect, text, st);
        }

        // ───── Preview (edit mode) ─────
        static void StartPreview(ToolkitAnimator[] anims)
        {
            StopPreview();
            AnimPreviewRunner.Stop();
            List<Transform> tmp = new List<Transform>();
            for (int i = 0; i < anims.Length; i++)
            {
                if (anims[i] == null || !anims[i].enabled) continue;
                anims[i].ResetState();
                PreviewAnims.Add(anims[i]);
                tmp.Clear();
                anims[i].GetAffectedTransforms(tmp);
                for (int k = 0; k < tmp.Count; k++)
                    if (tmp[k] != null && !Snapshot.ContainsKey(tmp[k]))
                    {
                        Snapshot[tmp[k]] = new Vector3[] { tmp[k].localPosition, tmp[k].localScale };
                        SnapshotRot[tmp[k]] = tmp[k].localRotation;
                    }
            }
            if (PreviewAnims.Count == 0) return;
            _previewing = true;
            _last = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min((float)(now - _last), 0.1f);
            _last = now;
            for (int i = 0; i < PreviewAnims.Count; i++)
                if (PreviewAnims[i] != null && PreviewAnims[i].enabled) PreviewAnims[i].Tick(dt);
            SceneView.RepaintAll();
        }

        public static void StopPreview()
        {
            if (!_previewing) return;
            EditorApplication.update -= Tick;
            _previewing = false;
            for (int i = 0; i < PreviewAnims.Count; i++)
                if (PreviewAnims[i] != null) PreviewAnims[i].ClearOffset();
            // Khôi phục chính xác để tránh sai số float tích luỹ
            foreach (KeyValuePair<Transform, Vector3[]> kv in Snapshot)
            {
                if (kv.Key == null) continue;
                kv.Key.localPosition = kv.Value[0];
                kv.Key.localScale = kv.Value[1];
                kv.Key.localRotation = SnapshotRot[kv.Key];
            }
            Snapshot.Clear(); SnapshotRot.Clear(); PreviewAnims.Clear();
            SceneView.RepaintAll();
        }
    }
}
