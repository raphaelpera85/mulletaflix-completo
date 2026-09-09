import React, { FunctionComponent, useCallback, useState } from 'react';
import { useApi } from 'hooks/useApi';
import { getScheduledTasksApi } from '@jellyfin/sdk/lib/utils/api/scheduled-tasks-api';
import type { TaskTriggerInfo } from '@jellyfin/sdk/lib/generated-client/models/task-trigger-info';
import type { TaskTriggerInfoType } from '@jellyfin/sdk/lib/generated-client/models/task-trigger-info-type';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Button from '@mui/material/Button';
import FormControl from '@mui/material/FormControl';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import AddIcon from '@mui/icons-material/Add';
import globalize from 'lib/globalize';

type IProps = {
    taskId: string;
    open: boolean;
    onClose: () => void;
    onSave: () => void;
};

type TriggerState = TaskTriggerInfo & { id: string };

type TriggerRowProps = {
    trigger: TriggerState;
    triggerTypes: { value: TaskTriggerInfoType; label: string }[];
    onUpdate: (id: string, field: keyof TaskTriggerInfo, value: unknown) => void;
    onRemove: (id: string) => void;
};

const TriggerRow: FunctionComponent<TriggerRowProps> = ({ trigger, triggerTypes, onUpdate, onRemove }) => {
    const handleTypeChange = useCallback((event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
        onUpdate(trigger.id, 'Type', event.target.value as TaskTriggerInfoType);
    }, [onUpdate, trigger.id]);

    const handleTimeChange = useCallback((event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
        const date = new Date(`1970-01-01T${event.target.value}`);
        const ticks = date.getHours() * 3600 + date.getMinutes() * 60;
        onUpdate(trigger.id, 'TimeOfDayTicks', ticks * 10000000);
    }, [onUpdate, trigger.id]);

    const handleDayChange = useCallback((event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
        onUpdate(trigger.id, 'DayOfWeek', parseInt(event.target.value, 10));
    }, [onUpdate, trigger.id]);

    const handleIntervalChange = useCallback((event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
        onUpdate(trigger.id, 'IntervalTicks', parseInt(event.target.value, 10) * 600000000);
    }, [onUpdate, trigger.id]);

    const handleRemove = useCallback(() => {
        onRemove(trigger.id);
    }, [onRemove, trigger.id]);

    return (
        <Stack spacing={2} direction='row' alignItems='center'>
            <FormControl sx={{ minWidth: 200 }}>
                <TextField
                    select
                    label={globalize.translate('LabelTriggerType')}
                    value={trigger.Type}
                    onChange={handleTypeChange}
                >
                    {triggerTypes.map(type => (
                        <MenuItem key={type.value} value={type.value}>
                            {type.label}
                        </MenuItem>
                    ))}
                </TextField>
            </FormControl>

            {trigger.Type === 'DailyTrigger' || trigger.Type === 'WeeklyTrigger' ? (
                <FormControl sx={{ minWidth: 200 }}>
                    <TextField
                        type='time'
                        label={globalize.translate('LabelTimeOfDay')}
                        value={trigger.TimeOfDayTicks ? new Date((trigger.TimeOfDayTicks / 10000)).toISOString().slice(11, 16) : ''}
                        onChange={handleTimeChange}
                        slotProps={{ inputLabel: { shrink: true } }}
                    />
                </FormControl>
            ) : null}

            {trigger.Type === 'WeeklyTrigger' ? (
                <FormControl sx={{ minWidth: 200 }}>
                    <TextField
                        select
                        label={globalize.translate('LabelDayOfWeek')}
                        value={trigger.DayOfWeek ?? 0}
                        onChange={handleDayChange}
                    >
                        <MenuItem value={0}>{globalize.translate('LabelSunday')}</MenuItem>
                        <MenuItem value={1}>{globalize.translate('LabelMonday')}</MenuItem>
                        <MenuItem value={2}>{globalize.translate('LabelTuesday')}</MenuItem>
                        <MenuItem value={3}>{globalize.translate('LabelWednesday')}</MenuItem>
                        <MenuItem value={4}>{globalize.translate('LabelThursday')}</MenuItem>
                        <MenuItem value={5}>{globalize.translate('LabelFriday')}</MenuItem>
                        <MenuItem value={6}>{globalize.translate('LabelSaturday')}</MenuItem>
                    </TextField>
                </FormControl>
            ) : null}

            {trigger.Type === 'IntervalTrigger' ? (
                <FormControl sx={{ minWidth: 200 }}>
                    <TextField
                        type='number'
                        label={globalize.translate('LabelIntervalMinutes')}
                        value={trigger.IntervalTicks ? trigger.IntervalTicks / 600000000 : ''}
                        onChange={handleIntervalChange}
                    />
                </FormControl>
            ) : null}

            <Button variant='outlined' color='error' size='small' onClick={handleRemove}>
                {globalize.translate('ButtonRemove')}
            </Button>
        </Stack>
    );
};

