using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Casper.DataForge.CrossPlatform.Engine;

public sealed class CasperEngineClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string ExecutablePath
    {
        get
        {
            string? runtimeDirectory = GetRuntimeDirectory();

            string fileName = OperatingSystem.IsWindows()
                ? "casper.exe"
                : "casper";

            if (runtimeDirectory is not null)
            {
                string platformPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "Engine",
                    "bin",
                    runtimeDirectory,
                    fileName);

                if (File.Exists(platformPath))
                    return platformPath;
            }

            return Path.Combine(
                AppContext.BaseDirectory,
                "Engine",
                "bin",
                OperatingSystem.IsWindows() ? "casper.exe" : "casper");
        }
    }

    public bool IsAvailable => File.Exists(ExecutablePath);

    public async Task<CasperResponse> QueryAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty.", nameof(query));

        if (!IsAvailable)
            throw new FileNotFoundException(
                "Casper engine executable was not found.",
                ExecutablePath);

        cancellationToken.ThrowIfCancellationRequested();
        string executablePath = ExecutablePath;

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!
        };

        startInfo.ArgumentList.Add(query);

        using var process = new Process
        {
            StartInfo = startInfo
        };

        if (!process.Start())
            throw new InvalidOperationException("Casper engine did not start.");

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Terminate(process);
            await process.WaitForExitAsync();
            throw;
        }

        string output = await outputTask;
        string error = await errorTask;

        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                $"Casper returned no JSON. ExitCode={process.ExitCode}. Error={error}");
        }

        CasperResponse? response;

        try
        {
            response = JsonSerializer.Deserialize<CasperResponse>(
                output,
                JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Casper returned invalid JSON. ExitCode={process.ExitCode}. Raw={output}",
                exception);
        }

        if (response is null)
            throw new InvalidDataException("Casper returned an empty JSON value.");

        return response with
        {
            ExitCode = process.ExitCode,
            StandardError = error,
            Sources = response.Sources ?? Array.Empty<CasperSource>()
        };
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
            // The process exited between cancellation and termination.
        }
    }
}

public sealed record CasperResponse
{
    [JsonPropertyName("query")]
    public string? Query { get; init; }

    [JsonPropertyName("answer")]
    public string? Answer { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }

    [JsonPropertyName("elapsed_ms")]
    public long ElapsedMilliseconds { get; init; }

    [JsonPropertyName("violated")]
    public bool Violated { get; init; }

    [JsonPropertyName("rejected")]
    public bool Rejected { get; init; }

    [JsonPropertyName("proof")]
    public string? Proof { get; init; }

    [JsonPropertyName("proof_file")]
    public string? ProofFile { get; init; }

    [JsonPropertyName("n_sources")]
    public int SourceCount { get; init; }

    [JsonPropertyName("sources")]
    public IReadOnlyList<CasperSource> Sources { get; init; } =
        Array.Empty<CasperSource>();

    [JsonIgnore]
    public int ExitCode { get; init; }

    [JsonIgnore]
    public string StandardError { get; init; } = string.Empty;
}

public sealed record CasperSource
{
    [JsonPropertyName("n")]
    public int Number { get; init; }

    [JsonPropertyName("score")]
    public double Score { get; init; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("snippet")]
    public string? Snippet { get; init; }
}
