using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AnimationToolkit.EditorTools
{
    /// <summary>
    /// Kiểm tra "hở art" khi animation chạy: art được cắt thành layer thì lúc cử động có thể lộ KHE NỨT (giữa chân và đùi...) hoặc LỖ THỦNG.
    /// Chạy từng animation (từng khung 1/30s) trên camera tạm, đếm pixel trống mà hai phía trong ~4 texel đều đặc (khe) hoặc
    /// trống nhưng không nối ra ngoài (lỗ), rồi bỏ những khe đã có sẵn lúc nghỉ. Chuyển động cả thân không bị tính là hở.
    /// Dùng bởi skill sau mỗi lần cắt art. Không đụng camera của scene; mọi thay đổi tạm được khôi phục.
    /// </summary>
    public static class AnimGapCheck
    {
        public sealed class Result
        {
            public string state;
            public int maxNewGap;
            public int worstFrame;
            public float meanNewGap;
            public float maxRatio;      // maxNewGap / số pixel đặc lúc nghỉ
            public string mapPath;
            public bool Pass { get { return maxRatio < 0.0015f; } }
        }

        /// <param name="root">Gốc của đối tượng đã animate.</param>
        /// <param name="ignoreNames">Tên GameObject không tính vào thân (bóng, bong bóng, nền...).</param>
        public static List<Result> Run(GameObject root, string[] ignoreNames, string mapDir, int frames = 240)
        {
            List<Result> results = new List<Result>();
            SpriteRenderer[] srs = root.GetComponentsInChildren<SpriteRenderer>(false);
            List<SpriteRenderer> body = new List<SpriteRenderer>();
            for (int i = 0; i < srs.Length; i++)
            {
                bool skip = false;
                if (ignoreNames != null) for (int k = 0; k < ignoreNames.Length; k++) if (srs[i].name == ignoreNames[k]) skip = true;
                if (!skip) body.Add(srs[i]);
            }
            if (body.Count == 0) return results;

            // Dựng khung: bao quanh mọi sprite lúc nghỉ, chừa lề 25% (để thân nảy/lắc không ra khỏi khung)
            ToolkitAnimator[] all = root.GetComponentsInChildren<ToolkitAnimator>(true);
            Dictionary<ToolkitAnimator, bool> savedEnabled = new Dictionary<ToolkitAnimator, bool>();
            for (int i = 0; i < all.Length; i++) { savedEnabled[all[i]] = all[i].enabled; all[i].ClearOffset(); all[i].enabled = false; }
            Bounds bounds = body[0].bounds;
            for (int i = 1; i < body.Count; i++) bounds.Encapsulate(body[i].bounds);
            float h = bounds.size.y * 1.25f, w = bounds.size.x * 1.25f;
            float size = Mathf.Max(h, w / 1.0f) * 0.5f;
            int RH = 600, RW = Mathf.Max(64, Mathf.RoundToInt(RH * (w / h)));

            // Chỉ vẽ thân: đưa các renderer ra layer riêng, các renderer khác bị ẩn bằng cullingMask
            const int Layer = 31;
            Dictionary<GameObject, int> savedLayer = new Dictionary<GameObject, int>();
            for (int i = 0; i < body.Count; i++) { savedLayer[body[i].gameObject] = body[i].gameObject.layer; body[i].gameObject.layer = Layer; }

            GameObject camGo = new GameObject("__GapCheckCam") { hideFlags = HideFlags.HideAndDontSave };
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = h * 0.5f; cam.aspect = (float)RW / RH;
            cam.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z - 10f);
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << Layer;
            RenderTexture rt = new RenderTexture(RW, RH, 24, RenderTextureFormat.ARGB32);
            int R = Mathf.Max(3, RH / 150);

            try
            {
                Color32[] restCol = Render(cam, rt, RW, RH);
                bool[] rest = ToSolid(restCol);
                int restSolid = 0; for (int i = 0; i < rest.Length; i++) if (rest[i]) restSolid++;
                bool[] restCracks = Dil(Cracks(rest, RW, RH, R), RW, RH, R + 2);
                bool[] restEnc = Dil(Enclosed(rest, RW, RH), RW, RH, 3);

                AnimStateSet set = root.GetComponentInChildren<AnimStateSet>(true);
                int groups = set != null && set.Count > 0 ? set.Count : 1;
                for (int g = 0; g < groups; g++)
                {
                    ToolkitAnimator[] anims = set != null && set.Count > 0 ? set.StateAnimators(g) : all;
                    string name = set != null && set.Count > 0 ? set.StateName(g) : "All";
                    for (int i = 0; i < all.Length; i++) { all[i].ClearOffset(); all[i].enabled = false; }
                    for (int i = 0; i < anims.Length; i++) { if (anims[i] == null) continue; anims[i].enabled = true; anims[i].Weight = 1f; anims[i].ResetState(); }

                    int worst = 0, wf = 0; long total = 0; bool[] wS = null, wC = null, wE = null; Color32[] wCol = null;
                    for (int f = 0; f < frames; f++)
                    {
                        for (int i = 0; i < anims.Length; i++) if (anims[i] != null) anims[i].Tick(1f / 30f);
                        Color32[] col = Render(cam, rt, RW, RH);
                        bool[] sol = ToSolid(col);
                        bool[] cr = Cracks(sol, RW, RH, R), en = Enclosed(sol, RW, RH);
                        int n = 0; bool[] cm = new bool[RW * RH], em = new bool[RW * RH];
                        for (int i = 0; i < cr.Length; i++)
                        {
                            if (cr[i] && !restCracks[i]) { n++; cm[i] = true; }
                            if (en[i] && !restEnc[i]) { n++; em[i] = true; }
                        }
                        total += n;
                        if (n > worst || wCol == null) { worst = n; wf = f; wS = sol; wC = cm; wE = em; wCol = col; }
                    }
                    Result r = new Result { state = name, maxNewGap = worst, worstFrame = wf, meanNewGap = total / (float)frames, maxRatio = restSolid > 0 ? worst / (float)restSolid : 0f };
                    if (wS != null && !string.IsNullOrEmpty(mapDir))
                    {
                        Directory.CreateDirectory(mapDir);
                        Texture2D o = new Texture2D(RW, RH, TextureFormat.RGBA32, false);
                        Color32[] px = new Color32[RW * RH];
                        for (int i = 0; i < px.Length; i++)
                        {
                            Color32 c = wS[i] ? new Color32(200, 200, 200, 255) : new Color32(255, 255, 255, 255);
                            if (wC[i]) c = new Color32(255, 0, 0, 255);   // khe nứt mới
                            if (wE[i]) c = new Color32(0, 0, 255, 255);   // lỗ thủng mới
                            px[i] = c;
                        }
                        o.SetPixels32(px);
                        r.mapPath = Path.Combine(mapDir, "gap_" + name + ".png");
                        File.WriteAllBytes(r.mapPath, o.EncodeToPNG());
                        Object.DestroyImmediate(o);

                        // Ảnh màu: [nghỉ | khung tệ nhất] trên nền hồng đậm — chỗ hở/khe hiện thành màu hồng, NHÌN được bằng mắt
                        Texture2D sheet = new Texture2D(RW * 2 + 8, RH, TextureFormat.RGBA32, false);
                        Color32[] sp = new Color32[sheet.width * RH];
                        Color32 magenta = new Color32(255, 0, 200, 255);
                        for (int y = 0; y < RH; y++)
                            for (int x = 0; x < sheet.width; x++)
                            {
                                Color32 c;
                                if (x < RW) c = restCol[y * RW + x];
                                else if (x < RW + 8) c = new Color32(0, 0, 0, 255);
                                else c = wCol[y * RW + (x - RW - 8)];
                                if (c.a < 255) { float a = c.a / 255f; c = new Color32((byte)(c.r * a + magenta.r * (1 - a)), (byte)(c.g * a + magenta.g * (1 - a)), (byte)(c.b * a + magenta.b * (1 - a)), 255); }
                                sp[y * sheet.width + x] = c;
                            }
                        sheet.SetPixels32(sp);
                        File.WriteAllBytes(Path.Combine(mapDir, "motion_" + name + "_rest_vs_worst.png"), sheet.EncodeToPNG());
                        Object.DestroyImmediate(sheet);
                    }
                    results.Add(r);
                }
            }
            finally
            {
                for (int i = 0; i < all.Length; i++) { all[i].ClearOffset(); all[i].enabled = savedEnabled[all[i]]; }
                foreach (KeyValuePair<GameObject, int> kv in savedLayer) if (kv.Key != null) kv.Key.layer = kv.Value;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camGo);
            }
            return results;
        }

        /// <summary>
        /// Kiểm tra RÁC trong từng layer: tách các thành phần liên thông (8 hướng, alpha &gt; 25) của sprite và báo mảnh rời nhỏ.
        /// Mảnh rác thường là một mẩu viền/lông của bộ phận khác lọt vào ô cắt — nó sẽ "bay theo" layer khi animation chạy.
        /// Layer hợp lệ có thể có nhiều mảnh (hai bàn tay) nhưng mỗi mảnh phải đủ lớn và có ý nghĩa; mảnh nhỏ bị đánh dấu FRAGMENT.
        /// </summary>
        public static string AuditLayers(GameObject root)
        {
            StringBuilder sb = new StringBuilder();
            SpriteRenderer[] srs = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int s = 0; s < srs.Length; s++)
            {
                Sprite sp = srs[s].sprite;
                if (sp == null || sp.texture == null) continue;
                string path = AssetDatabase.GetAssetPath(sp.texture);
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                t.LoadImage(File.ReadAllBytes(path));
                int w = t.width, h = t.height;
                Color32[] p = t.GetPixels32();
                Object.DestroyImmediate(t);

                int[] label = new int[w * h];
                List<int> sizes = new List<int>(); List<Vector2> centers = new List<Vector2>();
                Queue<int> q = new Queue<int>();
                int total = 0;
                for (int i = 0; i < p.Length; i++)
                {
                    if (p[i].a <= 25 || label[i] != 0) continue;
                    int id = sizes.Count + 1; int n = 0; float cx = 0, cy = 0;
                    q.Enqueue(i); label[i] = id;
                    while (q.Count > 0)
                    {
                        int k = q.Dequeue(); int x = k % w, y = k / w; n++; cx += x; cy += y;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                                int j = yy * w + xx; if (label[j] != 0 || p[j].a <= 25) continue;
                                label[j] = id; q.Enqueue(j);
                            }
                    }
                    sizes.Add(n); centers.Add(new Vector2(cx / n, cy / n)); total += n;
                }
                if (sizes.Count == 0) { sb.AppendLine("EMPTY  " + srs[s].name + " (" + sp.name + ")"); continue; }
                int max = 0; for (int i = 0; i < sizes.Count; i++) max = Mathf.Max(max, sizes[i]);
                List<string> frag = new List<string>();
                for (int i = 0; i < sizes.Count; i++)
                    if (sizes[i] < Mathf.Max(12, Mathf.RoundToInt(total * 0.03f)))
                        frag.Add(sizes[i] + "px at (" + centers[i].x.ToString("0") + "," + centers[i].y.ToString("0") + ") of " + w + "x" + h);
                sb.AppendLine((frag.Count == 0 ? "ok     " : "FRAGMENT ") + srs[s].name + ": " + sizes.Count + " piece(s), largest " + max + "px" + (frag.Count > 0 ? "  junk: " + string.Join("; ", frag.ToArray()) : ""));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Vẽ RIÊNG các renderer có tên trong names (lúc nghỉ) ra PNG trên nền hồng đậm. Dùng để xem base một mình: sau khi cắt, base phải là thân sạch
        /// với màu phẳng ở chỗ đã khoét; bất kỳ nét tối/mảnh viền lạ nào còn nằm trong base ở chỗ bộ phận di chuyển là phần LẼ RA phải đi theo
        /// bộ phận đó (nó sẽ đứng yên khi bộ phận cử động → "mất viền").
        /// </summary>
        public static void RenderOnly(GameObject root, string[] names, string pngPath, int height = 900)
        {
            SpriteRenderer[] srs = root.GetComponentsInChildren<SpriteRenderer>(false);
            List<SpriteRenderer> pick = new List<SpriteRenderer>();
            for (int i = 0; i < srs.Length; i++) for (int k = 0; k < names.Length; k++) if (srs[i].name == names[k]) pick.Add(srs[i]);
            if (pick.Count == 0) return;
            ToolkitAnimator[] all = root.GetComponentsInChildren<ToolkitAnimator>(true);
            for (int i = 0; i < all.Length; i++) all[i].ClearOffset();
            Bounds b = pick[0].bounds; for (int i = 1; i < pick.Count; i++) b.Encapsulate(pick[i].bounds);
            const int Layer = 31;
            Dictionary<GameObject, int> saved = new Dictionary<GameObject, int>();
            for (int i = 0; i < pick.Count; i++) { saved[pick[i].gameObject] = pick[i].gameObject.layer; pick[i].gameObject.layer = Layer; }
            float h = b.size.y * 1.1f, w = b.size.x * 1.1f;
            int RH = height, RW = Mathf.Max(64, Mathf.RoundToInt(RH * (w / h)));
            GameObject camGo = new GameObject("__RenderOnlyCam") { hideFlags = HideFlags.HideAndDontSave };
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = h * 0.5f; cam.aspect = (float)RW / RH;
            cam.transform.position = new Vector3(b.center.x, b.center.y, b.center.z - 10f);
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0); cam.cullingMask = 1 << Layer;
            RenderTexture rt = new RenderTexture(RW, RH, 24, RenderTextureFormat.ARGB32);
            try
            {
                Color32[] px = Render(cam, rt, RW, RH);
                Color32 mg = new Color32(255, 0, 200, 255);
                for (int i = 0; i < px.Length; i++)
                {
                    if (px[i].a >= 255) continue;
                    float a = px[i].a / 255f;
                    px[i] = new Color32((byte)(px[i].r * a + mg.r * (1 - a)), (byte)(px[i].g * a + mg.g * (1 - a)), (byte)(px[i].b * a + mg.b * (1 - a)), 255);
                }
                Texture2D t = new Texture2D(RW, RH, TextureFormat.RGBA32, false);
                t.SetPixels32(px);
                Directory.CreateDirectory(Path.GetDirectoryName(pngPath));
                File.WriteAllBytes(pngPath, t.EncodeToPNG());
                Object.DestroyImmediate(t);
            }
            finally
            {
                foreach (KeyValuePair<GameObject, int> kv in saved) if (kv.Key != null) kv.Key.layer = kv.Value;
                rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(camGo);
            }
        }

        public static string Report(List<Result> results)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < results.Count; i++)
            {
                Result r = results[i];
                sb.AppendLine((r.Pass ? "PASS " : "FAIL ") + r.state + ": new gap px max=" + r.maxNewGap + " (frame " + r.worstFrame + ") mean=" + r.meanNewGap.ToString("F1") + " ratio=" + (r.maxRatio * 100f).ToString("F3") + "%  map=" + r.mapPath);
            }
            return sb.ToString();
        }

        // ─── ảnh nhị phân ───
        static Color32[] Render(Camera cam, RenderTexture rt, int RW, int RH)
        {
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            RenderTexture prev = RenderTexture.active; RenderTexture.active = rt;
            Texture2D t = new Texture2D(RW, RH, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, RW, RH), 0, 0);
            RenderTexture.active = prev;
            Color32[] p = t.GetPixels32();
            Object.DestroyImmediate(t);
            return p;
        }

        static bool[] ToSolid(Color32[] p)
        {
            bool[] a = new bool[p.Length];
            for (int i = 0; i < a.Length; i++) a[i] = p[i].a > 128;
            return a;
        }

        static bool[] Cracks(bool[] s, int RW, int RH, int R)
        {
            bool[] o = new bool[RW * RH];
            int[] nextS = new int[Mathf.Max(RW, RH)];
            for (int y = 0; y < RH; y++)
            {
                int ns = 1 << 20;
                for (int x = RW - 1; x >= 0; x--) { if (s[y * RW + x]) ns = x; nextS[x] = ns; }
                int last = -(1 << 20);
                for (int x = 0; x < RW; x++)
                {
                    if (s[y * RW + x]) { last = x; continue; }
                    if (x - last <= R && nextS[x] - x <= R) o[y * RW + x] = true;
                }
            }
            for (int x = 0; x < RW; x++)
            {
                int ns = 1 << 20;
                for (int y = RH - 1; y >= 0; y--) { if (s[y * RW + x]) ns = y; nextS[y] = ns; }
                int last = -(1 << 20);
                for (int y = 0; y < RH; y++)
                {
                    if (s[y * RW + x]) { last = y; continue; }
                    if (y - last <= R && nextS[y] - y <= R) o[y * RW + x] = true;
                }
            }
            return o;
        }

        static bool[] Dil(bool[] m, int RW, int RH, int r)
        {
            bool[] o = new bool[m.Length];
            for (int y = 0; y < RH; y++)
                for (int x = 0; x < RW; x++)
                {
                    if (!m[y * RW + x]) continue;
                    for (int dy = -r; dy <= r; dy++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx >= 0 && yy >= 0 && xx < RW && yy < RH) o[yy * RW + xx] = true;
                        }
                }
            return o;
        }

        static bool[] Enclosed(bool[] s, int RW, int RH)
        {
            bool[] outside = new bool[RW * RH];
            Queue<int> q = new Queue<int>();
            for (int x = 0; x < RW; x++) { q.Enqueue(x); q.Enqueue((RH - 1) * RW + x); }
            for (int y = 0; y < RH; y++) { q.Enqueue(y * RW); q.Enqueue(y * RW + RW - 1); }
            while (q.Count > 0)
            {
                int i = q.Dequeue();
                if (outside[i] || s[i]) continue;
                outside[i] = true;
                int x = i % RW, y = i / RW;
                if (x > 0) q.Enqueue(i - 1); if (x < RW - 1) q.Enqueue(i + 1); if (y > 0) q.Enqueue(i - RW); if (y < RH - 1) q.Enqueue(i + RW);
            }
            bool[] o = new bool[RW * RH];
            for (int i = 0; i < o.Length; i++) o[i] = !s[i] && !outside[i];
            return o;
        }
    }
}
