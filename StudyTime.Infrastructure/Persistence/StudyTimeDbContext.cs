using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StudyTime.Domain.Entities;

namespace StudyTime.Infrastructure.Persistence
{
    public class StudyTimeDbContext(DbContextOptions<StudyTimeDbContext> options) : DbContext(options)
    {
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
            base.OnModelCreating(modelBuilder);

            // --- TASKITEM İLİŞKİ AYARLARI (Hata Çözümü) ---
            modelBuilder.Entity<TaskItem>()
                .HasOne(t => t.Lesson)           // ÖNEMLİ: Generic <Lesson> yerine lambda (t.Lesson) kullandık.
                .WithMany()                      // Lesson içinde "Tasks" listesi yoksa boş bırakılır.
                .HasForeignKey(t => t.LessonId)  // FK'nın LessonId olduğunu netleştiriyoruz.
                .OnDelete(DeleteBehavior.Restrict); // Lesson silinirse hata ver (veya SetNull yapabilirsiniz).

            // --- GLOBAL QUERY FILTERS (Soft Delete) ---
            // Tüm Lesson ve TaskItem sorgularına otomatik olarak WHERE IsDeleted = 0 eklenir.
            // Bypass için: .IgnoreQueryFilters() kullan (örn. Admin raporu, restore işlemi)
            modelBuilder.Entity<Lesson>().HasQueryFilter(l => !l.IsDeleted);
            modelBuilder.Entity<TaskItem>().HasQueryFilter(t => !t.IsDeleted);

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

            // --- VIEW AYARLARI ---
            // EF Core'a bunun bir VIEW olduğunu ve Key'i olmadığını söylüyoruz
            modelBuilder.Entity<DashboardSummaryView>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("v_DashboardSummary"); // Veritabanındaki View'ın tam adı
            });
        }
    }
}