using UnityEngine;

namespace AnimationToolkit
{
    /// <summary>Offset so với pose gốc: vị trí cộng, xoay nhân, scale nhân.</summary>
    public struct AnimPose
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public static AnimPose Identity
        {
            get
            {
                AnimPose p;
                p.position = Vector3.zero;
                p.rotation = Quaternion.identity;
                p.scale = Vector3.one;
                return p;
            }
        }
    }

    /// <summary>
    /// Áp offset lên một Transform theo kiểu DELTA (chỉ cộng phần chênh so với frame trước).
    /// Vì vậy nhiều animator (nhiều layer) có thể cùng tác động lên 1 Transform mà không giẫm lên nhau,
    /// và Clear() trả Transform về đúng pose gốc mà không cần lưu rest pose.
    /// Lưu ý: nếu code khác gán tuyệt đối localPosition mỗi frame trên cùng Transform thì delta sẽ lệch —
    /// hãy animate Transform con (visual) thay vì Transform bị code khác ghi đè.
    /// </summary>
    public sealed class OffsetChannel
    {
        Transform _target;
        Vector3 _lastPos;
        Quaternion _lastRot = Quaternion.identity;
        Vector3 _lastScale = Vector3.one;

        public Transform Target { get { return _target; } }

        public void SetTarget(Transform t)
        {
            if (_target == t) return;
            Clear();
            _target = t;
        }

        public void Apply(AnimPose pose, float weight)
        {
            if (_target == null) return;

            Vector3 pos = pose.position * weight;
            Quaternion rot = Quaternion.Slerp(Quaternion.identity, pose.rotation, weight);
            Vector3 sc = Vector3.Lerp(Vector3.one, pose.scale, weight);

            _target.localPosition += pos - _lastPos;
            _target.localRotation = (rot * Quaternion.Inverse(_lastRot)) * _target.localRotation;
            _target.localScale = Vector3.Scale(_target.localScale, Div(sc, _lastScale));

            _lastPos = pos;
            _lastRot = rot;
            _lastScale = sc;
        }

        public void Clear()
        {
            if (_target != null)
            {
                _target.localPosition -= _lastPos;
                _target.localRotation = Quaternion.Inverse(_lastRot) * _target.localRotation;
                _target.localScale = Vector3.Scale(_target.localScale, Div(Vector3.one, _lastScale));
            }
            _lastPos = Vector3.zero;
            _lastRot = Quaternion.identity;
            _lastScale = Vector3.one;
        }

        static Vector3 Div(Vector3 a, Vector3 b)
        {
            // Tránh chia cho 0 khi scale offset = 0
            return new Vector3(
                a.x / Mathf.Max(0.0001f, b.x),
                a.y / Mathf.Max(0.0001f, b.y),
                a.z / Mathf.Max(0.0001f, b.z));
        }
    }
}
