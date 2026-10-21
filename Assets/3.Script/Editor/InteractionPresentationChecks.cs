#if UNITY_EDITOR
using System;
using System.Linq;
using EscapeGame.Interaction;
using EscapeGame.Player;
using UnityEditor;
using UnityEngine;

namespace EscapeGame.Editor
{
    public static class InteractionPresentationChecks
    {
        [MenuItem("Tools/Game/문 회전·클로즈업 방향 검사")]
        public static void RunMenu() => Run();
        public static int Run()
        {
            int count = 0;
            void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); count++; }
            var closed = Quaternion.Euler(-90, -90, 0);
            foreach (var parent in new[] { Quaternion.identity, Quaternion.Euler(0, 71, 0), Quaternion.Euler(20, 42, -15) })
            {
                foreach (int angle in new[] { 90000, -90000, 0 })
                {
                    var actual = parent * InteractionPresentationGeometry.DoorLocalRotation(closed, parent, angle);
                    var expected = Quaternion.AngleAxis(angle / 1000f, Vector3.up) * (parent * closed);
                    Check(Quaternion.Angle(actual, expected) < .01f, "임포트 축·부모 회전과 독립적인 월드 Y 회전");
                }
            }
            var hingeWorld = new Vector3(1, 2, 1);
            var centerWorld = new Vector3(2, 2, 1);
            foreach (int angle in new[] { 90000, -90000 })
            {
                var actual = InteractionPresentationGeometry.DoorWorldPosition(centerWorld, hingeWorld, angle);
                var expected = new Vector3(1, 2, angle > 0 ? 0 : 2);
                Check(Vector3.Distance(actual, expected) < .0001f, "경첩을 중심으로 문 위치 공전");
                Check(Mathf.Abs(Vector3.Distance(actual, hingeWorld) - 1) < .0001f, "경첩과 문 중심 사이 반경 유지");
            }
            Check(Vector3.Distance(InteractionPresentationGeometry.DoorWorldPosition(centerWorld, hingeWorld, 0), centerWorld) < .0001f, "각도 0에서 원래 문 위치");
            foreach (int yaw in new[] { 0, 90, 180, 270 })
            {
                var view = Quaternion.Euler(55, yaw, 0);
                var offset = InteractionPresentationGeometry.PreviewOffset(new[] { 0, 300, -500 }, view * Vector3.forward, view * Vector3.right);
                var expected = Quaternion.Euler(0, yaw, 0) * new Vector3(0, .3f, -.5f);
                Check(Vector3.Distance(offset, expected) < .0001f, "클릭 시점 수평 방향을 따라 미리보기 오프셋 적용");
            }
            Check(Vector3.Distance(InteractionPresentationGeometry.HorizontalForward(Vector3.down, Vector3.right), Vector3.forward) < .0001f, "수직 시점의 안정적인 수평 기준");
            Check(Vector3.Distance(InteractionPresentationGeometry.PreviewOffset(new[] { 100, 0, 0 }, Vector3.forward, Vector3.right), new Vector3(.1f, 0, 0)) < .0001f, "정수 mm와 관찰자 오른쪽 방향");
            var settings = Resources.Load<PlayerPresentationSettings>("PlayerPresentation");
            settings.Validate();
            Check(settings.PlaceholderMaterial != null && settings.PlaceholderMaterial.shader.isSupported, "빌드용 임시 모델 머티리얼 참조와 지원 셰이더");
            var dependencies = AssetDatabase.GetDependencies("Assets/Resources/PlayerPresentation.asset", true);
            Check(dependencies.Contains(AssetDatabase.GetAssetPath(settings.PlaceholderMaterial)), "Resources 설정을 통해 머티리얼이 빌드 의존성에 포함");
            Debug.Log($"[상호작용 표현] 축·시점·머티리얼 검사 {count}개 통과");
            return count;
        }
    }
}
#endif
