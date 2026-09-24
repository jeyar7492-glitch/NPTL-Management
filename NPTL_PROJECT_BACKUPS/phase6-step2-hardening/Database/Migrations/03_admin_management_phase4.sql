-- ============================================================================
-- NPTEL Management System - Migration 03
-- Phase 4: Admin Management, Course Status, and Index Optimizations
-- Non-destructive, idempotent migration
-- ============================================================================

-- 1. COURSES TABLE EXTENSIONS
-- Support active/archived status for courses
ALTER TABLE courses
    ADD COLUMN IF NOT EXISTS status VARCHAR(50) NOT NULL DEFAULT 'Active';

-- 2. USERS TABLE INDEXING
-- Optimize active/inactive user filtering for dashboard metrics
CREATE INDEX IF NOT EXISTS idx_users_is_active ON users(is_active);
CREATE INDEX IF NOT EXISTS idx_users_role_is_active ON users(role, is_active);

-- 3. AUDIT LOGS INDEXING
-- Optimize audit log filtering by action and user
CREATE INDEX IF NOT EXISTS idx_audit_logs_action ON audit_logs(action);
CREATE INDEX IF NOT EXISTS idx_audit_logs_user_action ON audit_logs(user_id, action);

-- 4. CERTIFICATES VERIFIED STATUS INDEXING
-- Optimize certificate verification queue lookups
CREATE INDEX IF NOT EXISTS idx_certificates_verified_status ON certificates(verified_status);
