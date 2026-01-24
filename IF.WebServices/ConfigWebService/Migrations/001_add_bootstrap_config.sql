-- Migration: Add bootstrap_config column and convert user_config/service_config to jsonb
-- Run against the if_config database

-- First, add bootstrap_config column as jsonb
ALTER TABLE public.usr_svc_settings
ADD COLUMN IF NOT EXISTS bootstrap_config jsonb;

-- Convert user_config from varchar to jsonb
-- This requires a temp column approach to safely convert
ALTER TABLE public.usr_svc_settings
ADD COLUMN IF NOT EXISTS user_config_jsonb jsonb;

UPDATE public.usr_svc_settings
SET user_config_jsonb = user_config::jsonb
WHERE user_config IS NOT NULL AND user_config != '';

ALTER TABLE public.usr_svc_settings
DROP COLUMN user_config;

ALTER TABLE public.usr_svc_settings
RENAME COLUMN user_config_jsonb TO user_config;

-- Convert service_config from varchar to jsonb
ALTER TABLE public.usr_svc_settings
ADD COLUMN IF NOT EXISTS service_config_jsonb jsonb;

UPDATE public.usr_svc_settings
SET service_config_jsonb = service_config::jsonb
WHERE service_config IS NOT NULL AND service_config != '';

ALTER TABLE public.usr_svc_settings
DROP COLUMN service_config;

ALTER TABLE public.usr_svc_settings
RENAME COLUMN service_config_jsonb TO service_config;

-- Add unique constraint on realm + client (if not already exists)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint 
        WHERE conname = 'uq_usr_svc_settings_realm_client'
    ) THEN
        ALTER TABLE public.usr_svc_settings
        ADD CONSTRAINT uq_usr_svc_settings_realm_client 
        UNIQUE (realm, client);
    END IF;
END $$;

-- Create index for faster lookups by realm and client
CREATE INDEX IF NOT EXISTS idx_usr_svc_settings_realm_client 
ON public.usr_svc_settings (realm, client);

-- Verify the changes
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_schema = 'public' 
  AND table_name = 'usr_svc_settings'
ORDER BY ordinal_position;
