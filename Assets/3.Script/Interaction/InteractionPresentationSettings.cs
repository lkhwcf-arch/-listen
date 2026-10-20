using System;
using TMPro;
using UnityEngine;

namespace EscapeGame.Interaction
{
    [CreateAssetMenu(menuName = "EscapeGame/상호작용 표현 설정")]
    public sealed class InteractionPresentationSettings : ScriptableObject
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private int previewLayer = 31;
        [SerializeField] private int previewSizePixels = 512;
        [SerializeField] private int previewFovMilliDegrees = 40000;
        public TMP_FontAsset Font => font;
        public int PreviewLayer => previewLayer;
        public int PreviewSizePixels => previewSizePixels;
        public int PreviewFovMilliDegrees => previewFovMilliDegrees;
        public void Validate()
        {
            if (font == null || previewLayer < 8 || previewLayer > 31 || previewSizePixels < 128 || previewSizePixels > 2048 ||
                previewFovMilliDegrees < 10000 || previewFovMilliDegrees > 120000)
                throw new InvalidOperationException("InteractionPresentation 폰트·미리보기 레이어/해상도/시야각을 확인하세요.");
        }
    }
}
