using StudyTime.Application.Interfaces;

namespace StudyTime.Infrastructure.Services
{
    public class SystemContextState : ISystemContextState
    {
        private static readonly AsyncLocal<bool> IsSystemFlag = new();

        public bool IsSystemContext => IsSystemFlag.Value;

        public static IDisposable BeginSystemContext()
        {
            var previous = IsSystemFlag.Value;
            IsSystemFlag.Value = true;
            return new ResetHandle(previous);
        }

        private sealed class ResetHandle : IDisposable
        {
            private readonly bool _previous;

            public ResetHandle(bool previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                IsSystemFlag.Value = _previous;
            }
        }
    }
}
