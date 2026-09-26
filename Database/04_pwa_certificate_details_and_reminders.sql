-- Phase PWA: certificate metadata + in-app reminder support
-- Apply once to an existing PostgreSQL/Supabase database.

ALTER TABLE certificates
    ADD COLUMN IF NOT EXISTS certificate_number varchar(100),
    ADD COLUMN IF NOT EXISTS score numeric(5,2),
    ADD COLUMN IF NOT EXISTS pass_status varchar(50),
    ADD COLUMN IF NOT EXISTS reminder_enabled boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS reminder_date timestamptz,
    ADD COLUMN IF NOT EXISTS last_reminder_sent_date timestamptz;

CREATE INDEX IF NOT EXISTS idx_certificates_reminder_due
    ON certificates (reminder_enabled, reminder_date)
    WHERE reminder_enabled = true AND reminder_date IS NOT NULL;
