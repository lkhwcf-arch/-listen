#if UNITY_EDITOR
using System;
using EscapeGame.Data;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EscapeGame.Editor
{
    // Inspector 값을 덮어쓰지 않습니다. 빌드 때 누락·중복·잘못된 범위만 검사합니다.
    public sealed class GameFeedbackBuilder : IPreprocessBuildWithReport
    {
        public int callbackOrder => 1;
        public void OnPreprocessBuild(BuildReport report)
        {
            try { Validate(); } catch (Exception error) { throw new BuildFailedException(error.Message); }
        }
        [MenuItem("Tools/Game/사운드·결과 UI 설정 검사")]
        public static void Validate()
        {
            var asset = Resources.Load<GameFeedbackAssets>("GameFeedback") ?? throw new InvalidOperationException("Resources/GameFeedback 설정이 없습니다.");
            asset.Validate();
            var prefab = asset.ResultPrefab;
            if (prefab == null || prefab.GetComponent<Canvas>() == null || prefab.transform.Find("Panel/Time_Text")?.GetComponent<TMPro.TMP_InputField>() == null ||
                prefab.transform.Find("Panel/Time_Space")?.GetComponent<TMPro.TMP_InputField>() == null || prefab.transform.Find("Panel/Button")?.GetComponent<UnityEngine.UI.Button>() == null)
                throw new InvalidOperationException("Result_UI의 Canvas·Panel/Time_Text·Time_Space·Button을 확인하세요.");
            Debug.Log("[게임 표현] 사운드·결과 UI 설정 검사 통과");
        }
    }
}
#endif
