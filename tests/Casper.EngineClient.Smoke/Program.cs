using Casper.DataForge.CrossPlatform.Engine;

var client = new CasperEngineClient();

Console.WriteLine($"EnginePath={client.ExecutablePath}");
Console.WriteLine($"EngineAvailable={client.IsAvailable}");

if (!client.IsAvailable)
{
    Environment.ExitCode = 2;
    return;
}

try
{
    CasperResponse result = await client.QueryAsync("who is n8n");

    Console.WriteLine($"ExitCode={result.ExitCode}");
    Console.WriteLine($"Query={result.Query}");
    Console.WriteLine($"Confidence={result.Confidence}");
    Console.WriteLine($"Sources={result.SourceCount}");
    Console.WriteLine($"Proof={result.Proof}");
    Console.WriteLine($"Error={result.Error}");

    bool valid =
        result.ExitCode == 0 &&
        result.Query == "who is n8n" &&
        result.SourceCount > 0 &&
        !string.IsNullOrWhiteSpace(result.Proof);

    Console.WriteLine($"CLIENT_SMOKE_PASS={valid}");

    Environment.ExitCode = valid ? 0 : 3;
}
catch (Exception exception)
{
    Console.WriteLine("CLIENT_SMOKE_PASS=False");
    Console.WriteLine(exception);
    Environment.ExitCode = 4;
}
