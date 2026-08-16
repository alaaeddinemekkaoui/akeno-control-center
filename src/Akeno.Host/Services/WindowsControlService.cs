using System.Diagnostics;

namespace Akeno.Host.Services;

public sealed class WindowsControlService
{
    public async Task<ControlExecutionResult> SetBrightnessAsync(double value)
    {
        if (!OperatingSystem.IsWindows()) return new(false, "Brightness control requires Windows.");
        var v = (int)Math.Clamp(Math.Round(value), 0, 100);
        var command = $"$m=Get-CimInstance -Namespace root/WMI -ClassName WmiMonitorBrightnessMethods -ErrorAction Stop; $m.WmiSetBrightness(1,{v}) | Out-Null";
        return await RunAsync("powershell.exe", $"-NoProfile -NonInteractive -Command \"{command}\"");
    }

    public async Task<ControlExecutionResult> RunActionAsync(string action)
    {
        if (!OperatingSystem.IsWindows()) return new(false, $"{action} requires Windows.");
        return action switch
        {
            "system.lock" => await RunAsync("rundll32.exe", "user32.dll,LockWorkStation"),
            "system.sleep" => await RunAsync("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0"),
            "system.restart" => await RunAsync("shutdown.exe", "/r /t 0"),
            "system.shutdown" => await RunAsync("shutdown.exe", "/s /t 0"),
            _ => new(false, "Unknown system action.")
        };
    }

    private static async Task<ControlExecutionResult> RunAsync(string fileName, string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true
                }
            };
            process.Start();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var error = await errorTask;
            return process.ExitCode == 0 ? new(true, "OK") : new(false, string.IsNullOrWhiteSpace(error) ? $"Exit code {process.ExitCode}" : error.Trim());
        }
        catch (Exception ex)
        {
            return new(false, ex.Message);
        }
    }
}

public sealed record ControlExecutionResult(bool Success, string Message);
