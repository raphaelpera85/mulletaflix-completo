(function () {
    type LegacyCreateElement = {
        (this: unknown, ...args: unknown[]): unknown;
        call(thisArg: unknown, ...args: unknown[]): unknown;
        apply(thisArg: unknown, args: unknown[]): unknown;
        __patched?: boolean;
    };

    const patch = (target: { createElement?: unknown; [key: string]: unknown }) => {
        if (!target || typeof target.createElement !== 'function') return;
        const original = target.createElement as LegacyCreateElement;
        if (original.__patched) return;

        target.createElement = function (this: unknown, ...args: unknown[]) {
            const [tagName, options] = args as [string, ElementCreationOptions?];
            if (options && typeof options === 'object' && options.is) {
                return original.call(this, tagName, options.is);
            }
            return original.apply(this, args);
        };
        (target.createElement as LegacyCreateElement).__patched = true;
    };

    const patchNS = (target: { createElementNS?: unknown; [key: string]: unknown }) => {
        if (!target || typeof target.createElementNS !== 'function') return;
        const original = target.createElementNS as LegacyCreateElement;
        if (original.__patched) return;

        target.createElementNS = function (this: unknown, ...args: unknown[]) {
            const [namespace, tagName, options] = args as [string, string, ElementCreationOptions?];
            if (options && typeof options === 'object' && options.is) {
                return original.call(this, namespace, tagName, options.is);
            }
            return original.apply(this, args);
        };
        (target.createElementNS as LegacyCreateElement).__patched = true;
    };

    if (typeof Document !== 'undefined') {
        patch(Document.prototype as unknown as Record<string, unknown>);
        patchNS(Document.prototype as unknown as Record<string, unknown>);
    }
    if (typeof HTMLDocument !== 'undefined') {
        patch(HTMLDocument.prototype as unknown as Record<string, unknown>);
        patchNS(HTMLDocument.prototype as unknown as Record<string, unknown>);
    }
    if (typeof document !== 'undefined') {
        patch(document as unknown as Record<string, unknown>);
        patchNS(document as unknown as Record<string, unknown>);
    }
})();

export {};
