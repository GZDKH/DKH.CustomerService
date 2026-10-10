using System.Text.Json;
using Npgsql;

namespace DKH.CustomerService.IntegrationTests.Integration.Journal;

/// <summary>Optional receipt-bound, socket-only disposable PostgreSQL 17 backend; never a supplied database URL.</summary>
internal sealed class JournalLocalPostgres : IAsyncDisposable
{
    private static readonly JsonSerializerOptions ReceiptJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private readonly Receipt _receipt;
    private readonly string _database = "dkh_journal_fixture_" + Guid.NewGuid().ToString("N");
    private bool _created;

    private JournalLocalPostgres(Receipt receipt) => _receipt = receipt;

    public static JournalLocalPostgres? FromEnvironment()
    {
        var path = Environment.GetEnvironmentVariable("DKH_JOURNAL_PG_FIXTURE_RECEIPT");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var receipt = JsonSerializer.Deserialize<Receipt>(File.ReadAllText(path), ReceiptJsonOptions);
        if (receipt is null || receipt.Scope != "dkh-customer-journal-disposable-tests" || receipt.Stage != "running"
            || !Guid.TryParse(receipt.OwnerSession, out _) || receipt.TcpListener || receipt.PostgresVersion != "17.8"
            || receipt.Port is < 1024 or > 65535 || string.IsNullOrEmpty(receipt.SystemIdentifier)
            || receipt.Username != "dkh_journal_fixture_owner" || !Path.IsPathFullyQualified(receipt.Root)
            || !Path.GetFileName(receipt.Root).StartsWith("dkh-ej02-pg17-", StringComparison.Ordinal)
            || Path.GetFullPath(receipt.DataDirectory) != Path.Combine(Path.GetFullPath(receipt.Root), "data")
            || Path.GetFullPath(receipt.SocketDirectory) != Path.Combine(Path.GetFullPath(receipt.Root), "socket"))
        {
            throw new InvalidOperationException("Local journal tests require a matching owned disposable server receipt.");
        }

        return new JournalLocalPostgres(receipt);
    }

    public async Task<string> CreateDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString("postgres"));
        await connection.OpenAsync();
        await ValidateServerAsync(connection);
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", connection);
        await command.ExecuteNonQueryAsync();
        _created = true;
        return ConnectionString(_database);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_created)
        {
            return;
        }

        using var poolConnection = new NpgsqlConnection(ConnectionString(_database));
        NpgsqlConnection.ClearPool(poolConnection);
        await using var connection = new NpgsqlConnection(ConnectionString("postgres"));
        await connection.OpenAsync();
        await ValidateServerAsync(connection);
        // The application is already disposed. EF's cached datasource can retain idle
        // sessions; FORCE is confined to this helper's freshly generated database.
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{_database}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
        _created = false;
    }

    private async Task ValidateServerAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("SELECT current_setting('data_directory'), system_identifier::text, current_setting('server_version_num') FROM pg_control_system()", connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || reader.GetString(0) != _receipt.DataDirectory
            || reader.GetString(1) != _receipt.SystemIdentifier || reader.GetString(2) != "170008")
        {
            throw new InvalidOperationException("Disposable journal server identity does not match its receipt.");
        }
    }

    private string ConnectionString(string database) => new NpgsqlConnectionStringBuilder
    {
        Host = _receipt.SocketDirectory,
        Port = _receipt.Port,
        Username = _receipt.Username,
        Database = database,
        Timeout = 5,
        IncludeErrorDetail = false,
    }.ConnectionString;

    private sealed record Receipt(string Scope, string OwnerSession, string Root, string DataDirectory,
        string SocketDirectory, int Port, string Username, string SystemIdentifier, string Stage, string PostgresVersion, bool TcpListener);
}