const ScheduleBackupDialog: FunctionComponent<IProps> = ({ taskId, open, onClose, onSave }) => {
    const { api } = useApi();
    const [triggers, setTriggers] = useState<TriggerState[]>([]);
    const [isLoading, setIsLoading] = useState(false);

    const triggerTypes: { value: TaskTriggerInfoType; label: string }[] = [
        { value: 'DailyTrigger', label: globalize.translate('LabelDaily') },
        { value: 'WeeklyTrigger', label: globalize.translate('LabelWeekly') },
        { value: 'IntervalTrigger', label: globalize.translate('LabelInterval') },
        { value: 'StartupTrigger', label: globalize.translate('LabelStartup') }
    ];

    const loadTask = useCallback(async () => {
        if (!api || !taskId) return;

        try {
            const response = await getScheduledTasksApi(api).getTask({ taskId });
            const task = response.data;

            if (task.Triggers) {
                setTriggers(task.Triggers.map(trigger => ({ ...trigger, id: crypto.randomUUID() })));
            }
            // Use IsEnabled from IConfigurableScheduledTask, not TaskState
        } catch (error) {
            console.error('Failed to load task:', error);
        }
    }, [api, taskId]);

    const handleSave = useCallback(async () => {
        if (!api || !taskId) return;

        setIsLoading(true);
        try {
            await getScheduledTasksApi(api).updateTask({
                taskId,
                taskTriggerInfo: triggers.map(trigger => {
                    const apiTrigger = { ...trigger };
                    Reflect.deleteProperty(apiTrigger, 'id');
                    return apiTrigger;
                })
            });
            onSave();
        } catch (error) {
            console.error('Failed to save task:', error);
        } finally {
            setIsLoading(false);
        }
    }, [api, taskId, triggers, onSave]);

    const addTrigger = useCallback(() => {
        setTriggers(prev => [...prev, { id: crypto.randomUUID(), Type: 'DailyTrigger', TimeOfDayTicks: 3 * 60 * 60 * 10000000 }]);
    }, []);

    const removeTrigger = useCallback((id: string) => {
        setTriggers(prev => prev.filter(trigger => trigger.id !== id));
    }, []);

    const updateTrigger = useCallback((id: string, field: keyof TaskTriggerInfo, value: unknown) => {
        setTriggers(prev => prev.map(trigger => trigger.id === id ? { ...trigger, [field]: value } : trigger));
    }, []);

    React.useEffect(() => {
        if (open) {
            void loadTask();
        }
    }, [open, loadTask]);

    return (
        <Dialog open={open} onClose={onClose} maxWidth='md' fullWidth>
            <DialogTitle>{globalize.translate('HeaderScheduleBackup')}</DialogTitle>
            <DialogContent>
                <Stack spacing={3}>
                    <Typography variant='h6'>{globalize.translate('LabelTriggers')}</Typography>
                    {triggers.map(trigger => (
                        <TriggerRow
                            key={trigger.id}
                            trigger={trigger}
                            triggerTypes={triggerTypes}
                            onUpdate={updateTrigger}
                            onRemove={removeTrigger}
                        />
                    ))}

                    <Button
                        variant='outlined'
                        startIcon={<AddIcon />}
                        onClick={addTrigger}
                    >
                        {globalize.translate('ButtonAddTrigger')}
                    </Button>
                </Stack>
            </DialogContent>
            <DialogActions>
                <Button onClick={onClose}>{globalize.translate('ButtonCancel')}</Button>
                <Button
                    variant='contained'
                    onClick={handleSave}
                    disabled={isLoading}
                >
                    {isLoading ? globalize.translate('LabelSaving') : globalize.translate('ButtonSave')}
                </Button>
            </DialogActions>
        </Dialog>
    );
};

export default ScheduleBackupDialog;
