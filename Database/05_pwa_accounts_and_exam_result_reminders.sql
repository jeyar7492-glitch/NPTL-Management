-- PWA student account fields + automatic result reminder tracking
ALTER TABLE students
    ADD COLUMN IF NOT EXISTS semester integer NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS academic_year varchar(20);

ALTER TABLE exam_status
    ADD COLUMN IF NOT EXISTS last_result_reminder_date timestamptz;

CREATE INDEX IF NOT EXISTS idx_exam_status_result_reminder
    ON exam_status (exam_date, last_result_reminder_date)
    WHERE exam_date IS NOT NULL AND score IS NULL;
