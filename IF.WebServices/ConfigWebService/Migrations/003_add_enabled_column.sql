-- Migration: Add enabled column for soft-disable functionality
-- Run after 001_add_bootstrap_config.sql

-- Add enabled column with default true (existing records remain active)
ALTER TABLE public.usr_svc_settings
ADD COLUMN IF NOT EXISTS enabled boolean NOT NULL DEFAULT true;

-- Create index for filtering by enabled status
CREATE INDEX IF NOT EXISTS idx_usr_svc_settings_enabled 
ON public.usr_svc_settings (enabled);

-- Verify the changes
SELECT column_name, data_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_schema = 'public' 
  AND table_name = 'usr_svc_settings'
ORDER BY ordinal_position;
