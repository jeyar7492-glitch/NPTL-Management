-- ============================================================================
-- NPTEL Management System - Migration 02
-- Student NPTEL Workflow Schema Extension
-- Non-destructive, idempotent migration
-- ============================================================================

-- 1. COURSES TABLE EXTENSIONS
-- Support real course start and end dates
ALTER TABLE courses
    ADD COLUMN IF NOT EXISTS course_start_date TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS course_end_date TIMESTAMPTZ NULL;

-- 2. COURSE TIMELINE TABLE EXTENSIONS
-- Full milestone representations with titles, descriptions, dates, and order
ALTER TABLE course_timeline
    ADD COLUMN IF NOT EXISTS title VARCHAR(255) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS description TEXT NULL,
    ADD COLUMN IF NOT EXISTS event_date TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS display_order INT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ NOT NULL DEFAULT NOW();

-- 3. EXAM STATUS TABLE EXTENSIONS
-- Support exam application tracking and application deadlines
ALTER TABLE exam_status
    ADD COLUMN IF NOT EXISTS exam_application_status VARCHAR(50) NULL DEFAULT 'NotStarted',
    ADD COLUMN IF NOT EXISTS exam_application_date TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS exam_application_deadline TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS exam_status VARCHAR(50) NULL DEFAULT 'NotStarted';

-- 4. CERTIFICATES TABLE EXTENSIONS
-- Support complete certificate submission, verification, and receipt tracking
ALTER TABLE certificates
    ADD COLUMN IF NOT EXISTS submitted_date TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS verified_date TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS received_date TIMESTAMPTZ NULL;

-- 5. NOTIFICATIONS TABLE EXTENSIONS
-- Link notifications to specific NPTEL course registrations
ALTER TABLE notifications
    ADD COLUMN IF NOT EXISTS related_registration_id UUID NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'fk_notifications_registration'
    ) THEN
        ALTER TABLE notifications
            ADD CONSTRAINT fk_notifications_registration
            FOREIGN KEY (related_registration_id) 
            REFERENCES nptel_registrations(registration_id) 
            ON DELETE SET NULL;
    END IF;
END $$;

-- 6. INDEXES FOR WORKFLOW QUERY OPTIMIZATION
CREATE INDEX IF NOT EXISTS idx_course_timeline_registration_id 
    ON course_timeline(registration_id);

CREATE INDEX IF NOT EXISTS idx_course_timeline_display_order 
    ON course_timeline(registration_id, display_order);

CREATE INDEX IF NOT EXISTS idx_notifications_related_registration 
    ON notifications(related_registration_id);

CREATE INDEX IF NOT EXISTS idx_nptel_registrations_status 
    ON nptel_registrations(status);
