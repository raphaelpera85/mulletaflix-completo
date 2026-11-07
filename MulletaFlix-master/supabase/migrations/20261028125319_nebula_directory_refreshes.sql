CREATE TABLE IF NOT EXISTS public.nebula_directory_refreshes (
    directory_path TEXT PRIMARY KEY
);

ALTER TABLE public.nebula_directory_refreshes ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS nebula_directory_refreshes_service_role_all ON public.nebula_directory_refreshes;
CREATE POLICY nebula_directory_refreshes_service_role_all
    ON public.nebula_directory_refreshes FOR ALL TO service_role USING (true) WITH CHECK (true);

REVOKE ALL ON TABLE public.nebula_directory_refreshes FROM anon, authenticated;
GRANT ALL ON TABLE public.nebula_directory_refreshes TO service_role;
