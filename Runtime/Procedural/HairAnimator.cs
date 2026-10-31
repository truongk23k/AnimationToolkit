using UnityEngine;

namespace AnimationToolkit
{
    /// <summary>
    /// Tóc đung đưa nhẹ, trễ pha. Mỗi "section" (lọn tóc / mảng tóc) là 1 chain riêng.
    /// autoSpreadPhase rải pha giữa các lọn để chúng không đồng bộ. Kết hợp SecondaryMotionAnimator
    /// ở lọn tóc nếu muốn tóc phản ứng khi đầu di chuyển.
    /// </summary>
    [AddComponentMenu("Animation Toolkit/Hair Animator")]
    public class HairAnimator : ChainWaveAnimator
    {
        [Tooltip("Spread phase across sections automatically so they do not move in sync.")]
        [SerializeField] bool autoSpreadPhase = true;
        [SerializeField] float spreadPerSection = 0.17f;

        protected override float ChainPhase(int index)
        {
            return chains[index].phase + (autoSpreadPhase ? index * spreadPerSection : 0f);
        }

        void Reset()
        {
            amplitude = 5f; frequency = 0.35f; phaseDelayPerSegment = 0.15f; rootGain = 0.3f; tipGain = 1.5f;
        }
    }
}
