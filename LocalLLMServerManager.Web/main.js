import { dotnet } from './_framework/dotnet.js';

const is_browser = typeof window != "undefined";
if (!is_browser) {
    throw new Error(`Expected to be running in a browser`);
}

globalThis.getOrigin = function () {
    return window.location.origin;
};

const { runMain } = await dotnet
    .withConfigSrc('./LocalLLMServerManager.Web.runtimeconfig.json')
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

await runMain();
