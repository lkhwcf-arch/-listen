using System;
using EscapeGame.Data.Tables;
using EscapeGame.World;
using UnityEngine;

namespace EscapeGame.Interaction
{
    public sealed class WorldInteractionTarget : MonoBehaviour, IAnchorDataReceiver, ICloseupTarget
    {
        public AnchorData Anchor { get; private set; }
        public PrefabFeaturePlan Features { get; private set; }
        public bool IsAvailable => this != null && Anchor != null && Anchor.Active && isActiveAndEnabled;
        public Color HighlightColor { get; private set; }
        public bool SupportsCloseup => Features != null && Features.Has(FeatureScripts.Closeup);
        public void Initialize(AnchorData anchor, CsvGameData data)
        {
            if (!(anchor is AnchorInteractData) || !anchor.Active) throw new InvalidOperationException("활성 ANCHOR_INTERACT만 상호작용 대상으로 구성합니다.");
            Features = new PrefabFeaturePlan(data, anchor.PrefabId);
            if (!Features.Has(FeatureScripts.Interactable)) throw new InvalidOperationException("상호작용 대상에 Interactable 선언이 없습니다.");
            if (SupportsCloseup) _ = Features.CloseupCameraOffsetMm();
            var module = System.Linq.Enumerable.Single(Features.Components, row => row.Script == FeatureScripts.Interactable);
            if (!ColorUtility.TryParseHtmlString(ComponentParameters.Read(module.Params, "HighlightColor"), out var color))
                throw new InvalidOperationException("HighlightColor는 Unity HTML 색 이름 또는 #RRGGBB 값이어야 합니다.");
            HighlightColor = color;
            if (!System.Linq.Enumerable.Any(GetComponentsInChildren<Collider>(true), collider => collider.enabled && !collider.isTrigger && collider.gameObject.layer != 2))
                throw new InvalidOperationException("Interactable 프리팹에 클릭 가능한 비 Trigger Collider가 필요합니다: " + anchor.PrefabId);
            Anchor = anchor;
        }
    }
    public static class AnchorFeatureFactory
    {
        public static void Compose(GameObject instance, AnchorData anchor, CsvGameData data)
        {
            var plan = new PrefabFeaturePlan(data, anchor.PrefabId);
            if (!(anchor is AnchorInteractData) || !plan.Has(FeatureScripts.Interactable)) return;
            if (instance.GetComponent<WorldInteractionTarget>() == null) instance.AddComponent<WorldInteractionTarget>();
            // AnchorInstance.Initialize가 구성 완료 후 모든 수신기에 같은 불변 데이터를 주입합니다.
        }
    }
}
