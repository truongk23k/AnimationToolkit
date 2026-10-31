using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnimationToolkit.EditorTools
{
    /// <summary>
    /// Sinh art chim xanh (side view, mỗi bộ phận một PNG riêng, pivot đúng khớp) rồi dựng scene BirdAnimTest
    /// với 2 animation: Idle (đậu trên cành) và Fly (vỗ cánh bay tại chỗ), chuyển qua lại bằng AnimStateSet.
    /// Vẽ bằng khoảng cách có dấu (SDF) nên viền mượt.
    /// </summary>
    public static class BirdSampleGenerator
    {
        const string ArtDir = "Assets/AnimationToolkit/Samples/BirdArt";
        const string ScenePath = "Assets/AnimationToolkit/Samples/BirdAnimTest.unity";

        delegate Color Shade(float x, float y);

        static readonly Color Clear = new Color(0, 0, 0, 0);
        static readonly Color Navy = new Color(0.08f, 0.1f, 0.3f);
        static readonly Color BlueL = new Color(0.45f, 0.76f, 1f);
        static readonly Color BlueM = new Color(0.25f, 0.5f, 0.96f);
        static readonly Color BlueD = new Color(0.14f, 0.3f, 0.8f);
        static readonly Color Orange = new Color(1f, 0.72f, 0.4f);
        static readonly Color Cream = new Color(1f, 0.93f, 0.78f);
        static readonly Color BeakA = new Color(1f, 0.76f, 0.22f);
        static readonly Color BeakLine = new Color(0.62f, 0.28f, 0.1f);

        // (không còn menu: tạo bằng skill /unity-animation-toolkit)
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(ArtDir)) AssetDatabase.CreateFolder("Assets/AnimationToolkit/Samples", "BirdArt");

            Sprite body = Body(), head = Head(), eyeOpen = EyeOpen(), eyeClosed = EyeClosed();
            Sprite wing = Wing(), tail1 = TailSeg("tail_1", 112, 78, 92, 36, 28), tail2 = TailSeg("tail_2", 104, 60, 84, 28, 14);
            Sprite leg = Leg(), branch = Branch(), cloud = Cloud();
            AssetDatabase.SaveAssets();

            Scene(body, head, eyeOpen, eyeClosed, wing, tail1, tail2, leg, branch, cloud);
        }

        // ═════════════ Scene + animation ═════════════
        static Transform Part(string name, Transform parent, Vector3 pos, Sprite sp, int order, float rotZ = 0f, Color? tint = null)
        {
            GameObject g = new GameObject(name);
            if (parent != null) g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = sp; sr.sortingOrder = order;
            if (tint.HasValue) sr.color = tint.Value;
            return g.transform;
        }

        static void Scene(Sprite body, Sprite head, Sprite eyeOpen, Sprite eyeClosed, Sprite wing, Sprite tail1, Sprite tail2, Sprite leg, Sprite branch, Sprite cloud)
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Camera cam = Camera.main;
            cam.orthographic = true; cam.orthographicSize = 3.5f;
            cam.transform.position = new Vector3(0f, 1.45f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.85f, 0.97f);

            // ── Nền ──
            Part("Cloud1", null, new Vector3(-2.6f, 3.9f, 0), cloud, -10, 0, new Color(1, 1, 1, 0.95f));
            Part("Cloud2", null, new Vector3(2.7f, 3.0f, 0), cloud, -10, 0, new Color(1, 1, 1, 0.8f)).localScale = new Vector3(0.75f, 0.75f, 1f);
            Part("Branch", null, new Vector3(0f, -0.18f, 0), branch, 0);

            // ── Chim: container "Bird" có pivot ở chân (mặt cành, y=0) ──
            Transform bird = new GameObject("Bird").transform;

            Transform legL = Part("LegBack", bird, new Vector3(-0.14f, 0.64f, 0), leg, 1);
            Transform legR = Part("LegFront", bird, new Vector3(0.3f, 0.64f, 0), leg, 1);
            Transform tail1T = Part("Tail1", bird, new Vector3(-0.7f, 1.05f, 0), tail1, 2, 4f);
            Transform tail2T = Part("Tail2", tail1T, new Vector3(-0.82f, 0f, 0), tail2, 2, 3f);
            Transform wingFar = Part("WingFar", bird, new Vector3(0.10f, 1.52f, 0), wing, 2, 14f, new Color(0.62f, 0.68f, 0.9f));
            Transform bodyT = Part("Body", bird, new Vector3(0f, 0.5f, 0), body, 3);
            Transform wingNear = Part("WingNear", bird, new Vector3(-0.08f, 1.44f, 0), wing, 4, 8f);
            Transform headT = Part("Head", bird, new Vector3(0.52f, 1.72f, 0), head, 6);
            Transform eyeT = Part("Eye", headT, new Vector3(0.3f, 0.58f, 0), eyeOpen, 7);

            // ── Chuyển động: IDLE (đậu trên cành) ──
            List<ToolkitAnimator> idle = new List<ToolkitAnimator>();
            idle.Add(AnimKit.Breathe(bodyT, 0.03f, 0.45f));

            PoseSequenceAnimator look = AnimKit.Keyframes(headT, AnimLoopMode.Loop,
                Key(0f, 0, 0, 0), Key(1.2f, 0, 0, 9), Key(2.2f, 0, 0, 9), Key(2.9f, 0, 0, -7), Key(3.9f, 0, 0, -7),
                Key(4.7f, 0, 0, 0), Key(5.2f, 0.05f, -0.1f, -16, EaseType.CubicOut), Key(5.4f, 0, 0, 0),
                Key(5.65f, 0.05f, -0.1f, -16, EaseType.CubicOut), Key(5.85f, 0, 0, 0), Key(7f, 0, 0, 0));
            idle.Add(look);

            PoseSequenceAnimator shuffle = AnimKit.Keyframes(wingNear, AnimLoopMode.Loop,
                Key(0f, 0, 0, 0), Key(3.0f, 0, 0, 0), Key(3.12f, 0, 0, -16, EaseType.CubicOut), Key(3.3f, 0, 0, 0),
                Key(3.42f, 0, 0, -11, EaseType.CubicOut), Key(3.6f, 0, 0, 0), Key(7f, 0, 0, 0));
            idle.Add(shuffle);
            idle.Add(AnimKit.Swing(wingNear, 2f, 0.3f));

            BlinkAnimator blink = eyeT.gameObject.AddComponent<BlinkAnimator>();
            blink.SetupSprites(new[] { eyeT.GetComponent<SpriteRenderer>() }, eyeOpen, null, eyeClosed);
            blink.Configure(2.2f, 5f);
            SerializedObject sb = new SerializedObject(blink);
            sb.FindProperty("blinkDuration").floatValue = 0.22f;
            sb.FindProperty("doubleBlinkChance").floatValue = 0.2f;
            sb.ApplyModifiedProperties();
            idle.Add(blink);

            TailAnimator tailIdle = tail1T.gameObject.AddComponent<TailAnimator>();
            tailIdle.Chains = new[] { new WaveChain { segments = new[] { tail1T, tail2T } } };
            tailIdle.Configure(5f, 0.35f, 0.15f);
            idle.Add(tailIdle);

            // ── Chuyển động: FLY (vỗ cánh bay tại chỗ) ──
            List<ToolkitAnimator> fly = new List<ToolkitAnimator>();
            FlapAnimator flap = bird.gameObject.AddComponent<FlapAnimator>();
            flap.Wings = new[] { new FlapWing { wing = wingNear, side = 1f } };
            flap.Body = bird;
            flap.Configure(4.5f, 115f, 0.16f);
            flap.SetAngles(-82f, 36f);               // đỉnh nhịp: cánh dựng lên; đáy nhịp: cánh quạt xuống
            fly.Add(flap);

            FlapAnimator flapFar = bird.gameObject.AddComponent<FlapAnimator>();
            flapFar.Wings = new[] { new FlapWing { wing = wingFar, side = 1f } };
            flapFar.Configure(4.5f, 115f, 0f);
            flapFar.SetAngles(-74f, 30f);
            flapFar.TimeOffset = 0.035f;             // cánh xa trễ nhẹ → có chiều sâu
            fly.Add(flapFar);

            fly.Add(AnimKit.MoveBy(bird, new Vector3(0f, 0.45f, 0f), 0.6f, EaseType.CubicOut)); // nhấc khỏi cành và giữ độ cao
            fly.Add(AnimKit.Keyframes(legL, AnimLoopMode.Once, Key(0f, 0, 0, 0), TuckKey(0.4f)));  // co chân lại
            fly.Add(AnimKit.Keyframes(legR, AnimLoopMode.Once, Key(0f, 0, 0, 0), TuckKey(0.45f)));
            fly.Add(AnimKit.Keyframes(headT, AnimLoopMode.Once, Key(0f, 0, 0, 0), Key(0.5f, 0.05f, 0.02f, -6, EaseType.CubicOut))); // đầu hơi chúi tới

            SineWaveAnimator headBob = headT.gameObject.AddComponent<SineWaveAnimator>();
            headBob.Configure(new Vector3(0f, 0.04f, 0f), 0f, 4.5f, 0.3f);
            fly.Add(headBob);

            TailAnimator tailFly = tail1T.gameObject.AddComponent<TailAnimator>();
            tailFly.Chains = new[] { new WaveChain { segments = new[] { tail1T, tail2T } } };
            tailFly.Configure(9f, 2.25f, 0.12f);
            fly.Add(tailFly);

            // ── 2 trạng thái + chuyển có cross-fade ──
            AnimStateSet set = bird.gameObject.AddComponent<AnimStateSet>();
            SerializedObject so = new SerializedObject(set);
            SerializedProperty st = so.FindProperty("states");
            st.arraySize = 2;
            SetState(st.GetArrayElementAtIndex(0), "Idle", idle);
            SetState(st.GetArrayElementAtIndex(1), "Fly", fly);
            so.FindProperty("startState").stringValue = "Idle";
            so.ApplyModifiedProperties();
            for (int i = 0; i < fly.Count; i++) fly[i].enabled = false; // lúc edit chỉ Idle chạy; AnimStateSet bật/tắt khi Play

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[BirdSample] Scene saved: " + ScenePath);
        }

        static PoseKey Key(float t, float x, float y, float rotZ, EaseType ease = EaseType.SineInOut)
        {
            return AnimKit.Key(t, new Vector3(x, y, 0f), rotZ, 1f, ease);
        }

        static PoseKey TuckKey(float t)
        {
            return new PoseKey { time = t, position = Vector3.zero, euler = new Vector3(0f, 0f, -92f), scale = new Vector3(1f, 0.45f, 1f), ease = EaseType.CubicOut };
        }

        static void SetState(SerializedProperty p, string name, List<ToolkitAnimator> animators)
        {
            p.FindPropertyRelative("name").stringValue = name;
            // Cả Idle và Fly là vòng lặp: loop=false thì state đứng hình sau lượt đầu (Fly chỉ chạy 0.6s rồi đóng băng)
            p.FindPropertyRelative("loop").boolValue = true;
            SerializedProperty arr = p.FindPropertyRelative("animators");
            arr.arraySize = animators.Count;
            for (int i = 0; i < animators.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = animators[i];
        }

        // ═════════════ Sprites ═════════════

        static Sprite Body()
        {
            int W = 200, H = 180; float cx = 100, cy = 88;
            return Make("bird_body", W, H, new Vector2(cx, cy - 76), (x, y) =>
            {
                float d = Ellipse(x, y, cx, cy, 88f, 76f);
                float t = Mathf.Clamp01((y - 12f) / 152f);
                Color back = Color.Lerp(BlueM, BlueD, Mathf.SmoothStep(0.45f, 1f, t));
                Color c = Fill(back, Navy, d, 3.5f);
                if (d < -2f)
                {
                    // bụng cam-kem, chuyển màu mềm với lưng xanh
                    float bd = Ellipse(x, y, cx + 14f, cy - 28f, 68f, 54f);
                    float ba = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-bd / 16f));
                    Color belly = Color.Lerp(Orange, Cream, Mathf.SmoothStep(0.1f, 0.9f, (y - 30f) / 80f));
                    // hoa văn lông ngực: vòng cung nhỏ so le
                    const float size = 20f;
                    float rowH = size * 0.5f; int row = Mathf.FloorToInt(y / rowH); float off = (row & 1) * size * 0.5f;
                    float lx = (Mathf.Floor((x + off) / size) + 0.5f) * size - off, ly = row * rowH;
                    float ring = Mathf.Abs(Mathf.Sqrt((x - lx) * (x - lx) + (y - ly) * (y - ly)) - size * 0.52f);
                    belly = Color.Lerp(belly, new Color(0.9f, 0.55f, 0.3f), 0.28f * Cov(ring - 1f));
                    Color inside = Color.Lerp(c, belly, ba);
                    inside.a = c.a;
                    c = Color.Lerp(c, inside, Mathf.Clamp01((-d) / 3f));
                    // ánh sáng nhẹ trên lưng
                    float hl = RotEllipse(x, y, cx - 30f, cy + 44f, 38f, 15f, 70f);
                    c = Over(new Color(1f, 1f, 1f, 0.28f * Mathf.Clamp01(-hl / 10f)), c);
                }
                return c;
            });
        }

        static Sprite Head()
        {
            int W = 178, H = 190; float cx = 84, cy = 80, rr = 62;
            return Make("bird_head", W, H, new Vector2(cx, cy - rr * 0.8f), (x, y) =>
            {
                float d0 = Ellipse(x, y, cx, cy, rr, rr * 0.97f);
                // mào: 3 chùm lông ngả ra sau
                float t1 = RotEllipse(x, y, cx - 20f, cy + 60f, 8f, 26f, 28f);
                float t2 = RotEllipse(x, y, cx - 5f, cy + 66f, 8.5f, 28f, 12f);
                float t3 = RotEllipse(x, y, cx + 10f, cy + 61f, 7.5f, 24f, -6f);
                float crest = Mathf.Min(t1, Mathf.Min(t2, t3));
                float d = Mathf.Min(d0, crest);
                float t = Mathf.Clamp01((y - (cy - rr)) / (2f * rr + 30f));
                Color col = Color.Lerp(BlueL, BlueM, Mathf.SmoothStep(0.35f, 1f, t));
                if (crest < d0) col = Color.Lerp(col, BlueD, 0.35f);
                Color c = Fill(col, Navy, d, 3.5f);
                if (d0 < -2f)
                {
                    // má hồng mềm
                    float cheek = Ellipse(x, y, cx + 28f, cy - 16f, 19f, 13f);
                    c = Over(new Color(1f, 0.5f, 0.6f, 0.6f * Mathf.Clamp01(-cheek / 10f)), c);
                    float hl = RotEllipse(x, y, cx - 24f, cy + 34f, 22f, 10f, 40f);
                    c = Over(new Color(1f, 1f, 1f, 0.3f * Mathf.Clamp01(-hl / 7f)), c);
                }
                // mỏ (tam giác bo tròn), đè lên đầu
                Vector2 p = new Vector2(x, y);
                float bd = TriSd(p, new Vector2(cx + 50f, cy + 9f), new Vector2(cx + 50f, cy - 11f), new Vector2(cx + 88f, cy - 2f)) - 5f;
                if (bd < 1f)
                {
                    Color beak = Color.Lerp(BeakA, new Color(1f, 0.58f, 0.15f), Mathf.Clamp01((cy + 12f - y) / 26f));
                    Color bc = Fill(beak, BeakLine, bd, 2.5f);
                    float line = DistSeg(p, new Vector2(cx + 52f, cy - 3f), new Vector2(cx + 84f, cy - 3.5f)) - 1.2f;
                    bc = Over(new Color(BeakLine.r, BeakLine.g, BeakLine.b, Cov(line)), bc);
                    c = Over(bc, c);
                }
                return c;
            });
        }

        static Sprite EyeOpen()
        {
            int W = 52, H = 60; float cx = 26, cy = 30;
            return Make("bird_eye_open", W, H, new Vector2(cx, cy), (x, y) =>
            {
                float d = Ellipse(x, y, cx, cy, 17f, 21f);
                Color c = Fill(new Color(0.07f, 0.07f, 0.17f), Navy, d, 2.5f);
                if (d < -2f)
                {
                    float iris = Ellipse(x, y, cx, cy - 1f, 13.5f, 17.5f);
                    c = Over(Fill(new Color(0.14f, 0.2f, 0.45f), new Color(0.07f, 0.07f, 0.17f), iris, 4f), c);
                    float h1 = Ellipse(x, y, cx + 5f, cy + 8f, 5.8f, 5.8f);
                    float h2 = Ellipse(x, y, cx - 6f, cy - 9f, 2.8f, 2.8f);
                    c = Over(new Color(1f, 1f, 1f, Cov(Mathf.Min(h1, h2))), c);
                }
                return c;
            });
        }

        static Sprite EyeClosed()
        {
            int W = 52, H = 60; float cx = 26, cy = 30;
            return Make("bird_eye_closed", W, H, new Vector2(cx, cy), (x, y) =>
            {
                float dx = x - cx;
                if (Mathf.Abs(dx) > 19f) return Clear;
                float f = cy - 4f + 0.032f * dx * dx;   // ‿ mắt nhắm thư giãn
                float slope = 0.064f * dx;
                float dist = Mathf.Abs(y - f) / Mathf.Sqrt(1f + slope * slope) - 2.4f;
                float end = Mathf.Max(0f, Mathf.Abs(dx) - 17f);
                return Fill(Navy, Navy, dist + end, 1f);
            });
        }

        /// <summary>Cánh nhìn nghiêng: pivot ở vai (đầu phải sprite), thân cánh chỉ về bên trái; mép dưới có vân lông.</summary>
        static Sprite Wing()
        {
            int W = 196, H = 118; float px = 176, py = 66; float L = 160;
            return Make("bird_wing", W, H, new Vector2(px, py), (x, y) =>
            {
                float u = px - x, v = y - py;
                float tt = Mathf.Clamp01(u / L);
                float w = 38f * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Pow(tt, 2.6f)), 0.55f) + 5f;
                float upper = w * 0.62f;
                float scallop = u > 55f ? 0.13f * w * Mathf.Abs(Mathf.Sin(Mathf.PI * u / 24f)) : 0f;
                float lower = w * (1f - 0.1f) + scallop * 1.4f;
                float dv = Mathf.Max(v - upper, -v - lower);
                float dEnd = Mathf.Max(-u, u - L);
                float d = Mathf.Max(dv, dEnd);
                float dBase = Ellipse(x, y, px, py, 30f, 30f);
                d = Mathf.Min(d, dBase);
                // màu: lông phủ (trên, gần vai) sáng hơn, lông bay (dưới, về đầu cánh) đậm hơn
                float cov = Mathf.Clamp01((v + upper) / (upper + lower));
                Color col = Color.Lerp(BlueM, BlueD, Mathf.SmoothStep(0.15f, 0.95f, 1f - cov));
                col = Color.Lerp(col, BlueL, 0.55f * Mathf.SmoothStep(0.5f, 0f, tt) * (cov));
                Color c = Fill(col, Navy, d, 3f);
                if (d < -2f)
                {
                    float k = Mathf.Repeat(u * 0.9f - v * 0.55f, 22f);
                    c = Over(new Color(Navy.r, Navy.g, Navy.b, 0.32f * Cov(k - 1.6f < 0f ? -1f : (k - 1.6f))), c);
                    // viền sáng ở mép lông bay
                    float edge = Mathf.Clamp01(1f - (-d) / 7f) * Mathf.SmoothStep(0.25f, 0.9f, tt);
                    c = Over(new Color(0.75f, 0.9f, 1f, 0.55f * edge), c);
                }
                return c;
            });
        }

        static Sprite TailSeg(string name, int W, int H, float len, float hwBase, float hwTip)
        {
            float px = W - 10, cy = H * 0.5f;
            return Make(name, W, H, new Vector2(px, cy), (x, y) =>
            {
                float left = px - len;
                float d = Tapered(y, x, cy, left, px, hwBase, hwTip, 16f); // chuỗi nằm ngang: gốc ở bên phải
                float t = Mathf.Clamp01((px - x) / len);
                Color col = Color.Lerp(BlueD, BlueM, 0.3f + 0.3f * t);
                Color c = Fill(col, Navy, d, 3f);
                if (d < -2f)
                {
                    float mid = Mathf.Abs(y - cy) - 1.4f;
                    c = Over(new Color(BlueL.r, BlueL.g, BlueL.b, 0.55f * Cov(mid)), c);
                    float tip = Mathf.SmoothStep(0.7f, 1f, t);
                    c = Over(new Color(0.8f, 0.92f, 1f, 0.35f * tip), c);
                }
                return c;
            });
        }

        static Sprite Leg()
        {
            int W = 46, H = 88;
            return Make("bird_leg", W, H, new Vector2(23, H - 6), (x, y) =>
            {
                Color fill = new Color(1f, 0.66f, 0.25f), line = new Color(0.65f, 0.32f, 0.1f);
                Vector2 p = new Vector2(x, y);
                float shin = Tapered(x, y, 23f, 12f, H - 6f, 3.6f, 3f, 3f);
                float t1 = DistSeg(p, new Vector2(23, 15), new Vector2(40, 8)) - 2.6f;
                float t2 = DistSeg(p, new Vector2(23, 15), new Vector2(34, 5)) - 2.6f;
                float t3 = DistSeg(p, new Vector2(23, 15), new Vector2(9, 9)) - 2.6f;
                return Fill(fill, line, Mathf.Min(shin, Mathf.Min(t1, Mathf.Min(t2, t3))), 1.8f);
            });
        }

        static Sprite Branch()
        {
            int W = 660, H = 150; float cy = 52;
            return Make("branch", W, H, new Vector2(W * 0.5f, cy), (x, y) =>
            {
                float d = Tapered(y, x, cy, 14f, W - 14f, 19f, 15f, 14f);
                float t = Mathf.Clamp01((y - (cy - 19f)) / 38f);
                Color bark = Color.Lerp(new Color(0.42f, 0.27f, 0.17f), new Color(0.64f, 0.44f, 0.27f), t);
                Color c = Fill(bark, new Color(0.22f, 0.13f, 0.08f), d, 3f);
                if (d < -2f)
                {
                    float line = Mathf.Abs(Mathf.Sin((x * 0.045f) + (y * 0.2f))) ;
                    c = Over(new Color(0.2f, 0.12f, 0.07f, 0.35f * Mathf.SmoothStep(0.9f, 1f, line)), c);
                    float knot = Ellipse(x, y, 190f, cy + 2f, 11f, 8f);
                    c = Over(Fill(new Color(0.34f, 0.2f, 0.12f), new Color(0.2f, 0.12f, 0.07f), knot, 2f), c);
                }
                // lá: các cặp lá ở hai đầu cành
                float[,] leaves = { { 560, 78, 38 }, { 596, 36, -52 }, { 616, 70, 10 }, { 80, 40, 64 }, { 50, 70, -30 } };
                for (int i = 0; i < leaves.GetLength(0); i++)
                {
                    float ld = RotEllipse(x, y, leaves[i, 0], leaves[i, 1], 15f, 34f, leaves[i, 2]);
                    if (ld < 1f)
                    {
                        float lt = Mathf.Clamp01(0.5f + (y - leaves[i, 1]) / 60f);
                        Color lc = Fill(Color.Lerp(new Color(0.2f, 0.55f, 0.27f), new Color(0.45f, 0.82f, 0.4f), lt), new Color(0.1f, 0.3f, 0.15f), ld, 2.5f);
                        float rib = Mathf.Abs(RotRibDist(x, y, leaves[i, 0], leaves[i, 1], leaves[i, 2])) - 0.9f;
                        lc = Over(new Color(0.75f, 0.95f, 0.6f, 0.6f * Cov(rib)), lc);
                        c = Over(lc, c);
                    }
                }
                return c;
            });
        }

        static Sprite Cloud()
        {
            int W = 300, H = 140;
            return Make("cloud", W, H, new Vector2(W * 0.5f, H * 0.5f), (x, y) =>
            {
                float d = Mathf.Min(Mathf.Min(Ellipse(x, y, 80, 58, 44, 36), Ellipse(x, y, 140, 76, 56, 48)), Mathf.Min(Ellipse(x, y, 200, 60, 46, 38), Ellipse(x, y, 245, 52, 32, 28)));
                d = Mathf.Min(d, Ellipse(x, y, 150f, 38f, 112f, 24f)); // đáy mây tròn, không cắt phẳng
                float t = Mathf.Clamp01((y - 20f) / 80f);
                Color col = Color.Lerp(new Color(0.84f, 0.92f, 1f), Color.white, Mathf.SmoothStep(0f, 0.7f, t));
                Color c = Fill(col, col, d, 1f);
                return c;
            });
        }

        // ═════════════ Drawing helpers ═════════════

        static float Cov(float d) { return Mathf.Clamp01(0.5f - d); }

        static Color Fill(Color fill, Color outline, float d, float ow)
        {
            float a = Cov(d);
            if (a <= 0f) return Clear;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((-d - 0.5f) / ow));
            Color c = Color.Lerp(outline, fill, t);
            c.a = a * fill.a;
            return c;
        }

        static Color Over(Color top, Color bottom)
        {
            float a = top.a + bottom.a * (1f - top.a);
            if (a <= 0.0001f) return Clear;
            Color c = (top * top.a + bottom * bottom.a * (1f - top.a)) / a;
            c.a = a;
            return c;
        }

        static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float nx = (x - cx) / rx, ny = (y - cy) / ry;
            return (Mathf.Sqrt(nx * nx + ny * ny) - 1f) * Mathf.Min(rx, ry);
        }

        /// <summary>Elip xoay: rx là nửa trục ngang, ry nửa trục dọc, deg xoay ngược chiều kim đồng hồ.</summary>
        static float RotEllipse(float x, float y, float cx, float cy, float rx, float ry, float deg)
        {
            float a = deg * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            float dx = x - cx, dy = y - cy;
            float lx = dx * ca + dy * sa, ly = -dx * sa + dy * ca;
            return Ellipse(lx, ly, 0f, 0f, rx, ry);
        }

        static float RotRibDist(float x, float y, float cx, float cy, float deg)
        {
            float a = deg * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            float dx = x - cx, dy = y - cy;
            return dx * ca + dy * sa; // khoảng cách ngang tới trục dọc của lá
        }

        static float DistSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        static float TriSd(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 e0 = b - a, e1 = c - b, e2 = a - c;
            Vector2 v0 = p - a, v1 = p - b, v2 = p - c;
            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            float dx = Mathf.Min(Mathf.Min(Vector2.Dot(pq0, pq0), Vector2.Dot(pq1, pq1)), Vector2.Dot(pq2, pq2));
            float dy = Mathf.Min(Mathf.Min(s * (v0.x * e0.y - v0.y * e0.x), s * (v1.x * e1.y - v1.y * e1.x)), s * (v2.x * e2.y - v2.y * e2.x));
            return -Mathf.Sqrt(dx) * Mathf.Sign(dy);
        }

        /// <summary>Hộp bo góc có bề rộng nội suy từ "top" xuống "bottom" (dùng cho đuôi, chân, cành).</summary>
        static float Tapered(float x, float y, float cx, float bottom, float top, float hwTop, float hwBot, float radius)
        {
            float t = Mathf.Clamp01((top - y) / Mathf.Max(1f, top - bottom));
            float hw = Mathf.Lerp(hwTop, hwBot, t);
            float hh = (top - bottom) * 0.5f;
            float r = Mathf.Min(radius, Mathf.Min(hw, hh));
            float qx = Mathf.Abs(x - cx) - (hw - r);
            float qy = Mathf.Abs(y - (top + bottom) * 0.5f) - (hh - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static Sprite Make(string name, int w, int h, Vector2 pivotPx, Shade fn)
        {
            string path = ArtDir + "/" + name + ".png";
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = fn(x + 0.5f, y + 0.5f);
            tex.SetPixels(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 100f;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.filterMode = FilterMode.Bilinear;
            TextureImporterSettings s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            imp.SetTextureSettings(s);
            imp.spritePivot = new Vector2(pivotPx.x / w, pivotPx.y / h);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
