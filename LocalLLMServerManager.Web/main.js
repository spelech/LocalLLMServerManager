import { dotnet } from './_framework/dotnet.js';

const is_browser = typeof window != "undefined";
if (!is_browser) {
    throw new Error(`Expected to be running in a browser`);
}

const APP_VERSION = "3.11.0";

globalThis.getOrigin = function () {
    return window.location.origin;
};

globalThis.getAppVersion = function () {
    return APP_VERSION;
};

const { runMain } = await dotnet
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

