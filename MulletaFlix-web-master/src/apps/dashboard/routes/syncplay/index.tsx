import type { GroupInfoDto } from '@jellyfin/sdk/lib/generated-client/models/group-info-dto';
import { useState, useCallback } from 'react';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import MenuItem from '@mui/material/MenuItem';
import Menu from '@mui/material/Menu';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import Stack from '@mui/material/Stack';
import Grid from '@mui/material/Grid';
import Paper from '@mui/material/Paper';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import TextField from '@mui/material/TextField';
import PlayArrow from '@mui/icons-material/PlayArrow';
import Pause from '@mui/icons-material/Pause';
import SkipNext from '@mui/icons-material/SkipNext';
import SkipPrevious from '@mui/icons-material/SkipPrevious';
import People from '@mui/icons-material/People';
import PersonAdd from '@mui/icons-material/PersonAdd';
import Delete from '@mui/icons-material/Delete';
import MoreVert from '@mui/icons-material/MoreVert';
import Sync from '@mui/icons-material/Sync';
import Fullscreen from '@mui/icons-material/Fullscreen';

import { useSyncPlayGroups } from 'apps/experimental/features/syncPlay/hooks/api/useSyncPlayGroups';
import { useCreateSyncPlayGroup } from 'apps/experimental/features/syncPlay/hooks/api/useCreateSyncPlayGroup';
import { useJoinSyncPlayGroup } from 'apps/experimental/features/syncPlay/hooks/api/useJoinSyncPlayGroup';
import { useLeaveSyncPlayGroup } from 'apps/experimental/features/syncPlay/hooks/api/useLeaveSyncPlayGroup';
import { useSyncPlay } from 'apps/experimental/features/syncPlay/hooks/useSyncPlay';
import type { ApiClient } from 'jellyfin-apiclient';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import globalize from 'lib/globalize';
import { EmptyState } from 'components/EmptyState';

interface GroupInfoDtoWithPing extends GroupInfoDto {
    Ping?: number;
}

