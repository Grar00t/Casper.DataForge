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

public enum EngineIntegrityState
{
    Missing,
    Unpinned,
    Verified,
    Invalid
}

public sealed class CasperEngineClient
{
    public const string EnginePathEnvironmentVariable = "CASPER_DATAFORGE_ENGINE";
    public const string EngineSha256EnvironmentVariable = "CASPER_DATAFORGE_ENGINE_SHA256";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string? _configuredExecutablePath;
    private readonly string? _configuredExpectedSha256;

    public CasperEngineClient(
        TimeSpan? timeout = null,
        string? executablePath = null,
        string? expectedSha256 = null)
    {
        Timeout = timeout ?? TimeSpan.FromSeconds(30);
        if (Timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");

        string? configuredPath = FirstNonBlank(
            executablePath,
            Environment.GetEnvironmentVariable(EnginePathEnvironmentVariable));

        if (configuredPath is not null)
            _configuredExecutablePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredPath));

        string? configuredHash = FirstNonBlank(
            expectedSha256,
            Environment.GetEnvironmentVariable(EngineSha256EnvironmentVariable));

        if (configuredHash is not null)
        {
            configuredHash = configuredHash.Trim();
            if (!IsSha256(configuredHash))
                throw new ArgumentException(
                    "Expected Casper engine SHA-256 must contain exactly 64 hexadecimal characters.",
                    nameof(expectedSha256));

            _configuredExpectedSha256 = configuredHash.ToUpperInvariant();
        }
    }

    public TimeSpan Timeout { get; }

    public bool UsesConfiguredExecutable => _configuredExecutablePath is not null;

    public string ExecutablePath => _configuredExecutablePath ?? ResolveBundledExecutablePath();

    public bool IsAvailable => File.Exists(ExecutablePath);

    public EngineIntegrityState IntegrityState
    {
        get
        {
            if (!IsAvailable)
                return EngineIntegrityState.Missing;

            try
            {
                string? expectedSha256 = ResolveExpectedSha256(ExecutablePath);
                if (expectedSha256 is null)
                    return EngineIntegrityState.Unpinned;

                return VerifySha256(expectedSha256)
                    ? EngineIntegrityState.Verified
                    : EngineIntegrityState.Invalid;
            }
            catch (InvalidDataException)
            {
                return EngineIntegrityState.Invalid;
            }
            catch (IOException)
            {
                return EngineIntegrityState.Invalid;
            }
            catch (UnauthorizedAccessException)
            {
                return EngineIntegrityState.Invalid;
            }
            catch (CryptographicException)
            {
                return EngineIntegrityState.Invalid;
            }
        }
    }

    public string ComputeSha256()
    {
        if (!IsAvailable)
            throw new FileNotFoundException("Casper engine executable was not found.", ExecutablePath);

        return ComputeFileSha256(ExecutablePath);
    }

    public bool VerifySha256(string expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256) || !IsAvailable)
            return false;

        string expected = expectedSha256.Trim();
        return IsSha256(expected) &&
               string.Equals(ComputeSha256(), expected, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<CasperResponse> QueryAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty.", nameof(query));
        if (!IsAvailable)
            throw new FileNotFoundException("Casper engine executable was not found.", ExecutablePath);

        string executablePath = ExecutablePath;
        string? expectedHash = ResolveExpectedSha256(executablePath);

        if (expectedHash is not null && !VerifySha256(expectedHash))
            throw new InvalidDataException("Casper engine SHA-256 does not match the configured or bundled digest.");

        string workingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory;

        using CancellationTokenSource timeoutCts = new(Timeout);
        using CancellationTokenSource linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        ProcessStartInfo startInfo = new()
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = workingDirectory
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
        catch (OperationCanceledException)
            when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
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

        if (string.IsNullOrWhiteSpace(output))
        {
            if (exitCode != 0)
                throw new InvalidOperationException(
                    $"Casper engine exited with code {exitCode} and returned no JSON. Error={error.Trim()}");

            throw new InvalidDataException($"Casper returned no JSON. Error={error.Trim()}");
        }

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

        IReadOnlyList<CasperSource> sources = response.Sources ?? Array.Empty<CasperSource>();
        response = response with
        {
            ExitCode = exitCode,
            StandardError = error,
            Sources = sources
        };

        ValidateResponse(query, response);
        return ValidateProofFile(response, workingDirectory);
    }

    public static void ValidateResponse(string query, CasperResponse response)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty.", nameof(query));
        ArgumentNullException.ThrowIfNull(response);

        bool structuredPolicyOutcome = response.Violated || response.Rejected;
        bool acceptedExitCode =
            response.ExitCode == 0 ||
            (response.ExitCode == 1 && structuredPolicyOutcome);

        if (!acceptedExitCode)
            throw new InvalidDataException(
                $"Casper exit code {response.ExitCode} is not valid for the returned response state.");
        if (response.SourceCount < 0)
            throw new InvalidDataException("Casper returned a negative source count.");
        if (response.Sources is null)
            throw new InvalidDataException("Casper returned a null source collection.");
        if (response.SourceCount != response.Sources.Count)
            throw new InvalidDataException(
                $"Casper source count mismatch: declared {response.SourceCount}, actual {response.Sources.Count}.");
        if (double.IsNaN(response.Confidence) ||
            double.IsInfinity(response.Confidence) ||
            response.Confidence < 0.0 ||
            response.Confidence > 1.0)
            throw new InvalidDataException("Casper confidence is outside [0,1].");
        if (response.ElapsedMilliseconds < 0)
            throw new InvalidDataException("Casper returned a negative elapsed time.");
        if (!string.IsNullOrWhiteSpace(response.Query) &&
            !string.Equals(response.Query, query, StringComparison.Ordinal))
            throw new InvalidDataException("Casper echoed a query that does not match the submitted query.");
        if (!string.IsNullOrWhiteSpace(response.Proof) && !IsSha256(response.Proof))
            throw new InvalidDataException("Casper proof is not a canonical SHA-256 value.");

        var sourceNumbers = new HashSet<int>();
        for (var index = 0; index < response.Sources.Count; index++)
        {
            CasperSource source = response.Sources[index];
            if (source.Number < 0)
                throw new InvalidDataException("Casper returned a negative source number.");

            int effectiveNumber = source.Number == 0 ? index + 1 : source.Number;
            if (!sourceNumbers.Add(effectiveNumber))
                throw new InvalidDataException(
                    $"Casper returned colliding source number {effectiveNumber}.");

            if (double.IsNaN(source.Score) || double.IsInfinity(source.Score))
                throw new InvalidDataException("Casper returned a non-finite source score.");
            if (!string.IsNullOrWhiteSpace(source.Sha256) && !IsSha256(source.Sha256))
                throw new InvalidDataException("Casper returned a source SHA-256 value with an invalid format.");
        }
    }

    public static CasperResponse ValidateProofFile(
        CasperResponse response,
        string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (string.IsNullOrWhiteSpace(workingDirectory))
            throw new ArgumentException("Working directory cannot be empty.", nameof(workingDirectory));

        if (string.IsNullOrWhiteSpace(response.ProofFile))
            return response with
            {
                ProofFileDeclaredHash = null,
                ProofFileBound = false
            };

        string proofPath;
        try
        {
            string requestedPath = response.ProofFile.Trim();
            proofPath = Path.IsPathRooted(requestedPath)
                ? Path.GetFullPath(requestedPath)
                : Path.GetFullPath(Path.Combine(workingDirectory, requestedPath));
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            throw new InvalidDataException("Casper returned an invalid proof file path.", exception);
        }

        if (!File.Exists(proofPath))
            throw new InvalidDataException($"Casper proof file does not exist: {response.ProofFile}");

        string declaredHash;
        try
        {
            using var reader = new StreamReader(proofPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            string? header = reader.ReadLine();
            if (!string.Equals(header, "NIYAH-PROOF-V1", StringComparison.Ordinal))
                throw new InvalidDataException("Casper proof file has an unsupported or missing header.");

            string? hashLine = reader.ReadLine();
            const string prefix = "hash: ";
            if (hashLine is null || !hashLine.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidDataException("Casper proof file is missing its declared hash.");

            declaredHash = hashLine[prefix.Length..].Trim();
            if (!IsSha256(declaredHash))
                throw new InvalidDataException("Casper proof file declares an invalid SHA-256 value.");
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("Casper proof file could not be read.", exception);
        }

        bool bound = false;
        if (!string.IsNullOrWhiteSpace(response.Proof))
        {
            if (!string.Equals(response.Proof, declaredHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Casper proof file hash does not match the proof digest in the response.");

            bound = true;
        }

        return response with
        {
            ProofFile = proofPath,
            ProofFileDeclaredHash = declaredHash.ToUpperInvariant(),
            ProofFileBound = bound
        };
    }

    private string? ResolveExpectedSha256(string executablePath)
    {
        if (UsesConfiguredExecutable)
            return _configuredExpectedSha256;

        return ReadBundledSha256(executablePath);
    }

    private string ResolveBundledExecutablePath()
    {
        string fileName = OperatingSystem.IsWindows() ? "casper.exe" : "casper";
        string? runtimeDirectory = GetRuntimeDirectory();

        if (runtimeDirectory is not null)
        {
            string platformPath =
                Path.Combine(AppContext.BaseDirectory, "Engine", "bin", runtimeDirectory, fileName);
            if (File.Exists(platformPath))
                return platformPath;
        }

        return Path.Combine(AppContext.BaseDirectory, "Engine", "bin", fileName);
    }

    private static string ReadBundledSha256(string executablePath)
    {
        string? directory = Path.GetDirectoryName(executablePath);
        if (directory is null)
            throw new InvalidDataException("Casper bundled engine directory could not be resolved.");

        string manifestPath = Path.Combine(directory, "CASPER-EXE-MANIFEST.txt");
        if (!File.Exists(manifestPath))
            throw new InvalidDataException($"Casper bundled engine manifest is missing: {manifestPath}");

        string? fileName = null;
        string? sha256 = null;

        foreach (string line in File.ReadLines(manifestPath))
        {
            int separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();

            if (key.Equals("File", StringComparison.OrdinalIgnoreCase))
                fileName = value;
            else if (key.Equals("SHA256", StringComparison.OrdinalIgnoreCase))
                sha256 = value;
        }

        if (string.IsNullOrWhiteSpace(fileName))
            throw new InvalidDataException($"Casper manifest is missing File=: {manifestPath}");
        if (!string.Equals(fileName, Path.GetFileName(executablePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"Casper manifest file name '{fileName}' does not match '{Path.GetFileName(executablePath)}'.");
        if (string.IsNullOrWhiteSpace(sha256) || !IsSha256(sha256))
            throw new InvalidDataException($"Casper manifest contains an invalid SHA256=: {manifestPath}");

        return sha256.ToUpperInvariant();
    }

    private static string ComputeFileSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string? FirstNonBlank(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first))
            return first.Trim();
        if (!string.IsNullOrWhiteSpace(second))
            return second.Trim();
        return null;
    }

    private static bool IsSha256(string value)
    {
        if (value.Length != 64)
            return false;

        foreach (char character in value)
        {
            if (!Uri.IsHexDigit(character))
                return false;
        }

        return true;
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
    [JsonPropertyName("sources")] public IReadOnlyList<CasperSource> Sources { get; init; } =
        Array.Empty<CasperSource>();
    [JsonIgnore] public int ExitCode { get; init; }
    [JsonIgnore] public string StandardError { get; init; } = string.Empty;
    [JsonIgnore] public string? ProofFileDeclaredHash { get; init; }
    [JsonIgnore] public bool ProofFileBound { get; init; }
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
