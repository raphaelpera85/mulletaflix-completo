import { afterEach, describe, expect, it, vi } from 'vitest';
import { loadSectionItems } from './asyncItemsSection';

vi.mock('lib/globalize', () => ({
    default: {
        translate: (key: string) => ({
            AccessibilityLoading: 'Carregando',
            ErrorDefault: 'Request failed.',
            Retry: 'Retry'
        })[key] || key
    }
}));

describe('async items section loading', () => {
    afterEach(() => {
        vi.restoreAllMocks();
        while (document.body.firstChild) {
            document.body.removeChild(document.body.firstChild);
        }
    });

    function createSection() {
        const section = document.createElement('section');
        section.className = 'hide';
        const title = document.createElement('h2');
        title.textContent = 'Episodes';
        const itemsContainer = document.createElement('div');
        section.append(title, itemsContainer);
        document.body.append(section);
        return { section, itemsContainer };
    }

    it('shows a retryable partial error instead of hiding a failed section', async () => {
        const { section, itemsContainer } = createSection();
        const loadItems = vi.fn()
            .mockRejectedValueOnce(new Error('temporary failure'))
            .mockResolvedValueOnce({ Items: ['Episode 1'] });
        const renderItems = vi.fn((items: string[]) => {
            itemsContainer.textContent = items.join(', ');
        });
        vi.spyOn(console, 'error').mockImplementation(() => undefined);

        loadSectionItems({
            section,
            loadItems,
            selectItems: result => (result as { Items: string[] }).Items,
            renderItems,
            label: 'episodes'
        });

        await vi.waitFor(() => expect(section.querySelector('[role="alert"]')).not.toBeNull());
        expect(section.classList.contains('hide')).toBe(false);
        expect(section.querySelector('[role="alert"]')?.textContent).toContain('Request failed.');
        expect(section.querySelector('[role="status"]')).toBeNull();
        expect(section.querySelector('h2')?.nextElementSibling).toBe(section.querySelector('[role="alert"]'));

        (section.querySelector('button') as HTMLButtonElement).click();

        await vi.waitFor(() => expect(itemsContainer.textContent).toBe('Episode 1'));
        expect(loadItems).toHaveBeenCalledTimes(2);
        expect(renderItems).toHaveBeenCalledWith(['Episode 1']);
        expect(section.querySelector('[role="alert"]')).toBeNull();
        expect(section.classList.contains('hide')).toBe(false);
        expect(section.hasAttribute('aria-busy')).toBe(false);
    });

    it('keeps a successful empty result hidden without showing an error', async () => {
        const { section } = createSection();
        const renderItems = vi.fn(() => undefined);

        loadSectionItems({
            section,
            loadItems: vi.fn().mockResolvedValue({ Items: [] }),
            selectItems: result => (result as { Items: string[] }).Items,
            renderItems,
            label: 'seasons'
        });

        await vi.waitFor(() => expect(section.hasAttribute('aria-busy')).toBe(false));
        expect(section.classList.contains('hide')).toBe(true);
        expect(section.querySelector('[role="status"]')).toBeNull();
        expect(section.querySelector('[role="alert"]')).toBeNull();
        expect(renderItems).not.toHaveBeenCalled();
    });

    it('announces and displays loading while the section request is pending', async () => {
        const { section, itemsContainer } = createSection();
        let resolveItems!: (result: { Items: string[] }) => void;
        const loadItems = vi.fn(() => new Promise<{ Items: string[] }>(resolve => {
            resolveItems = resolve;
        }));

        loadSectionItems({
            section,
            loadItems,
            selectItems: result => result.Items,
            renderItems: items => {
                itemsContainer.textContent = items.join(', ');
            },
            label: 'episodes'
        });

        await vi.waitFor(() => expect(section.querySelector('[role="status"]')?.textContent).toBe('Carregando'));
        expect(section.classList.contains('hide')).toBe(false);
        expect(section.getAttribute('aria-busy')).toBe('true');

        resolveItems({ Items: ['Episode 1'] });

        await vi.waitFor(() => expect(itemsContainer.textContent).toBe('Episode 1'));
        expect(section.querySelector('[role="status"]')).toBeNull();
        expect(section.hasAttribute('aria-busy')).toBe(false);
    });
});
