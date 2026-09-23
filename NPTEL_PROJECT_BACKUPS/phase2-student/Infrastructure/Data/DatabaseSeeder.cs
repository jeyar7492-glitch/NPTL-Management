using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Enums;
using NPTELManagement.Core.Interfaces;
using ExamStatusEntity = NPTELManagement.Core.Entities.ExamStatus;

namespace NPTELManagement.Infrastructure.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, IPasswordHasher passwordHasher)
    {
        // 1. Department
        if (!await context.Departments.AnyAsync(d => d.Code == "CSE"))
        {
            context.Departments.Add(new Department
            {
                DepartmentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "CSE",
                Name = "Computer Science and Engineering"
            });
            await context.SaveChangesAsync();
        }

        // 2. Classes
        if (!await context.Classes.AnyAsync())
        {
            context.Classes.AddRange(
                new ClassEntity { ClassId = Guid.Parse("22222222-2222-2222-2222-222222222201"), Department = "CSE", Year = 1, Section = "A" },
                new ClassEntity { ClassId = Guid.Parse("22222222-2222-2222-2222-222222222202"), Department = "CSE", Year = 2, Section = "A" },
                new ClassEntity { ClassId = Guid.Parse("22222222-2222-2222-2222-222222222203"), Department = "CSE", Year = 3, Section = "A" },
                new ClassEntity { ClassId = Guid.Parse("22222222-2222-2222-2222-222222222204"), Department = "CSE", Year = 4, Section = "A" }
            );
            await context.SaveChangesAsync();
        }

        // 3. Courses
        var course1Id = Guid.Parse("33333333-3333-3333-3333-333333333301");
        var course2Id = Guid.Parse("33333333-3333-3333-3333-333333333302");

        var course1 = await context.Courses.FirstOrDefaultAsync(c => c.CourseId == course1Id);
        if (course1 == null)
        {
            course1 = new Course
            {
                CourseId = course1Id,
                CourseCode = "noc24-cs01",
                CourseName = "Programming in Java",
                DurationWeeks = 12,
                CourseStartDate = DateTime.UtcNow.AddDays(-60),
                CourseEndDate = DateTime.UtcNow.AddDays(30)
            };
            context.Courses.Add(course1);
        }
        else
        {
            course1.CourseStartDate ??= DateTime.UtcNow.AddDays(-60);
            course1.CourseEndDate ??= DateTime.UtcNow.AddDays(30);
        }

        var course2 = await context.Courses.FirstOrDefaultAsync(c => c.CourseId == course2Id);
        if (course2 == null)
        {
            course2 = new Course
            {
                CourseId = course2Id,
                CourseCode = "noc24-cs02",
                CourseName = "Design and Analysis of Algorithms",
                DurationWeeks = 8,
                CourseStartDate = DateTime.UtcNow.AddDays(-30),
                CourseEndDate = DateTime.UtcNow.AddDays(30)
            };
            context.Courses.Add(course2);
        }
        else
        {
            course2.CourseStartDate ??= DateTime.UtcNow.AddDays(-30);
            course2.CourseEndDate ??= DateTime.UtcNow.AddDays(30);
        }
        await context.SaveChangesAsync();

        // 4. Admin User
        var adminUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        if (!await context.Users.AnyAsync(u => u.Username == "admin_cse_01"))
        {
            var adminUser = new User
            {
                Id = adminUserId,
                Username = "admin_cse_01",
                PasswordHash = passwordHasher.HashPassword("Admin@Nptel2026"),
                Role = UserRole.Admin,
                Email = "admin.cse@college.edu",
                IsActive = true
            };
            context.Users.Add(adminUser);

            var admin = new Admin
            {
                AdminId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"),
                UserId = adminUserId,
                AdminIdentifier = "ADM-CSE-01"
            };
            context.Admins.Add(admin);
            await context.SaveChangesAsync();
        }

        // 5. Staff User (CSE 3rd Year In-Charge)
        var staffUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        if (!await context.Users.AnyAsync(u => u.Username == "CSE-STF-01"))
        {
            var staffUser = new User
            {
                Id = staffUserId,
                Username = "CSE-STF-01",
                PasswordHash = passwordHasher.HashPassword("Staff@Nptel2026"),
                Role = UserRole.Staff,
                Email = "staff.cse01@college.edu",
                IsActive = true
            };
            context.Users.Add(staffUser);

            var staff = new Staff
            {
                StaffId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01"),
                UserId = staffUserId,
                StaffName = "Dr. K. Ramanathan",
                StaffIdentifier = "CSE-STF-01",
                Department = "CSE",
                AssignedYear = 3,
                AssignedClass = "A"
            };
            context.StaffMembers.Add(staff);
            await context.SaveChangesAsync();
        }

        // 6. Student 1 (CSE 3rd Year - Assigned to Staff 1)
        var student1UserId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var student1Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccc01");
        if (!await context.Users.AnyAsync(u => u.Username == "951021104001"))
        {
            var studentUser = new User
            {
                Id = student1UserId,
                Username = "951021104001",
                PasswordHash = passwordHasher.HashPassword("Student@Nptel2026"),
                Role = UserRole.Student,
                Email = "student.951021104001@college.edu",
                IsActive = true
            };
            context.Users.Add(studentUser);

            var student = new Student
            {
                StudentId = student1Id,
                UserId = student1UserId,
                Name = "Aravind Swaminathan",
                RegisterNumber = "951021104001",
                Department = "CSE",
                ClassSection = "A",
                Year = 3,
                Batch = "2021-2025",
                Email = "student.951021104001@college.edu",
                Phone = "9876543210"
            };
            context.Students.Add(student);
            await context.SaveChangesAsync();
        }

        // Student 1 Registration (Programming in Java - InProgress)
        var reg1Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd01");
        var reg1 = await context.NptelRegistrations.FirstOrDefaultAsync(r => r.RegistrationId == reg1Id);
        if (reg1 == null)
        {
            reg1 = new NptelRegistration
            {
                RegistrationId = reg1Id,
                StudentId = student1Id,
                CourseId = course1Id,
                EnrollmentDate = DateTime.UtcNow.AddDays(-60),
                Status = RegistrationStatus.InProgress
            };
            context.NptelRegistrations.Add(reg1);
            await context.SaveChangesAsync();
        }
        else
        {
            reg1.Status = RegistrationStatus.InProgress;
            await context.SaveChangesAsync();
        }

        // Student 1 Timeline (11 Stages)
        if (!await context.CourseTimelines.AnyAsync(t => t.RegistrationId == reg1Id))
        {
            var timelineItems = new List<CourseTimeline>
            {
                new() { TimelineId = Guid.NewGuid(), RegistrationId = reg1Id, Title = "Course Registered", Description = "Enrolled via SWAYAM portal for Programming in Java.", Status = "Completed", EventDate = DateTime.UtcNow.AddDays(-60), DisplayOrder = 1, WeekNumber = 0 },
                new() { TimelineId = Guid.NewGuid(), RegistrationId = reg1Id, Title = "Course In Progress", Description = "Active participation in weekly lectures and assessments.", Status = "Completed", EventDate = DateTime.UtcNow.AddDays(-55), DisplayOrder = 2, WeekNumber = 1 },
                new() { TimelineId = Guid.NewGuid(), RegistrationId = reg1Id, Title = "Course Completed", Description = "12 weeks curriculum and all mandatory assignments submitted.", Status = "Current", EventDate = DateTime.UtcNow.AddDays(30), DisplayOrder = 3, WeekNumber = 12 },
                new() { TimelineId = Guid.NewGuid(), RegistrationId = reg1Id, Title = "Exam Application Pending", Description = "Registration window opened on NPTEL portal.", Status = "Completed", EventDate = DateTime.UtcNow.AddDays(-35), DisplayOrder = 4 },
                new() { TimelineId = Guid.NewGuid(), RegistrationId = reg1Id, Title = "Exam Applied", Description = "Examination fee paid and test centre selected.", Status = "Completed", EventDate = DateTime.UtcNow.AddDays(-20), DisplayOrder = 5 },
                new() { TimelineId = Guid.NewGuid(), RegistrationId = reg1Id, Title = "Exam Completed", Description = "Proctored in-person examination.", Status = "Pending", EventDate = DateTime.UtcNow.AddDays(15), DisplayOrder = 6 },
                new() { TimelineId = Guid.NewGuid(), RegistrationId = reg1Id, Title = "Certificate Submission Pending", Description = "Awaiting official score and e-certificate release.", Status = "Pending", DisplayOrder = 7 },
                new() { TimelineId = Guid.NewGuid(), RegistrationId = reg1Id, Title = "Certificate Submitted", Description = "E-Certificate uploaded by student for department records.", Status = "Pending", DisplayOrder = 8 },
                new() { TimelineId = Guid.NewGuid(), RegistrationId = reg1Id, Title = "Certificate Under Verification", Description = "Staff coordinator reviewing grade and authenticity.", Status = "Pending", DisplayOrder = 9 },
                new() { TimelineId = Guid.NewGuid(), RegistrationId = reg1Id, Title = "Certificate Verified", Description = "Certificate successfully verified by faculty in-charge.", Status = "Pending", DisplayOrder = 10 },
                new() { TimelineId = Guid.NewGuid(), RegistrationId = reg1Id, Title = "Certificate Received", Description = "Credit transfer and final administrative sign-off.", Status = "Pending", DisplayOrder = 11 }
            };
            context.CourseTimelines.AddRange(timelineItems);
            await context.SaveChangesAsync();
        }

        // Student 1 Exam Status
        var exam1 = await context.ExamStatuses.FirstOrDefaultAsync(e => e.RegistrationId == reg1Id);
        if (exam1 == null)
        {
            context.ExamStatuses.Add(new ExamStatusEntity
            {
                ExamStatusId = Guid.NewGuid(),
                RegistrationId = reg1Id,
                ExamApplicationStatus = "Applied",
                ExamApplicationDate = DateTime.UtcNow.AddDays(-20),
                ExamApplicationDeadline = DateTime.UtcNow.AddDays(-10),
                ExamDate = DateTime.UtcNow.AddDays(15),
                HallTicketStatus = "Available",
                Status = "Scheduled",
                Score = null,
                PassStatus = null
            });
            await context.SaveChangesAsync();
        }
        else
        {
            exam1.ExamApplicationStatus = "Applied";
            exam1.ExamApplicationDate = DateTime.UtcNow.AddDays(-20);
            exam1.ExamApplicationDeadline = DateTime.UtcNow.AddDays(-10);
            exam1.ExamDate = DateTime.UtcNow.AddDays(15);
            exam1.HallTicketStatus = "Available";
            exam1.Status = "Scheduled";
            await context.SaveChangesAsync();
        }

        // Student 1 Certificate
        var cert1 = await context.Certificates.FirstOrDefaultAsync(c => c.RegistrationId == reg1Id);
        if (cert1 == null)
        {
            context.Certificates.Add(new Certificate
            {
                CertificateId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01"),
                RegistrationId = reg1Id,
                StoragePath = null,
                IssuedDate = null,
                VerifiedStatus = CertificateStatus.Pending
            });
            await context.SaveChangesAsync();
        }
        else
        {
            cert1.VerifiedStatus = CertificateStatus.Pending;
            cert1.StoragePath = null;
            await context.SaveChangesAsync();
        }

        // Student 1 Notifications
        if (!await context.Notifications.AnyAsync(n => n.UserId == student1UserId))
        {
            context.Notifications.AddRange(
                new Notification
                {
                    NotificationId = Guid.Parse("11111111-2222-3333-4444-555555555501"),
                    UserId = student1UserId,
                    Title = "Exam Application Confirmed",
                    Message = "Your exam application for Programming in Java is confirmed. Hall ticket is available.",
                    IsRead = false,
                    RelatedRegistrationId = reg1Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                },
                new Notification
                {
                    NotificationId = Guid.Parse("11111111-2222-3333-4444-555555555502"),
                    UserId = student1UserId,
                    Title = "Week 8 Assignment Released",
                    Message = "Week 8 programming assignment is now live on the NPTEL portal. Due date: Wednesday.",
                    IsRead = true,
                    RelatedRegistrationId = reg1Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-7)
                },
                new Notification
                {
                    NotificationId = Guid.Parse("11111111-2222-3333-4444-555555555503"),
                    UserId = student1UserId,
                    Title = "Hall Ticket Released",
                    Message = "Hall ticket for upcoming NPTEL proctored exam is now available.",
                    IsRead = false,
                    RelatedRegistrationId = reg1Id,
                    CreatedAt = DateTime.UtcNow.AddHours(-12)
                }
            );
            await context.SaveChangesAsync();
        }

        // 7. Student 2 (CSE 1st Year - Out-of-scope for Staff 1)
        var student2UserId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var student2Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffff02");
        if (!await context.Users.AnyAsync(u => u.Username == "951021104002"))
        {
            var student2User = new User
            {
                Id = student2UserId,
                Username = "951021104002",
                PasswordHash = passwordHasher.HashPassword("Student@Nptel2026"),
                Role = UserRole.Student,
                Email = "student.951021104002@college.edu",
                IsActive = true
            };
            context.Users.Add(student2User);

            var student2 = new Student
            {
                StudentId = student2Id,
                UserId = student2UserId,
                Name = "Bhavani S",
                RegisterNumber = "951021104002",
                Department = "CSE",
                ClassSection = "A",
                Year = 1, // 1st year (Staff 1 is 3rd year!)
                Batch = "2023-2027",
                Email = "student.951021104002@college.edu",
                Phone = "9876543211"
            };
            context.Students.Add(student2);
            await context.SaveChangesAsync();
        }

        // Student 2 Registration (Course 2 - Registered)
        var reg2Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd02");
        if (!await context.NptelRegistrations.AnyAsync(r => r.RegistrationId == reg2Id))
        {
            context.NptelRegistrations.Add(new NptelRegistration
            {
                RegistrationId = reg2Id,
                StudentId = student2Id,
                CourseId = course2Id,
                EnrollmentDate = DateTime.UtcNow.AddDays(-25),
                Status = RegistrationStatus.Registered
            });
            await context.SaveChangesAsync();

            var timeline2 = new List<CourseTimeline>
            {
                new() { TimelineId = Guid.NewGuid(), RegistrationId = reg2Id, Title = "Course Registered", Description = "Enrolled via SWAYAM portal for Design and Analysis of Algorithms.", Status = "Completed", EventDate = DateTime.UtcNow.AddDays(-25), DisplayOrder = 1, WeekNumber = 0 },
                new() { TimelineId = Guid.NewGuid(), RegistrationId = reg2Id, Title = "Course In Progress", Description = "Course starting soon.", Status = "Current", DisplayOrder = 2 },
                new() { TimelineId = Guid.NewGuid(), RegistrationId = reg2Id, Title = "Course Completed", Description = "8 weeks curriculum.", Status = "Pending", DisplayOrder = 3 }
            };
            context.CourseTimelines.AddRange(timeline2);

            context.ExamStatuses.Add(new ExamStatusEntity
            {
                ExamStatusId = Guid.NewGuid(),
                RegistrationId = reg2Id,
                ExamApplicationStatus = "NotStarted",
                Status = "NotStarted"
            });

            context.Certificates.Add(new Certificate
            {
                CertificateId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee02"),
                RegistrationId = reg2Id,
                VerifiedStatus = CertificateStatus.Pending
            });

            context.Notifications.Add(new Notification
            {
                NotificationId = Guid.Parse("11111111-2222-3333-4444-555555555599"),
                UserId = student2UserId,
                Title = "Welcome to NPTEL",
                Message = "You have successfully registered for Design and Analysis of Algorithms.",
                IsRead = false,
                RelatedRegistrationId = reg2Id,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });

            await context.SaveChangesAsync();
        }
    }
}
