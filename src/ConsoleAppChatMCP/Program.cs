using System.ClientModel;
using ConsoleAppChatMCP.Tracing;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenAI;
using OpenAI.Chat;
using Azure.AI.OpenAI;

var standardForegroundColor = ConsoleColor.White;
Console.ForegroundColor = standardForegroundColor;
Console.WriteLine("***** Testes com Microsoft Agente Framework + Azure OpenAI + MCP Microsoft Learn *****");
Console.WriteLine();

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

var resourceBuilder = ResourceBuilder
    .CreateDefault()
    .AddService(OpenTelemetryExtensions.ServiceName);

var traceProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource(OpenTelemetryExtensions.ServiceName)
    .AddHttpClientInstrumentation()
    .AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri(configuration["OpenTelemetry:Endpoint"]!);
    })
    .Build();

var mcpName = configuration["MCP:Name"]!;
await using var mcpClient = await McpClient.CreateAsync(new HttpClientTransport(new()
{
    Name = mcpName,
    Endpoint = new Uri(configuration["MCP:Endpoint"]!)
}));


Console.WriteLine($"Ferramentas do MCP:");
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"***** {mcpName} *****");
var tools = await mcpClient.ListToolsAsync().ConfigureAwait(false);
Console.WriteLine($"Quantidade de ferramentas disponiveis = {tools.Count}");
Console.WriteLine();
foreach (var tool in tools)
{
    Console.WriteLine($"* {tool.Name}: {tool.Description}");
}
Console.ForegroundColor = standardForegroundColor;
Console.WriteLine();


var agent = new AzureOpenAIClient(endpoint: new Uri(configuration["AzureOpenAI:Endpoint"]!),
        credential: new ApiKeyCredential(configuration["AzureOpenAI:ApiKey"]!))
    .GetChatClient(configuration["AzureOpenAI:DeploymentName"]!)
    .CreateAIAgent(instructions: "Você é um assistente de IA que ajuda a sugerir a usuarios com duvidas " +
            "conteudos da documentacao oficial da Microsoft e que se encontram no Microsoft Learn. " +
            "Ao gerar uma resposta coloque sempre no texto e de forma explicita o link de cada " +
            "documentacao que voce sugerir.",
        tools: [.. tools.Cast<AITool>()])
    .AsBuilder()
    .UseOpenTelemetry(sourceName: OpenTelemetryExtensions.ServiceName)
    .Build();

while (true)
{
    Console.WriteLine("Sua pergunta:");
    Console.ForegroundColor = ConsoleColor.Cyan;
    var userPrompt = Console.ReadLine();
    Console.ForegroundColor = standardForegroundColor;

    using var activity1 = OpenTelemetryExtensions.ActivitySource
        .StartActivity("PerguntaChatIAMCP")!;

    var result = await agent.RunAsync(userPrompt!);

    Console.WriteLine();
    Console.WriteLine("Resposta da IA:");
    Console.WriteLine();

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;

    Console.WriteLine(result.AsChatResponse().Messages.Last().Text);

    Console.ForegroundColor = standardForegroundColor;

    Console.WriteLine();
    Console.WriteLine();

    activity1.Stop();
}