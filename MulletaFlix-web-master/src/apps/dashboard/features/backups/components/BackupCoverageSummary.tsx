import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import type { BackupManifestDto } from '@jellyfin/sdk/lib/generated-client/models/backup-manifest-dto';
import type { SupabaseBackupStatus } from '../api/useSupabaseBackupStatus';

type Props = {
    latestBackup?: BackupManifestDto;
    status?: SupabaseBackupStatus;
    loading: boolean;
    error: boolean;
    onRetry: () => void;
};

const formatDate = (value?: string | null) => {
    if (!value) return 'Sem execução registrada';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? 'Data indisponível' : date.toLocaleString();
};

const ResultChip = ({ failed }: { failed?: boolean | null }) => {
    if (failed === true) return <Chip size='small' color='error' label='Falha' />;
    if (failed === false) return <Chip size='small' color='success' label='Concluído' />;
    return <Chip size='small' label='Resultado não informado' />;
};

const getConnectionLabel = (status: SupabaseBackupStatus) => {
    if (!status.IsConfigured) return 'Supabase não configurado';
    return status.IsConnected ? 'Supabase conectado' : 'Supabase sem conexão';
};

const CoverageCard = ({ title, children }: { title: string; children: React.ReactNode }) => (
    <Paper component='section' aria-label={title} variant='outlined' sx={{ p: 2, minWidth: 0 }}>
        <Stack spacing={1} sx={{ overflowWrap: 'anywhere' }}>
            <Typography variant='h3'>{title}</Typography>
            {children}
        </Stack>
    </Paper>
);

const BackupCoverageSummary = ({ latestBackup, status, loading, error, onRetry }: Props) => (
    <Stack spacing={2} component='section' aria-label='Cobertura dos backups'>
        <Typography variant='h2'>Cobertura dos backups</Typography>
        {loading && <Typography role='status'>Carregando destinos remotos…</Typography>}
        {error && <Alert severity='error' action={<Button color='inherit' onClick={onRetry}>Tentar novamente</Button>}>Não foi possível consultar os backups do Supabase.</Alert>}
        {status && <Chip sx={{ alignSelf: 'flex-start' }} label={getConnectionLabel(status)} color={status.IsConnected ? 'success' : 'default'} />}
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: 'repeat(3, minmax(0, 1fr))' }, gap: 2 }}>
            <CoverageCard title='ZIP local do servidor'>
                <Typography variant='body2'>{latestBackup ? 'Escopo selecionado no último ZIP:' : 'Nenhum ZIP disponível.'}</Typography>
                {latestBackup && <>
                    <Typography variant='body2'>Configurações locais, coleções, playlists e definições de bibliotecas.</Typography>
                    <Typography variant='body2'>Banco relacional e usuários: {latestBackup.Options?.Database ? 'selecionado' : 'não selecionado'}.</Typography>
                    <Typography variant='body2'>Metadados internos: {latestBackup.Options?.Metadata ? 'selecionados' : 'não selecionados'}; legendas: {latestBackup.Options?.Subtitles ? 'selecionadas' : 'não selecionadas'}; trickplay: {latestBackup.Options?.Trickplay ? 'selecionado' : 'não selecionado'}.</Typography>
                    <Typography variant='body2' color='text.secondary'>{latestBackup.Path}</Typography>
                </>}
                <Typography variant='body2' color='text.secondary'>O ZIP não inclui o catálogo MongoDB nem os arquivos originais de mídia.</Typography>
            </CoverageCard>
            <CoverageCard title='Catálogo Nebula no Supabase'>
                <Typography variant='body2'>Documentos do catálogo MongoDB, identificadores das partes Telegram e usuários FTP. Os vídeos permanecem na origem.</Typography>
                <ResultChip failed={status?.LastBackupFailed} />
                <Typography variant='body2'>Última execução registrada: {formatDate(status?.LastBackupAttemptTime ?? status?.LastBackupTime)}</Typography>
                {status?.LastBackupFailed === false && <Typography variant='body2'>Processados nesta execução: {status.LastBackupProcessedFilesCount ?? 'não informado'} documentos e {status.LastBackupProcessedUsersCount ?? 'não informado'} usuários FTP. Essa contagem pode representar apenas as alterações desde o backup anterior.</Typography>}
                {status?.LastBackupStatus && <Typography variant='body2' color='text.secondary'>{status.LastBackupStatus}</Typography>}
                {status?.LastRestoreTime && <Alert severity={status.LastRestoreFailed ? 'error' : 'info'}>Última restauração operacional: {formatDate(status.LastRestoreTime)}. {status.LastRestoreStatus}</Alert>}
            </CoverageCard>
            <CoverageCard title='Usuários do aplicativo no Supabase'>
                <Typography variant='body2'>Contas e dados de usuários do MulletaFlix. Este backup tem execução independente do catálogo Nebula.</Typography>
                <ResultChip failed={status?.LastUsersBackupFailed} />
                <Typography variant='body2'>Última execução registrada: {formatDate(status?.LastUsersBackupTime)}</Typography>
                {status?.LastUsersBackupFailed === false && <Typography variant='body2'>Usuários processados: {status.LastUsersBackupCount ?? 'não informado'}.</Typography>}
                {status?.LastUsersBackupStatus && <Typography variant='body2' color='text.secondary'>{status.LastUsersBackupStatus}</Typography>}
            </CoverageCard>
        </Box>
        <Alert severity='info'>A presença de uma cópia ou uma restauração operacional não comprova recuperação completa. Ainda não há registro de exercício isolado com todos os destinos nesta interface.</Alert>
    </Stack>
);

export default BackupCoverageSummary;
