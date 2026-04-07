using McpNetwork.WinNuxService;
using Microsoft.Extensions.Logging;
using Sample.Host.Interfaces;

namespace Sample.Host.Services
{
    internal class InternalPluginService : WinNuxServiceBase
    {

        private readonly IDependency dependency;
        private readonly ILogger<InternalPluginService> logger;

        public InternalPluginService(ILogger<InternalPluginService> logger, IDependency dependency)
        {
            this.logger = logger;
            this.dependency = dependency;
        }

        protected async override Task ExecuteAsync(CancellationToken token)
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
