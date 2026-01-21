-- Migration: 001_add_method_source_code_and_cleanup.sql
-- Description: Adds source_code to method table and creates cleanup procedure
-- Date: 2026-01-09

-- Add source_code column to method table if it doesn't exist
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'method' AND column_name = 'source_code'
    ) THEN
        ALTER TABLE method ADD COLUMN source_code TEXT;
        COMMENT ON COLUMN method.source_code IS 'The full source code of the method implementation';
    END IF;
END $$;

-- Create index for method lookup by class and name
CREATE INDEX IF NOT EXISTS idx_method_class_name ON method(class_idx, method_name);
CREATE INDEX IF NOT EXISTS idx_method_unit_name ON method(unit_idx, method_name);
CREATE INDEX IF NOT EXISTS idx_method_standalone ON method(unit_idx, is_standalone) WHERE is_standalone = TRUE;

-- Procedure to cleanup stale records from previous runs
-- Deletes units not in the current unit list, cascading to all dependent records
CREATE OR REPLACE PROCEDURE sp_cleanup_stale_records(
    p_project_idx INTEGER,
    p_current_unit_names TEXT[]
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_stale_unit_ids INTEGER[];
    v_stale_class_ids INTEGER[];
    v_stale_record_ids INTEGER[];
    v_deleted_count INTEGER;
BEGIN
    -- Find units that are no longer in the project
    SELECT ARRAY_AGG(idx) INTO v_stale_unit_ids
    FROM unit
    WHERE project_idx = p_project_idx
      AND unit_name != ALL(p_current_unit_names);

    IF v_stale_unit_ids IS NULL OR array_length(v_stale_unit_ids, 1) IS NULL THEN
        RAISE NOTICE 'No stale units to clean up for project %', p_project_idx;
        RETURN;
    END IF;

    RAISE NOTICE 'Found % stale units to clean up', array_length(v_stale_unit_ids, 1);

    -- Get class IDs for stale units
    SELECT ARRAY_AGG(idx) INTO v_stale_class_ids
    FROM class
    WHERE unit_idx = ANY(v_stale_unit_ids);

    -- Get record IDs for stale units
    SELECT ARRAY_AGG(idx) INTO v_stale_record_ids
    FROM record
    WHERE unit_idx = ANY(v_stale_unit_ids);

    -- Delete query parameters (via queries)
    DELETE FROM query_parameter
    WHERE query_idx IN (
        SELECT idx FROM query WHERE unit_idx = ANY(v_stale_unit_ids)
    );
    GET DIAGNOSTICS v_deleted_count = ROW_COUNT;
    RAISE NOTICE 'Deleted % query parameters', v_deleted_count;

    -- Delete query field accesses (via queries)
    DELETE FROM query_field_access
    WHERE query_idx IN (
        SELECT idx FROM query WHERE unit_idx = ANY(v_stale_unit_ids)
    );
    GET DIAGNOSTICS v_deleted_count = ROW_COUNT;
    RAISE NOTICE 'Deleted % query field accesses', v_deleted_count;

    -- Delete queries
    DELETE FROM query WHERE unit_idx = ANY(v_stale_unit_ids);
    GET DIAGNOSTICS v_deleted_count = ROW_COUNT;
    RAISE NOTICE 'Deleted % queries', v_deleted_count;

    -- Delete method parameters
    IF v_stale_class_ids IS NOT NULL THEN
        DELETE FROM method_parameter
        WHERE method_idx IN (
            SELECT idx FROM method WHERE class_idx = ANY(v_stale_class_ids)
        );
    END IF;
    DELETE FROM method_parameter
    WHERE method_idx IN (
        SELECT idx FROM method WHERE unit_idx = ANY(v_stale_unit_ids)
    );
    IF v_stale_record_ids IS NOT NULL THEN
        DELETE FROM method_parameter
        WHERE method_idx IN (
            SELECT idx FROM method WHERE record_idx = ANY(v_stale_record_ids)
        );
    END IF;
    GET DIAGNOSTICS v_deleted_count = ROW_COUNT;
    RAISE NOTICE 'Deleted % method parameters', v_deleted_count;

    -- Delete methods (class methods, standalone methods, record methods)
    IF v_stale_class_ids IS NOT NULL THEN
        DELETE FROM method WHERE class_idx = ANY(v_stale_class_ids);
    END IF;
    DELETE FROM method WHERE unit_idx = ANY(v_stale_unit_ids);
    IF v_stale_record_ids IS NOT NULL THEN
        DELETE FROM method WHERE record_idx = ANY(v_stale_record_ids);
    END IF;
    GET DIAGNOSTICS v_deleted_count = ROW_COUNT;
    RAISE NOTICE 'Deleted % methods', v_deleted_count;

    -- Delete class interfaces
    IF v_stale_class_ids IS NOT NULL THEN
        DELETE FROM class_interface WHERE class_idx = ANY(v_stale_class_ids);
        GET DIAGNOSTICS v_deleted_count = ROW_COUNT;
        RAISE NOTICE 'Deleted % class interfaces', v_deleted_count;
    END IF;

    -- Delete fields
    IF v_stale_class_ids IS NOT NULL THEN
        DELETE FROM field WHERE class_idx = ANY(v_stale_class_ids);
    END IF;
    IF v_stale_record_ids IS NOT NULL THEN
        DELETE FROM field WHERE record_idx = ANY(v_stale_record_ids);
    END IF;
    GET DIAGNOSTICS v_deleted_count = ROW_COUNT;
    RAISE NOTICE 'Deleted % fields', v_deleted_count;

    -- Delete properties
    IF v_stale_class_ids IS NOT NULL THEN
        DELETE FROM property WHERE class_idx = ANY(v_stale_class_ids);
        GET DIAGNOSTICS v_deleted_count = ROW_COUNT;
        RAISE NOTICE 'Deleted % properties', v_deleted_count;
    END IF;

    -- Delete records
    DELETE FROM record WHERE unit_idx = ANY(v_stale_unit_ids);
    GET DIAGNOSTICS v_deleted_count = ROW_COUNT;
    RAISE NOTICE 'Deleted % records', v_deleted_count;

    -- Delete classes
    DELETE FROM class WHERE unit_idx = ANY(v_stale_unit_ids);
    GET DIAGNOSTICS v_deleted_count = ROW_COUNT;
    RAISE NOTICE 'Deleted % classes', v_deleted_count;

    -- Delete form components
    DELETE FROM form_component WHERE unit_idx = ANY(v_stale_unit_ids);
    GET DIAGNOSTICS v_deleted_count = ROW_COUNT;
    RAISE NOTICE 'Deleted % form components', v_deleted_count;

    -- Finally delete the units themselves
    DELETE FROM unit WHERE idx = ANY(v_stale_unit_ids);
    GET DIAGNOSTICS v_deleted_count = ROW_COUNT;
    RAISE NOTICE 'Deleted % units', v_deleted_count;

    -- Note: We do NOT delete projects - they persist across scans
END;
$$;

-- Function to get or create a method with source code support
CREATE OR REPLACE FUNCTION fn_get_or_create_method(
    p_class_idx INTEGER,
    p_record_idx INTEGER,
    p_unit_idx INTEGER,
    p_method_name TEXT,
    p_kind method_kind,
    p_return_type TEXT,
    p_visibility class_visibility,
    p_is_virtual BOOLEAN,
    p_is_override BOOLEAN,
    p_is_abstract BOOLEAN,
    p_is_static BOOLEAN,
    p_is_overload BOOLEAN,
    p_is_standalone BOOLEAN,
    p_source_code TEXT DEFAULT NULL
)
RETURNS INTEGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_method_idx INTEGER;
BEGIN
    -- Try to find existing method
    IF p_class_idx IS NOT NULL THEN
        SELECT idx INTO v_method_idx
        FROM method
        WHERE class_idx = p_class_idx AND method_name = p_method_name
        LIMIT 1;
    ELSIF p_unit_idx IS NOT NULL AND p_is_standalone THEN
        SELECT idx INTO v_method_idx
        FROM method
        WHERE unit_idx = p_unit_idx AND method_name = p_method_name AND is_standalone = TRUE
        LIMIT 1;
    ELSIF p_record_idx IS NOT NULL THEN
        SELECT idx INTO v_method_idx
        FROM method
        WHERE record_idx = p_record_idx AND method_name = p_method_name
        LIMIT 1;
    END IF;

    -- If found, update source code if provided
    IF v_method_idx IS NOT NULL THEN
        IF p_source_code IS NOT NULL THEN
            UPDATE method SET source_code = p_source_code WHERE idx = v_method_idx;
        END IF;
        RETURN v_method_idx;
    END IF;

    -- Create new method
    INSERT INTO method (
        class_idx, record_idx, unit_idx, method_name, kind, return_type,
        visibility, is_virtual, is_override, is_abstract, is_static,
        is_overload, is_standalone, source_code
    )
    VALUES (
        p_class_idx, p_record_idx, p_unit_idx, p_method_name, p_kind, p_return_type,
        p_visibility, p_is_virtual, p_is_override, p_is_abstract, p_is_static,
        p_is_overload, p_is_standalone, p_source_code
    )
    RETURNING idx INTO v_method_idx;

    RETURN v_method_idx;
END;
$$;

-- Grant execute permissions
GRANT EXECUTE ON PROCEDURE sp_cleanup_stale_records(INTEGER, TEXT[]) TO delphianalyser;
GRANT EXECUTE ON PROCEDURE sp_cleanup_stale_records(INTEGER, TEXT[]) TO analysis_service;
GRANT EXECUTE ON FUNCTION fn_get_or_create_method(INTEGER, INTEGER, INTEGER, TEXT, method_kind, TEXT, class_visibility, BOOLEAN, BOOLEAN, BOOLEAN, BOOLEAN, BOOLEAN, BOOLEAN, TEXT) TO delphianalyser;
GRANT EXECUTE ON FUNCTION fn_get_or_create_method(INTEGER, INTEGER, INTEGER, TEXT, method_kind, TEXT, class_visibility, BOOLEAN, BOOLEAN, BOOLEAN, BOOLEAN, BOOLEAN, BOOLEAN, TEXT) TO analysis_service;

-- Add comment
COMMENT ON PROCEDURE sp_cleanup_stale_records IS 'Removes units and all dependent records that are not in the current unit list';
COMMENT ON FUNCTION fn_get_or_create_method IS 'Gets or creates a method record with optional source code';
