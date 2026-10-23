#if UNITY_EDITOR
using System;
using EscapeGame.Audio;
using UnityEditor;
using UnityEngine;

namespace EscapeGame.Editor
{
    public static class DoorwayAudioChecks
    {
        [MenuItem("Tools/Game/방 배경음 문 통과 검사")]
        public static void Run()
        {
            int checks = 0;
            void Check(bool value, string why)
            {
                if (!value)
                    throw new InvalidOperationException(why);
                checks++;
            }
            foreach (int yaw in new[] { 0, 90, 180, 270 })
                foreach (bool imported in new[] { false, true })
                {
                    var rotation = Quaternion.Euler(0, yaw, 0) * (imported ? Quaternion.Euler(-90, 0, 0) : Quaternion.identity);
                    var matrix = Matrix4x4.TRS(new Vector3(3, 14, 7), rotation, new Vector3(1.2f, .8f, 1.1f));
                    var box = new Bounds(Vector3.zero, imported ? new Vector3(1.2f, .08f, 2.3f) : new Vector3(1.2f, 2.3f, .08f));
                    var crossing = new DoorwayCrossingFrame(matrix, box, .05);
                    var center = matrix.MultiplyPoint3x4(Vector3.zero);
                    var normal = (rotation * (imported ? Vector3.up : Vector3.forward)).normalized;
                    var a = center + Vector3.up * .45f + normal * .25f;
                    var b = center + Vector3.up * .45f - normal * .25f;
                    Check(!crossing.Crossed(a, a, true), "첫 표본은 기준 설정");
                    Check(crossing.Crossed(a, b, true), "눈높이에서 회전·스케일이 있는 문을 통과");
                    Check(crossing.Crossed(b, a, true), "이전 방으로 돌아가는 통과");
                    Check(!crossing.Crossed(a, b, false), "닫힌 문 통과 차단");
                    crossing.Reset();
                    var outside = Vector3.Cross(Vector3.up, normal) * 5;
                    crossing.Crossed(a + outside, a + outside, true);
                    Check(!crossing.Crossed(a + outside, b + outside, true), "문 폭 바깥 통과 제외");
                }
            var fade = new AmbientCrossfade(2, 3);
            fade.Select(0, true);
            fade.Select(1);
            fade.Tick(1.5);
            Check(Math.Abs(fade.Gain(0) - .5) < .0001 && Math.Abs(fade.Gain(1) - .5) < .0001, "3초 전환의 중간 볼륨");
            fade.Tick(1.5);
            Check(fade.Gain(0) == 0 && fade.Gain(1) == 1, "3초 뒤 이전 음 제거");
            Debug.Log($"[방 배경음] 문 회전·왕복·높이·전환 검사 {checks}개 통과");
        }
    }
}
#endif
