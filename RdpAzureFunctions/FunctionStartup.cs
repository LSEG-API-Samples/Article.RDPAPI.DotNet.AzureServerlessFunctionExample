using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using RdpAzureFunctions;

[assembly: FunctionsStartup(typeof(Startup))]

namespace RdpAzureFunctions
{
    /// <summary>
    /// Startup Function,Azure will call this function first and we call HttpClient to use in Azure functions.
    /// </summary>
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddHttpClient();

        }
    }
}