const Component = () => {
    const apiClient = ServerConnections.currentApiClient() as unknown as ApiClient | undefined;
    const { isActive, currentGroup, syncPlay } = useSyncPlay();
    const { data: groups, isLoading: isGroupsLoading, refetch: refetchGroups } = useSyncPlayGroups();
    const createGroup = useCreateSyncPlayGroup();
    const joinGroup = useJoinSyncPlayGroup();
    const leaveGroup = useLeaveSyncPlayGroup();

    const [ anchorEl, setAnchorEl ] = useState<null | HTMLElement>(null);
    const [ selectedGroup, setSelectedGroup ] = useState<GroupInfoDto | null>(null);
    const [ isCreatingGroup, setIsCreatingGroup ] = useState(false);
    const [ newGroupName, setNewGroupName ] = useState('');

    const handleMenuOpen = useCallback((event: React.MouseEvent<HTMLElement>, group: GroupInfoDto) => {
        setAnchorEl(event.currentTarget);
        setSelectedGroup(group);
    }, []);

    const handleMenuClose = useCallback(() => {
        setAnchorEl(null);
        setSelectedGroup(null);
    }, []);

    const openCreateGroup = useCallback(() => setIsCreatingGroup(true), []);
    const closeCreateGroup = useCallback(() => setIsCreatingGroup(false), []);
    const handleGroupNameChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        setNewGroupName(event.target.value);
    }, []);

    const handleCreateGroup = useCallback(() => {
        if (!newGroupName.trim()) return;
        createGroup.mutate({ newGroupRequestDto: { GroupName: newGroupName } }, {
            onSuccess: () => {
                setIsCreatingGroup(false);
                setNewGroupName('');
                void refetchGroups();
            }
        });
    }, [ newGroupName, createGroup, refetchGroups ]);

    const handleJoinGroup = useCallback((groupId: string) => {
        joinGroup.mutate({ joinGroupRequestDto: { GroupId: groupId } }, {
            onSuccess: () => {
                void refetchGroups();
                handleMenuClose();
            }
        });
    }, [ joinGroup, refetchGroups, handleMenuClose ]);

    const handleLeaveGroup = useCallback(() => {
        leaveGroup.mutate(undefined, {
            onSuccess: () => {
                void refetchGroups();
            }
        });
    }, [ leaveGroup, refetchGroups ]);

    const handlePlay = useCallback(() => {
        if (syncPlay && apiClient) {
            syncPlay.Manager.resumeGroupPlayback(apiClient);
        }
    }, [syncPlay, apiClient]);

    const handlePause = useCallback(() => {
        if (syncPlay && apiClient) {
            syncPlay.Manager.haltGroupPlayback(apiClient);
        }
    }, [syncPlay, apiClient]);

    const handleNext = useCallback(() => {
        if (syncPlay && apiClient) {
            syncPlay.Manager.haltGroupPlayback(apiClient);
        }
    }, [syncPlay, apiClient]);

    const handlePrevious = useCallback(() => {
        if (syncPlay && apiClient) {
            syncPlay.Manager.haltGroupPlayback(apiClient);
        }
    }, [syncPlay, apiClient]);

    if (isGroupsLoading) {
        return (
            <Paper style={{ padding: 32, textAlign: 'center' }}>
                <CircularProgress />
            </Paper>
        );
    }

    return (
        <Stack spacing={3} style={{ padding: '24px' }}>
            <Stack direction='row' spacing={2} justifyContent='space-between' alignItems='center'>
                <Typography variant='h4'>SyncPlay</Typography>
                <Stack direction='row' spacing={2}>
                    <Button
                        variant='outlined'
                        startIcon={<PersonAdd />}
                        onClick={openCreateGroup}
                    >
                        {globalize.translate('SyncPlayCreateGroup')}
                    </Button>
                    {isActive && (
                        <Button
                            variant='outlined'
                            color='error'
                            startIcon={<Delete />}
                            onClick={handleLeaveGroup}
                        >
                            {globalize.translate('SyncPlayLeaveGroup')}
                        </Button>
                    )}
                </Stack>
            </Stack>

            {isCreatingGroup && (
                <Paper elevation={3} style={{ padding: '24px', maxWidth: 400 }}>
                    <Typography variant='h6' gutterBottom>
                        {globalize.translate('SyncPlayCreateGroup')}
                    </Typography>
                    <Stack spacing={2} style={{ width: '100%' }}>
                        <TextField
                            value={newGroupName}
                            onChange={handleGroupNameChange}
                            placeholder={globalize.translate('SyncPlayGroupName')}
                            fullWidth
                        />
                        <Stack direction='row' spacing={2} justifyContent='flex-end'>
                            <Button onClick={closeCreateGroup}>
                                {globalize.translate('Cancel')}
                            </Button>
                            <Button
                                variant='contained'
                                onClick={handleCreateGroup}
                                disabled={!newGroupName.trim()}
                            >
                                {globalize.translate('Create')}
                            </Button>
                        </Stack>
                    </Stack>
                </Paper>
            )}

            {isActive && currentGroup && (
                <Paper elevation={3} style={{ padding: '24px' }}>
                    <Stack direction='row' spacing={2} justifyContent='space-between' alignItems='center' style={{ flexWrap: 'wrap' }}>
                        <Stack>
                            <Typography variant='h6'>{currentGroup.GroupName}</Typography>
                            <Typography variant='body2' color='text.secondary'>
                                {globalize.translate('SyncPlayGroupOwner')}: {currentGroup.Participants?.[0] || 'Unknown'}
                            </Typography>
                        </Stack>
                        <Stack direction='row' spacing={2}>
                            <Tooltip title={globalize.translate('SyncPlayPlay')}>
                                <IconButton
                                    onClick={handlePlay}
                                    disabled={syncPlay?.Manager.isPlaylistEmpty?.()}
                                    aria-label={globalize.translate('SyncPlayPlay')}
                                >
                                    <PlayArrow />
                                </IconButton>
                            </Tooltip>
                            <Tooltip title={globalize.translate('SyncPlayPause')}>
                                <IconButton
                                    onClick={handlePause}
                                    disabled={!syncPlay?.Manager.isPlaybackActive?.()}
                                    aria-label={globalize.translate('SyncPlayPause')}
                                >
                                    <Pause />
                                </IconButton>
                            </Tooltip>
                            <Tooltip title={globalize.translate('SyncPlayPrevious')}>
                                <IconButton
                                    onClick={handlePrevious}
                                    aria-label={globalize.translate('SyncPlayPrevious')}
                                >
                                    <SkipPrevious />
                                </IconButton>
                            </Tooltip>
                            <Tooltip title={globalize.translate('SyncPlayNext')}>
                                <IconButton
                                    onClick={handleNext}
                                    aria-label={globalize.translate('SyncPlayNext')}
                                >
                                    <SkipNext />
                                </IconButton>
                            </Tooltip>
                            <Tooltip title={globalize.translate('SyncPlayFullscreen')}>
                                <IconButton
                                    aria-label={globalize.translate('SyncPlayFullscreen')}
                                >
                                    <Fullscreen />
                                </IconButton>
                            </Tooltip>
                        </Stack>
                    </Stack>
                    <Stack direction='row' spacing={1} style={{ marginTop: 8, flexWrap: 'wrap' }}>
                        <Chip
                            label={currentGroup.Participants && currentGroup.Participants.length > 1 ? `${currentGroup.Participants.length} ${globalize.translate('SyncPlayUsers')}` : globalize.translate('SyncPlaySolo')}
                            icon={<People />}
                            size='small'
                            variant='outlined'
                        />
                        <Chip
                            label={globalize.translate('SyncPlayHost')}
                            icon={<Sync />}
                            size='small'
                            color='primary'
                        />
                        <Chip
                            label={`${globalize.translate('SyncPlayPing')}: ${(currentGroup as GroupInfoDtoWithPing).Ping ?? '—'} ms`}
                            size='small'
                            variant='outlined'
                        />
                    </Stack>
                    {currentGroup.Participants && currentGroup.Participants.length > 0 && (
                        <Stack direction='row' spacing={1} style={{ marginTop: 16, flexWrap: 'wrap' }}>
                            {currentGroup.Participants.map((participant, index) => (
                                <Chip
                                    key={participant}
                                    label={participant}
                                    avatar={<Typography variant='caption'>{participant?.charAt(0).toUpperCase()}</Typography>}
                                    size='small'
                                    variant={index === 0 ? 'filled' : 'outlined'}
                                    color={index === 0 ? 'primary' : 'default'}
                                />
                            ))}
                        </Stack>
                    )}
                </Paper>
            )}

            <Typography variant='h6' style={{ marginTop: 24 }}>
                {globalize.translate('SyncPlayAvailableGroups')}
            </Typography>

            {groups && groups.length > 0 ? (
                <Grid container spacing={3}>
                    {groups.map(group => (
                        <Grid item xs={12} sm={6} md={4} lg={3} key={group.GroupId ?? group.GroupName}>
                            <Paper elevation={2} style={{ padding: '16px', height: '100%', display: 'flex', flexDirection: 'column' }}>
                                <Stack direction='row' spacing={1} justifyContent='space-between' alignItems='flex-start'>
                                    <Typography variant='subtitle1' noWrap>{group.GroupName}</Typography>
                                    <Menu
                                        anchorEl={anchorEl}
                                        open={Boolean(anchorEl && selectedGroup?.GroupId === group.GroupId)}
                                        onClose={handleMenuClose}
                                        transformOrigin={{ horizontal: 'right', vertical: 'top' }}
                                        anchorOrigin={{ horizontal: 'right', vertical: 'bottom' }}
                                    >
                                        {!isActive && (
                                            // eslint-disable-next-line react/jsx-no-bind
                                            <MenuItem onClick={() => handleJoinGroup(group.GroupId?.toString() ?? '')}>
                                                {globalize.translate('SyncPlayJoinGroup')}
                                            </MenuItem>
                                        )}
                                        {isActive && currentGroup?.GroupId === group.GroupId && (
                                            <MenuItem onClick={handleLeaveGroup}>
                                                {globalize.translate('SyncPlayLeaveGroup')}
                                            </MenuItem>
                                        )}
                                    </Menu>
                                    <IconButton
                                        size='small'
                                        // eslint-disable-next-line react/jsx-no-bind
                                        onClick={(e) => handleMenuOpen(e, group)}
                                        disabled={isActive && currentGroup?.GroupId === group.GroupId}
                                    >
                                        <MoreVert />
                                    </IconButton>
                                </Stack>
                                <Typography variant='body2' color='text.secondary' style={{ marginTop: 8 }}>
                                    {globalize.translate('SyncPlayGroupOwner')}: {group.Participants?.[0] || 'Unknown'}
                                </Typography>
                                <Typography variant='body2' color='text.secondary'>
                                    {globalize.translate('SyncPlayUsers')}: {group.Participants?.length || 0}
                                </Typography>
                            </Paper>
                        </Grid>
                    ))}
                </Grid>
            ) : (
                <Paper style={{ padding: '16px' }}>
                    <EmptyState
                        title={globalize.translate('SyncPlayNoGroupsAvailable')}
                        description={globalize.translate('SyncPlayCreateFirstGroupDescription') || undefined}
                        action={(
                            <Button
                                variant='contained'
                                startIcon={<PersonAdd />}
                                onClick={openCreateGroup}
                            >
                                {globalize.translate('SyncPlayCreateFirstGroup')}
                            </Button>
                        )}
                    />
                </Paper>
            )}
        </Stack>
    );
};

export default Component;
