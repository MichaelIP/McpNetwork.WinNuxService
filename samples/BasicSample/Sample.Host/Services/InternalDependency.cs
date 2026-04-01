using Microsoft.Extensions.Logging;
using Sample.Host.Interfaces;

namespace Sample.Host.Services
{
    internal class InternalDependency : IDependency
    {

        private ILogger<InternalDependency> logger;

        public InternalDependency(ILogger<InternalDependency> logger) 
        { 
            this.logger = logger;
        }

        public void SignalAlive()
        {
            logger.LogInformation("InternalDependency is present.");
        }
    }
}
