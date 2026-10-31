using UnityEditor;
using UnityEngine;

namespace AnimationToolkit.EditorTools
{
    /// <summary>
    /// Inspector kiểu Spine cho AnimStateSet. Ô "Animation" sổ xuống để chọn animation CHẠY:
    /// - edit mode: chọn là chạy thử ngay trên object trong scene (Loop tắt thì chạy một lần rồi giữ pose cuối);
    /// - Play mode: chọn là chuyển animation thật (cross-fade), Loop / Time Scale đổi có hiệu lực ngay.
    /// Không có nút nào trên màn hình Game — mọi điều khiển nằm ở đây.
    /// </summary>
    [CustomEditor(typeof(AnimStateSet))]
    public class AnimStateSetEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() { return Application.isPlaying; }

        public override void OnInspectorGUI()
        {
            AnimStateSet set = (AnimStateSet)target;
            serializedObject.Update();
            SerializedProperty states = serializedObject.FindProperty("states");
            SerializedProperty start = serializedObject.FindProperty("startState");
            int count = states.arraySize;

            if (count == 0)
            {
                EditorGUILayout.HelpBox("Chưa có animation nào. Tạo bằng skill /unity-animation-toolkit, hoặc thêm ở mục States bên dưới.", MessageType.Info);
            }
            else
            {
                string[] names = new string[count];
                for (int i = 0; i < count; i++) names[i] = states.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;

                bool playing = Application.isPlaying;
                bool previewing = AnimPreviewRunner.IsRunning(set);

                // Animation đang chọn: lúc Play = animation đang chạy; edit = đang preview hoặc animation khởi đầu
                int cur;
                if (playing) cur = Mathf.Max(0, set.CurrentIndex);
                else if (previewing) cur = Mathf.Max(0, set.CurrentIndex);
                else cur = Mathf.Max(0, System.Array.IndexOf(names, start.stringValue));
                cur = Mathf.Clamp(cur, 0, count - 1);

                // ── Animation ▾ ──
                EditorGUI.BeginChangeCheck();
                int pick = EditorGUILayout.Popup(new GUIContent("Animation", "Chọn animation để chạy"), cur, names);
                if (EditorGUI.EndChangeCheck() && pick != cur)
                {
                    if (playing) set.Play(pick, 0.3f);
                    else
                    {
                        start.stringValue = names[pick];           // animation khởi đầu (lưu vào scene)
                        serializedObject.ApplyModifiedProperties();
                        AnimPreviewRunner.Start(set, pick);        // và chạy luôn để xem
                    }
                    cur = pick;
                    GUIUtility.ExitGUI();
                }

                // ── Loop (bật/tắt, có hiệu lực ngay) ──
                SerializedProperty loopP = states.GetArrayElementAtIndex(cur).FindPropertyRelative("loop");
                EditorGUI.BeginChangeCheck();
                bool loop = EditorGUILayout.Toggle(new GUIContent("Loop", "Tắt: chạy một lần rồi giữ pose cuối"), loopP.boolValue);
                if (EditorGUI.EndChangeCheck())
                {
                    if (playing) set.SetStateLoop(cur, loop);
                    else
                    {
                        loopP.boolValue = loop;
                        serializedObject.ApplyModifiedProperties();
                        if (previewing) AnimPreviewRunner.Start(set, cur);
                    }
                    GUIUtility.ExitGUI();
                }

                // ── Time Scale ──
                SerializedProperty ts = serializedObject.FindProperty("timeScale");
                EditorGUI.BeginChangeCheck();
                float scale = EditorGUILayout.Slider(new GUIContent("Time Scale"), ts.floatValue, 0f, 3f);
                if (EditorGUI.EndChangeCheck())
                {
                    ts.floatValue = scale;
                    serializedObject.ApplyModifiedProperties();
                    if (playing) set.TimeScale = scale;
                    else if (previewing) AnimPreviewRunner.Start(set, cur);
                }

                // ── Thông tin + điều khiển preview (edit mode) ──
                string info = names[cur] + "  ·  " + (loopP.boolValue ? "lặp mãi" : "chạy một lần (" + set.StateLength(cur).ToString("0.0#") + "s)")
                    + "  ·  " + set.StateAnimators(cur).Length + " animator";
                if (playing && set.IsFrozen && set.CurrentIndex == cur) info += "  ·  ĐÃ XONG, giữ pose cuối";
                EditorGUILayout.LabelField(info, EditorStyles.miniLabel);

                if (!playing)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUI.backgroundColor = previewing ? new Color(1f, 0.6f, 0.5f) : new Color(0.55f, 0.9f, 0.6f);
                    if (GUILayout.Button(previewing ? "■  Dừng xem thử" : "▶  Chạy thử animation này", GUILayout.Height(24)))
                    {
                        if (previewing) AnimPreviewRunner.Stop(); else AnimPreviewRunner.Start(set, cur);
                        GUIUtility.ExitGUI();
                    }
                    GUI.backgroundColor = Color.white;
                    GUI.enabled = previewing;
                    if (GUILayout.Button("↻  Chạy lại", GUILayout.Height(24), GUILayout.Width(90))) { AnimPreviewRunner.Start(set, cur); GUIUtility.ExitGUI(); }
                    GUI.enabled = true;
                    EditorGUILayout.EndHorizontal();
                }
                else if (set.IsFrozen || !loopP.boolValue)
                {
                    if (GUILayout.Button("↻  Chạy lại animation này")) set.Restart();
                }
            }

            EditorGUILayout.Space(2);
            if (GUILayout.Button("Mở trong Animation Viewer (timeline, tua, đồ thị)"))
            {
                Selection.activeGameObject = set.gameObject;
                EditorApplication.ExecuteMenuItem("Tools/Animation Toolkit/Animation Viewer");
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("crossFade"), new GUIContent("Cross Fade (s)"));
            EditorGUILayout.PropertyField(states, new GUIContent("States  (loop, duration, animator của từng animation)"), true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
