-- ============================================================================
-- NPTEL Management System - Initial Controlled Development Accounts Seed
-- Script: 02_seed_initial_accounts.sql
-- Description: Seeds master department, classes, sample course, and controlled
--              initial accounts for Admin, Staff, and Student roles.
--
-- SECURITY NOTICE:
--   NO PLAINTEXT PASSWORDS ARE STORED IN THIS SCRIPT.
--   The :ADMIN_PASSWORD_HASH, :STAFF_PASSWORD_HASH, and :STUDENT_PASSWORD_HASH
--   placeholders must be populated with cryptographically hashed passwords
--   (e.g., generated via the backend BCrypt / ASP.NET Identity PasswordHasher)
--   prior to execution, or supplied via environment parameters.
-- ============================================================================

-- 1. Master Department Seed
INSERT INTO departments (department_id, code, name)
VALUES 
    ('11111111-1111-1111-1111-111111111111', 'CSE', 'Computer Science and Engineering')
ON CONFLICT (code) DO NOTHING;

-- 2. Master Classes Seeds (CSE Years 1 through 4)
INSERT INTO classes (class_id, department, year, section)
VALUES 
    ('22222222-2222-2222-2222-222222222201', 'CSE', 1, 'A'),
    ('22222222-2222-2222-2222-222222222202', 'CSE', 2, 'A'),
    ('22222222-2222-2222-2222-222222222203', 'CSE', 3, 'A'),
    ('22222222-2222-2222-2222-222222222204', 'CSE', 4, 'A')
ON CONFLICT (department, year, section) DO NOTHING;

-- 3. Sample Courses Seed
INSERT INTO courses (course_id, course_code, course_name, duration_weeks)
VALUES 
    ('33333333-3333-3333-3333-333333333301', 'noc24-cs01', 'Programming in Java', 12),
    ('33333333-3333-3333-3333-333333333302', 'noc24-cs02', 'Design and Analysis of Algorithms', 8)
ON CONFLICT (course_code) DO NOTHING;

-- 4. Controlled Admin Development Account
-- Replace ':ADMIN_PASSWORD_HASH' with the BCrypt hash of your admin password.
DO $$
DECLARE
    v_admin_user_id UUID := 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
    v_admin_hash TEXT := '$2a$11$kIwxEiM6/ascQSajGFDtNuvDuuphjYZQfQh6S8nt8jILP2f263pBG';
BEGIN
    INSERT INTO users (id, username, password_hash, role, email, is_active)
    VALUES (v_admin_user_id, 'admin_cse_01', v_admin_hash, 'Admin', 'admin.cse@college.edu', TRUE)
    ON CONFLICT (username) DO UPDATE 
    SET password_hash = EXCLUDED.password_hash, updated_at = NOW();

    INSERT INTO admins (admin_id, user_id, admin_identifier)
    VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01', v_admin_user_id, 'ADM-CSE-01')
    ON CONFLICT (admin_identifier) DO NOTHING;
END $$;

-- 5. Controlled Staff Development Account (CSE 3rd Year In-Charge)
-- Replace ':STAFF_PASSWORD_HASH' with the BCrypt hash of your staff password.
DO $$
DECLARE
    v_staff_user_id UUID := 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
    v_staff_hash TEXT := '$2a$11$4ENk0Q.Fk3gp490XFyLu2.Eqi.TtHF803FUEHVgyFtZJ.M/lS77X6';
BEGIN
    INSERT INTO users (id, username, password_hash, role, email, is_active)
    VALUES (v_staff_user_id, 'CSE-STF-01', v_staff_hash, 'Staff', 'staff.cse01@college.edu', TRUE)
    ON CONFLICT (username) DO UPDATE 
    SET password_hash = EXCLUDED.password_hash, updated_at = NOW();

    INSERT INTO staff (staff_id, user_id, staff_name, staff_identifier, department, assigned_year, assigned_class)
    VALUES ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01', v_staff_user_id, 'Dr. K. Ramanathan', 'CSE-STF-01', 'CSE', 3, 'A')
    ON CONFLICT (staff_identifier) DO NOTHING;
END $$;

-- 6. Controlled Student Development Account (CSE 3rd Year)
-- Replace ':STUDENT_PASSWORD_HASH' with the BCrypt hash of your student password.
DO $$
DECLARE
    v_student_user_id UUID := 'cccccccc-cccc-cccc-cccc-cccccccccccc';
    v_student_hash TEXT := '$2a$11$y2OIIH7TKdmcA8AQ21YAKOhtwtYw7O7.azYyWCMs0M2N0AXyYgoCm';
BEGIN
    INSERT INTO users (id, username, password_hash, role, email, is_active)
    VALUES (v_student_user_id, '951021104001', v_student_hash, 'Student', 'student.951021104001@college.edu', TRUE)
    ON CONFLICT (username) DO UPDATE 
    SET password_hash = EXCLUDED.password_hash, updated_at = NOW();

    INSERT INTO students (student_id, user_id, name, register_number, department, class_section, year, batch, email, phone)
    VALUES ('cccccccc-cccc-cccc-cccc-cccccccccc01', v_student_user_id, 'Aravind Swaminathan', '951021104001', 'CSE', 'A', 3, '2021-2025', 'student.951021104001@college.edu', '9876543210')
    ON CONFLICT (register_number) DO NOTHING;
END $$;
