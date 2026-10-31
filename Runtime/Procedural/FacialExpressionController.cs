using System;
using UnityEngine;

namespace AnimationToolkit
{
    [Serializable]
    public class FaceExpression
    {
        public string name = "Neutral";
        [Tooltip("Sprite for the eyes in this expression (null = keep current).")]
        public Sprite eyes;
        [Tooltip("Sprite for the mouth in this expression (null = keep current).")]
        public Sprite mouth;
        [Tooltip("Hide the pupil renderers (use when the eye sprite is a closed/squinting eye).")]
        public bool hidePupils;
    }

    /// <summary>
    /// Đổi biểu cảm bằng cách thay sprite mắt/miệng. Cần artwork có sprite riêng cho từng biểu cảm.
    /// Nếu có BlinkAnimator, mắt "mở" sẽ tự cập nhật theo biểu cảm hiện tại.
    /// Component tự tắt (enabled=false) khi không có biểu cảm tạm thời để không tốn Update.
    /// </summary>
    [AddComponentMenu("Animation Toolkit/Facial Expression Controller")]
    public class FacialExpressionController : MonoBehaviour
    {
        [SerializeField] SpriteRenderer[] eyeRenderers;
        [SerializeField] SpriteRenderer[] mouthRenderers;
        [Tooltip("Renderers hidden when an expression has hidePupils (e.g. pupils drawn on top of the eye sprite).")]
        [SerializeField] Renderer[] pupilRenderers;
        [SerializeField] FaceExpression[] expressions;
        [Tooltip("Expression applied on Awake. Empty = none.")]
        [SerializeField] string defaultExpression = "Neutral";
        [Tooltip("Optional: keeps the blink 'open' sprite in sync with the expression.")]
        [SerializeField] BlinkAnimator blink;

        string _current;
        string _returnTo;
        float _returnAt;
        bool _temporary;

        public string Current { get { return _current; } }

        public string[] ExpressionNames
        {
            get
            {
                int n = expressions != null ? expressions.Length : 0;
                string[] r = new string[n];
                for (int i = 0; i < n; i++) r[i] = expressions[i] != null ? expressions[i].name : "";
                return r;
            }
        }

        public bool Has(string expressionName) { return Find(expressionName) != null; }

        /// <summary>Biểu cảm áp lúc Awake. Đặt cái này khi chọn biểu cảm lúc edit để nó không bị reset về mặc định khi vào Play.</summary>
        public void SetDefaultExpression(string expressionName) { defaultExpression = expressionName; }

        void Awake()
        {
            if (!string.IsNullOrEmpty(defaultExpression)) SetExpression(defaultExpression);
            enabled = false; // chỉ bật lại khi có biểu cảm tạm thời
        }

        FaceExpression Find(string n)
        {
            if (expressions == null) return null;
            for (int i = 0; i < expressions.Length; i++)
                if (expressions[i] != null && string.Equals(expressions[i].name, n, StringComparison.OrdinalIgnoreCase))
                    return expressions[i];
            return null;
        }

        /// <summary>Trả về false nếu biểu cảm không tồn tại (không đổi gì).</summary>
        public bool SetExpression(string expressionName)
        {
            FaceExpression e = Find(expressionName);
            if (e == null) return false;
            if (e.eyes != null)
            {
                SetSprites(eyeRenderers, e.eyes);
                if (blink != null) blink.SetOpenSprite(e.eyes);
            }
            if (e.mouth != null) SetSprites(mouthRenderers, e.mouth);
            if (pupilRenderers != null)
                for (int i = 0; i < pupilRenderers.Length; i++)
                    if (pupilRenderers[i] != null) pupilRenderers[i].enabled = !e.hidePupils;
            _current = e.name;
            return true;
        }

        /// <summary>Hiện biểu cảm trong duration giây rồi quay lại biểu cảm trước đó.</summary>
        public bool PlayTemporary(string expressionName, float duration)
        {
            string back = _temporary ? _returnTo : _current;
            if (!SetExpression(expressionName)) return false;
            _returnTo = back;
            _returnAt = Time.time + duration;
            _temporary = true;
            enabled = true;
            return true;
        }

        void Update()
        {
            if (!_temporary || Time.time < _returnAt) return;
            _temporary = false;
            if (!string.IsNullOrEmpty(_returnTo)) SetExpression(_returnTo);
            enabled = false;
        }

        static void SetSprites(SpriteRenderer[] rs, Sprite s)
        {
            if (rs == null) return;
            for (int i = 0; i < rs.Length; i++)
                if (rs[i] != null) rs[i].sprite = s;
        }
    }
}
