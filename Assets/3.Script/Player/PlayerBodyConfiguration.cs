using System;
using UnityEngine;

namespace EscapeGame.Player
{
    public static class PlayerBodyConfiguration
    {
        public static void Apply(CharacterController controller, PlayerCharacterSettings settings, int skinWidthMm)
        {
            if (controller == null || settings == null) throw new ArgumentNullException("캡슐과 캐릭터 설정이 필요합니다.");
            if (skinWidthMm < 1 || skinWidthMm * 2L > settings.CapsuleRadiusMm)
                throw new InvalidOperationException("SkinWidth는 1mm 이상, 캡슐 반지름의 절반 이하여야 합니다.");
            controller.height = settings.CapsuleHeightMm / 1000f;
            controller.radius = settings.CapsuleRadiusMm / 1000f;
            controller.center = Vector3.up * (settings.CapsuleHeightMm / 2000f);
            controller.stepOffset = settings.StepOffsetMm / 1000f;
            controller.skinWidth = skinWidthMm / 1000f;
            controller.minMoveDistance = 0;
        }
    }
}
