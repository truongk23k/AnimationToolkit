using UnityEngine;

namespace AnimationToolkit
{
    public enum EaseType
    {
        Linear,
        SineIn, SineOut, SineInOut,
        QuadIn, QuadOut, QuadInOut,
        CubicIn, CubicOut, CubicInOut,
        BackOut, ElasticOut, BounceOut,
        SmoothStep, SmootherStep
    }

    public enum AnimLoopMode
    {
        Once,
        Loop,
        PingPong
    }

    /// <summary>Hàm easing thuần (không alloc, không reflection) — an toàn cho Luna/WebGL.</summary>
    public static class AnimEasing
    {
        public static float Evaluate(EaseType type, float t)
        {
            t = Mathf.Clamp01(t);
            switch (type)
            {
                case EaseType.SineIn: return 1f - Mathf.Cos(t * Mathf.PI * 0.5f);
                case EaseType.SineOut: return Mathf.Sin(t * Mathf.PI * 0.5f);
                case EaseType.SineInOut: return 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI);
                case EaseType.QuadIn: return t * t;
                case EaseType.QuadOut: return 1f - (1f - t) * (1f - t);
                case EaseType.QuadInOut: return t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);
                case EaseType.CubicIn: return t * t * t;
                case EaseType.CubicOut: { float u = 1f - t; return 1f - u * u * u; }
                case EaseType.CubicInOut:
                    if (t < 0.5f) return 4f * t * t * t;
                    else { float u = 1f - t; return 1f - 4f * u * u * u; }
                case EaseType.BackOut:
                    {
                        const float c1 = 1.70158f;
                        const float c3 = c1 + 1f;
                        float u = t - 1f;
                        return 1f + c3 * u * u * u + c1 * u * u;
                    }
                case EaseType.ElasticOut:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
                case EaseType.BounceOut: return BounceOut(t);
                case EaseType.SmoothStep: return t * t * (3f - 2f * t);
                case EaseType.SmootherStep: return t * t * t * (t * (t * 6f - 15f) + 10f);
                default: return t;
            }
        }

        static float BounceOut(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }

        /// <summary>Đổi thời gian tuyến tính thành tiến độ 0..1 theo loop mode. finished chỉ true với Once.</summary>
        public static float Progress(float time, float duration, AnimLoopMode mode, out bool finished)
        {
            finished = false;
            if (duration <= 0.0001f) { finished = true; return 1f; }
            float n = time / duration;
            switch (mode)
            {
                case AnimLoopMode.Loop:
                    return n - Mathf.Floor(n);
                case AnimLoopMode.PingPong:
                    {
                        float m = n - 2f * Mathf.Floor(n * 0.5f); // 0..2
                        return m <= 1f ? m : 2f - m;
                    }
                default:
                    if (n >= 1f) { finished = true; return 1f; }
                    return Mathf.Max(0f, n);
            }
        }
    }
}
