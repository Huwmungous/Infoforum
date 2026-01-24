-- Migration: Populate bootstrap_config for existing rows
-- Run after 001_add_bootstrap_config.sql

-- Update existing rows to have bootstrap_config based on user_config/service_config patterns
-- This extracts common bootstrap values and creates a unified bootstrap_config

-- For SfdDevelopment_dev
UPDATE public.usr_svc_settings
SET bootstrap_config = jsonb_build_object(
    'realm', COALESCE(user_config->>'realm', user_config->>'Realm', realm),
    'userClientId', COALESCE(user_config->>'clientId', user_config->>'ClientId'),
    'serviceClientId', COALESCE(service_config->>'clientId', service_config->>'ClientId'),
    'serviceClientSecret', COALESCE(service_config->>'clientSecret', service_config->>'ClientSecret'),
    'openIdConfig', COALESCE(user_config->>'openIdConfig', user_config->>'OpenIdConfig'),
    'loggerService', COALESCE(user_config->>'loggerService', user_config->>'LoggerService'),
    'logLevel', COALESCE(user_config->>'logLevel', user_config->>'LogLevel', 'Information'),
    'allowedScopes', COALESCE(user_config->'allowedScopes', user_config->'AllowedScopes', '["openid", "profile", "email"]'::jsonb),
    'requiresRelay', COALESCE((user_config->>'requiresRelay')::boolean, (user_config->>'RequiresRelay')::boolean, false)
)
WHERE bootstrap_config IS NULL
  AND user_config IS NOT NULL;

-- Example of adding a new complete entry with all three configs
/*
INSERT INTO public.usr_svc_settings (realm, client, user_config, service_config, bootstrap_config)
VALUES (
    'ExampleRealm',
    'example-client',
    '{
        "ClientId": "example-usr",
        "LoggerService": "https://example.com/logs",
        "LogLevel": "Debug",
        "RequiresRelay": false,
        "AllowedScopes": ["openid", "profile", "email"]
    }'::jsonb,
    '{
        "ClientId": "example-svc",
        "ClientSecret": "your-secret-here",
        "LoggerService": "https://example.com/logs",
        "LogLevel": "Debug",
        "RequiresRelay": false
    }'::jsonb,
    '{
        "realm": "ExampleRealm",
        "userClientId": "example-usr",
        "serviceClientId": "example-svc",
        "serviceClientSecret": "your-secret-here",
        "openIdConfig": "https://example.com/auth/realms",
        "loggerService": "https://example.com/logs",
        "logLevel": "Debug",
        "allowedScopes": ["openid", "profile", "email"],
        "requiresRelay": false
    }'::jsonb
);
*/

-- Example of storing database connection configs in service_config
/*
UPDATE public.usr_svc_settings
SET service_config = service_config || '{
    "loggerdb": {
        "host": "intelligence",
        "port": 6543,
        "database": "If_Log",
        "userName": "logger_service",
        "password": "your-password-here",
        "requiresRelay": false
    },
    "firebirddb": {
        "host": "syden-ses-vm",
        "port": 3050,
        "database": "C:\\ReferenceDBs\\DEV\\ENGLAND.FDB",
        "userName": "your-user",
        "password": "your-password",
        "charset": "UTF8",
        "role": "RDB$USER",
        "requiresRelay": false
    }
}'::jsonb
WHERE realm = 'YourRealm' AND client = 'your-client';
*/

-- Verify the data
SELECT 
    idx,
    realm,
    client,
    bootstrap_config IS NOT NULL AS has_bootstrap,
    user_config IS NOT NULL AS has_user_config,
    service_config IS NOT NULL AS has_service_config
FROM public.usr_svc_settings
ORDER BY idx;
