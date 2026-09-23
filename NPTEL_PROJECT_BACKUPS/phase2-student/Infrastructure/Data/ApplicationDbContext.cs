using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Enums;
using ExamStatusEntity = NPTELManagement.Core.Entities.ExamStatus;

namespace NPTELManagement.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Staff> StaffMembers => Set<Staff>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<ClassEntity> Classes => Set<ClassEntity>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<NptelRegistration> NptelRegistrations => Set<NptelRegistration>();
    public DbSet<CourseTimeline> CourseTimelines => Set<CourseTimeline>();
    public DbSet<ExamStatusEntity> ExamStatuses => Set<ExamStatusEntity>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Users
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.Property(e => e.Role)
                .HasColumnName("role")
                .HasMaxLength(20)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        });

        // 2. Students
        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("students");
            entity.HasKey(e => e.StudentId);
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
            entity.Property(e => e.RegisterNumber).HasColumnName("register_number").HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.RegisterNumber).IsUnique();
            entity.Property(e => e.Department).HasColumnName("department").HasMaxLength(50).IsRequired();
            entity.Property(e => e.ClassSection).HasColumnName("class_section").HasMaxLength(50);
            entity.Property(e => e.Year).HasColumnName("year").IsRequired();
            entity.Property(e => e.Batch).HasColumnName("batch").HasMaxLength(50);
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255);
            entity.Property(e => e.Phone).HasColumnName("phone").HasMaxLength(20);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

            entity.HasOne(e => e.User)
                .WithOne(u => u.Student)
                .HasForeignKey<Student>(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 3. Staff
        modelBuilder.Entity<Staff>(entity =>
        {
            entity.ToTable("staff");
            entity.HasKey(e => e.StaffId);
            entity.Property(e => e.StaffId).HasColumnName("staff_id");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.StaffName).HasColumnName("staff_name").HasMaxLength(150).IsRequired();
            entity.Property(e => e.StaffIdentifier).HasColumnName("staff_identifier").HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.StaffIdentifier).IsUnique();
            entity.Property(e => e.Department).HasColumnName("department").HasMaxLength(50).IsRequired();
            entity.Property(e => e.AssignedYear).HasColumnName("assigned_year").IsRequired();
            entity.Property(e => e.AssignedClass).HasColumnName("assigned_class").HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

            entity.HasOne(e => e.User)
                .WithOne(u => u.Staff)
                .HasForeignKey<Staff>(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 4. Admins
        modelBuilder.Entity<Admin>(entity =>
        {
            entity.ToTable("admins");
            entity.HasKey(e => e.AdminId);
            entity.Property(e => e.AdminId).HasColumnName("admin_id");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.AdminIdentifier).HasColumnName("admin_identifier").HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.AdminIdentifier).IsUnique();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

            entity.HasOne(e => e.User)
                .WithOne(u => u.Admin)
                .HasForeignKey<Admin>(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 5. Departments
        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("departments");
            entity.HasKey(e => e.DepartmentId);
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        });

        // 6. Classes
        modelBuilder.Entity<ClassEntity>(entity =>
        {
            entity.ToTable("classes");
            entity.HasKey(e => e.ClassId);
            entity.Property(e => e.ClassId).HasColumnName("class_id");
            entity.Property(e => e.Department).HasColumnName("department").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Year).HasColumnName("year").IsRequired();
            entity.Property(e => e.Section).HasColumnName("section").HasMaxLength(10).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            entity.HasIndex(e => new { e.Department, e.Year, e.Section }).IsUnique();
        });

        // 7. Courses
        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("courses");
            entity.HasKey(e => e.CourseId);
            entity.Property(e => e.CourseId).HasColumnName("course_id");
            entity.Property(e => e.CourseCode).HasColumnName("course_code").HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.CourseCode).IsUnique();
            entity.Property(e => e.CourseName).HasColumnName("course_name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.DurationWeeks).HasColumnName("duration_weeks").IsRequired();
            entity.Property(e => e.CourseStartDate).HasColumnName("course_start_date").HasColumnType("timestamp with time zone");
            entity.Property(e => e.CourseEndDate).HasColumnName("course_end_date").HasColumnType("timestamp with time zone");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        });

        // 8. NptelRegistrations
        modelBuilder.Entity<NptelRegistration>(entity =>
        {
            entity.ToTable("nptel_registrations");
            entity.HasKey(e => e.RegistrationId);
            entity.Property(e => e.RegistrationId).HasColumnName("registration_id");
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.CourseId).HasColumnName("course_id").IsRequired();
            entity.Property(e => e.EnrollmentDate).HasColumnName("enrollment_date").HasColumnType("timestamp with time zone");
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(50)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

            entity.HasIndex(e => new { e.StudentId, e.CourseId }).IsUnique();

            entity.HasOne(e => e.Student)
                .WithMany(s => s.Registrations)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Course)
                .WithMany(c => c.Registrations)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 9. CourseTimeline
        modelBuilder.Entity<CourseTimeline>(entity =>
        {
            entity.ToTable("course_timeline");
            entity.HasKey(e => e.TimelineId);
            entity.Property(e => e.TimelineId).HasColumnName("timeline_id");
            entity.Property(e => e.RegistrationId).HasColumnName("registration_id").IsRequired();
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
            entity.Property(e => e.EventDate).HasColumnName("event_date").HasColumnType("timestamp with time zone");
            entity.Property(e => e.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
            entity.Property(e => e.WeekNumber).HasColumnName("week_number");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

            entity.HasOne(e => e.Registration)
                .WithMany(r => r.Timeline)
                .HasForeignKey(e => e.RegistrationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 10. ExamStatus
        modelBuilder.Entity<ExamStatusEntity>(entity =>
        {
            entity.ToTable("exam_status");
            entity.HasKey(e => e.ExamStatusId);
            entity.Property(e => e.ExamStatusId).HasColumnName("exam_status_id");
            entity.Property(e => e.RegistrationId).HasColumnName("registration_id").IsRequired();
            entity.HasIndex(e => e.RegistrationId).IsUnique();
            entity.Property(e => e.ExamApplicationStatus).HasColumnName("exam_application_status").HasMaxLength(50);
            entity.Property(e => e.ExamApplicationDate).HasColumnName("exam_application_date").HasColumnType("timestamp with time zone");
            entity.Property(e => e.ExamApplicationDeadline).HasColumnName("exam_application_deadline").HasColumnType("timestamp with time zone");
            entity.Property(e => e.ExamDate).HasColumnName("exam_date").HasColumnType("timestamp with time zone");
            entity.Property(e => e.HallTicketStatus).HasColumnName("hall_ticket_status").HasMaxLength(50);
            entity.Property(e => e.Status).HasColumnName("exam_status").HasMaxLength(50);
            entity.Property(e => e.Score).HasColumnName("score").HasPrecision(5, 2);
            entity.Property(e => e.PassStatus).HasColumnName("pass_status").HasMaxLength(50);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

            entity.HasOne(e => e.Registration)
                .WithOne(r => r.ExamStatus)
                .HasForeignKey<ExamStatusEntity>(e => e.RegistrationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 11. Certificates
        modelBuilder.Entity<Certificate>(entity =>
        {
            entity.ToTable("certificates");
            entity.HasKey(e => e.CertificateId);
            entity.Property(e => e.CertificateId).HasColumnName("certificate_id");
            entity.Property(e => e.RegistrationId).HasColumnName("registration_id").IsRequired();
            entity.HasIndex(e => e.RegistrationId).IsUnique();
            entity.Property(e => e.StoragePath).HasColumnName("storage_path");
            entity.Property(e => e.SubmittedDate).HasColumnName("submitted_date").HasColumnType("timestamp with time zone");
            entity.Property(e => e.IssuedDate).HasColumnName("issued_date").HasColumnType("timestamp with time zone");
            entity.Property(e => e.VerifiedDate).HasColumnName("verified_date").HasColumnType("timestamp with time zone");
            entity.Property(e => e.ReceivedDate).HasColumnName("received_date").HasColumnType("timestamp with time zone");
            entity.Property(e => e.VerifiedStatus)
                .HasColumnName("verified_status")
                .HasMaxLength(50)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

            entity.HasOne(e => e.Registration)
                .WithOne(r => r.Certificate)
                .HasForeignKey<Certificate>(e => e.RegistrationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 12. Notifications
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(e => e.NotificationId);
            entity.Property(e => e.NotificationId).HasColumnName("notification_id");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Message).HasColumnName("message").IsRequired();
            entity.Property(e => e.IsRead).HasColumnName("is_read").HasDefaultValue(false);
            entity.Property(e => e.RelatedRegistrationId).HasColumnName("related_registration_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");

            entity.HasOne(e => e.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.RelatedRegistration)
                .WithMany()
                .HasForeignKey(e => e.RelatedRegistrationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 13. AuditLogs
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.LogId);
            entity.Property(e => e.LogId).HasColumnName("log_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Details).HasColumnName("details");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(50);
            entity.Property(e => e.Timestamp).HasColumnName("timestamp").HasColumnType("timestamp with time zone");

            entity.HasOne(e => e.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
