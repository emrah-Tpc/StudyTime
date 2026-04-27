namespace StudyTime.Application.Interfaces
{
    public interface ICurrentUserService
    {
        string? UserId { get; }
        bool IsAuthenticated { get; }
        bool IsSystemContext { get; }
        string? Email { get; }
    }
}
