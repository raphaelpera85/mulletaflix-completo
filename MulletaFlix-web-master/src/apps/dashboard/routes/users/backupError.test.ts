import { describe, expect, it } from 'vitest';
import { getBackupErrorMessage } from './backupError';

describe('getBackupErrorMessage', () => {
    it('reads the API message from responseJSON', () => {
        expect(getBackupErrorMessage({ responseJSON: { Message: 'Management API recusou o token.' } }))
            .toBe('Management API recusou o token.');
    });

    it('parses an API error serialized in response.data', () => {
        expect(getBackupErrorMessage({ response: { data: '{"message":"Project ID inválido."}' } }))
            .toBe('Project ID inválido.');
    });

    it('falls back to the request error message', () => {
        expect(getBackupErrorMessage({ message: 'Falha de rede.' })).toBe('Falha de rede.');
    });

    it('does not expose an object representation when no message exists', () => {
        expect(getBackupErrorMessage({ response: { data: { code: 'bad_request' } } })).toBe('erro desconhecido');
    });
});
