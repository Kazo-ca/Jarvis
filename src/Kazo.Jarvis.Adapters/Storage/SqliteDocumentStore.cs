using System.Text.Json;
using Kazo.Jarvis.Adapters.Files;
using Kazo.Jarvis.Kernel.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kazo.Jarvis.Adapters.Storage;

/// <summary>
/// The document store in SQLite, <c>data\jarvis.db</c> (ADR 0004, section 3). The schema version is the
/// database's <c>user_version</c>; an older database is copied to <c>backups\</c> before it is migrated, a newer
/// one is left untouched and the store refuses to work (<see cref="StorageUnavailableException"/>).
/// Deleted content is overwritten (<c>secure_delete</c>), so an erased conversation does not stay in the file.
/// </summary>
public sealed class SqliteDocumentStore : IDocumentStore
{
    /// <summary>
    /// Schema steps: step <c>n</c> takes the database from version <c>n</c> to <c>n + 1</c>. Never edit a
    /// published step; add one.
    /// </summary>
    internal static readonly IReadOnlyList<string> SchemaSteps =
    [
        """
        CREATE TABLE documents (
            seq        INTEGER PRIMARY KEY AUTOINCREMENT,
            id         TEXT    NOT NULL UNIQUE,
            collection TEXT    NOT NULL,
            at         INTEGER NOT NULL,
            content    TEXT    NOT NULL
        );
        CREATE INDEX documents_by_period ON documents (collection, at, seq);
        """,
    ];

    private readonly string databaseFile;
    private readonly string backups;
    private readonly IReadOnlyList<string> steps;
    private readonly TimeProvider time;
    private readonly ILogger logger;
    private readonly string connectionString;
    private readonly SemaphoreSlim opening = new(1, 1);
    private Exception? unavailable;
    private bool ready;

    /// <summary>Creates the store; the database is opened, created or migrated on first use.</summary>
    /// <param name="paths">Where the files are.</param>
    /// <param name="time">Clock: time of the documents added without one, names of the backup copies.</param>
    /// <param name="logger">Log; it never receives a document's content.</param>
    public SqliteDocumentStore(JarvisPaths paths, TimeProvider time, ILogger<SqliteDocumentStore>? logger = null)
        : this(paths?.DatabaseFile!, paths?.Backups!, time, logger, SchemaSteps)
    {
    }

