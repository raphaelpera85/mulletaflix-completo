import type { PlaybackReportDto } from 'apps/dashboard/features/playback/api/usePlaybackReports';
import type { MRT_Cell, MRT_Row } from 'material-react-table';

export interface PlaybackReportCell {
    cell: MRT_Cell<PlaybackReportDto>;
    row: MRT_Row<PlaybackReportDto>;
}