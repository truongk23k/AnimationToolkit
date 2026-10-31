using UnityEngine;

namespace AnimationToolkit
{
    /// <summary>
    /// Thỉnh thoảng đổi sang một biểu cảm trong chốc lát rồi về lại (vd nhắm mắt cười, nháy mắt).
    /// Cần FacialExpressionController có biểu cảm tương ứng. Không dùng coroutine, chỉ chạy khi enabled.
    /// </summary>
    [AddComponentMenu("Animation Toolkit/Expression Pulse")]
    public class ExpressionPulse : MonoBehaviour
    {
        [SerializeField] FacialExpressionController face;
        [SerializeField] string expression = "Happy";
        [SerializeField, Min(0.1f)] float minInterval = 3f;
        [SerializeField, Min(0.1f)] float maxInterval = 6f;
        [SerializeField, Min(0.05f)] float duration = 0.7f;

        float _next;

        public void Setup(FacialExpressionController f, string expr, float min, float max, float dur)
        {
            face = f; expression = expr; minInterval = min; maxInterval = Mathf.Max(min, max); duration = dur;
        }

        void OnEnable()
        {
            if (face == null) face = GetComponent<FacialExpressionController>();
            Schedule();
        }

        void Schedule() { _next = Time.time + Random.Range(minInterval, maxInterval); }

        void Update()
        {
            if (face == null || Time.time < _next) return;
            face.PlayTemporary(expression, duration);
            Schedule();
        }
    }
}
