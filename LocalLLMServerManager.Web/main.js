import { dotnet } from './_framework/dotnet.js';

const is_browser = typeof window != "undefined";
if (!is_browser) {
    throw new Error(`Expected to be running in a browser`);
}

globalThis.getOrigin = function () {
    return window.location.origin;
};

const APP_VERSION = "3.9.0";

const { runMain } = await dotnet
    .withConfigSrc(`./dotnet.boot.js?v=${APP_VERSION}`)
    .withResourceLoader((type, name, defaultUri) => {
        if (defaultUri) {
            const sep = defaultUri.includes('?') ? '&' : '?';
            return `${defaultUri}${sep}v=${APP_VERSION}`;
        }
        return defaultUri;
    })
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

await runMain();
