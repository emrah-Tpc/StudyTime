using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StudyTime.Application.Interfaces;
using StudyTime.Domain.Entities;

namespace StudyTime.Infrastructure.Persistence
{
    public class StudyTimeDbContext : IdentityDbContext<AppUser>
    {
        private readonly ICurrentUserService _currentUserService;
        private string? CurrentUserId => _currentUserService.UserId;
        private bool IsSystemContext => _currentUserService.IsSystemContext;

        public StudyTimeDbContext(DbContextOptions<StudyTimeDbContext> options, ICurrentUserService currentUserService) : base(options)
        {
            _currentUserService = currentUserService;
        }

        public DbSet<Lesson> Lessons { get; set; }
        public DbSet<TaskItem> Tasks { get; set; }
        public DbSet<StudySession> StudySessions { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        // View için DbSet'i de diğerlerinin yanına aldık
        public DbSet<DashboardSummaryView> DashboardSummaries { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.ConfigureWarnings(warnings =>
            {
                // "Bekleyen model değişikliği" uyarısını sustur
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning);
                // StudySession → Lesson required FK + global filter uyarısı:
                // StudySession'lar arşiv amaçlı silinmiş lesson'a ait olabilir.
                // Bu kasıtlı bir tasarım kararı; uyarı bastırılıyor.
                warnings.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning);
            });

            base.OnConfiguring(optionsBuilder);
        }

        // İki farklı metodu TEK bir OnModelCreating içinde birleştirdik 👇
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Identity tabloları (AspNetUsers vs.) için zorunlu:
            base.OnModelCreating(modelBuilder);

            // --- USER İLİŞKİLERİ (AUTH) ---
            modelBuilder.Entity<Lesson>()
                .HasOne(l => l.User)
                .WithMany(u => u.Lessons)
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TaskItem>()
                .HasOne(t => t.User)
                .WithMany(u => u.Tasks)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StudySession>()
                .HasOne(s => s.User)
                .WithMany(u => u.StudySessions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- TASKITEM İLİŞKİ AYARLARI (Hata Çözümü) ---
            modelBuilder.Entity<TaskItem>()
                .HasOne(t => t.Lesson)           // ÖNEMLİ: Generic <Lesson> yerine lambda (t.Lesson) kullandık.
                .WithMany()                      // Lesson içinde "Tasks" listesi yoksa boş bırakılır.
                .HasForeignKey(t => t.LessonId)  // FK'nın LessonId olduğunu netleştiriyoruz.
                .OnDelete(DeleteBehavior.Restrict); // Lesson silinirse hata ver (veya SetNull yapabilirsiniz).

            // --- GLOBAL QUERY FILTERS (Soft Delete + Tenant/User Isolation) ---
            // Tüm sorgulara otomatik olarak UserId VE IsDeleted kontrolü eklenir.
            modelBuilder.Entity<Lesson>().HasQueryFilter(l =>
                !l.IsDeleted &&
                (IsSystemContext || (!string.IsNullOrWhiteSpace(CurrentUserId) && l.UserId == CurrentUserId)));
            modelBuilder.Entity<TaskItem>().HasQueryFilter(t =>
                !t.IsDeleted &&
                (IsSystemContext || (!string.IsNullOrWhiteSpace(CurrentUserId) && t.UserId == CurrentUserId)));
            modelBuilder.Entity<StudySession>().HasQueryFilter(s =>
                !s.IsDeleted &&
                (IsSystemContext || (!string.IsNullOrWhiteSpace(CurrentUserId) && s.UserId == CurrentUserId)));
            modelBuilder.Entity<Notification>().HasQueryFilter(n =>
                !n.IsDeleted &&
                (IsSystemContext || (!string.IsNullOrWhiteSpace(CurrentUserId) && n.UserId == CurrentUserId)));
            modelBuilder.Entity<DashboardSummaryView>().HasQueryFilter(v =>
                IsSystemContext || (!string.IsNullOrWhiteSpace(CurrentUserId) && v.UserId == CurrentUserId));

            // --- STUDYSESSION İLİŞKİ AYARLARI ---
            modelBuilder.Entity<StudySession>()
                .HasOne<TaskItem>()              // TaskItem ile ilişki
                .WithMany()
                .HasForeignKey(s => s.TaskId)
                .OnDelete(DeleteBehavior.Cascade); // Task silinirse ona bağlı çalışma oturumları da silinsin.

            modelBuilder.Entity<StudySession>()
                .HasOne(s => s.Lesson)
                .WithMany()
                .HasForeignKey(s => s.LessonId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- STUDYSESSION SINGLE ACTIVE SESSION CONSTRAINT ---
            modelBuilder.Entity<StudySession>()
                .HasIndex(s => s.UserId)
                .IsUnique()
                .HasFilter("[EndedAt] IS NULL AND [IsDeleted] = 0");

            // --- VIEW AYARLARI ---
            // EF Core'a bunun bir VIEW olduğunu ve Key'i olmadığını söylüyoruz
            modelBuilder.Entity<DashboardSummaryView>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("v_DashboardSummary", "dbo"); // Veritabanındaki View'ın tam adı
            });
        }
        
        // --- OTOMATİK USERID ATAMASI (AUDIT) ---
        public override int SaveChanges()
        {
            ApplyUserIdToEntities();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyUserIdToEntities();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void ApplyUserIdToEntities()
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId)) return;

            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added);

            foreach (var entry in entries)
            {
                // Reflection ile UserId property'sini bul ve doldur
                var userIdProperty = entry.Metadata.FindProperty("UserId");
                if (userIdProperty != null && entry.Property("UserId").CurrentValue == null)
                {
                    entry.Property("UserId").CurrentValue = userId;
                }
            }
        }
    }
}