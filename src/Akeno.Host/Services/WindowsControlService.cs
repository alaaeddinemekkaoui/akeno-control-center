using System.Diagnostics;

namespace Akeno.Host.Services;

public sealed class WindowsControlService
{
    public async Task<ControlExecutionResult> SetBrightnessAsync(double value)
    {
        if (!OperatingSystem.IsWindows()) return new(false, "Brightness control requires Windows.");
        var v = (int)Math.Clamp(Math.Round(value), 0, 100);
        var command = "$m=Get-CimInstance -Namespace root/WMI -ClassName WmiMonitorBrightnessMethods -ErrorAction Stop; if(-not $m){ throw 'No internal monitor brightness support detected.' }; $m.WmiSetBrightness(1," + v + ") | Out-Null";
        return await RunAsync("powershell.exe", $"-NoProfile -NonInteractive -Command \"{command}\"");
    }

    public async Task<(bool Available, double? Value, string? Error)> TryReadBrightnessAsync()
    {
        if (!OperatingSystem.IsWindows()) return (false, null, "Brightness control requires Windows.");
        var command = "try { $b=Get-CimInstance -Namespace root/WMI -ClassName WmiMonitorBrightness -ErrorAction Stop | Select-Object -First 1 -ExpandProperty CurrentBrightness; if($null -eq $b){ Write-Output 'UNAVAILABLE' } else { Write-Output $b } } catch { Write-Output 'UNAVAILABLE' }";
        var result = await RunAsync("powershell.exe", $"-NoProfile -NonInteractive -Command \"{command}\"");
        if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
        {
            return (false, null, "Brightness control is not supported on this monitor.");
        }

        if (result.Output.Trim().Equals("UNAVAILABLE", StringComparison.OrdinalIgnoreCase))
        {
            return (false, null, "Brightness control is not supported on this monitor.");
        }

        return double.TryParse(result.Output.Trim(), out var value)
            ? (true, Math.Clamp(value, 0, 100), null)
            : (false, null, "Brightness control is not supported on this monitor.");
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
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = (await outputTask).Trim();
            var error = (await errorTask).Trim();

            if (process.ExitCode == 0)
            {
                return new(true, "OK", output);
            }

            return new(false, string.IsNullOrWhiteSpace(error) ? $"Exit code {process.ExitCode}" : error, output);
        }
        catch (Exception ex)
        {
            return new(false, ex.Message, null);
        }
    }
}

public sealed record ControlExecutionResult(bool Success, string Message, string? Output = null);
