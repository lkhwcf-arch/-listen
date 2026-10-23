using System;

namespace EscapeGame.Readiness
{
    // 모든 필수 조건이 준비된 경우에만 완료입니다.
    public sealed class AllReadinessCheck : IReadinessCheck
    {
        private readonly IReadinessCheck[] checks;

        public AllReadinessCheck(params IReadinessCheck[] checks)
        {
            if (checks == null || checks.Length == 0)
                throw new ArgumentException("준비 조건을 한 개 이상 지정하세요.", nameof(checks));
            this.checks = (IReadinessCheck[])checks.Clone();
            foreach (var check in this.checks)
                if (check == null)
                    throw new ArgumentException("준비 조건이 비어 있습니다.", nameof(checks));
        }

        public bool IsReady
        {
            get
            {
                foreach (var check in checks)
                    if (!check.IsReady)
                        return false;
                return true;
            }
        }
    }
}
