using McpNetwork.WinNuxService.Interfaces;
using Microsoft.Extensions.Logging;
using Sample.Host.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sample.Host.Services
{
    internal class InternalPluginService : IWinNuxService
    {

        private Task? loop;
        private readonly IDependency dependency;
        private readonly ILogger<InternalPluginService> logger;

        public InternalPluginService(ILogger<InternalPluginService> logger, IDependency dependency)
        {
            this.logger = logger;
            this.dependency = dependency;
        }

        public Task OnStartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("InternalPluginService started");
            loop = RunLoop(cancellationToken);
            return Task.CompletedTask;
        }

        public async Task OnStopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("InternalPluginService stopping");
            if (loop != null)
                await loop;
        }

        private async Task RunLoop(CancellationToken token)
        {

            try
            {
                while (!token.IsCancellationRequested)
                {
                    dependency.SignalAlive();
                    await Task.Delay(TimeSpan.FromMilliseconds(1500), token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown — suppress
                Console.WriteLine("InternalPluginService canceled gracefully.");
            }
            catch (Exception ex)
            {
                logger.LogError($"Unexpected exception in RunLoop: {ex}");
            }
        }

    }
}
