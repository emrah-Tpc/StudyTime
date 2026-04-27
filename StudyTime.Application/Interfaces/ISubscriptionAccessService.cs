namespace StudyTime.Application.Interfaces
{
    public interface ISubscriptionAccessService
    {
        Task<bool> HasActivePremiumAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> CanAccessPremiumFeaturesAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> CanUseDesktopAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
