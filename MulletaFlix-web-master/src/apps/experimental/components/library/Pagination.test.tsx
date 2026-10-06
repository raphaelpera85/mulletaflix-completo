import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { describe, expect, it, vi, beforeEach } from 'vitest';

vi.mock('lib/globalize', () => ({
    default: {
        translate: (key: string) => ({
            Previous: 'Anterior',
            Next: 'Próximo'
        }[key] || key)
    }
}));

import Pagination from './Pagination';

// W4.3 - interaction/pagination coverage for the library grid pager. Real
// click simulation (createRoot + dispatchEvent), not just static markup,
// since the thing worth locking in here is behavior: which StartIndex a
// click computes, and that the loading/disabled state actually blocks it.
describe('Pagination', () => {
    let container: HTMLDivElement;
    let root: ReturnType<typeof createRoot>;
    let scrollToSpy: ReturnType<typeof vi.spyOn>;

    beforeEach(() => {
        container = document.createElement('div');
        root = createRoot(container);
        scrollToSpy = vi.spyOn(window, 'scrollTo').mockImplementation(() => undefined);
    });

    const clickButton = async (title: string) => {
        const button = Array.from(container.querySelectorAll('button'))
            .find(b => b.title === title);
        await act(async () => button?.dispatchEvent(new MouseEvent('click', { bubbles: true })));
    };

    it('disables Previous on the first page and enables Next when more pages remain', async () => {
        await act(async () => root.render(
            <Pagination setLibraryViewSettings={vi.fn()} index={0} pageSize={50} total={200} />
        ));

        const previous = Array.from(container.querySelectorAll('button')).find(b => b.title === 'Anterior');
        const next = Array.from(container.querySelectorAll('button')).find(b => b.title === 'Próximo');

        expect(previous?.disabled).toBe(true);
        expect(next?.disabled).toBe(false);

        await act(async () => root.unmount());
    });

    it('disables Next on the last page and enables Previous', async () => {
        await act(async () => root.render(
            <Pagination setLibraryViewSettings={vi.fn()} index={150} pageSize={50} total={200} />
        ));

        const previous = Array.from(container.querySelectorAll('button')).find(b => b.title === 'Anterior');
        const next = Array.from(container.querySelectorAll('button')).find(b => b.title === 'Próximo');

        expect(previous?.disabled).toBe(false);
        expect(next?.disabled).toBe(true);

        await act(async () => root.unmount());
    });

    it('the disabled prop forces both buttons off regardless of index/total (loading state)', async () => {
        await act(async () => root.render(
            <Pagination setLibraryViewSettings={vi.fn()} index={50} pageSize={50} total={200} disabled />
        ));

        const previous = Array.from(container.querySelectorAll('button')).find(b => b.title === 'Anterior');
        const next = Array.from(container.querySelectorAll('button')).find(b => b.title === 'Próximo');

        expect(previous?.disabled).toBe(true);
        expect(next?.disabled).toBe(true);

        await act(async () => root.unmount());
    });

    it('advances StartIndex by pageSize and scrolls to top when Next is clicked', async () => {
        const setLibraryViewSettings = vi.fn();
        await act(async () => root.render(
            <Pagination setLibraryViewSettings={setLibraryViewSettings} index={50} pageSize={50} total={200} />
        ));

        await clickButton('Próximo');

        expect(setLibraryViewSettings).toHaveBeenCalledOnce();
        // The setter is passed a functional updater (prevState => ({...})); exercise
        // it the same way React does, against a representative previous state.
        const updater = setLibraryViewSettings.mock.calls[0][0] as (prev: { StartIndex: number }) => { StartIndex: number };
        expect(updater({ StartIndex: 50 })).toEqual({ StartIndex: 100 });
        expect(scrollToSpy).toHaveBeenCalledWith(0, 0);

        await act(async () => root.unmount());
    });

    it('retreats StartIndex by pageSize, clamped to zero, when Previous is clicked', async () => {
        const setLibraryViewSettings = vi.fn();
        await act(async () => root.render(
            <Pagination setLibraryViewSettings={setLibraryViewSettings} index={30} pageSize={50} total={200} />
        ));

        await clickButton('Anterior');

        expect(setLibraryViewSettings).toHaveBeenCalledOnce();
        const updater = setLibraryViewSettings.mock.calls[0][0] as (prev: { StartIndex: number }) => { StartIndex: number };
        // index (30) - pageSize (50) would be negative; must clamp to 0, not go negative.
        expect(updater({ StartIndex: 30 })).toEqual({ StartIndex: 0 });
        expect(scrollToSpy).toHaveBeenCalledWith(0, 0);

        await act(async () => root.unmount());
    });
});
