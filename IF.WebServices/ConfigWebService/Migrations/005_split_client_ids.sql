-- Migration: Add type column and create separate records per type
-- Run this against your ifconfig database

-- Step 1: Create new table with type column
CREATE TABLE IF NOT EXISTS public.usr_svc_settings_new (
    idx SERIAL PRIMARY KEY,
    app_domain VARCHAR(100) NOT NULL,
    type VARCHAR(20) NOT NULL CHECK (type IN ('bootstrap', 'user', 'service')),
    config JSONB,
    UNIQUE(app_domain, type)
);

-- Step 2: Drop old table and rename new
DROP TABLE IF EXISTS public.usr_svc_settings;
ALTER TABLE public.usr_svc_settings_new RENAME TO usr_svc_settings;

-- Step 3: Insert Infoforum bootstrap config (shared auth settings)
INSERT INTO public.usr_svc_settings (app_domain, type, config)
VALUES (
    'Infoforum',
    'bootstrap',
    '{
        "realm": "LongmanRd",
        "openIdConfig": "https://longmanrd.net/auth/realms",
        "loggerService": "https://longmanrd.net/logger",
        "userClientId": "infoforum-user",
        "serviceClientId": "infoforum-service"
    }'::jsonb
);

-- Step 4: Insert Infoforum user config (app-specific settings)
INSERT INTO public.usr_svc_settings (app_domain, type, config)
VALUES (
    'Infoforum',
    'user',
    '{
        "logLevel": "Information"
    }'::jsonb
);

-- Step 5: Insert Infoforum service config (service-specific settings)
INSERT INTO public.usr_svc_settings (app_domain, type, config)
VALUES (
    'Infoforum',
    'service',
    '{
        "logLevel": "Information"
    }'::jsonb
);

-- Verify the results
SELECT app_domain, type, config FROM public.usr_svc_settings ORDER BY app_domain, type;
