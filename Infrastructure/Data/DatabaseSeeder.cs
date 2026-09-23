using Microsoft.EntityFrameworkCore;
using NPTELManagement.Core.Entities;
using NPTELManagement.Core.Enums;
using NPTELManagement.Core.Interfaces;

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
        if (!await context.Courses.AnyAsync())
        {
            context.Courses.AddRange(
                new Course
                {
                    CourseId = Guid.Parse("33333333-3333-3333-3333-333333333301"),
                    CourseCode = "noc24-cs01",
                    CourseName = "Programming in Java",
                    DurationWeeks = 12
                },
                new Course
                {
                    CourseId = Guid.Parse("33333333-3333-3333-3333-333333333302"),
                    CourseCode = "noc24-cs02",
                    CourseName = "Design and Analysis of Algorithms",
                    DurationWeeks = 8
                }
            );
            await context.SaveChangesAsync();
        }

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

            // Add Registration & Certificate for statistics testing
            var regId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd01");
            context.NptelRegistrations.Add(new NptelRegistration
            {
                RegistrationId = regId,
                StudentId = student1Id,
                CourseId = Guid.Parse("33333333-3333-3333-3333-333333333301"),
                EnrollmentDate = DateTime.UtcNow,
                Status = RegistrationStatus.Completed
            });

            context.Certificates.Add(new Certificate
            {
                CertificateId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01"),
                RegistrationId = regId,
                StoragePath = "certificates/student/cccccccc-cccc-cccc-cccc-cccccccccc01/cert.pdf",
                IssuedDate = DateTime.UtcNow,
                VerifiedStatus = CertificateStatus.Verified
            });

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
    }
}
