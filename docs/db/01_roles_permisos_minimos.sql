-- =============================================================================
-- Logística & Movilidad — roles de PostgreSQL con permisos mínimos
-- =============================================================================
-- Se usan DOS roles:
--   logistica_owner : dueño del esquema. Solo lo usan las migraciones (dotnet ef / bundle).
--   logistica_app   : el que usa la API en tiempo de ejecución. NO es dueño de nada, así que
--                     no puede hacer DROP, ALTER ni TRUNCATE. Sobre "auditoria" solo puede
--                     SELECT e INSERT (sin UPDATE ni DELETE).
--
-- Ejecutar como superusuario (postgres), pasando las contraseñas como variables de psql,
-- NUNCA escritas en este archivo:
--
--   psql -h localhost -U postgres -d logistica_db \
--        -v owner_password="$LOGISTICA_OWNER_PASSWORD" \
--        -v app_password="$LOGISTICA_APP_PASSWORD" \
--        -f docs/db/01_roles_permisos_minimos.sql
--
-- Orden recomendado:
--   1) Este script (crea roles y privilegios por defecto).
--   2) Migraciones con la cadena de conexión de logistica_owner.
--   3) Volver a correr la sección "PERMISOS SOBRE TABLAS EXISTENTES" (es idempotente)
--      o confiar en los privilegios por defecto definidos abajo.
--   4) La API corre con la cadena de conexión de logistica_app.
-- =============================================================================

\set ON_ERROR_STOP on

-- ---------------------------------------------------------------- roles
SELECT format('CREATE ROLE logistica_owner LOGIN PASSWORD %L', :'owner_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'logistica_owner') \gexec

SELECT format('CREATE ROLE logistica_app LOGIN PASSWORD %L', :'app_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'logistica_app') \gexec

ALTER ROLE logistica_app NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
ALTER ROLE logistica_app CONNECTION LIMIT 50;
ALTER ROLE logistica_app SET statement_timeout = '30s';

-- ---------------------------------------------------------------- base de datos y esquema
SELECT format('GRANT CONNECT ON DATABASE %I TO logistica_owner, logistica_app', current_database()) \gexec
SELECT format('REVOKE CREATE, TEMPORARY ON DATABASE %I FROM PUBLIC', current_database()) \gexec

ALTER SCHEMA public OWNER TO logistica_owner;
REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO logistica_app;   -- sin CREATE: no puede crear ni borrar objetos

-- ---------------------------------------------------------------- privilegios por defecto
-- Toda tabla que cree logistica_owner (migraciones futuras) nace con estos permisos para la app.
ALTER DEFAULT PRIVILEGES FOR ROLE logistica_owner IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO logistica_app;
ALTER DEFAULT PRIVILEGES FOR ROLE logistica_owner IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO logistica_app;

-- ---------------------------------------------------------------- PERMISOS SOBRE TABLAS EXISTENTES
-- Idempotente: se puede volver a correr después de cada migración.
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO logistica_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO logistica_app;

-- Auditoría: solo inserción y lectura. Sin UPDATE, DELETE ni TRUNCATE.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'public' AND tablename = 'auditoria') THEN
        REVOKE UPDATE, DELETE, TRUNCATE ON public.auditoria FROM logistica_app;
        GRANT SELECT, INSERT ON public.auditoria TO logistica_app;
    END IF;
    -- El historial de migraciones solo lo toca el dueño.
    IF EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'public' AND tablename = '__EFMigrationsHistory') THEN
        REVOKE INSERT, UPDATE, DELETE, TRUNCATE ON public."__EFMigrationsHistory" FROM logistica_app;
    END IF;
END $$;

-- ---------------------------------------------------------------- verificación
-- Debe devolver solo SELECT e INSERT para logistica_app sobre auditoria.
SELECT grantee, privilege_type
FROM information_schema.role_table_grants
WHERE table_name = 'auditoria' AND grantee = 'logistica_app'
ORDER BY privilege_type;
