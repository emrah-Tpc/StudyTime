using Microsoft.AspNetCore.Identity;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Entities;

namespace StudyTime.Application.Services
{
    public class SubscriptionAccessService : ISubscriptionAccessService
    {
        private readonly UserManager<AppUser> _userManager;

        public SubscriptionAccessService(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        public Task<bool> HasActivePremiumAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return CanAccessPremiumFeaturesAsync(userId, cancellationToken);
        }

        public async Task<bool> CanAccessPremiumFeaturesAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                return false;

            return user.HasActivePremium(DateTime.UtcNow);
        }

        public Task<bool> CanUseDesktopAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return CanAccessPremiumFeaturesAsync(userId, cancellationToken);
        }
    }
}
