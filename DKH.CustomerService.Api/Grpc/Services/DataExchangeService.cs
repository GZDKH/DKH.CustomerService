using System.Text;
using DKH.CustomerService.Api.Grpc.Helpers;
using DKH.CustomerService.Contracts.Customer.Api.DataExchange.v1;
using DKH.Platform;
using DKH.Platform.DataExchange;
using DKH.Platform.DataExchange.Export;
using DKH.Platform.DataExchange.Import;
using DKH.Platform.DataExchange.Options;
using Google.Protobuf;
using Grpc.Core;
using Enum = System.Enum;

namespace DKH.CustomerService.Api.Grpc.Services;

public class DataExchangeService(
    IPlatformImportService importService,
    IPlatformExportService exportService,
    IPlatformDataReaderFactory dataReaderFactory,
    IPlatformImportProfileStore importProfileStore)
    : Contracts.Customer.Api.DataExchange.v1.DataExchangeService.DataExchangeServiceBase
{
    private static readonly HttpClient HttpClient = new();

    public override async Task<ImportResponse> Import(ImportRequest request, ServerCallContext context)
    {
        var (profile, format) = ResolveProfile(request.Profile, request.Format);
        var tempFile = Path.GetTempFileName();

        try
        {
            if (!string.IsNullOrWhiteSpace(request.SourceUrl))
            {
                await DownloadToFileAsync(request.SourceUrl, tempFile, context.CancellationToken).ConfigureAwait(false);
            }
            else
            {
                await File.WriteAllBytesAsync(tempFile, request.Content.ToByteArray(), context.CancellationToken)
                    .ConfigureAwait(false);
            }

            await using var stream = File.OpenRead(tempFile);
            var result = await ImportAsync(profile, stream, format, context.CancellationToken).ConfigureAwait(false);

            return new ImportResponse
            {
                Processed = result.Processed,
                Failed = result.Errors.Count,
                Errors = { result.Errors.Select(e => $"{e.RowNumber}:{e.Message}") },
            };
        }
        finally
        {
            TryDelete(tempFile);
        }
    }

    public override async Task<ExportResponse> Export(ExportRequest request, ServerCallContext context)
    {
        var (profile, format) = ResolveProfile(request.Profile, request.Format);

        using var stream = new MemoryStream();
        var exportContext = context.GetHttpContext();
        AttachExportParameters(exportContext, request);

        await ExportAsync(profile, stream, format, context.CancellationToken).ConfigureAwait(false);
        stream.Position = 0;

        if (!string.IsNullOrWhiteSpace(request.DestinationUrl))
        {
            await UploadAsync(request.DestinationUrl, stream, context.CancellationToken).ConfigureAwait(false);
            return new ExportResponse();
        }

        return new ExportResponse { Content = ByteString.FromStream(stream) };
    }

    public override async Task<ImportResponse> ImportStream(
        IAsyncStreamReader<ImportChunk> requestStream,
        ServerCallContext context)
    {
        var tempFile = Path.GetTempFileName();
        string? profile = null;
        PlatformFileFormat? format = null;

        try
        {
            await using var file = File.Create(tempFile);
            while (await requestStream.MoveNext(context.CancellationToken).ConfigureAwait(false))
            {
                var chunk = requestStream.Current;
                profile ??= chunk.Profile;
                format ??= ParseFormat(chunk.Format);

                if (chunk.Content is { Length: > 0 })
                {
                    await file.WriteAsync(chunk.Content.Memory, context.CancellationToken).ConfigureAwait(false);
                }
                else if (!string.IsNullOrWhiteSpace(chunk.SourceUrl) && file.Length == 0)
                {
                    await file.FlushAsync(context.CancellationToken).ConfigureAwait(false);
                    await DownloadToFileAsync(chunk.SourceUrl, tempFile, context.CancellationToken).ConfigureAwait(false);
                }
            }

            if (string.IsNullOrWhiteSpace(profile) || format is null)
            {
                throw GrpcValidationHelper.CreateValidationException(
                    "profile",
                    "profile and format are required in the first chunk.");
            }

            await file.FlushAsync(context.CancellationToken).ConfigureAwait(false);

            await using var readStream = File.OpenRead(tempFile);
            var result = await ImportAsync(profile!, readStream, format!.Value, context.CancellationToken).ConfigureAwait(false);

            return new ImportResponse
            {
                Processed = result.Processed,
                Failed = result.Errors.Count,
                Errors = { result.Errors.Select(e => $"{e.RowNumber}:{e.Message}") },
            };
        }
        finally
        {
            TryDelete(tempFile);
        }
    }

    public override async Task ExportStream(
        ExportRequest request,
        IServerStreamWriter<ExportChunk> responseStream,
        ServerCallContext context)
    {
        var (profile, format) = ResolveProfile(request.Profile, request.Format);
        var tempFile = Path.GetTempFileName();

        try
        {
            await using (var fs = File.Create(tempFile))
            {
                AttachExportParameters(context.GetHttpContext(), request);
                await ExportAsync(profile, fs, format, context.CancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(request.DestinationUrl))
            {
                await using var uploadStream = File.OpenRead(tempFile);
                await UploadAsync(request.DestinationUrl, uploadStream, context.CancellationToken).ConfigureAwait(false);
                return;
            }

            const int bufferSize = 128 * 1024;
            await using var readStream = File.OpenRead(tempFile);
            var buffer = new byte[bufferSize];
            int read;
            while ((read = await readStream.ReadAsync(buffer.AsMemory(0, bufferSize), context.CancellationToken)
                       .ConfigureAwait(false)) > 0)
            {
                await responseStream.WriteAsync(new ExportChunk { Content = ByteString.CopyFrom(buffer, 0, read) })
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            TryDelete(tempFile);
        }
    }

    private static void AttachExportParameters(HttpContext? httpContext, ExportRequest request)
    {
        if (httpContext is null)
        {
            return;
        }

        void Set(string key, object value)
            => httpContext.Items[key] = value;

        if (!string.IsNullOrWhiteSpace(request.Language))
        {
            Set("language", request.Language);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            Set("search", request.Search);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            Set("status", request.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.OrderBy))
        {
            Set("orderBy", request.OrderBy);
        }

        if (request.Page > 0)
        {
            Set("page", request.Page);
        }

        if (request.PageSize > 0)
        {
            Set("pageSize", request.PageSize);
        }
    }

    private static async Task DownloadToFileAsync(string url, string destination, CancellationToken cancellationToken)
    {
        await using var httpStream = await HttpClient.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
        await using var fileStream = File.Create(destination);
        await httpStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // ignore cleanup errors
        }
    }

    private static async Task UploadAsync(string destinationUrl, Stream content, CancellationToken cancellationToken)
    {
        content.Position = 0;
        using var request = new HttpRequestMessage(HttpMethod.Put, destinationUrl);
        request.Content = new StreamContent(content);

        var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new PlatformException($"Upload failed: {response.StatusCode} {body}");
        }
    }

    private static (string profile, PlatformFileFormat format) ResolveProfile(string profile, string formatRaw)
    {
        var format = ParseFormat(formatRaw);
        return (profile.ToLowerInvariant(), format);
    }

    private Task<PlatformImportResult> ImportAsync(string profile, Stream source, PlatformFileFormat format, CancellationToken ct)
        => importService.ImportAsync(profile, source, format, cancellationToken: ct);

    private Task<PlatformExportResult> ExportAsync(string profile, Stream destination, PlatformFileFormat format, CancellationToken ct)
        => exportService.ExportAsync(profile, destination, format, ct);

    private static PlatformFileFormat ParseFormat(string formatRaw)
    {
        if (Enum.TryParse<PlatformFileFormat>(formatRaw, true, out var format))
        {
            return format;
        }

        throw GrpcValidationHelper.CreateValidationException("format", $"Unsupported format '{formatRaw}'");
    }

    public override async Task<ValidateImportResponse> ValidateImport(
        ValidateImportRequest request,
        ServerCallContext context)
    {
        var (profile, format) = ResolveProfile(request.Profile, request.Format);
        var importProfile = await importProfileStore.GetAsync(profile, context.CancellationToken).ConfigureAwait(false)
                            ?? throw GrpcValidationHelper.CreateValidationException("profile", $"Profile '{profile}' not found");

        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();
        var totalRecords = 0;
        var validRecords = 0;

        using var stream = new MemoryStream(request.Content.ToByteArray());
        var readerRequest = new DataReaderRequest(format);
        var options = new PlatformDataExchangeOptions();
        await using var reader = await dataReaderFactory.CreateAsync(stream, readerRequest, options);

        var requiredColumns = importProfile.Columns
            .Where(c => c.Required)
            .Select(c => c.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        await foreach (var row in reader.ReadAsync(context.CancellationToken).ConfigureAwait(false))
        {
            totalRecords++;
            var rowErrors = new List<ValidationError>();

            // Check required columns
            foreach (var required in requiredColumns)
            {
                var value = row.Values.ElementAtOrDefault(
                    row.Headers.ToList().FindIndex(h => string.Equals(h, required, StringComparison.OrdinalIgnoreCase)));

                if (string.IsNullOrWhiteSpace(value))
                {
                    rowErrors.Add(new ValidationError
                    {
                        Line = row.RowNumber,
                        Field = required,
                        Message = $"Required field '{required}' is missing or empty",
                        Value = value ?? string.Empty,
                    });
                }
            }

            if (rowErrors.Count == 0)
            {
                validRecords++;
            }

            errors.AddRange(rowErrors);
        }

        return new ValidateImportResponse
        {
            Valid = errors.Count == 0,
            TotalRecords = totalRecords,
            ValidRecords = validRecords,
            Errors = { errors },
            Warnings = { warnings },
        };
    }

    public override async Task<GetImportTemplateResponse> GetImportTemplate(
        GetImportTemplateRequest request,
        ServerCallContext context)
    {
        var (profile, format) = ResolveProfile(request.Profile, request.Format);
        var importProfile = await importProfileStore.GetAsync(profile, context.CancellationToken).ConfigureAwait(false)
                            ?? throw GrpcValidationHelper.CreateValidationException("profile", $"Profile '{profile}' not found");

        var columns = importProfile.Columns.Select(c => c.Name).ToList();
        var contentType = GetContentType(format);
        var extension = GetFileExtension(format);
        var filename = $"{profile}_template.{extension}";

        using var stream = new MemoryStream();

        if (format == PlatformFileFormat.Json)
        {
            // Generate JSON template
            var template = GenerateJsonTemplate(columns, request.IncludeExample);
            var bytes = Encoding.UTF8.GetBytes(template);
            await stream.WriteAsync(bytes, context.CancellationToken).ConfigureAwait(false);
        }
        else
        {
            // For CSV/Excel, use export service with empty data
            // Just write headers
            var header = string.Join(",", columns.Select(c => $"\"{c}\""));
            var content = request.IncludeExample
                ? $"{header}\n{string.Join(",", columns.Select(_ => "\"\""))}"
                : header;
            var bytes = Encoding.UTF8.GetBytes(content);
            await stream.WriteAsync(bytes, context.CancellationToken).ConfigureAwait(false);
        }

        stream.Position = 0;

        return new GetImportTemplateResponse
        {
            Content = await ByteString.FromStreamAsync(stream, context.CancellationToken).ConfigureAwait(false),
            ContentType = contentType,
            Filename = filename,
        };
    }

    private static string GenerateJsonTemplate(List<string> columns, bool includeExample)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"items\": [");

        if (includeExample)
        {
            sb.AppendLine("    {");
            for (var i = 0; i < columns.Count; i++)
            {
                var comma = i < columns.Count - 1 ? "," : "";
                sb.Append("      \"").Append(columns[i]).Append("\": \"\"").Append(comma).AppendLine();
            }

            sb.AppendLine("    }");
        }

        sb.AppendLine("  ]");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GetContentType(PlatformFileFormat format) => format switch
    {
        PlatformFileFormat.Json => "application/json",
        PlatformFileFormat.Csv => "text/csv",
        PlatformFileFormat.Excel => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        PlatformFileFormat.Xml => "application/xml",
        _ => "application/octet-stream",
    };

    private static string GetFileExtension(PlatformFileFormat format) => format switch
    {
        PlatformFileFormat.Json => "json",
        PlatformFileFormat.Csv => "csv",
        PlatformFileFormat.Excel => "xlsx",
        PlatformFileFormat.Xml => "xml",
        _ => "bin",
    };
}
