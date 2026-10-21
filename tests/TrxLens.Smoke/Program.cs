using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

if (args.Length != 2 || !File.Exists(args[0]) || !Directory.Exists(args[1]))
{
    Console.Error.WriteLine("Usage: TrxLens.Smoke <server-dll> <samples-directory>");
    return 2;
}

Dictionary<string, string?> environment = StdioClientTransportOptions.GetDefaultEnvironmentVariables();
environment["TRXLENS_RESULTS_DIR"] = Path.GetFullPath(args[1]);
environment["DOTNET_NOLOGO"] = "1";

StdioClientTransport transport = new(new StdioClientTransportOptions
{
    Command = "dotnet",
    Arguments = [Path.GetFullPath(args[0])],
    InheritEnvironmentVariables = false,
    EnvironmentVariables = environment
});

await using McpClient client = await McpClient.CreateAsync(transport);
IList<McpClientTool> tools = await client.ListToolsAsync();
string[] expectedTools = ["list_test_runs", "summarize_test_run", "get_failed_tests", "compare_test_runs"];
foreach (string name in expectedTools)
{
    Ensure(tools.Any(tool => tool.Name == name), "MCP tool not discovered: " + name);
}

Ensure(tools.Count == expectedTools.Length, "Unexpected MCP tools were discovered.");
Ensure((await client.ListResourcesAsync()).Any(resource => resource.Uri == "trxlens://capabilities"),
    "Capabilities resource not discovered.");
Ensure((await client.ListPromptsAsync()).Any(prompt => prompt.Name == "investigate_test_failures"),
    "Investigation prompt not discovered.");

ReadResourceResult resourceResult = await client.ReadResourceAsync("trxlens://capabilities");
Ensure(resourceResult.Contents.OfType<TextResourceContents>()
    .Any(content => content.Text.Contains("Maximum", StringComparison.OrdinalIgnoreCase)
        || content.Text.Contains("maximumRuns", StringComparison.OrdinalIgnoreCase)),
    "Capabilities resource could not be read.");

CallToolResult list = await client.CallToolAsync("list_test_runs");
Ensure(list.IsError != true, "list_test_runs returned an MCP tool error.");
Ensure(Contains(list, "baseline.trx") && Contains(list, "current.trx"), "Run listing omitted sample fixtures.");

CallToolResult summary = await client.CallToolAsync("summarize_test_run", new Dictionary<string, object?>
{
    ["run"] = "current.trx"
});
Ensure(summary.IsError != true, "summarize_test_run returned an MCP tool error.");
Ensure(Contains(summary, "Failed"), "Summary was not serialized.");

CallToolResult failed = await client.CallToolAsync("get_failed_tests", new Dictionary<string, object?>
{
    ["run"] = "current.trx"
});
Ensure(failed.IsError != true && Contains(failed, "Page did not load"), "Failure evidence not returned.");

CallToolResult comparison = await client.CallToolAsync("compare_test_runs", new Dictionary<string, object?>
{
    ["baseline"] = "baseline.trx",
    ["current"] = "current.trx"
});
Ensure(comparison.IsError != true, "Comparison returned an MCP tool error.");
Ensure(Contains(comparison, "Sample.CheckoutTests.Loads")
    && Contains(comparison, "Sample.CheckoutTests.Refunds")
    && Contains(comparison, "Sample.CheckoutTests.Legacy"), "Comparison omitted expected evidence.");

CallToolResult unsafePath = await client.CallToolAsync("summarize_test_run", new Dictionary<string, object?>
{
    ["run"] = "../other.trx"
});
Ensure(unsafePath.IsError == true, "Unsafe paths were not rejected as MCP errors.");

GetPromptResult prompt = await client.GetPromptAsync("investigate_test_failures", new Dictionary<string, object?>
{
    ["baseline"] = "baseline.trx",
    ["current"] = "current.trx"
});
Ensure(prompt.Messages.Count > 0, "MCP prompt yielded no messages.");

Console.WriteLine("MCP stdio discovery, tools, resource, prompt and invalid-path checks passed.");
return 0;

static bool Contains(CallToolResult result, string fragment) =>
    result.Content.OfType<TextContentBlock>()
        .Any(item => item.Text.Contains(fragment, StringComparison.OrdinalIgnoreCase));

static void Ensure(bool condition, string error)
{
    if (!condition)
    {
        throw new InvalidOperationException(error);
    }
}
