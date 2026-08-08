using System.Diagnostics;
using System.Text;

namespace LocalLLMServerManager.Services;

public class GitUpdateService : IGitUpdateService
{
    public bool IsValidBranchName(string branch)
    {
        if (string.IsNullOrWhiteSpace(branch)) return false;
        if (branch.Length > 255) return false;
        if (branch.StartsWith("-") || branch.StartsWith("/") || branch.StartsWith(".")) return false;
        if (branch.EndsWith(".lock") || branch.EndsWith("/")) return false;
        if (branch.Contains("..") || branch.Contains("@{") || branch.Contains("//")) return false;

        foreach (char c in branch)
        {
            if (char.IsControl(c) || char.IsWhiteSpace(c)) return false;
            if (c == '~' || c == '^' || c == ':' || c == '?' || c == '*' || c == '[' || c == '\\' || c == '"' || c == '\'' || c == ';')
            {
                return false;
            }
        }
        return true;
    }

    public async Task<(bool Success, string Output, string Error)> RunCommandAsync(string appPath, string[] args, string workingDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = appPath,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await process.WaitForExitAsync(cts.Token);
            return (process.ExitCode == 0, outputBuilder.ToString(), errorBuilder.ToString());
        }
        catch (Exception ex)
        {
            return (false, outputBuilder.ToString(), ex.Message);
        }
    }
}
