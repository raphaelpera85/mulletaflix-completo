import { expect, it, vi } from 'vitest';
import { getPluginControllerUrl } from './pluginControllerUrl';

it('authenticates protected controller module imports', () => {
    const getUrl = vi.fn(path => 'https://server.test/base' + path);
    const url = new URL(getPluginControllerUrl({ getUrl, accessToken: () => 'test-token' }, 'configurationpage?name=NebulaFTP-config'));
    expect(url.pathname).toBe('/base/web/configurationpage');
    expect(url.searchParams.get('name')).toBe('NebulaFTP-config');
    expect(url.searchParams.get('api_key')).toBe('test-token');
});

it('does not add an empty authentication query parameter', () => {
    const getUrl = vi.fn(path => 'https://server.test' + path);
    const url = new URL(getPluginControllerUrl({ getUrl, accessToken: () => '' }, 'configurationpage?name=plugin'));
    expect(url.searchParams.has('api_key')).toBe(false);
});
