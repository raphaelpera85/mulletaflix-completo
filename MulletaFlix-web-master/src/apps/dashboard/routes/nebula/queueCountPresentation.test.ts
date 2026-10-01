import { describe, expect, it } from 'vitest';

import { getUploadQueueCountPresentation } from './queueCountPresentation';

describe('getUploadQueueCountPresentation', () => {
    it('uses the persisted MongoDB total instead of the bounded worker snapshot', () => {
        expect(getUploadQueueCountPresentation(70, {
            isAvailable: true,
            pendingCount: 1248,
            retryCount: 12
        })).toEqual({
            totalPendingCount: 1248,
            loadedCount: 70,
            retryCount: 12,
            totalDescription: '1248 item(ns) pendente(s) no MongoDB',
            loadedDescription: '70 carregado(s) nos workers · 12 aguardando retry'
        });
    });

    it('preserves a real zero total without falling back to loaded items', () => {
        expect(getUploadQueueCountPresentation(4, {
            isAvailable: true,
            pendingCount: 0,
            retryCount: 0
        })).toEqual({
            totalPendingCount: 0,
            loadedCount: 4,
            retryCount: 0,
            totalDescription: '0 item(ns) pendente(s) no MongoDB',
            loadedDescription: '4 carregado(s) nos workers'
        });
    });

    it('does not present the worker snapshot as the total when MongoDB is unavailable', () => {
        expect(getUploadQueueCountPresentation(70, { isAvailable: false, pendingCount: 900 })).toEqual({
            totalPendingCount: null,
            loadedCount: 70,
            retryCount: null,
            totalDescription: 'Total pendente indisponível',
            loadedDescription: '70 carregado(s) nos workers'
        });
    });

    it('normalizes invalid counts without inventing a queue total', () => {
        expect(getUploadQueueCountPresentation(-2, {
            isAvailable: true,
            pendingCount: Number.NaN,
            retryCount: -1
        })).toEqual({
            totalPendingCount: null,
            loadedCount: 0,
            retryCount: null,
            totalDescription: 'Total pendente indisponível',
            loadedDescription: '0 carregado(s) nos workers'
        });
    });
});