    /// <summary>Same, with other schema steps: lets the tests run a migration.</summary>
    internal SqliteDocumentStore(string databaseFile, string backups, TimeProvider time, ILogger? logger, IReadOnlyList<string> steps)
    {
        ArgumentNullException.ThrowIfNull(databaseFile);
        ArgumentNullException.ThrowIfNull(backups);
        ArgumentNullException.ThrowIfNull(time);

        this.databaseFile = databaseFile;
        this.backups = backups;
        this.time = time;
        this.steps = steps;
        this.logger = logger ?? NullLogger.Instance;

        // No pooling: the file is released after each operation, so it can be copied or deleted at any time.
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databaseFile,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = 30,
        }.ToString();
    }

    /// <inheritdoc />
    public IDocumentCollection<T> Collection<T>(string name)
        where T : class
    {
        DocumentCollections.EnsureValidName(name);
        return new SqliteCollection<T>(this, name);
    }

    /// <inheritdoc />
    public async ValueTask EraseAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, "DELETE FROM documents;", CancellationToken.None);

        // Shrinks the file and empties the write-ahead log, which could still hold the erased pages.
        await ExecuteAsync(connection, "VACUUM; PRAGMA wal_checkpoint(TRUNCATE);", CancellationToken.None);
        logger.LogInformation("Every document was erased.");
    }

    private async ValueTask<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);
        return await ConnectAsync(cancellationToken);
    }

    private async ValueTask<SqliteConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await ExecuteAsync(connection, "PRAGMA secure_delete = ON; PRAGMA foreign_keys = ON;", cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private async ValueTask EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref ready))
        {
            return;
        }

        await opening.WaitAsync(cancellationToken);
        try
        {
            if (unavailable is not null)
            {
                throw new StorageUnavailableException(unavailable.Message, unavailable);
            }

            if (!ready)
            {
                await PrepareAsync(cancellationToken);
                Volatile.Write(ref ready, true);
            }
        }
        finally
        {
            opening.Release();
        }
    }

    private async ValueTask PrepareAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databaseFile))!);
        await using var connection = await ConnectAsync(cancellationToken);
        var version = Convert.ToInt32(await ScalarAsync(connection, "PRAGMA user_version;", cancellationToken));

        if (version > steps.Count)
        {
            Fail($"The database was written by a newer version (schema {version}, this one knows {steps.Count}). It is left untouched.");
        }

        if (version > 0 && version < steps.Count)
        {
            var copy = AtomicFile.BackupPath(databaseFile, backups, $"v{version}", time.GetUtcNow());
            Directory.CreateDirectory(backups);
            await using (var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = copy, Pooling = false }.ToString()))
            {
                await backup.OpenAsync(cancellationToken);
                connection.BackupDatabase(backup);
            }

            logger.LogInformation("Database copied to the backups folder before its migration from schema {Version}.", version);
        }

        for (var step = version; step < steps.Count; step++)
        {
            try
            {
                await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
                await ExecuteAsync(connection, steps[step], cancellationToken, transaction);
                await ExecuteAsync(connection, $"PRAGMA user_version = {step + 1};", cancellationToken, transaction);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (SqliteException exception)
            {
                Fail($"Migrating the database from schema {step} failed. It is left at that version.", exception);
            }
        }

        await ExecuteAsync(connection, "PRAGMA journal_mode = WAL;", cancellationToken);
    }

    private void Fail(string message, Exception? cause = null)
    {
        var exception = cause is null ? new StorageUnavailableException(message) : new StorageUnavailableException(message, cause);
        unavailable = exception;
        logger.LogError(cause, "{Problem}", message);
        throw exception;
    }

    private static async ValueTask ExecuteAsync(
        SqliteConnection connection, string sql, CancellationToken cancellationToken, SqliteTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async ValueTask<object?> ScalarAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    /// <summary>One collection: every query filters on its name.</summary>
    private sealed class SqliteCollection<T>(SqliteDocumentStore store, string name) : IDocumentCollection<T>
        where T : class
    {
        public string Name => name;

        public async ValueTask<StoredDocument<T>> AddAsync(T document, DateTimeOffset? at = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(document);

            // Stored to the millisecond: the returned document says exactly what a later read gives.
            var milliseconds = (at ?? store.time.GetUtcNow()).ToUnixTimeMilliseconds();
            var stored = new StoredDocument<T>(
                DocumentId.New(DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)),
                DateTimeOffset.FromUnixTimeMilliseconds(milliseconds),
                document);

            await using var connection = await store.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO documents (id, collection, at, content) VALUES ($id, $collection, $at, $content);";
            command.Parameters.AddWithValue("$id", stored.Id.ToString());
            command.Parameters.AddWithValue("$collection", name);
            command.Parameters.AddWithValue("$at", milliseconds);
            command.Parameters.AddWithValue("$content", JsonSerializer.Serialize(document, JarvisJson.Compact));
            await command.ExecuteNonQueryAsync(cancellationToken);
            return stored;
        }

        public async ValueTask<StoredDocument<T>?> GetAsync(DocumentId id, CancellationToken cancellationToken = default)
        {
            var found = await QueryAsync(
                "SELECT id, at, content FROM documents WHERE collection = $collection AND id = $id;",
                parameters => parameters.AddWithValue("$id", id.ToString()),
                cancellationToken);
            return found.Count == 0 ? null : found[0];
        }

        public async ValueTask<IReadOnlyList<StoredDocument<T>>> ReadPeriodAsync(
            DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
            await QueryAsync(
                "SELECT id, at, content FROM documents WHERE collection = $collection AND at >= $from AND at < $to ORDER BY at, seq;",
                parameters =>
                {
                    parameters.AddWithValue("$from", from.ToUnixTimeMilliseconds());
                    parameters.AddWithValue("$to", to.ToUnixTimeMilliseconds());
                },
                cancellationToken);

        public async ValueTask<bool> DeleteAsync(DocumentId id, CancellationToken cancellationToken = default) =>
            await ChangeAsync(
                "DELETE FROM documents WHERE collection = $collection AND id = $id;",
                parameters => parameters.AddWithValue("$id", id.ToString()),
                cancellationToken) > 0;

        public async ValueTask<int> ClearAsync(CancellationToken cancellationToken = default) =>
            await ChangeAsync("DELETE FROM documents WHERE collection = $collection;", _ => { }, cancellationToken);

        private async ValueTask<int> ChangeAsync(string sql, Action<SqliteParameterCollection> bind, CancellationToken cancellationToken)
        {
            await using var connection = await store.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$collection", name);
            bind(command.Parameters);
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async ValueTask<List<StoredDocument<T>>> QueryAsync(
            string sql, Action<SqliteParameterCollection> bind, CancellationToken cancellationToken)
        {
            await using var connection = await store.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$collection", name);
            bind(command.Parameters);

            var documents = new List<StoredDocument<T>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var content = JsonSerializer.Deserialize<T>(reader.GetString(2), JarvisJson.Compact)
                    ?? throw new JsonException($"A document of collection {name} is empty.");
                documents.Add(new StoredDocument<T>(
                    new DocumentId(Guid.Parse(reader.GetString(0))),
                    DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1)),
                    content));
            }

            return documents;
        }
    }
}
