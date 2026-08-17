using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Casper.DataForge.CrossPlatform.Engine;

public sealed class CasperEngineClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CasperEngineClient(TimeSpan? timeout = null)
    {
        Timeout = timeout ?? TimeSpan.FromSeconds(30);
        if (Timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");
    }

    public TimeSpan Timeout { get; }

    public string ExecutablePath
    {
        get
        {
            string fileName = OperatingSystem.IsWindows() ? "casper.exe" : "casper";
            string? runtimeDirectory = GetRuntimeDirectory();

            if (runtimeDirectory is not null)
            {
                string platformPath = Path.Combine(AppContext.BaseDirectory, "Engine", "bin", runtimeDirectory, fileName);
                if (File.Exists(platformPath))
                    return platformPath;
            }

            return Path.Combine(AppContext.BaseDirectory, "Engine", "bin", fileName);
        }
    }

    public bool IsAvailable => File.Exists(ExecutablePath);

    public string ComputeSha256()
    {
        if (!IsAvailable)
            throw new FileNotFoundException("Casper engine executable was not found.", ExecutablePath);

        using FileStream stream = File.OpenRead(ExecutablePath);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public bool VerifySha256(string expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256) || !IsAvailable)
            return false;

        string expected = expectedSha256.Trim();
        return expected.Length == 64 && string.Equals(ComputeSha256(), expected, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<CasperResponse> QueryAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty.", nameof(query));
        if (!IsAvailable)
            throw new FileNotFoundException("Casper engine executable was not found.", ExecutablePath);

        using CancellationTokenSource timeoutCts = new(Timeout);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        ProcessStartInfo startInfo = new()
        {
            FileName = ExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(ExecutablePath) ?? AppContext.BaseDirectory
        };
        startInfo.ArgumentList.Add(query);

        using Process process = new() { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("Casper engine did not start.");

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            Terminate(process);
            await process.WaitForExitAsync().ConfigureAwait(false);
            throw new TimeoutException($"Casper engine exceeded {Timeout.TotalSeconds:0.###} seconds.");
        }
        catch (OperationCanceledException)
        {
            Terminate(process);
            await process.WaitForExitAsync().ConfigureAwait(false);
            throw;
        }

        string output = await outputTask.ConfigureAwait(false);
        string error = await errorTask.ConfigureAwait(false);
        int exitCode = process.ExitCode;

        if (exitCode != 0)
            throw new InvalidOperationException($"Casper engine exited with code {exitCode}. Error={error.Trim()}");
        if (string.IsNullOrWhiteSpace(output))
            throw new InvalidDataException($"Casper returned no JSON. Error={error.Trim()}");

        CasperResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<CasperResponse>(output, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Casper returned invalid JSON.", exception);
        }

        if (response is null)
            throw new InvalidDataException("Casper returned an empty JSON value.");
        if (response.SourceCount < 0)
            throw new InvalidDataException("Casper returned a negative source count.");

        IReadOnlyList<CasperSource> sources = response.Sources ?? Array.Empty<CasperSource>();
        if (response.SourceCount != sources.Count)
            throw new InvalidDataException($"Casper source count mismatch: declared {response.SourceCount}, actual {sources.Count}.");

        return response with { ExitCode = exitCode, StandardError = error, Sources = sources };
    }

    private static string? GetRuntimeDirectory()
    {
        string architecture = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => string.Empty
        };

        if (architecture.Length == 0)
            return null;
        if (OperatingSystem.IsWindows())
            return $"win-{architecture}";
        if (OperatingSystem.IsMacOS())
            return $"osx-{architecture}";
        if (OperatingSystem.IsLinux())
            return $"linux-{architecture}";
        return null;
    }

    private static void Terminate(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }
}

public sealed record CasperResponse
{
    [JsonPropertyName("query")] public string? Query { get; init; }
    [JsonPropertyName("answer")] public string? Answer { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }
    [JsonPropertyName("confidence")] public double Confidence { get; init; }
    [JsonPropertyName("elapsed_ms")] public long ElapsedMilliseconds { get; init; }
    [JsonPropertyName("violated")] public bool Violated { get; init; }
    [JsonPropertyName("rejected")] public bool Rejected { get; init; }
    [JsonPropertyName("proof")] public string? Proof { get; init; }
    [JsonPropertyName("proof_file")] public string? ProofFile { get; init; }
    [JsonPropertyName("n_sources")] public int SourceCount { get; init; }
    [JsonPropertyName("sources")] public IReadOnlyList<CasperSource> Sources { get; init; } = Array.Empty<CasperSource>();
    [JsonIgnore] public int ExitCode { get; init; }
    [JsonIgnore] public string StandardError { get; init; } = string.Empty;
}

public sealed record CasperSource
{
    [JsonPropertyName("n")] public int Number { get; init; }
    [JsonPropertyName("score")] public double Score { get; init; }
    [JsonPropertyName("sha256")] public string? Sha256 { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("snippet")] public string? Snippet { get; init; }
}
