using Microsoft.AspNetCore.Identity;
using StudyTime.Domain.Enums;

namespace StudyTime.Domain.Entities
{
    public class AppUser : IdentityUser
    {
        public string? FullName { get; set; }
        
        // Premium status
        public bool IsPremium { get; set; }
        public DateTime? PremiumUntil { get; set; }
        public SubscriptionType SubscriptionType { get; set; } = SubscriptionType.Free;

        // Desktop Auth
        public string? DesktopHwid { get; set; }
        public string? DesktopRefreshToken { get; set; }
        public DateTime? DesktopRefreshTokenExpiryTime { get; set; }

        // Mobile Auth
        public string? MobileHwid { get; set; }
        public string? MobileRefreshToken { get; set; }
        public DateTime? MobileRefreshTokenExpiryTime { get; set; }
        
        // Navigation properties
        public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
        public virtual ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
        public virtual ICollection<StudySession> StudySessions { get; set; } = new List<StudySession>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

        public bool HasActivePremium(DateTime utcNow)
        {
            if (!IsPremium)
                return false;

            if (SubscriptionType == SubscriptionType.Lifetime)
                return true;

            if (!PremiumUntil.HasValue)
                return false;

            return PremiumUntil.Value > utcNow && SubscriptionType != SubscriptionType.Free;
        }
    }
}
