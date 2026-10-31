using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AnimationToolkit.EditorTools
{
    /// <summary>
    /// Ghi một animation (state của AnimStateSet hoặc toàn bộ animator) thành GIF lặp khít, để làm demo cho README.
    /// Render từng khung bằng camera riêng, tick animator theo dt cố định. Mỗi animator được SnapToLoop về đúng độ dài vòng GIF
    /// (random như Blink / ExpressionPulse bị tắt khi ghi) nên GIF lặp không giật. Mọi thay đổi tạm thời được khôi phục.
    /// Không cần ffmpeg/ImageMagick: có sẵn bộ mã hóa GIF (bảng màu median-cut + LZW).
    /// </summary>
    public static class GifRecorder
    {
        static Dictionary<string, float> SnapshotFloats(Object o)
        {
            Dictionary<string, float> d = new Dictionary<string, float>();
            SerializedObject so = new SerializedObject(o);
            SerializedProperty p = so.GetIterator();
            bool enter = true;
            while (p.NextVisible(enter))
            {
                enter = false; // chỉ cấp cao nhất
                if (p.propertyType == SerializedPropertyType.Float) d[p.propertyPath] = p.floatValue;
            }
            return d;
        }

        static void RestoreFloats(Object o, Dictionary<string, float> d)
        {
            SerializedObject so = new SerializedObject(o);
            foreach (KeyValuePair<string, float> kv in d)
            {
                SerializedProperty p = so.FindProperty(kv.Key);
                if (p != null && p.propertyType == SerializedPropertyType.Float) p.floatValue = kv.Value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Các khung của lần Record gần nhất (path = null thì chỉ chụp khung, không ghi GIF). Dùng với SaveSheet để NHÌN từng khung.</summary>
        public static List<Color32[]> LastFrames;

        /// <summary>Ghép các khung (theo chỉ số) thành một bảng ảnh PNG, cols cột — để xem chuyển động từng khung một.</summary>
        public static void SaveSheet(List<Color32[]> frames, int size, int[] indices, int cols, string pngPath)
        {
            int rows = (indices.Length + cols - 1) / cols;
            Texture2D t = new Texture2D(size * cols, size * rows, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[size * cols * size * rows];
            for (int n = 0; n < indices.Length; n++)
            {
                Color32[] f = frames[indices[n]];
                int cx = n % cols, cy = rows - 1 - n / cols;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        px[(cy * size + y) * (size * cols) + cx * size + x] = f[y * size + x];
            }
            t.SetPixels32(px);
            Directory.CreateDirectory(Path.GetDirectoryName(pngPath));
            File.WriteAllBytes(pngPath, t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }

        /// <summary>Độ lệch trung bình (0..255 trên mỗi kênh) giữa hai khung.</summary>
        static double Diff(Color32[] a, Color32[] b)
        {
            long s = 0;
            for (int i = 0; i < a.Length; i += 3) s += Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b);
            return s / (double)((a.Length / 3 + 1) * 3);
        }

        /// <param name="stateIndex">Chỉ số state của AnimStateSet; -1 = mọi animator.</param>
        /// <param name="loopSeconds">Độ dài vòng lặp mong muốn (làm tròn theo số khung).</param>
        /// <param name="delayCs">Thời gian mỗi khung, đơn vị 1/100 giây (5 = 20 fps, 7 ≈ 14 fps).</param>
        /// <param name="preroll">Giây tick trước khi ghi (cho animation một lần như cất cánh chạy xong).</param>
        public static string Record(GameObject root, int stateIndex, float loopSeconds, int delayCs, int size,
            Vector2 center, float orthoSize, string path, float preroll)
        {
            int frames = Mathf.Max(2, Mathf.RoundToInt(loopSeconds * 100f / delayCs));
            float dt = delayCs / 100f;
            float loop = frames * dt;

            AnimStateSet set = root.GetComponentInChildren<AnimStateSet>(true);
            ToolkitAnimator[] all = root.GetComponentsInChildren<ToolkitAnimator>(true);
            ExpressionPulse[] pulses = root.GetComponentsInChildren<ExpressionPulse>(true);

            // ── lưu trạng thái để khôi phục ──
            bool wasActive = root.activeSelf;
            // CHỈ lưu/khôi phục các trường float cấp cao nhất (SnapToLoop chỉ đổi tần số). KHÔNG dùng EditorJsonUtility.FromJsonOverwrite:
            // nó làm mất tham chiếu tới Transform/SpriteRenderer trong scene (cánh chim, đốt đuôi...) và scene lưu ra bị hỏng.
            Dictionary<ToolkitAnimator, Dictionary<string, float>> floats = new Dictionary<ToolkitAnimator, Dictionary<string, float>>();
            Dictionary<ToolkitAnimator, bool> en = new Dictionary<ToolkitAnimator, bool>();
            Dictionary<Transform, Vector3[]> pose = new Dictionary<Transform, Vector3[]>();
            Dictionary<Transform, Quaternion> rot = new Dictionary<Transform, Quaternion>();
            List<Transform> tmp = new List<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                floats[all[i]] = SnapshotFloats(all[i]);
                en[all[i]] = all[i].enabled;
                tmp.Clear(); all[i].GetAffectedTransforms(tmp);
                for (int k = 0; k < tmp.Count; k++)
                    if (tmp[k] != null && !pose.ContainsKey(tmp[k])) { pose[tmp[k]] = new Vector3[] { tmp[k].localPosition, tmp[k].localScale }; rot[tmp[k]] = tmp[k].localRotation; }
            }
            Dictionary<ExpressionPulse, bool> pulseEn = new Dictionary<ExpressionPulse, bool>();
            for (int i = 0; i < pulses.Length; i++) { pulseEn[pulses[i]] = pulses[i].enabled; pulses[i].enabled = false; }

            Camera main = Camera.main;
            GameObject camGo = new GameObject("__GifCam") { hideFlags = HideFlags.HideAndDontSave };
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = orthoSize; cam.aspect = 1f;
            cam.transform.position = new Vector3(center.x, center.y, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = main != null ? main.backgroundColor : new Color(0.9f, 0.9f, 0.9f);
            RenderTexture rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            List<Color32[]> captured = new List<Color32[]>();
            string info;
            try
            {
                root.SetActive(true);
                ToolkitAnimator[] group = set != null && stateIndex >= 0 ? set.StateAnimators(stateIndex) : all;
                for (int i = 0; i < all.Length; i++) { all[i].ClearOffset(); all[i].enabled = false; }
                // Snap về đúng độ dài vòng (bản gốc được khôi phục từ JSON ở finally)
                for (int i = 0; i < group.Length; i++) if (group[i] != null) group[i].SnapToLoop(loop);
                if (set != null && stateIndex >= 0) set.Restart(stateIndex);
                else for (int i = 0; i < group.Length; i++) if (group[i] != null) { group[i].enabled = true; group[i].Weight = 1f; group[i].ResetState(); }
                for (int i = 0; i < group.Length; i++)
                    if (group[i] is BlinkAnimator) { group[i].ClearOffset(); group[i].enabled = false; } // ngẫu nhiên → không lặp khít

                int pre = Mathf.RoundToInt(preroll / dt);
                for (int f = 0; f < pre + frames; f++)
                {
                    if (set != null) set.Tick(dt);
                    for (int i = 0; i < all.Length; i++) if (all[i].enabled) all[i].Tick(dt);
                    if (f < pre) continue;
                    cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
                    RenderTexture prev = RenderTexture.active; RenderTexture.active = rt;
                    Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
                    t.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                    RenderTexture.active = prev;
                    captured.Add(t.GetPixels32());
                    Object.DestroyImmediate(t);
                }
                LastFrames = captured;
                int bytes = 0;
                if (!string.IsNullOrEmpty(path))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    bytes = GifWriter.Write(path, captured, size, size, delayCs);
                }
                // Kiểm tra vòng lặp khít: khung cuối → khung đầu không được "nhảy" hơn một bước thường
                double step = 0; for (int f = 1; f < captured.Count; f++) step += Diff(captured[f - 1], captured[f]);
                step /= (captured.Count - 1);
                // Aliasing: animator nhanh nhất phải có ≥ 6 khung mỗi chu kỳ, nếu không GIF nhấp nháy như bánh xe quay ngược (dù game thật mượt)
                float fastest = float.MaxValue;
                for (int i = 0; i < group.Length; i++)
                    if (group[i] != null && group[i].enabled && group[i].IsLooping && group[i].EffectiveCycle > 0.0001f) fastest = Mathf.Min(fastest, group[i].EffectiveCycle);
                string alias = fastest < float.MaxValue
                    ? "  | fastest cycle " + fastest.ToString("0.00") + "s = " + (fastest / dt).ToString("0.0") + " frames/cycle" + (fastest / dt < 6f ? "  WARNING: aliasing, lower delayCs (use 3 or 2)" : "")
                    : "";
                double seam = Diff(captured[captured.Count - 1], captured[0]);
                info = Path.GetFileName(path) + ": " + frames + " frames, " + (loop).ToString("0.00") + "s loop, " + (bytes / 1024) + " KB | seam diff " + seam.ToString("F3") + " vs avg step " + step.ToString("F3") + (seam <= step * 1.5 + 0.02 ? "  (loops cleanly)" : "  (LOOP JUMP!)") + alias;
            }
            finally
            {
                for (int i = 0; i < all.Length; i++) all[i].ClearOffset();
                foreach (KeyValuePair<Transform, Vector3[]> kv in pose)
                    if (kv.Key != null) { kv.Key.localPosition = kv.Value[0]; kv.Key.localScale = kv.Value[1]; kv.Key.localRotation = rot[kv.Key]; }
                foreach (KeyValuePair<ToolkitAnimator, Dictionary<string, float>> kv in floats) if (kv.Key != null) RestoreFloats(kv.Key, kv.Value);
                foreach (KeyValuePair<ToolkitAnimator, bool> kv in en) if (kv.Key != null) kv.Key.enabled = kv.Value;
                foreach (KeyValuePair<ExpressionPulse, bool> kv in pulseEn) if (kv.Key != null) kv.Key.enabled = kv.Value;
                if (set != null) set.ResetRuntime();
                root.SetActive(wasActive);
                rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(camGo);
            }
            return info;
        }
    }

    /// <summary>Bộ ghi GIF89a: bảng màu toàn cục median-cut 256 màu + nén LZW, lặp vô hạn.</summary>
    public static class GifWriter
    {
        public static int Write(string path, List<Color32[]> frames, int w, int h, int delayCs)
        {
            byte[][] palette = BuildPalette(frames);
            Dictionary<int, byte> cache = new Dictionary<int, byte>();
            using (MemoryStream ms = new MemoryStream())
            {
                // Header + logical screen + global colour table
                WriteAscii(ms, "GIF89a");
                WriteU16(ms, w); WriteU16(ms, h);
                ms.WriteByte(0xF7); ms.WriteByte(0); ms.WriteByte(0);
                for (int i = 0; i < 256; i++) { ms.WriteByte(palette[i][0]); ms.WriteByte(palette[i][1]); ms.WriteByte(palette[i][2]); }
                // NETSCAPE2.0: lặp vô hạn
                ms.WriteByte(0x21); ms.WriteByte(0xFF); ms.WriteByte(11); WriteAscii(ms, "NETSCAPE2.0");
                ms.WriteByte(3); ms.WriteByte(1); WriteU16(ms, 0); ms.WriteByte(0);

                byte[] indices = new byte[w * h];
                for (int f = 0; f < frames.Count; f++)
                {
                    Color32[] px = frames[f];
                    for (int y = 0; y < h; y++)           // ReadPixels là dưới-lên, GIF là trên-xuống
                    {
                        int srcRow = (h - 1 - y) * w;
                        for (int x = 0; x < w; x++)
                        {
                            Color32 c = px[srcRow + x];
                            int key = (c.r << 16) | (c.g << 8) | c.b; byte idx;
                            if (!cache.TryGetValue(key, out idx)) { idx = Nearest(palette, c.r, c.g, c.b); cache[key] = idx; }
                            indices[y * w + x] = idx;
                        }
                    }
                    // Graphic control extension
                    ms.WriteByte(0x21); ms.WriteByte(0xF9); ms.WriteByte(4); ms.WriteByte(0x04); WriteU16(ms, delayCs); ms.WriteByte(0); ms.WriteByte(0);
                    // Image descriptor
                    ms.WriteByte(0x2C); WriteU16(ms, 0); WriteU16(ms, 0); WriteU16(ms, w); WriteU16(ms, h); ms.WriteByte(0);
                    ms.WriteByte(8);
                    byte[] lzw = Lzw(indices, 8);
                    for (int p = 0; p < lzw.Length; p += 255)
                    {
                        int n = Mathf.Min(255, lzw.Length - p);
                        ms.WriteByte((byte)n); ms.Write(lzw, p, n);
                    }
                    ms.WriteByte(0);
                }
                ms.WriteByte(0x3B);
                File.WriteAllBytes(path, ms.ToArray());
                return (int)ms.Length;
            }
        }

        static void WriteAscii(Stream s, string t) { for (int i = 0; i < t.Length; i++) s.WriteByte((byte)t[i]); }
        static void WriteU16(Stream s, int v) { s.WriteByte((byte)(v & 0xFF)); s.WriteByte((byte)((v >> 8) & 0xFF)); }

        static byte Nearest(byte[][] pal, int r, int g, int b)
        {
            int best = 0, bd = int.MaxValue;
            for (int i = 0; i < pal.Length; i++)
            {
                int dr = pal[i][0] - r, dg = pal[i][1] - g, db = pal[i][2] - b;
                int d = dr * dr * 3 + dg * dg * 4 + db * db * 2; // trọng số theo độ nhạy mắt
                if (d < bd) { bd = d; best = i; if (d == 0) break; }
            }
            return (byte)best;
        }

        // ── bảng màu median-cut từ mẫu của mọi khung ──
        static byte[][] BuildPalette(List<Color32[]> frames)
        {
            List<Color32> samples = new List<Color32>();
            int stride = Mathf.Max(1, (frames.Count * frames[0].Length) / 120000);
            int counter = 0;
            for (int f = 0; f < frames.Count; f++)
                for (int i = 0; i < frames[f].Length; i++) if ((counter++ % stride) == 0) samples.Add(frames[f][i]);

            List<Color32[]> boxes = new List<Color32[]>();
            boxes.Add(samples.ToArray());
            while (boxes.Count < 256)
            {
                int pick = -1; int bestRange = 0; int bestCh = 0;
                for (int b = 0; b < boxes.Count; b++)
                {
                    if (boxes[b].Length < 2) continue;
                    int rmin = 255, rmax = 0, gmin = 255, gmax = 0, bmin = 255, bmax = 0;
                    Color32[] a = boxes[b];
                    for (int i = 0; i < a.Length; i++)
                    {
                        if (a[i].r < rmin) rmin = a[i].r; if (a[i].r > rmax) rmax = a[i].r;
                        if (a[i].g < gmin) gmin = a[i].g; if (a[i].g > gmax) gmax = a[i].g;
                        if (a[i].b < bmin) bmin = a[i].b; if (a[i].b > bmax) bmax = a[i].b;
                    }
                    int rr = (rmax - rmin) * 3, gr = (gmax - gmin) * 4, br = (bmax - bmin) * 2;
                    int range = Mathf.Max(rr, Mathf.Max(gr, br));
                    if (range > bestRange) { bestRange = range; pick = b; bestCh = range == rr ? 0 : (range == gr ? 1 : 2); }
                }
                if (pick < 0 || bestRange == 0) break;
                Color32[] box = boxes[pick];
                int[] keys = new int[box.Length];
                for (int i = 0; i < box.Length; i++) keys[i] = bestCh == 0 ? box[i].r : (bestCh == 1 ? box[i].g : box[i].b);
                System.Array.Sort(keys, box);
                int mid = box.Length / 2;
                Color32[] lo = new Color32[mid], hi = new Color32[box.Length - mid];
                System.Array.Copy(box, 0, lo, 0, mid); System.Array.Copy(box, mid, hi, 0, box.Length - mid);
                boxes[pick] = lo; boxes.Add(hi);
            }
            byte[][] pal = new byte[256][];
            for (int i = 0; i < 256; i++)
            {
                if (i < boxes.Count && boxes[i].Length > 0)
                {
                    long r = 0, g = 0, b = 0; Color32[] a = boxes[i];
                    for (int k = 0; k < a.Length; k++) { r += a[k].r; g += a[k].g; b += a[k].b; }
                    pal[i] = new byte[] { (byte)(r / a.Length), (byte)(g / a.Length), (byte)(b / a.Length) };
                }
                else pal[i] = new byte[] { 0, 0, 0 };
            }
            return pal;
        }

        // ── LZW (GIF): mã 9..12 bit, clear/EOI chuẩn ──
        static byte[] Lzw(byte[] data, int minCode)
        {
            int clear = 1 << minCode, eoi = clear + 1, next = eoi + 1, codeSize = minCode + 1;
            Dictionary<int, int> dict = new Dictionary<int, int>(8192);
            List<byte> outp = new List<byte>(data.Length / 3);
            int cur = 0, nbits = 0;
            System.Action<int> emit = code =>
            {
                cur |= code << nbits; nbits += codeSize;
                while (nbits >= 8) { outp.Add((byte)(cur & 0xFF)); cur >>= 8; nbits -= 8; }
            };
            emit(clear);
            int prefix = data[0];
            for (int i = 1; i < data.Length; i++)
            {
                int k = data[i]; int key = (prefix << 8) | k; int code;
                if (dict.TryGetValue(key, out code)) { prefix = code; continue; }
                emit(prefix);
                int c = next++;
                dict[key] = c;
                if (c == (1 << codeSize) && codeSize < 12) codeSize++;
                if (next >= 4096) { emit(clear); dict.Clear(); next = eoi + 1; codeSize = minCode + 1; }
                prefix = k;
            }
            emit(prefix); emit(eoi);
            if (nbits > 0) outp.Add((byte)(cur & 0xFF));
            return outp.ToArray();
        }
    }
}
