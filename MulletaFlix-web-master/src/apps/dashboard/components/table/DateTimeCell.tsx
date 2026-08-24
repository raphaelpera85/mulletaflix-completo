import format from 'date-fns/format';
import type { MRT_Cell } from 'material-react-table';
import { FC } from 'react';

import { useLocale } from 'hooks/useLocale';

interface CellProps {
    cell: MRT_Cell<any>
}

const DateTimeCell: FC<CellProps> = ({ cell }) => {
    const { dateFnsLocale } = useLocale();

    return format(cell.getValue<Date>(), 'Pp', { locale: dateFnsLocale });
};

export default DateTimeCell;
