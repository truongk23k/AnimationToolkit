using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AnimationToolkit.EditorTools
{
    /// <summary>
    /// Bake các animator của toolkit thành AnimationClip (keyframe Transform). Mẫu 1 chu kỳ đầy đủ,
    /// khép kín key cuối = key đầu và dùng tiếp tuyến trung tâm (wrap-around) để loop không giật.
    /// Không bake: Blink (ngẫu nhiên), đổi sprite, alpha của TweenAnimator.
    /// </summary>
    public static class ClipBaker
    {
        public class Result
        {
            public AnimationClip clip;
            public int curveCount;
            public List<string> notes = new List<string>();
        }

        public static Result Bake(GameObject root, float duration, int fps, bool loop, bool legacy, bool snapToLoop)
        {
            Result res = new Result();
            ToolkitAnimator[] all = root.GetComponentsInChildren<ToolkitAnimator>(false);
            List<ToolkitAnimator> anims = new List<ToolkitAnimator>();
            for (int i = 0; i < all.Length; i++)
            {
                if (!all[i].enabled) continue;
                if (!all[i].IsBakeable) { res.notes.Add("Skipped (not bakeable): " + all[i].GetType().Name + " on " + all[i].name); continue; }
                anims.Add(all[i]);
            }
            if (anims.Count == 0) { res.notes.Add("No bakeable animators found."); return res; }

            List<Transform> targets = new List<Transform>();
            List<Transform> tmp = new List<Transform>();
            for (int i = 0; i < anims.Count; i++)
            {
                tmp.Clear();
                anims[i].GetAffectedTransforms(tmp);
                for (int k = 0; k < tmp.Count; k++)
                    if (tmp[k] != null && !targets.Contains(tmp[k]) && (tmp[k] == root.transform || tmp[k].IsChildOf(root.transform)))
                        targets.Add(tmp[k]);
            }

            // Đưa mọi thứ về pose gốc rồi chụp rest pose
            for (int i = 0; i < anims.Count; i++) anims[i].ResetState();
            int n = targets.Count;
            Vector3[] restP = new Vector3[n]; Quaternion[] restR = new Quaternion[n]; Vector3[] restS = new Vector3[n];
            for (int i = 0; i < n; i++) { restP[i] = targets[i].localPosition; restR[i] = targets[i].localRotation; restS[i] = targets[i].localScale; }

            if (snapToLoop)
                for (int i = 0; i < anims.Count; i++)
                {
                    Undo.RecordObject(anims[i], "Snap to loop");
                    anims[i].SnapToLoop(duration);
                }

            float dt = 1f / fps;
            // Warm-up cho animator có trạng thái (spring), không ghi lại
            for (int w = 0; w < fps * 2; w++)
            {
                for (int i = 0; i < anims.Count; i++) anims[i].ClearOffset();
                for (int i = 0; i < anims.Count; i++) anims[i].Tick(dt);
            }
            for (int i = 0; i < anims.Count; i++) { anims[i].ClearOffset(); anims[i].SetTime(0f); }

            int frames = Mathf.Max(2, Mathf.RoundToInt(duration * fps));
            int samples = frames + 1;
            Vector3[][] P = new Vector3[n][]; Vector3[][] E = new Vector3[n][]; Vector3[][] S = new Vector3[n][];
            for (int i = 0; i < n; i++) { P[i] = new Vector3[samples]; E[i] = new Vector3[samples]; S[i] = new Vector3[samples]; }

            for (int f = 0; f < samples; f++)
            {
                for (int i = 0; i < anims.Count; i++) anims[i].ClearOffset();
                for (int i = 0; i < anims.Count; i++) anims[i].Tick(f == 0 ? 0f : dt);
                for (int i = 0; i < n; i++)
                {
                    P[i][f] = targets[i].localPosition;
                    S[i][f] = targets[i].localScale;
                    Vector3 e = targets[i].localRotation.eulerAngles;
                    if (f > 0) e = Unwrap(e, E[i][f - 1]);
                    E[i][f] = e;
                }
            }

            // Trả về pose gốc chính xác
            for (int i = 0; i < anims.Count; i++) anims[i].ClearOffset();
            for (int i = 0; i < n; i++) { targets[i].localPosition = restP[i]; targets[i].localRotation = restR[i]; targets[i].localScale = restS[i]; }

            AnimationClip clip = new AnimationClip();
            clip.frameRate = fps;
            clip.legacy = legacy;
            string[] px = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
            string[] ex = { "localEulerAnglesRaw.x", "localEulerAnglesRaw.y", "localEulerAnglesRaw.z" };
            string[] sx = { "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };

            for (int i = 0; i < n; i++)
            {
                string path = AnimationUtility.CalculateTransformPath(targets[i], root.transform);
                for (int axis = 0; axis < 3; axis++)
                {
                    res.curveCount += AddCurve(clip, path, px[axis], P[i], axis, dt, loop, 1e-5f);
                    res.curveCount += AddCurve(clip, path, ex[axis], E[i], axis, dt, loop, 1e-3f);
                    res.curveCount += AddCurve(clip, path, sx[axis], S[i], axis, dt, loop, 1e-5f);
                }
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (legacy && loop) clip.wrapMode = WrapMode.Loop;

            res.clip = clip;
            if (res.curveCount == 0) res.notes.Add("No transform changed; clip is empty.");
            return res;
        }

        static Vector3 Unwrap(Vector3 e, Vector3 prev)
        {
            e.x = prev.x + Mathf.DeltaAngle(prev.x, e.x);
            e.y = prev.y + Mathf.DeltaAngle(prev.y, e.y);
            e.z = prev.z + Mathf.DeltaAngle(prev.z, e.z);
            return e;
        }

        static float Get(Vector3 v, int axis) { return axis == 0 ? v.x : (axis == 1 ? v.y : v.z); }

        static int AddCurve(AnimationClip clip, string path, string prop, Vector3[] data, int axis, float dt, bool loop, float epsilon)
        {
            int count = data.Length;
            float min = float.MaxValue, max = float.MinValue;
            float[] v = new float[count];
            for (int i = 0; i < count; i++)
            {
                v[i] = Get(data[i], axis);
                if (v[i] < min) min = v[i];
                if (v[i] > max) max = v[i];
            }
            if (max - min < epsilon) return 0;

            if (loop) v[count - 1] = v[0]; // khép kín chu kỳ

            Keyframe[] keys = new Keyframe[count];
            for (int i = 0; i < count; i++)
            {
                float prev, next;
                if (loop)
                {
                    // Chu kỳ có count-1 khoảng; key cuối trùng key đầu nên wrap qua phần tử kề
                    prev = i == 0 ? v[count - 2] : v[i - 1];
                    next = i == count - 1 ? v[1] : v[i + 1];
                }
                else
                {
                    prev = i == 0 ? v[0] : v[i - 1];
                    next = i == count - 1 ? v[count - 1] : v[i + 1];
                }
                float slope = (next - prev) / (2f * dt);
                keys[i] = new Keyframe(i * dt, v[i], slope, slope);
            }
            EditorCurveBinding b = EditorCurveBinding.FloatCurve(path, typeof(Transform), prop);
            AnimationUtility.SetEditorCurve(clip, b, new AnimationCurve(keys));
            return 1;
        }
    }
}
