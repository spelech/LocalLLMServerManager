namespace LocalLLMServerManager.Services;

public interface IGitUpdateService
{
    bool IsValidBranchName(string branch);
    Task<(bool Success, string Output, string Error)> RunCommandAsync(string appPath, string[] args, string workingDir);
}
