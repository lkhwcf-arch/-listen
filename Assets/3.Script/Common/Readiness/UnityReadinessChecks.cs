using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EscapeGame.Readiness
{
    public sealed class LoadedSceneReadinessCheck : IReadinessCheck
    {
        private readonly Scene scene;
        public LoadedSceneReadinessCheck(Scene scene)
        {
            if (!scene.IsValid())
                throw new ArgumentException("유효한 씬이 필요합니다.", nameof(scene));
            this.scene = scene;
        }
        public bool IsReady => scene.IsValid() && scene.isLoaded;
    }

    // 동기 초기화 컴포넌트용 조건입니다. 비동기 준비는 별도 IReadinessCheck로 추가합니다.
    public sealed class ActiveComponentsReadinessCheck : IReadinessCheck
    {
        private readonly Behaviour[] components;
        public ActiveComponentsReadinessCheck(Behaviour[] components)
        {
            if (components == null || components.Length == 0)
                throw new ArgumentException("필수 컴포넌트를 지정하세요.", nameof(components));
            this.components = (Behaviour[])components.Clone();
            foreach (var component in this.components)
                if (component == null)
                    throw new ArgumentException("필수 컴포넌트 참조가 비어 있습니다.", nameof(components));
        }
        public bool IsReady
        {
            get
            {
                foreach (var component in components)
                    if (component == null || !component.isActiveAndEnabled)
                        return false;
                return true;
            }
        }
    }
}
