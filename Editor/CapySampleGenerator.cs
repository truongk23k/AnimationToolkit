using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnimationToolkit.EditorTools
{
    /// <summary>
    /// Tách ảnh phẳng capy.png (capybara ăn dưa hấu) thành các layer có alpha: base, mõm, dưa hấu, hai tay, hai chân, tai, trái tim, bóng.
    /// Phần bị khoét khỏi base được vá bằng màu lông xung quanh để bộ phận cử động không lộ lỗ. Sau đó dựng scene CapyAnimTest
    /// với 2 animation (Eat, Happy) trong AnimStateSet. Được gọi bởi skill, không có menu.
    /// </summary>
    public static class CapySampleGenerator
    {
        const string SrcPath = "Assets/AnimationToolkit/Samples/CapyArt/capy.png";
        const string PartsDir = "Assets/AnimationToolkit/Samples/CapyArt/Parts";
        const string ScenePath = "Assets/AnimationToolkit/Samples/CapyAnimTest.unity";

        static int W, H;
        static Color32[] S;                      // ảnh gốc, y từ trên xuống
        static Color32 Bg, ShadowCol;
        static readonly Color32 Outline = new Color32(42, 22, 50, 255);

        static bool[] bgM, shM, melon, hands, feetL, feetR, bubble, ear;
        static float[] snoutA; static Color32[] snoutC;

        // ═════════ Tiện ích ảnh ═════════
        static float Dist(Color32 a, Color32 b)
        {
            float r = a.r - b.r, g = a.g - b.g, bl = a.b - b.b;
            return Mathf.Sqrt(r * r + g * g + bl * bl);
        }
        static void Hsv(Color32 c, out float h, out float s, out float v) { Color.RGBToHSV(c, out h, out s, out v); }

        /// <summary>Màu lông bụng/đầu (nâu cam sáng). Dùng để (1) không lấy nhầm lông vào layer, (2) làm nguồn duy nhất khi vá lỗ.</summary>
        static bool IsBelly(Color32 c)
        {
            float h, s, v; Color.RGBToHSV(c, out h, out s, out v);
            return h > 0.035f && h < 0.115f && s > 0.35f && s < 0.85f && v > 0.58f && v < 0.92f;
        }
        static bool InR(int x, int y, int x0, int y0, int x1, int y1) { return x >= x0 && x <= x1 && y >= y0 && y <= y1; }
        static int Cnt(bool[] m) { int n = 0; for (int i = 0; i < m.Length; i++) if (m[i]) n++; return n; }

        static bool[] Dil(bool[] m, int r)
        {
            bool[] o = new bool[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (!m[y * W + x]) continue;
                    for (int dy = -r; dy <= r; dy++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx >= 0 && yy >= 0 && xx < W && yy < H) o[yy * W + xx] = true;
                        }
                }
            return o;
        }

        static bool[] Ero(bool[] m, int r)
        {
            bool[] inv = new bool[W * H];
            for (int i = 0; i < inv.Length; i++) inv[i] = !m[i];
            bool[] d = Dil(inv, r);
            bool[] o = new bool[W * H];
            for (int i = 0; i < o.Length; i++) o[i] = !d[i];
            return o;
        }

        static void Load()
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(SrcPath));
            W = tex.width; H = tex.height;
            Color32[] raw = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            S = new Color32[W * H];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) S[y * W + x] = raw[(H - 1 - y) * W + x];
            Bg = S[2 * W + 2];
        }

        // ═════════ Tách mặt nạ ═════════
        static void Segment()
        {
            int N = W * H;
            bgM = new bool[N]; shM = new bool[N];
            Queue<int> q = new Queue<int>();
            for (int x = 0; x < W; x++) { q.Enqueue(x); q.Enqueue((H - 1) * W + x); }
            for (int y = 0; y < H; y++) { q.Enqueue(y * W); q.Enqueue(y * W + W - 1); }
            while (q.Count > 0)
            {
                int i = q.Dequeue();
                if (bgM[i] || Dist(S[i], Bg) >= 22f) continue;
                bgM[i] = true;
                int x = i % W, y = i / W;
                if (x > 0) q.Enqueue(i - 1); if (x < W - 1) q.Enqueue(i + 1); if (y > 0) q.Enqueue(i - W); if (y < H - 1) q.Enqueue(i + W);
            }
            ShadowCol = S[200 * W + 60];
            q.Clear(); q.Enqueue(200 * W + 60);
            while (q.Count > 0)
            {
                int i = q.Dequeue();
                if (shM[i] || bgM[i] || Dist(S[i], ShadowCol) > 30f) continue;
                shM[i] = true;
                int x = i % W, y = i / W;
                if (y < 186) continue;
                if (x > 0) q.Enqueue(i - 1); if (x < W - 1) q.Enqueue(i + 1); if (y > 0) q.Enqueue(i - W); if (y < H - 1) q.Enqueue(i + W);
            }

            // dưa hấu: màu đỏ/hồng, xanh vỏ, trắng vỏ trong; đóng lỗ hạt rồi nở 2px để lấy cả viền
            bool[] fill = new bool[N];
            for (int y = 88; y <= 154; y++)
                for (int x = 42; x <= 144; x++)
                {
                    int i = y * W + x; if (bgM[i]) continue;
                    float h, s, v; Hsv(S[i], out h, out s, out v);
                    bool red = (h < 0.04f || h > 0.92f) && s > 0.25f && v > 0.5f;
                    bool green = h > 0.22f && h < 0.45f && s > 0.3f;
                    bool white = v > 0.86f && s < 0.22f && Dist(S[i], Bg) > 20f;
                    if (red || green || white) fill[i] = true;
                }
            bool[] closed = Ero(Dil(fill, 3), 3);

            // Hai tay: mọi pixel trong ô tay KHÔNG phải màu lông bụng/đầu và không phải ruột dưa → lông tay sẫm + viền
            hands = new bool[N];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x; if (bgM[i]) continue;
                    if (!(InR(x, y, 38, 108, 62, 144) || InR(x, y, 110, 120, 142, 148))) continue;
                    if (!fill[i] && !IsBelly(S[i])) hands[i] = true;
                }

            // Dưa hấu: ruột/vỏ đã đóng lỗ + viền (nở 2px) và MỌI pixel trong ô dưa không phải lông / viền sẫm / mõm
            bool[] full = Dil(closed, 2);
            melon = new bool[N];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (bgM[i] || hands[i]) continue;
                    if (full[i] && (closed[i] || (S[i].r < 110 && S[i].g < 90))) { melon[i] = true; continue; }
                    if (!InR(x, y, 42, 88, 144, 154) || InR(x, y, 70, 52, 116, 101)) continue;
                    if (IsBelly(S[i])) continue;
                    // chỉ nhận đúng màu dưa (hồng/đỏ, xanh vỏ, trắng vỏ trong) — không nhận viền nâu/xám lạc
                    float h, s, v; Hsv(S[i], out h, out s, out v);
                    bool redish = (h < 0.06f || h > 0.88f) && s > 0.15f && v > 0.45f;
                    bool greenish = h > 0.2f && h < 0.5f && s > 0.2f;
                    bool whitish = v > 0.8f && s < 0.3f && Dist(S[i], Bg) > 12f;
                    if (redish || greenish || whitish) melon[i] = true;
                }

            // Hai chân: mọi pixel trong ô chân không phải lông bụng
            feetL = new bool[N]; feetR = new bool[N];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x; if (bgM[i] || shM[i] || melon[i] || hands[i]) continue;
                    bool inL = InR(x, y, 30, 163, 52, 198), inR = InR(x, y, 113, 166, 142, 205);
                    if (!inL && !inR) continue;
                    if (IsBelly(S[i])) continue;
                    if (inL) feetL[i] = true; else feetR[i] = true;
                }

            bubble = new bool[N];
            for (int y = 26; y <= 52; y++) for (int x = 56; x <= 82; x++) { int i = y * W + x; if (!bgM[i]) bubble[i] = true; }
            // Tai KHÔNG cắt: tai và đầu cùng một màu nâu phẳng, không có viền nào làm ranh giới → mép cắt luôn lộ thành vệt nhòe.
            // Quy tắc của skill: bộ phận không có ranh giới thật (viền/đổi màu) thì để yên trong base, đừng ép cắt.
            ear = new bool[N];

            // Mõm: matte mềm giữa màu đầu và màu mõm → ghép lại tĩnh vẫn đúng từng pixel
            Color32 head = S[75 * W + 150], snout = S[80 * W + 90];
            float dx0 = snout.r - head.r, dy0 = snout.g - head.g, dz0 = snout.b - head.b;
            float len2 = dx0 * dx0 + dy0 * dy0 + dz0 * dz0;
            snoutA = new float[N]; snoutC = new Color32[N];
            for (int y = 52; y <= 104; y++)
                for (int x = 72; x <= 114; x++)
                {
                    int i = y * W + x;
                    if (bgM[i] || melon[i] || hands[i]) continue;
                    Color32 c = S[i];
                    float h, s, v; Hsv(c, out h, out s, out v);
                    float t = ((c.r - head.r) * dx0 + (c.g - head.g) * dy0 + (c.b - head.b) * dz0) / len2;
                    float rr = c.r - (head.r + dx0 * t), rg = c.g - (head.g + dy0 * t), rb = c.b - (head.b + dz0 * t);
                    float resid = Mathf.Sqrt(rr * rr + rg * rg + rb * rb);
                    // t > 1.2 = tối hơn cả màu mõm (viền đầu nằm trên đường kéo dài đầu→mõm trong không gian màu): KHÔNG phải mõm
                    if (resid < 30f && t > 0.04f && t < 1.2f)
                    {
                        float a = Mathf.Clamp01(t);
                        snoutA[i] = a;
                        // gỡ màu đầu ra khỏi pixel pha trộn để layer chỉ còn màu mõm
                        float inv = 1f - a;
                        snoutC[i] = a > 0.02f
                            ? new Color32((byte)Mathf.Clamp((c.r - head.r * inv) / a, 0, 255), (byte)Mathf.Clamp((c.g - head.g * inv) / a, 0, 255), (byte)Mathf.Clamp((c.b - head.b * inv) / a, 0, 255), 255)
                            : c;
                    }
                    // Vạch mũi/mắt (pixel tối) chỉ nhận trong vùng mũi thật. Nhận cả ô chữ nhật thì lọt luôn nét viền cong của đầu ở góc trên-trái
                    // (mảnh rác 37px bay theo mõm khi nhai).
                    // Trong vùng mũi/mắt thật: MỌI pixel không phải màu đầu thuộc về mõm (hai "mắt" bầu dục + vòng viền nâu sẫm quanh chúng + vạch mũi).
                    // Trước đây chỉ nhận pixel tối nên vòng viền quanh mắt nằm lại trong base và đứng yên khi mõm nhai → mắt "mất viền".
                    // Vùng này bắt đầu từ y=64 để vẫn loại nét viền cong của đầu ở phía trên-trái.
                    // Chỉ nhận pixel TỐI HƠN màu đầu: viền sáng của lông đầu (màu đào) cũng "khác màu đầu" nhưng không phải mõm, nhận vào là bay theo.
                    else if (InR(x, y, 74, 64, 108, 102) && Dist(c, head) > 14f &&
                             (0.3f * c.r + 0.59f * c.g + 0.11f * c.b) < (0.3f * head.r + 0.59f * head.g + 0.11f * head.b) - 12f) { snoutA[i] = 1f; snoutC[i] = c; }
                }

            // Mõm kéo dài XUỐNG DƯỚI dưa hấu (phần bị dưa che): nếu không, khi dưa nhấc lên/mõm nhích thì lộ dải màu đầu giữa mõm và dưa.
            // Phần kéo dài nằm sau dưa (dưa có order cao hơn) nên lúc nghỉ không nhìn thấy.
            for (int x = 72; x <= 114; x++)
            {
                int ys = -1;
                for (int y = 104; y >= 52; y--) if (snoutA[y * W + x] > 0.5f) { ys = y; break; }
                if (ys < 0) continue;
                Color32 c = snoutC[ys * W + x];
                for (int y = ys + 1; y <= ys + 10 && y < H; y++)
                {
                    int i = y * W + x;
                    if (melon[i]) { snoutA[i] = 1f; snoutC[i] = c; }
                }
            }
        }

        // ═════════ Dựng base (vá lỗ) ═════════
        const int EarCut = 44;

        /// <summary>Vùng bên trong thân thật (đầu + thân + đoạn ngực nối giữa) — lỗ ngoài vùng này để trong suốt.</summary>
        static bool FillAllowed(int x, int y)
        {
            float a = (x - 123f) / 62f, b = (y - 68f) / 45f;
            float c = (x - 122f) / 66f, d = (y - 155f) / 50f;
            float e = (x - 122f) / 66f, f = (y - 120f) / 30f;   // ngực: lấp khuyết ở mép trái bị tay/dưa che
            return a * a + b * b < 1f || c * c + d * d < 1f || e * e + f * f < 1f;
        }

        static Color32[] BuildBase()
        {
            int N = W * H;
            Color32[] b = new Color32[N];
            bool[] hole = new bool[N];
            Color32 headCol = S[75 * W + 150];
            for (int i = 0; i < N; i++)
            {
                b[i] = S[i];
                if (bgM[i] || shM[i] || bubble[i]) { b[i] = new Color32(0, 0, 0, 0); continue; }
                if (melon[i] || hands[i] || feetL[i] || feetR[i] || ear[i]) hole[i] = true;
                if (snoutA[i] > 0f) { b[i] = headCol; } // dưới mõm là màu đầu
            }
            // vá: lan màu từ pixel lông (không phải viền) vào lỗ nằm trong thân
            bool[] known = new bool[N];
            for (int i = 0; i < N; i++) known[i] = !hole[i] && b[i].a > 0 && IsBelly(b[i]);
            // Vá theo HÀNG: nội suy giữa pixel lông gần nhất bên trái và bên phải cùng hàng (lông bụng đổi màu theo dải ngang,
            // nên cho kết quả sạch hơn lan trung bình 3x3 — thứ sinh ra họa tiết chéo "chevron").
            bool[] filled = new bool[N];
            Color32 flat = new Color32(197, 126, 77, 255);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (!hole[i] || !FillAllowed(x, y)) continue;
                    Color32 cr, cc;
                    bool hr = AxisFill(b, known, x, y, 1, 0, out cr), hc = AxisFill(b, known, x, y, 0, 1, out cc);
                    Color32 col;
                    if (hr && hc) col = new Color32((byte)((cr.r + cc.r) / 2), (byte)((cr.g + cc.g) / 2), (byte)((cr.b + cc.b) / 2), 255); // hàng + cột → hết sọc ngang
                    else if (hr) col = cr;
                    else if (hc) col = cc;
                    else col = flat;
                    b[i] = col; filled[i] = true;
                }
            // Lỗ nằm NGOÀI thân (bàn chân, bàn tay thò ra): không để trong suốt — khi bộ phận xoay/nhích sẽ lộ khe giữa nó và thân.
            // Kéo dài màu của pixel đặc gần nhất vào lỗ (BFS nhiều nguồn, tối đa 14px), nhưng chừa vòng 2px sát nền để mép layer
            // (alpha mềm) vẫn pha với nền trong suốt chứ không thành viền bẩn lúc nghỉ.
            bool[] nearBg = new bool[N];
            bool[] bgOrShadow = new bool[N];
            for (int i = 0; i < N; i++) bgOrShadow[i] = bgM[i] || shM[i];
            nearBg = Dil(bgOrShadow, 2);
            int[] owner = new int[N];
            for (int i = 0; i < N; i++) owner[i] = -1;
            Queue<int> bq = new Queue<int>();
            // Nguồn kéo màu: chỉ lông hoặc viền tối. Lấy mọi pixel còn lại thì nhiễm cả vỏ dưa nhạt sót → ô xám/hồng lộ ra sau tay.
            for (int i = 0; i < N; i++)
            {
                if (b[i].a == 0 || hole[i]) continue;
                float sh, ss, sv; Hsv(b[i], out sh, out ss, out sv);
                if (IsBelly(b[i]) || sv < 0.35f) { owner[i] = i; bq.Enqueue(i); }
            }
            for (int i = 0; i < N; i++) if (filled[i]) { owner[i] = i; bq.Enqueue(i); }
            int[] dist = new int[N];
            while (bq.Count > 0)
            {
                int i = bq.Dequeue();
                if (dist[i] >= 14) continue;
                int x = i % W, y = i / W;
                int[] nbs = { x > 0 ? i - 1 : -1, x < W - 1 ? i + 1 : -1, y > 0 ? i - W : -1, y < H - 1 ? i + W : -1 };
                for (int k = 0; k < 4; k++)
                {
                    int j = nbs[k];
                    if (j < 0 || owner[j] >= 0 || !hole[j] || filled[j] || nearBg[j]) continue;
                    owner[j] = owner[i]; dist[j] = dist[i] + 1; bq.Enqueue(j);
                }
            }
            for (int i = 0; i < N; i++)
            {
                if (!hole[i] || filled[i]) continue;
                if (owner[i] >= 0 && owner[i] != i) { Color32 c = b[owner[i]]; c.a = 255; b[i] = c; filled[i] = true; }
                else b[i] = new Color32(0, 0, 0, 0); // còn lại (sát nền / quá xa) → trong suốt
            }

            // Viền tổng hợp: pixel vừa vá mà sát vùng trong suốt → tô màu viền (2px) để mép thân không bị trơ
            bool[] edge1 = new bool[N];
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int i = y * W + x; if (!filled[i]) continue;
                    if (b[i - 1].a == 0 || b[i + 1].a == 0 || b[i - W].a == 0 || b[i + W].a == 0) edge1[i] = true;
                }
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int i = y * W + x; if (!filled[i]) continue;
                    if (edge1[i] || edge1[i - 1] || edge1[i + 1] || edge1[i - W] || edge1[i + W]) b[i] = Outline;
                }
            SoftEdges(b);
            return b;
        }

        /// <summary>Nội suy màu dọc một trục (ngang hoặc dọc) giữa hai pixel lông gần nhất hai phía; false nếu không có phía nào.</summary>
        static bool AxisFill(Color32[] b, bool[] known, int x, int y, int dx, int dy, out Color32 col)
        {
            int ax = -1, ay = -1, bx = -1, by = -1, da = 0, db = 0;
            for (int k = 1; k <= 90; k++) { int xx = x - dx * k, yy = y - dy * k; if (xx < 0 || yy < 0 || xx >= W || yy >= H) break; if (known[yy * W + xx]) { ax = xx; ay = yy; da = k; break; } }
            for (int k = 1; k <= 90; k++) { int xx = x + dx * k, yy = y + dy * k; if (xx < 0 || yy < 0 || xx >= W || yy >= H) break; if (known[yy * W + xx]) { bx = xx; by = yy; db = k; break; } }
            col = default(Color32);
            if (da > 0 && db > 0)
            {
                float t = (float)da / (da + db);
                Color32 p = b[ay * W + ax], q = b[by * W + bx];
                col = new Color32((byte)Mathf.Lerp(p.r, q.r, t), (byte)Mathf.Lerp(p.g, q.g, t), (byte)Mathf.Lerp(p.b, q.b, t), 255);
                return true;
            }
            if (da > 0) { col = b[ay * W + ax]; return true; }
            if (db > 0) { col = b[by * W + bx]; return true; }
            return false;
        }

        /// <summary>Khử viền nền: pixel sát nền/bóng được đặt alpha theo độ lệch màu so với màu nền rồi gỡ màu nền ra.</summary>
        static void SoftEdges(Color32[] px)
        {
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int i = y * W + x;
                    if (px[i].a == 0 || bgM[i] || shM[i]) continue;
                    Color32 refC = Bg; bool near = false;
                    for (int dy = -1; dy <= 1 && !near; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int j = (y + dy) * W + x + dx;
                            if (bgM[j]) { refC = Bg; near = true; break; }
                            if (shM[j]) { refC = ShadowCol; near = true; break; }
                        }
                    if (!near) continue;
                    float denom = Mathf.Max(1f, Dist(Outline, refC));
                    float a = Mathf.Clamp01(Dist(px[i], refC) / denom);
                    if (a >= 0.98f) continue;
                    float inv = 1f - a;
                    a = Mathf.Max(a, 0.02f);
                    px[i] = new Color32((byte)Mathf.Clamp((px[i].r - refC.r * inv) / a, 0, 255), (byte)Mathf.Clamp((px[i].g - refC.g * inv) / a, 0, 255), (byte)Mathf.Clamp((px[i].b - refC.b * inv) / a, 0, 255), (byte)(a * 255f));
                }
        }

        // ═════════ Layer ═════════
        /// <summary>
        /// Layer từ mặt nạ. grow=true nở thêm 1px bằng màu mép của chính layer (không nở ra nền): khi Unity render bilinear, mép layer bị pha
        /// với vùng trong suốt nên hiện đường nối mảnh/nhạt; vòng nở này phủ đúng chỗ đó nên khi ghép tĩnh không còn đường nối.
        /// </summary>
        /// <summary>Xoá mọi pixel của layer không nối (8 hướng) với pixel hạt giống — loại mảnh rời/rác dính theo layer.</summary>
        static void KeepConnected(Color32[] px, int seedX, int seedY)
        {
            // Nối bằng pixel ĐẶC (alpha ≥ 60) bằng 4 hướng — pixel mờ (viền khử răng cưa của bộ phận khác) không đủ để làm cầu nối chéo.
            // Sau đó giữ thêm vành mềm 2px quanh phần đã giữ để không mất rìa trong suốt của chính layer.
            bool[] core = new bool[W * H];
            Queue<int> q = new Queue<int>();
            int start = seedY * W + seedX;
            if (px[start].a < 60) { Debug.LogWarning("KeepConnected: seed pixel is too transparent"); return; }
            q.Enqueue(start); core[start] = true;
            while (q.Count > 0)
            {
                int i = q.Dequeue(); int x = i % W, y = i / W;
                int[] nb = { x > 0 ? i - 1 : -1, x < W - 1 ? i + 1 : -1, y > 0 ? i - W : -1, y < H - 1 ? i + W : -1 };
                for (int k = 0; k < 4; k++)
                {
                    int j = nb[k]; if (j < 0 || core[j] || px[j].a < 60) continue;
                    core[j] = true; q.Enqueue(j);
                }
            }
            bool[] keep = Dil(core, 2);
            for (int i = 0; i < px.Length; i++) if (!keep[i]) px[i] = new Color32(0, 0, 0, 0);
        }

        static bool[] Union(params bool[][] ms)
        {
            bool[] o = new bool[W * H];
            for (int k = 0; k < ms.Length; k++) for (int i = 0; i < o.Length; i++) if (ms[k][i]) o[i] = true;
            return o;
        }

        /// <summary>
        /// Layer từ mặt nạ + VÒNG CHỒNG LẤN ringR px bằng pixel GỐC của vùng bên cạnh (không ra nền, không lấy pixel của layer khác).
        /// Vì sao: bộ phận gắn vào thân (cánh tay, cổ chân) mà chỉ vừa khít mặt nạ thì khi dịch 1–2px sẽ hở ngay chỗ nối. Có vòng chồng lấn,
        /// phần bị lộ phía sau là chính pixel gốc (lông/viền) chứ không phải khe. Lúc nghỉ vòng trùng khít base nên không đổi hình;
        /// mép layer cũng nằm trên pixel giống hệt nên hết đường nối bilinear.
        /// </summary>
        static Color32[] LayerFromMask(bool[] m, int ringR, bool[] exclude)
        {
            Color32[] l = new Color32[W * H];
            for (int i = 0; i < l.Length; i++) if (m[i]) l[i] = S[i];
            if (ringR > 0)
            {
                bool[] ring = Dil(m, ringR);
                for (int i = 0; i < l.Length; i++)
                    if (ring[i] && !m[i] && !bgM[i] && !shM[i] && !(exclude != null && exclude[i])) l[i] = S[i];
            }
            SoftEdges(l);
            return l;
        }

        sealed class Part { public string name; public Color32[] px; public float pivX, pivY; public int order; public Sprite sprite; public int x0, y1, w, h; }

        /// <summary>Xoá mọi mảnh liên thông (8 hướng) nhỏ hơn minPx pixel: hạt lẻ do ngưỡng alpha/khử viền sinh ra, sẽ "bay theo" layer.</summary>
        static void Despeckle(Color32[] px, int minPx)
        {
            int[] label = new int[W * H];
            List<int> members = new List<int>();
            Queue<int> q = new Queue<int>();
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a <= 25 || label[i] != 0) continue;
                members.Clear(); q.Enqueue(i); label[i] = 1;
                while (q.Count > 0)
                {
                    int k = q.Dequeue(); members.Add(k); int x = k % W, y = k / W;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                            int j = yy * W + xx; if (label[j] != 0 || px[j].a <= 25) continue;
                            label[j] = 1; q.Enqueue(j);
                        }
                }
                if (members.Count < minPx) for (int m = 0; m < members.Count; m++) px[members[m]] = new Color32(0, 0, 0, 0);
            }
            // pixel mờ (alpha ≤ 25) còn sót quanh vùng đã xoá cũng bỏ
            for (int i = 0; i < px.Length; i++) if (px[i].a > 0 && px[i].a <= 25) px[i] = new Color32(0, 0, 0, 0);
        }

        static Part MakePart(string name, Color32[] px, float pivX, float pivY, int order)
        {
            Despeckle(px, 8);
            int x0 = W, x1 = -1, y0 = H, y1 = -1;
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (px[y * W + x].a > 0) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
            if (x1 < 0) { Debug.LogWarning("Capy part empty: " + name); x0 = 0; x1 = 0; y0 = 0; y1 = 0; }
            x0 = Mathf.Max(0, x0 - 1); y0 = Mathf.Max(0, y0 - 1); x1 = Mathf.Min(W - 1, x1 + 1); y1 = Mathf.Min(H - 1, y1 + 1);
            int w = x1 - x0 + 1, h = y1 - y0 + 1;
            Color32[] outPx = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) outPx[(h - 1 - y) * w + x] = px[(y0 + y) * W + x0 + x];

            string path = PartsDir + "/" + name + ".png";
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels32(outPx);
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 100f; imp.mipmapEnabled = false; imp.alphaIsTransparency = true; imp.filterMode = FilterMode.Bilinear;
            TextureImporterSettings st = new TextureImporterSettings(); imp.ReadTextureSettings(st);
            st.spriteAlignment = (int)SpriteAlignment.Custom; imp.SetTextureSettings(st);
            // pivot tính theo toạ độ cạnh pixel; y đổi sang tính từ dưới lên
            imp.spritePivot = new Vector2((pivX - x0) / w, ((y1 + 1) - pivY) / h);
            imp.SaveAndReimport();
            return new Part { name = name, px = px, pivX = pivX, pivY = pivY, order = order, sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path), x0 = x0, y1 = y1, w = w, h = h };
        }

        // ═════════ Chạy ═════════
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(PartsDir)) AssetDatabase.CreateFolder("Assets/AnimationToolkit/Samples/CapyArt", "Parts");
            Load(); Segment();

            // Base: pivot giữa-đáy khung để thở/lắc không làm trôi bóng
            Color32[] baseData = BuildBase();
            Part pBase = MakePart("capy_base", baseData, W * 0.5f, H, 0);

            // Layer từng bộ phận
            Color32[] snoutPx = new Color32[W * H];
            for (int i = 0; i < snoutPx.Length; i++) if (snoutA[i] > 0f) { Color32 c = snoutC[i]; c.a = (byte)(snoutA[i] * 255f); snoutPx[i] = c; }
            Part pSnout = MakePart("capy_snout", snoutPx, 93f, 62f, 1);

            bool[] feetBoth = Union(feetL, feetR);
            Color32[] handsPx = LayerFromMask(hands, 3, feetBoth);   // tay cùng đi với dưa: vòng chồng lấn 3px phủ chỗ nối cánh tay–thân
            Color32[] melonPx = LayerFromMask(melon, 3, feetBoth);
            Part pMelon = MakePart("capy_watermelon", melonPx, 93f, 125f, 3);
            Part pHands = MakePart("capy_hands", handsPx, 93f, 125f, 4);   // cùng pivot với dưa hấu để làm con của nó

            Part pFootL = MakePart("capy_foot_L", LayerFromMask(feetL, 3, Union(melon, hands, feetR)), 46f, 168f, 2);
            Part pFootR = MakePart("capy_foot_R", LayerFromMask(feetR, 3, Union(melon, hands, feetL)), 127f, 172f, 2);

            // Trái tim: matte theo độ lệch so với nền (viền bong bóng trắng mềm)
            Color32[] bubPx = new Color32[W * H];
            Color32 white = new Color32(255, 255, 255, 255);
            for (int y = 24; y <= 54; y++)
                for (int x = 54; x <= 84; x++)
                {
                    int i = y * W + x; Color32 c = S[i];
                    // Chỉ lấy hình bong bóng (tròn + đuôi nhỏ), KHÔNG lấy ô chữ nhật: ô chữ nhật dính cả mẩu viền/lông đầu capy ở góc dưới phải,
                    // và mọi pixel tối/nâu bị loại để không có gì "bay theo" bong bóng.
                    float bx = x - 68.5f, by = y - 38.5f;
                    bool inCircle = bx * bx + by * by <= 19f * 19f, inTail = x >= 66 && x <= 78 && y >= 44 && y <= 51;
                    if (!inCircle && !inTail) continue;
                    float bh, bs, bv; Hsv(c, out bh, out bs, out bv);
                    if (bv < 0.5f) continue;
                    float a = Mathf.Clamp01(Dist(c, Bg) / Mathf.Max(1f, Dist(white, Bg) * 0.9f));
                    if (a < 0.06f) continue; // nhiễu nhẹ của màu nền không phải bong bóng
                    float inv = 1f - a; a = Mathf.Max(a, 0.02f);
                    bubPx[i] = new Color32((byte)Mathf.Clamp((c.r - Bg.r * inv) / a, 0, 255), (byte)Mathf.Clamp((c.g - Bg.g * inv) / a, 0, 255), (byte)Mathf.Clamp((c.b - Bg.b * inv) / a, 0, 255), (byte)(a * 255f));
                }
            KeepConnected(bubPx, 68, 38); // chỉ giữ mảnh liên thông chứa trái tim; mảnh rời (nhiễu, mẩu viền) bị bỏ
            Part pBubble = MakePart("capy_heart_bubble", bubPx, 68f, 39f, 5);

            Color32[] shPx = new Color32[W * H];
            for (int i = 0; i < shPx.Length; i++) if (shM[i]) shPx[i] = S[i];
            // Bóng phải là HÌNH ELIP TRỌN VẸN: phần nằm dưới thân/chân bị che nên layer bóng vốn trống ở đó. Thân nảy lên 1.6px hay chân xoay
            // thì lộ đúng chỗ đó → thành đường nền kem hở dưới bụng. Lấp các pixel vốn thuộc thân (không phải nền) nằm trong elip bóng bằng màu bóng
            // (lúc nghỉ bị thân che nên không đổi hình); pixel nền thì giữ nguyên.
            int sx0 = W, sx1 = -1, sy0 = H, sy1 = -1;
            for (int i = 0; i < shM.Length; i++) if (shM[i]) { int x = i % W, y = i / W; if (x < sx0) sx0 = x; if (x > sx1) sx1 = x; if (y < sy0) sy0 = y; if (y > sy1) sy1 = y; }
            float scx = (sx0 + sx1) * 0.5f, scy = (sy0 + sy1) * 0.5f, srx = (sx1 - sx0) * 0.5f + 2f, sry = (sy1 - sy0) * 0.5f + 3f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    float ex = (x - scx) / srx, ey = (y - scy) / sry;
                    if (ex * ex + ey * ey < 1f && !bgM[i] && shPx[i].a == 0) shPx[i] = ShadowCol;
                }
            Part pShadow = MakePart("capy_shadow", shPx, 112f, 200f, -2);
            AssetDatabase.SaveAssets();

            // ── Kiểm tra: ghép tĩnh các layer có khớp ảnh gốc không ──
            string check = Recomposite(new[] { pShadow, pBase, pSnout, pFootL, pFootR, pMelon, pHands, pBubble });

            BuildScene(pBase, pSnout, pMelon, pHands, pFootL, pFootR, pBubble, pShadow);
            Debug.Log("[CapySample] masks: melon=" + Cnt(melon) + " hands=" + Cnt(hands) + " feetL=" + Cnt(feetL) + " feetR=" + Cnt(feetR) + " ear=" + Cnt(ear) + " | " + check);
        }

        static string Recomposite(Part[] order)
        {
            float[] r = new float[W * H], g = new float[W * H], b = new float[W * H], a = new float[W * H];
            foreach (Part p in order)
                for (int i = 0; i < W * H; i++)
                {
                    Color32 c = p.px[i]; if (c.a == 0) continue;
                    float al = c.a / 255f;
                    r[i] = c.r * al + r[i] * (1f - al); g[i] = c.g * al + g[i] * (1f - al); b[i] = c.b * al + b[i] * (1f - al);
                    a[i] = al + a[i] * (1f - al);
                }
            double sum = 0; int n = 0, bad = 0;
            for (int i = 0; i < W * H; i++)
            {
                // so với ảnh gốc đặt trên nền
                float ar = a[i]; float cr = r[i] + Bg.r * (1f - ar), cg = g[i] + Bg.g * (1f - ar), cb = b[i] + Bg.b * (1f - ar);
                float d = (Mathf.Abs(cr - S[i].r) + Mathf.Abs(cg - S[i].g) + Mathf.Abs(cb - S[i].b)) / 3f;
                sum += d; n++; if (d > 40f) bad++;
            }
            return "recomposite meanAbsDiff=" + (sum / n).ToString("F2") + " pixelsOff>40=" + bad + "/" + n;
        }

        // ═════════ Scene + animation ═════════
        static Transform Obj(string name, Transform parent, Vector3 localPos, Sprite sprite, int order)
        {
            GameObject g = new GameObject(name);
            if (parent != null) g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            if (sprite != null)
            {
                SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
                sr.sprite = sprite; sr.sortingOrder = order;
            }
            return g.transform;
        }

        // vị trí (so với Base, đơn vị Unity) của điểm pivot của một part
        static Vector3 PivotInBase(Part p) { return new Vector3((p.pivX - W * 0.5f) / 100f, (H - p.pivY) / 100f, 0f); }

        static PoseKey K(float t, float x, float y, float rot, EaseType e = EaseType.SineInOut)
        {
            return AnimKit.Key(t, new Vector3(x, y, 0f), rot, 1f, e);
        }

        static void BuildScene(Part pBase, Part pSnout, Part pMelon, Part pHands, Part pFootL, Part pFootR, Part pBubble, Part pShadow)
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Camera cam = Camera.main;
            cam.orthographic = true; cam.orthographicSize = 1.45f;
            cam.transform.position = new Vector3(0f, 1.1f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(243, 232, 173, 255);

            Transform root = new GameObject("Capy").transform;
            Transform shadow = Obj("Shadow", root, PivotInBase(pShadow) + new Vector3(-0f, 0f, 0f) - new Vector3(0f, 0f, 0f), pShadow.sprite, pShadow.order);
            // Shadow đặt theo hệ của root (Base pivot nằm ở đáy-giữa khung = gốc root)
            Transform baseT = Obj("Base", root, Vector3.zero, pBase.sprite, pBase.order);
            Transform snoutT = Obj("Snout", baseT, PivotInBase(pSnout), pSnout.sprite, pSnout.order);
            Transform footL = Obj("FootL", baseT, PivotInBase(pFootL), pFootL.sprite, pFootL.order);
            Transform footR = Obj("FootR", baseT, PivotInBase(pFootR), pFootR.sprite, pFootR.order);
            Transform melonT = Obj("Watermelon", baseT, PivotInBase(pMelon), pMelon.sprite, pMelon.order);
            Transform handsT = Obj("Hands", melonT, PivotInBase(pHands) - PivotInBase(pMelon), pHands.sprite, pHands.order); // tay cầm → đi theo dưa hấu
            Transform bubbleT = Obj("HeartBubble", root, PivotInBase(pBubble), pBubble.sprite, pBubble.order);
            // Bubble là con của root nên không thở theo thân

            // ───── EAT: ngồi nhai dưa hấu ─────
            List<ToolkitAnimator> eat = new List<ToolkitAnimator>();
            eat.Add(AnimKit.Breathe(baseT, 0.012f, 0.45f));
            eat.Add(AnimKit.Swing(baseT, 0.8f, 0.6f));
            SineWaveAnimator chew = snoutT.gameObject.AddComponent<SineWaveAnimator>();
            chew.Configure(new Vector3(0f, 0.012f, 0f), 0.8f, 2.4f, 0f); eat.Add(chew);              // mõm nhai lên xuống (nhỏ: đường nối mõm/đầu chỉ chịu ~1px)
            SineWaveAnimator bob = baseT.gameObject.AddComponent<SineWaveAnimator>();
            bob.Configure(new Vector3(0f, 0.006f, 0f), 0f, 2.4f, 0.5f); eat.Add(bob);             // cả thân nảy theo nhịp nhai: không có đường nối nào để hở
            eat.Add(AnimKit.Keyframes(melonT, AnimLoopMode.Loop,                                      // đưa dưa hấu lên cắn từng miếng
                K(0f, 0, 0, 0), K(0.45f, 0.002f, 0.016f, 1.4f, EaseType.CubicOut), K(0.65f, 0.002f, 0.012f, 1.1f),
                K(0.85f, 0.002f, 0.018f, 1.7f), K(1.05f, 0.002f, 0.013f, 1.3f), K(1.8f, 0, 0, 0), K(3f, 0, 0, 0)));
            SwingAnimator fl = footL.gameObject.AddComponent<SwingAnimator>(); fl.Configure(3f, 1.0f, 0f, 0f); eat.Add(fl);   // chân đá đá vui vẻ
            SwingAnimator fr = footR.gameObject.AddComponent<SwingAnimator>(); fr.Configure(3f, 1.0f, 0f, 0f); fr.TimeOffset = 0.5f; eat.Add(fr);
            eat.Add(AnimKit.Hover(bubbleT, 0.05f, 0.6f));
            eat.Add(AnimKit.Pulse(bubbleT, 0.07f, 1.4f));
            eat.Add(AnimKit.Pulse(shadow, 0.012f, 0.45f));

            // ───── HAPPY: sướng quá, nhai nhanh + lắc lư ─────
            List<ToolkitAnimator> happy = new List<ToolkitAnimator>();
            happy.Add(AnimKit.Breathe(baseT, 0.02f, 0.9f));
            happy.Add(AnimKit.Swing(baseT, 2.0f, 1.1f));
            SineWaveAnimator bounce = baseT.gameObject.AddComponent<SineWaveAnimator>();
            bounce.Configure(new Vector3(0f, 0.016f, 0f), 0f, 3.6f, 0f); happy.Add(bounce);
            SineWaveAnimator chew2 = snoutT.gameObject.AddComponent<SineWaveAnimator>();
            chew2.Configure(new Vector3(0f, 0.016f, 0f), 1.2f, 3.6f, 0f); happy.Add(chew2);
            happy.Add(AnimKit.Keyframes(melonT, AnimLoopMode.Loop,
                K(0f, 0, 0, 0), K(0.25f, 0.003f, 0.022f, 2f, EaseType.CubicOut), K(0.4f, 0.003f, 0.016f, 1.4f),
                K(0.55f, 0.003f, 0.023f, 2.2f), K(0.7f, 0.003f, 0.016f, 1.4f), K(1.15f, 0, 0, 0), K(1.8f, 0, 0, 0)));
            SwingAnimator fl2 = footL.gameObject.AddComponent<SwingAnimator>(); fl2.Configure(6f, 1.6f, 0f, 0f); happy.Add(fl2);
            SwingAnimator fr2 = footR.gameObject.AddComponent<SwingAnimator>(); fr2.Configure(6f, 1.6f, 0f, 0f); fr2.TimeOffset = 0.28f; happy.Add(fr2);
            happy.Add(AnimKit.Hover(bubbleT, 0.08f, 1.0f));
            happy.Add(AnimKit.Pulse(bubbleT, 0.14f, 2.2f));
            happy.Add(AnimKit.Pulse(shadow, 0.03f, 2.2f));

            AnimStateSet set = root.gameObject.AddComponent<AnimStateSet>();
            SerializedObject so = new SerializedObject(set);
            SerializedProperty st = so.FindProperty("states");
            st.arraySize = 2;
            SetState(st.GetArrayElementAtIndex(0), "Eat", eat);
            SetState(st.GetArrayElementAtIndex(1), "Happy", happy);
            so.FindProperty("startState").stringValue = "Eat";
            so.ApplyModifiedProperties();
            for (int i = 0; i < happy.Count; i++) happy[i].enabled = false; // lúc edit chỉ Eat chạy

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static void SetState(SerializedProperty p, string name, List<ToolkitAnimator> animators)
        {
            p.FindPropertyRelative("name").stringValue = name;
            p.FindPropertyRelative("loop").boolValue = true;
            SerializedProperty arr = p.FindPropertyRelative("animators");
            arr.arraySize = animators.Count;
            for (int i = 0; i < animators.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = animators[i];
        }
    }
}
