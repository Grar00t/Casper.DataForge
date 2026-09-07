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

string? previousEngineSha =
    Environment.GetEnvironmentVariable(
        CasperEngineClient.EngineSha256EnvironmentVariable);

try
{
    Environment.SetEnvironmentVariable(
        CasperEngineClient.EngineSha256EnvironmentVariable,
        null);

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
        configuredClient.IntegrityState == EngineIntegrityState.Verified &&
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

    var unpinnedClient =
        new CasperEngineClient(
            TimeSpan.FromSeconds(1),
            fixtureExecutablePath);

    bool unpinnedStatePass =
        unpinnedClient.IntegrityState == EngineIntegrityState.Unpinned;

    Console.WriteLine($"UNPINNED_ENGINE_STATE_PASS={unpinnedStatePass}");

    if (!unpinnedStatePass)
    {
        Environment.ExitCode = 3;
        return;
    }

    var mismatchClient =
        new CasperEngineClient(
            TimeSpan.FromSeconds(1),
            fixtureExecutablePath,
            new string('0', 64));

    bool mismatchStatePass =
        mismatchClient.IntegrityState == EngineIntegrityState.Invalid;

    Console.WriteLine($"ENGINE_MISMATCH_STATE_PASS={mismatchStatePass}");

    if (!mismatchStatePass)
    {
        Environment.ExitCode = 4;
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
            environmentClient.IntegrityState == EngineIntegrityState.Verified &&
            string.Equals(
                environmentClient.ExecutablePath,
                Path.GetFullPath(fixtureExecutablePath),
                StringComparison.Ordinal);

        Console.WriteLine($"ENVIRONMENT_ENGINE_PASS={environmentPathPass}");

        if (!environmentPathPass)
        {
            Environment.ExitCode = 5;
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
        Environment.ExitCode = 6;
        return;
    }

    bool policyExitAccepted = true;
    try
    {
        CasperEngineClient.ValidateResponse(
            query,
            response with
            {
                ExitCode = 1,
                Violated = true
            });
    }
    catch
    {
        policyExitAccepted = false;
    }

    Console.WriteLine($"POLICY_EXIT_ACCEPTED_PASS={policyExitAccepted}");

    if (!policyExitAccepted)
    {
        Environment.ExitCode = 7;
        return;
    }

    bool unflaggedExitOneRejected = false;
    try
    {
        CasperEngineClient.ValidateResponse(
            query,
            response with
            {
                ExitCode = 1,
                Violated = false,
                Rejected = false
            });
    }
    catch (InvalidDataException)
    {
        unflaggedExitOneRejected = true;
    }

    Console.WriteLine($"UNFLAGGED_EXIT_ONE_REJECTED_PASS={unflaggedExitOneRejected}");

    if (!unflaggedExitOneRejected)
    {
        Environment.ExitCode = 8;
        return;
    }

    bool fatalExitRejected = false;
    try
    {
        CasperEngineClient.ValidateResponse(
            query,
            response with
            {
                ExitCode = 2,
                Violated = true
            });
    }
    catch (InvalidDataException)
    {
        fatalExitRejected = true;
    }

    Console.WriteLine($"FATAL_EXIT_REJECTED_PASS={fatalExitRejected}");

    if (!fatalExitRejected)
    {
        Environment.ExitCode = 9;
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
        Environment.ExitCode = 10;
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

    if (!invalidHashRejected)
    {
        Environment.ExitCode = 11;
        return;
    }

    string proofFileName = "casper_fixture.proof";
    string proofPath = Path.Combine(tempDirectory, proofFileName);
    File.WriteAllText(
        proofPath,
        $"NIYAH-PROOF-V1\nhash: {response.Proof}\nprompt_hash: {new string('C', 64)}\noutput_hash: {new string('D', 64)}\nrules_hash: {new string('0', 64)}\nprompt: {query}\noutput: {response.Answer}\n",
        new UTF8Encoding(false));

    CasperResponse boundProofResponse =
        CasperEngineClient.ValidateProofFile(
            response with { ProofFile = proofFileName },
            tempDirectory);

    bool proofBindingPass =
        boundProofResponse.ProofFileBound &&
        string.Equals(
            boundProofResponse.ProofFile,
            Path.GetFullPath(proofPath),
            StringComparison.Ordinal) &&
        string.Equals(
            boundProofResponse.ProofFileDeclaredHash,
            response.Proof,
            StringComparison.OrdinalIgnoreCase);

    Console.WriteLine($"PROOF_FILE_BINDING_PASS={proofBindingPass}");

    if (!proofBindingPass)
    {
        Environment.ExitCode = 12;
        return;
    }

    bool proofMismatchRejected = false;
    File.WriteAllText(
        proofPath,
        $"NIYAH-PROOF-V1\nhash: {new string('E', 64)}\n",
        new UTF8Encoding(false));

    try
    {
        _ = CasperEngineClient.ValidateProofFile(
            response with { ProofFile = proofFileName },
            tempDirectory);
    }
    catch (InvalidDataException)
    {
        proofMismatchRejected = true;
    }

    Console.WriteLine($"PROOF_FILE_MISMATCH_REJECTED_PASS={proofMismatchRejected}");

    bool pass =
        configuredPathPass &&
        unpinnedStatePass &&
        mismatchStatePass &&
        responseValidationPass &&
        policyExitAccepted &&
        unflaggedExitOneRejected &&
        fatalExitRejected &&
        mismatchRejected &&
        invalidHashRejected &&
        proofBindingPass &&
        proofMismatchRejected;

    Console.WriteLine($"CLIENT_SMOKE_PASS={pass}");
    Environment.ExitCode = pass ? 0 : 13;
}
finally
{
    Environment.SetEnvironmentVariable(
        CasperEngineClient.EngineSha256EnvironmentVariable,
        previousEngineSha);

    try
    {
        Directory.Delete(tempDirectory, recursive: true);
    }
    catch
    {
    }
}
