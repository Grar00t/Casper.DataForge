using System;
using System.IO;
using System.Text;
using Casper.DataForge.CrossPlatform.Engine;

string encodedUrl =
    "//duckduckgo.com/l/?uddg=https%3A%2F%2Fn8n.io%2F&amp;amp;rut=abc";

string normalizedUrl =
    SourceTextNormalizer.NormalizeUrl(encodedUrl);

string expectedUrl =
    "https://duckduckgo.com/l/?uddg=https%3A%2F%2Fn8n.io%2F&rut=abc";

bool urlNormalizationPass =
    string.Equals(
        normalizedUrl,
        expectedUrl,
        StringComparison.Ordinal);

Console.WriteLine($"NormalizedUrl={normalizedUrl}");
Console.WriteLine($"URL_NORMALIZATION_PASS={urlNormalizationPass}");

if (!urlNormalizationPass)
{
    Environment.ExitCode = 1;
    return;
}

const string payload = "casper-dataforge-smoke";
const string expectedSha256 =
    "F35A54438C4756A66B467114F051350A7EC1184F46BCDB7ADCF3C31ED79BB5DF";

string tempDirectory =
    Path.Combine(
        Path.GetTempPath(),
        "Casper.DataForge.Smoke",
        Guid.NewGuid().ToString("N"));

Directory.CreateDirectory(tempDirectory);
string fixtureExecutablePath = Path.Combine(tempDirectory, "engine.bin");

try
{
    File.WriteAllText(
        fixtureExecutablePath,
        payload,
        new UTF8Encoding(false));

    var configuredClient =
        new CasperEngineClient(
            TimeSpan.FromSeconds(1),
            fixtureExecutablePath,
            expectedSha256);

    bool configuredPathPass =
        configuredClient.UsesConfiguredExecutable &&
        configuredClient.IsAvailable &&
        string.Equals(
            configuredClient.ExecutablePath,
            Path.GetFullPath(fixtureExecutablePath),
            StringComparison.Ordinal) &&
        string.Equals(
            configuredClient.ComputeSha256(),
            expectedSha256,
            StringComparison.Ordinal) &&
        configuredClient.VerifySha256(expectedSha256);

    Console.WriteLine($"CONFIGURED_ENGINE_PASS={configuredPathPass}");

    if (!configuredPathPass)
    {
        Environment.ExitCode = 2;
        return;
    }

    string? previousEnginePath =
        Environment.GetEnvironmentVariable(
            CasperEngineClient.EnginePathEnvironmentVariable);

    try
    {
        Environment.SetEnvironmentVariable(
            CasperEngineClient.EnginePathEnvironmentVariable,
            fixtureExecutablePath);

        var environmentClient =
            new CasperEngineClient(
                TimeSpan.FromSeconds(1),
                expectedSha256: expectedSha256);

        bool environmentPathPass =
            environmentClient.UsesConfiguredExecutable &&
            string.Equals(
                environmentClient.ExecutablePath,
                Path.GetFullPath(fixtureExecutablePath),
                StringComparison.Ordinal);

        Console.WriteLine($"ENVIRONMENT_ENGINE_PASS={environmentPathPass}");

        if (!environmentPathPass)
        {
            Environment.ExitCode = 3;
            return;
        }
    }
    finally
    {
        Environment.SetEnvironmentVariable(
            CasperEngineClient.EnginePathEnvironmentVariable,
            previousEnginePath);
    }

    const string query = "who is n8n";
    var response = new CasperResponse
    {
        Query = query,
        Answer = "n8n",
        Confidence = 0.9,
        ElapsedMilliseconds = 1,
        Proof = new string('A', 64),
        SourceCount = 1,
        Sources =
        [
            new CasperSource
            {
                Number = 1,
                Score = 0.8,
                Sha256 = new string('B', 64),
                Title = "n8n",
                Url = "https://n8n.io/"
            }
        ],
        ExitCode = 0
    };

    bool responseValidationPass = true;
    try
    {
        CasperEngineClient.ValidateResponse(query, response);
    }
    catch
    {
        responseValidationPass = false;
    }

    Console.WriteLine($"RESPONSE_VALIDATION_PASS={responseValidationPass}");

    if (!responseValidationPass)
    {
        Environment.ExitCode = 4;
        return;
    }

    bool mismatchRejected = false;
    try
    {
        CasperEngineClient.ValidateResponse(
            "different query",
            response);
    }
    catch (InvalidDataException)
    {
        mismatchRejected = true;
    }

    Console.WriteLine($"MISMATCH_REJECTED_PASS={mismatchRejected}");

    if (!mismatchRejected)
    {
        Environment.ExitCode = 5;
        return;
    }

    bool invalidHashRejected = false;
    try
    {
        _ = new CasperEngineClient(
            expectedSha256: "not-a-sha256");
    }
    catch (ArgumentException)
    {
        invalidHashRejected = true;
    }

    Console.WriteLine($"INVALID_HASH_REJECTED_PASS={invalidHashRejected}");

    bool pass =
        configuredPathPass &&
        responseValidationPass &&
        mismatchRejected &&
        invalidHashRejected;

    Console.WriteLine($"CLIENT_SMOKE_PASS={pass}");
    Environment.ExitCode = pass ? 0 : 6;
}
finally
{
    try
    {
        Directory.Delete(tempDirectory, recursive: true);
    }
    catch
    {
    }
}
