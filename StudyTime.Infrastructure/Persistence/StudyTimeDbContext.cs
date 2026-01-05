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

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // "Bekleyen model değişikliği" uyarısını susturuyoruz
            optionsBuilder.ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning));

            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- TASKITEM İLİŞKİ AYARLARI (Hata Çözümü) ---
            modelBuilder.Entity<TaskItem>()
                .HasOne(t => t.Lesson)           // ÖNEMLİ: Generic <Lesson> yerine lambda (t.Lesson) kullandık.
                .WithMany()                      // Lesson içinde "Tasks" listesi yoksa boş bırakılır.
                .HasForeignKey(t => t.LessonId)  // FK'nın LessonId olduğunu netleştiriyoruz.
                .OnDelete(DeleteBehavior.Restrict); // Lesson silinirse hata ver (veya SetNull yapabilirsiniz).

            // --- STUDYSESSION İLİŞKİ AYARLARI ---
            modelBuilder.Entity<StudySession>()
                .HasOne<TaskItem>()              // TaskItem ile ilişki
                .WithMany()
                .HasForeignKey(s => s.TaskId)
                .OnDelete(DeleteBehavior.Cascade); // Task silinirse ona bağlı çalışma oturumları da silinsin.
        }
    }
}