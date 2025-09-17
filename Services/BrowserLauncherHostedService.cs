using Microsoft.Extensions.Hosting;
using System.Diagnostics;

namespace BallastLog.Mate.Services;

public class BrowserLauncherHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _lifetime;

    public BrowserLauncherHostedService(IHostApplicationLifetime lifetime) => _lifetime = lifetime;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _lifetime.ApplicationStarted.Register(() =>
        {
            try
            {
                var url = "http://127.0.0.1:7777";
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch { /* ignore */ }
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}