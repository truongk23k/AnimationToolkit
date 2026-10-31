using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnimationToolkit.EditorTools
{
    /// <summary>
    /// Chạy thử một AnimStateSet ngay trên object trong scene (edit mode), giống chọn Animation Name trong Spine:
    /// dùng đúng logic của AnimStateSet (loop, once+giữ pose, time scale), rồi trả pose gốc khi dừng.
    /// Tự dừng khi bỏ chọn object, lưu scene, vào Play mode hoặc compile lại.
    /// </summary>
    [InitializeOnLoad]
    public static class AnimPreviewRunner
    {
        public static event Action Starting;

        static AnimStateSet _set;
        static bool _running;
        static double _last;
        static readonly List<ToolkitAnimator> Animators = new List<ToolkitAnimator>();
        static readonly Dictionary<ToolkitAnimator, bool> SavedEnabled = new Dictionary<ToolkitAnimator, bool>();
        static readonly Dictionary<ToolkitAnimator, float> SavedWeight = new Dictionary<ToolkitAnimator, float>();
        static readonly Dictionary<ToolkitAnimator, float> SavedSpeed = new Dictionary<ToolkitAnimator, float>();
        static readonly Dictionary<Transform, Vector3[]> SavedPose = new Dictionary<Transform, Vector3[]>();
        static readonly Dictionary<Transform, Quaternion> SavedRot = new Dictionary<Transform, Quaternion>();

        static AnimPreviewRunner()
        {
            Selection.selectionChanged += OnSelection;
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingEditMode) Stop(); };
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorSceneManager.sceneSaving += (scene, path) => Stop();
        }

        public static bool IsRunning(AnimStateSet set) { return _running && _set == set; }
        public static bool Frozen { get { return _running && _set != null && _set.IsFrozen; } }

        static void OnSelection()
        {
            if (!_running || _set == null) return;
            GameObject sel = Selection.activeGameObject;
            if (sel == null || (sel != _set.gameObject && !sel.transform.IsChildOf(_set.transform))) Stop();
        }

        /// <summary>Chạy animation index của set từ đầu (dừng preview cũ nếu có).</summary>
        public static void Start(AnimStateSet set, int index)
        {
            if (set == null || set.Count == 0 || Application.isPlaying) return;
            Stop();
            if (Starting != null) Starting();
            AnimatorSelectionOverlay.StopPreview();

            _set = set;
            List<Transform> tmp = new List<Transform>();
            for (int s = 0; s < set.Count; s++)
            {
                ToolkitAnimator[] list = set.StateAnimators(s);
                for (int i = 0; i < list.Length; i++)
                {
                    ToolkitAnimator a = list[i];
                    if (a == null || SavedEnabled.ContainsKey(a)) continue;
                    SavedEnabled[a] = a.enabled; SavedWeight[a] = a.Weight; SavedSpeed[a] = a.Speed;
                    Animators.Add(a);
                    tmp.Clear();
                    a.GetAffectedTransforms(tmp);
                    for (int k = 0; k < tmp.Count; k++)
                        if (tmp[k] != null && !SavedPose.ContainsKey(tmp[k]))
                        {
                            SavedPose[tmp[k]] = new Vector3[] { tmp[k].localPosition, tmp[k].localScale };
                            SavedRot[tmp[k]] = tmp[k].localRotation;
                        }
                }
            }
            for (int i = 0; i < Animators.Count; i++) Animators[i].enabled = false;
            set.Restart(index);
            _running = true;
            _last = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        public static void Restart()
        {
            if (_running && _set != null) { int i = _set.CurrentIndex; AnimStateSet s = _set; Start(s, Mathf.Max(0, i)); }
        }

        static void Tick()
        {
            if (_set == null) { Stop(); return; }
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min((float)(now - _last), 0.1f);
            _last = now;
            _set.Tick(dt);
            for (int i = 0; i < Animators.Count; i++)
                if (Animators[i] != null && Animators[i].enabled) Animators[i].Tick(dt);
            SceneView.RepaintAll();
            InternalRepaint();
        }

        static void InternalRepaint()
        {
            foreach (UnityEditor.Editor e in Resources.FindObjectsOfTypeAll<UnityEditor.Editor>())
                if (e is AnimStateSetEditor) e.Repaint();
        }

        public static void Stop()
        {
            if (!_running && SavedPose.Count == 0) return;
            EditorApplication.update -= Tick;
            for (int i = 0; i < Animators.Count; i++) if (Animators[i] != null) Animators[i].ClearOffset();
            foreach (KeyValuePair<Transform, Vector3[]> kv in SavedPose)
            {
                if (kv.Key == null) continue;
                kv.Key.localPosition = kv.Value[0];
                kv.Key.localScale = kv.Value[1];
                kv.Key.localRotation = SavedRot[kv.Key];
            }
            foreach (KeyValuePair<ToolkitAnimator, bool> kv in SavedEnabled) if (kv.Key != null) kv.Key.enabled = kv.Value;
            foreach (KeyValuePair<ToolkitAnimator, float> kv in SavedWeight) if (kv.Key != null) kv.Key.Weight = kv.Value;
            foreach (KeyValuePair<ToolkitAnimator, float> kv in SavedSpeed) if (kv.Key != null) kv.Key.Speed = kv.Value;
            if (_set != null) _set.ResetRuntime();
            Animators.Clear(); SavedEnabled.Clear(); SavedWeight.Clear(); SavedSpeed.Clear(); SavedPose.Clear(); SavedRot.Clear();
            _running = false;
            _set = null;
            SceneView.RepaintAll();
        }
    }
}
