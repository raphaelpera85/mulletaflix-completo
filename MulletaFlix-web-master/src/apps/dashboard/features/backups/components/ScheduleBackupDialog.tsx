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
import FormControlLabel from '@mui/material/FormControlLabel';
import Checkbox from '@mui/material/Checkbox';
import FormGroup from '@mui/material/FormGroup';
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

const ScheduleBackupDialog: FunctionComponent<IProps> = ({ taskId, open, onClose, onSave }) => {
    const { api } = useApi();
    const [triggers, setTriggers] = useState<TaskTriggerInfo[]>([]);
    const [isLoading, setIsLoading] = useState(false);
    const [isEnabled, setIsEnabled] = useState(true);

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
                setTriggers(task.Triggers);
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
                taskTriggerInfo: triggers
            });
            onSave();
        } catch (error) {
            console.error('Failed to save task:', error);
        } finally {
            setIsLoading(false);
        }
    }, [api, taskId, triggers, onSave]);

    const addTrigger = useCallback(() => {
        setTriggers(prev => [...prev, { Type: 'DailyTrigger', TimeOfDayTicks: 3 * 60 * 60 * 10000000 }]);
    }, []);

    const removeTrigger = useCallback((index: number) => {
        setTriggers(prev => prev.filter((_, i) => i !== index));
    }, []);

    const updateTrigger = useCallback((index: number, field: keyof TaskTriggerInfo, value: unknown) => {
        setTriggers(prev => prev.map((trigger, i) => i === index ? { ...trigger, [field]: value } : trigger));
    }, []);

    React.useEffect(() => {
        if (open) {
            loadTask();
        }
    }, [open, loadTask]);

    return (
        <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
            <DialogTitle>{globalize.translate('HeaderScheduleBackup')}</DialogTitle>
            <DialogContent>
                <Stack spacing={3}>
                    <FormControl component="fieldset">
                        <FormControlLabel
                            control={
                                <Checkbox
                                    checked={isEnabled}
                                    onChange={e => setIsEnabled(e.target.checked)}
                                />
                            }
                            label={globalize.translate('LabelEnableScheduledBackup')}
                        />
                    </FormControl>

                    <Typography variant="h6">{globalize.translate('LabelTriggers')}</Typography>
                    {triggers.map((trigger, index) => (
                        <Stack key={index} spacing={2} direction="row" alignItems="center">
                            <FormControl sx={{ minWidth: 200 }}>
                                <TextField
                                    select
                                    label={globalize.translate('LabelTriggerType')}
                                    value={trigger.Type}
                                    onChange={e => updateTrigger(index, 'Type', e.target.value)}
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
                                        type="time"
                                        label={globalize.translate('LabelTimeOfDay')}
                                        value={trigger.TimeOfDayTicks ? new Date((trigger.TimeOfDayTicks / 10000)).toISOString().substr(11, 5) : ''}
                                        onChange={e => {
                                            const date = new Date(`1970-01-01T${e.target.value}`);
                                            const ticks = date.getHours() * 3600 + date.getMinutes() * 60;
                                            updateTrigger(index, 'TimeOfDayTicks', ticks * 10000000);
                                        }}
                                        InputLabelProps={{ shrink: true }}
                                    />
                                </FormControl>
                            ) : null}

                            {trigger.Type === 'WeeklyTrigger' ? (
                                <FormControl sx={{ minWidth: 200 }}>
                                    <TextField
                                        select
                                        label={globalize.translate('LabelDayOfWeek')}
                                        value={trigger.DayOfWeek ?? 0}
                                        onChange={e => updateTrigger(index, 'DayOfWeek', parseInt(e.target.value, 10))}
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
                                        type="number"
                                        label={globalize.translate('LabelIntervalMinutes')}
                                        value={trigger.IntervalTicks ? trigger.IntervalTicks / 600000000 : ''}
                                        onChange={e => updateTrigger(index, 'IntervalTicks', parseInt(e.target.value, 10) * 600000000)}
                                    />
                                </FormControl>
                            ) : null}

                            <Button
                                variant="outlined"
                                color="error"
                                size="small"
                                onClick={() => removeTrigger(index)}
                            >
                                {globalize.translate('ButtonRemove')}
                            </Button>
                        </Stack>
                    ))}

                    <Button
                        variant="outlined"
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
                    variant="contained"
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