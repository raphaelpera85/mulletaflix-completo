import format from 'date-fns/format';
import type { MRT_Cell, MRT_RowData } from 'material-react-table';

import { useLocale } from 'hooks/useLocale';

interface CellProps<TData extends MRT_RowData> {
    cell: MRT_Cell<TData, unknown>
}

const DateTimeCell = <TData extends MRT_RowData>({ cell }: CellProps<TData>) => {
    const { dateFnsLocale } = useLocale();

    return format(cell.getValue<Date>(), 'Pp', { locale: dateFnsLocale });
};

export default DateTimeCell;
