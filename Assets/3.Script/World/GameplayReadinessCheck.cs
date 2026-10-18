using EscapeGame.Readiness;
using UnityEngine.SceneManagement;

namespace EscapeGame.World
{
    internal sealed class GameplayReadinessCheck : IReadinessCheck
    {
        private readonly GameFlowBootstrapper owner;
        public GameplayReadinessCheck(GameFlowBootstrapper owner) => this.owner = owner;
        public bool IsReady => owner != null && owner.WorldReady && SceneManager.GetActiveScene().isLoaded;
    }
}
