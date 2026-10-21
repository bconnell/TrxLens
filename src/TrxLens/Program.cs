using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using TrxLens;

if (args.Length != 0)
{
    Console.Error.WriteLine("TrxLens accepts no command line arguments. Configure TRXLENS_RESULTS_DIR.");
    return 2;
}

string? resultsDirectory = Environment.GetEnvironmentVariable("TRXLENS_RESULTS_DIR");
if (string.IsNullOrWhiteSpace(resultsDirectory))
{
    Console.Error.WriteLine("TRXLENS_RESULTS_DIR must identify an existing directory containing TRX files.");
    return 2;
}

TrxRepository repository;
try
{
    repository = new TrxRepository(resultsDirectory);
}
catch (ArgumentException)
{
    Console.Error.WriteLine("TRXLENS_RESULTS_DIR is invalid.");
    return 2;
}
catch (IOException)
{
    Console.Error.WriteLine("TRXLENS_RESULTS_DIR could not be opened safely.");
    return 2;
}
catch (UnauthorizedAccessException)
{
    Console.Error.WriteLine("TRXLENS_RESULTS_DIR cannot be accessed.");
    return 2;
}

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Services.AddSingleton(repository);
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<TrxTools>()
    .WithResources<TrxResources>()
    .WithPrompts<TrxPrompts>();
await builder.Build().RunAsync();
return 0;
