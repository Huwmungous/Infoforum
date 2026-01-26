-- Migration: Refactor to use app_domain as single key
-- The realm moves into bootstrap_config JSONB

-- Step 1: Add app_domain column
ALTER TABLE usr_svc_settings ADD COLUMN IF NOT EXISTS app_domain VARCHAR(240);

-- Step 2: Populate app_domain from existing data (use client as app_domain initially)
UPDATE usr_svc_settings SET app_domain = client WHERE app_domain IS NULL;

-- Step 3: Move realm into bootstrap_config JSONB
UPDATE usr_svc_settings 
SET bootstrap_config = bootstrap_config || jsonb_build_object('realm', realm)
WHERE bootstrap_config IS NOT NULL 
  AND NOT bootstrap_config ? 'realm';

-- Step 4: Make app_domain NOT NULL and add unique constraint
ALTER TABLE usr_svc_settings ALTER COLUMN app_domain SET NOT NULL;

-- Step 5: Drop old columns (do this manually after verifying data)
-- ALTER TABLE usr_svc_settings DROP COLUMN realm;
-- ALTER TABLE usr_svc_settings DROP COLUMN client;

-- Step 6: Add unique constraint on app_domain
-- ALTER TABLE usr_svc_settings ADD CONSTRAINT uq_app_domain UNIQUE (app_domain);

-- Note: Steps 5 and 6 are commented out - run manually after verifying the migration worked
