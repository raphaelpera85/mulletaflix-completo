ALTER FUNCTION public.set_nebula_updated_at() SET search_path = '';

DROP INDEX IF EXISTS public.idx_mulletaflix_users_username;
DROP INDEX IF EXISTS public.idx_nebula_files_parent;
