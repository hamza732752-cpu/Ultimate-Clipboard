using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using UltimateClipboard.Models;

namespace UltimateClipboard.Data;

/// <summary>
/// SQLite-based persistence for clipboard history, snippets, and settings.
/// All public methods are async and safe to call from the UI thread.
/// </summary>
public class ClipboardDatabase : IDisposable
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    public ClipboardDatabase(string databasePath)
    {
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        InitializeDatabase();
    }

    /// <summary>
    /// Creates a database instance using an existing open connection (for testing with in-memory databases).
    /// </summary>
    public ClipboardDatabase(SqliteConnection sharedConnection)
    {
        _connectionString = sharedConnection.ConnectionString;
        InitializeWithConnection(sharedConnection);
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        InitializeWithConnection(connection);
    }

    private void InitializeWithConnection(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Entries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ContentHash TEXT NOT NULL,
                ContentType INTEGER NOT NULL DEFAULT 0,
                PlainText TEXT,
                RichTextData TEXT,
                HtmlData TEXT,
                ImageData BLOB,
                ThumbnailData BLOB,
                FilePaths TEXT,
                SourceApplication TEXT,
                CapturedAt TEXT NOT NULL,
                IsPinned INTEGER NOT NULL DEFAULT 0,
                IsFavorite INTEGER NOT NULL DEFAULT 0,
                Tags TEXT,
                ContentSizeBytes INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS IX_Entries_CapturedAt ON Entries(CapturedAt DESC);
            CREATE INDEX IF NOT EXISTS IX_Entries_ContentHash ON Entries(ContentHash);
            CREATE INDEX IF NOT EXISTS IX_Entries_ContentType ON Entries(ContentType);
            CREATE INDEX IF NOT EXISTS IX_Entries_IsPinned ON Entries(IsPinned);
            CREATE INDEX IF NOT EXISTS IX_Entries_IsFavorite ON Entries(IsFavorite);

            CREATE TABLE IF NOT EXISTS Snippets (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Content TEXT NOT NULL,
                Collection TEXT,
                Tags TEXT,
                CreatedAt TEXT NOT NULL,
                ModifiedAt TEXT NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0
            );

            CREATE VIRTUAL TABLE IF NOT EXISTS EntriesFts USING fts5(
                PlainText,
                content='Entries',
                content_rowid='Id'
            );
        ";
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    // ==================== Entries ====================

    /// <summary>
    /// Inserts a new clipboard entry. If a duplicate hash exists, moves the existing entry to the top instead.
    /// Returns the entry ID.
    /// </summary>
    public async Task<long> InsertEntryAsync(ClipboardEntry entry)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = CreateConnection();

            // Check for duplicate by hash
            if (!string.IsNullOrEmpty(entry.ContentHash))
            {
                using var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT Id FROM Entries WHERE ContentHash = @hash LIMIT 1";
                checkCmd.Parameters.AddWithValue("@hash", entry.ContentHash);
                var existingId = await checkCmd.ExecuteScalarAsync();

                if (existingId is not null)
                {
                    // Move existing entry to top by updating timestamp
                    long id = (long)existingId;
                    using var updateCmd = connection.CreateCommand();
                    updateCmd.CommandText = "UPDATE Entries SET CapturedAt = @capturedAt, SourceApplication = @source WHERE Id = @id";
                    updateCmd.Parameters.AddWithValue("@capturedAt", entry.CapturedAt.ToString("O"));
                    updateCmd.Parameters.AddWithValue("@source", (object?)entry.SourceApplication ?? DBNull.Value);
                    updateCmd.Parameters.AddWithValue("@id", id);
                    await updateCmd.ExecuteNonQueryAsync();
                    return id;
                }
            }

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Entries (ContentHash, ContentType, PlainText, RichTextData, HtmlData, ImageData, ThumbnailData, FilePaths, SourceApplication, CapturedAt, IsPinned, IsFavorite, Tags, ContentSizeBytes)
                VALUES (@hash, @type, @text, @rtf, @html, @image, @thumb, @files, @source, @captured, @pinned, @fav, @tags, @size);
                SELECT last_insert_rowid();";

            cmd.Parameters.AddWithValue("@hash", entry.ContentHash);
            cmd.Parameters.AddWithValue("@type", (int)entry.ContentType);
            cmd.Parameters.AddWithValue("@text", (object?)entry.PlainText ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@rtf", (object?)entry.RichTextData ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@html", (object?)entry.HtmlData ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@image", (object?)entry.ImageData ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@thumb", (object?)entry.ThumbnailData ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@files", (object?)entry.FilePaths ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@source", (object?)entry.SourceApplication ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@captured", entry.CapturedAt.ToString("O"));
            cmd.Parameters.AddWithValue("@pinned", entry.IsPinned ? 1 : 0);
            cmd.Parameters.AddWithValue("@fav", entry.IsFavorite ? 1 : 0);
            cmd.Parameters.AddWithValue("@tags", (object?)entry.Tags ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@size", entry.ContentSizeBytes);

            var result = await cmd.ExecuteScalarAsync();
            long newId = result is not null ? (long)result : 0;

            // Update FTS index
            if (!string.IsNullOrEmpty(entry.PlainText))
            {
                using var ftsCmd = connection.CreateCommand();
                ftsCmd.CommandText = "INSERT INTO EntriesFts(rowid, PlainText) VALUES (@id, @text)";
                ftsCmd.Parameters.AddWithValue("@id", newId);
                ftsCmd.Parameters.AddWithValue("@text", entry.PlainText);
                await ftsCmd.ExecuteNonQueryAsync();
            }

            return newId;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets recent clipboard entries, newest first.
    /// </summary>
    public async Task<List<ClipboardEntry>> GetRecentEntriesAsync(int limit = 100, int offset = 0)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, ContentHash, ContentType, PlainText, RichTextData, HtmlData, ImageData, ThumbnailData, FilePaths, SourceApplication, CapturedAt, IsPinned, IsFavorite, Tags, ContentSizeBytes
                FROM Entries
                ORDER BY IsPinned DESC, CapturedAt DESC
                LIMIT @limit OFFSET @offset";
            cmd.Parameters.AddWithValue("@limit", limit);
            cmd.Parameters.AddWithValue("@offset", offset);

            return await ReadEntriesAsync(cmd);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets a single entry by ID with complete data (including full image blob).
    /// </summary>
    public async Task<ClipboardEntry?> GetEntryByIdAsync(long id)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, ContentHash, ContentType, PlainText, RichTextData, HtmlData, ImageData, ThumbnailData, FilePaths, SourceApplication, CapturedAt, IsPinned, IsFavorite, Tags, ContentSizeBytes
                FROM Entries
                WHERE Id = @id LIMIT 1";
            cmd.Parameters.AddWithValue("@id", id);

            var list = await ReadEntriesAsync(cmd);
            return list.Count > 0 ? list[0] : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Searches clipboard entries using full-text search for text content, or filters by type.
    /// </summary>
    public async Task<List<ClipboardEntry>> SearchEntriesAsync(string? query = null, ClipboardContentType? typeFilter = null, bool? pinnedOnly = null, bool? favoritesOnly = null, int limit = 100)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = CreateConnection();
            using var cmd = connection.CreateCommand();

            var conditions = new List<string>();
            
            if (!string.IsNullOrWhiteSpace(query))
            {
                // Search across text, file paths, and image recognition tags
                conditions.Add("(e.PlainText LIKE @query OR e.Tags LIKE @query OR e.FilePaths LIKE @query)");
                cmd.Parameters.AddWithValue("@query", $"%{query}%");
            }

            if (typeFilter.HasValue)
            {
                conditions.Add("e.ContentType = @type");
                cmd.Parameters.AddWithValue("@type", (int)typeFilter.Value);
            }

            if (pinnedOnly == true)
            {
                conditions.Add("e.IsPinned = 1");
            }

            if (favoritesOnly == true)
            {
                conditions.Add("e.IsFavorite = 1");
            }

            string where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

            cmd.CommandText = $@"
                SELECT e.Id, e.ContentHash, e.ContentType, e.PlainText, e.RichTextData, e.HtmlData, e.ImageData, e.ThumbnailData, e.FilePaths, e.SourceApplication, e.CapturedAt, e.IsPinned, e.IsFavorite, e.Tags, e.ContentSizeBytes
                FROM Entries e
                {where}
                ORDER BY e.IsPinned DESC, e.CapturedAt DESC
                LIMIT @limit";
            cmd.Parameters.AddWithValue("@limit", limit);

            return await ReadEntriesAsync(cmd);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Toggles the pinned status of an entry.
    /// </summary>
    public async Task TogglePinAsync(long id)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Entries SET IsPinned = CASE WHEN IsPinned = 1 THEN 0 ELSE 1 END WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Toggles the favorite status of an entry.
    /// </summary>
    public async Task ToggleFavoriteAsync(long id)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Entries SET IsFavorite = CASE WHEN IsFavorite = 1 THEN 0 ELSE 1 END WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Deletes a single entry by ID.
    /// </summary>
    public async Task DeleteEntryAsync(long id)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = CreateConnection();

            // Remove from FTS
            using var ftsCmd = connection.CreateCommand();
            ftsCmd.CommandText = "INSERT INTO EntriesFts(EntriesFts, rowid, PlainText) VALUES('delete', @id, (SELECT PlainText FROM Entries WHERE Id = @id2))";
            ftsCmd.Parameters.AddWithValue("@id", id);
            ftsCmd.Parameters.AddWithValue("@id2", id);
            try { await ftsCmd.ExecuteNonQueryAsync(); } catch { /* FTS entry may not exist */ }

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Entries WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Clears all non-pinned entries.
    /// </summary>
    public async Task ClearHistoryAsync()
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = CreateConnection();

            using var ftsCmd = connection.CreateCommand();
            ftsCmd.CommandText = "DELETE FROM EntriesFts WHERE rowid IN (SELECT Id FROM Entries WHERE IsPinned = 0)";
            try { await ftsCmd.ExecuteNonQueryAsync(); } catch { }

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Entries WHERE IsPinned = 0";
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Enforces the maximum number of entries, deleting oldest non-pinned entries beyond the limit.
    /// </summary>
    public async Task EnforceHistoryLimitAsync(int maxEntries)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM Entries
                WHERE IsPinned = 0 AND Id NOT IN (
                    SELECT Id FROM Entries ORDER BY IsPinned DESC, CapturedAt DESC LIMIT @max
                )";
            cmd.Parameters.AddWithValue("@max", maxEntries);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Deletes non-pinned entries older than the specified number of days.
    /// </summary>
    public async Task EnforceRetentionAsync(int retentionDays)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = CreateConnection();
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays).ToString("O");

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Entries WHERE IsPinned = 0 AND CapturedAt < @cutoff";
            cmd.Parameters.AddWithValue("@cutoff", cutoff);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Checks if a content hash already exists in the database.
    /// </summary>
    public async Task<bool> DuplicateExistsAsync(string contentHash)
    {
        if (string.IsNullOrEmpty(contentHash))
            return false;

        await _lock.WaitAsync();
        try
        {
            using var connection = CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Entries WHERE ContentHash = @hash";
            cmd.Parameters.AddWithValue("@hash", contentHash);
            var result = await cmd.ExecuteScalarAsync();
            return result is long count && count > 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns the total number of entries.
    /// </summary>
    public async Task<long> GetEntryCountAsync()
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Entries";
            var result = await cmd.ExecuteScalarAsync();
            return result is long count ? count : 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    // ==================== Snippets ====================

    public async Task<long> InsertSnippetAsync(Snippet snippet)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Snippets (Title, Content, Collection, Tags, CreatedAt, ModifiedAt, SortOrder)
                VALUES (@title, @content, @collection, @tags, @created, @modified, @sort);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@title", snippet.Title);
            cmd.Parameters.AddWithValue("@content", snippet.Content);
            cmd.Parameters.AddWithValue("@collection", (object?)snippet.Collection ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tags", (object?)snippet.Tags ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@created", snippet.CreatedAt.ToString("O"));
            cmd.Parameters.AddWithValue("@modified", snippet.ModifiedAt.ToString("O"));
            cmd.Parameters.AddWithValue("@sort", snippet.SortOrder);

            var result = await cmd.ExecuteScalarAsync();
            return result is not null ? (long)result : 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateSnippetAsync(Snippet snippet)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE Snippets SET Title = @title, Content = @content, Collection = @collection,
                Tags = @tags, ModifiedAt = @modified, SortOrder = @sort WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", snippet.Id);
            cmd.Parameters.AddWithValue("@title", snippet.Title);
            cmd.Parameters.AddWithValue("@content", snippet.Content);
            cmd.Parameters.AddWithValue("@collection", (object?)snippet.Collection ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tags", (object?)snippet.Tags ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@modified", snippet.ModifiedAt.ToString("O"));
            cmd.Parameters.AddWithValue("@sort", snippet.SortOrder);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteSnippetAsync(long id)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Snippets WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<Snippet>> GetSnippetsAsync(string? collection = null)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = CreateConnection();
            using var cmd = connection.CreateCommand();

            if (collection is not null)
            {
                cmd.CommandText = "SELECT Id, Title, Content, Collection, Tags, CreatedAt, ModifiedAt, SortOrder FROM Snippets WHERE Collection = @col ORDER BY SortOrder, Title";
                cmd.Parameters.AddWithValue("@col", collection);
            }
            else
            {
                cmd.CommandText = "SELECT Id, Title, Content, Collection, Tags, CreatedAt, ModifiedAt, SortOrder FROM Snippets ORDER BY SortOrder, Title";
            }

            var snippets = new List<Snippet>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                snippets.Add(new Snippet
                {
                    Id = reader.GetInt64(0),
                    Title = reader.GetString(1),
                    Content = reader.GetString(2),
                    Collection = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Tags = reader.IsDBNull(4) ? null : reader.GetString(4),
                    CreatedAt = DateTime.Parse(reader.GetString(5)),
                    ModifiedAt = DateTime.Parse(reader.GetString(6)),
                    SortOrder = reader.GetInt32(7)
                });
            }
            return snippets;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<Snippet>> SearchSnippetsAsync(string query)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Title, Content, Collection, Tags, CreatedAt, ModifiedAt, SortOrder
                FROM Snippets
                WHERE Title LIKE @query OR Content LIKE @query
                ORDER BY SortOrder, Title";
            cmd.Parameters.AddWithValue("@query", $"%{query}%");

            var snippets = new List<Snippet>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                snippets.Add(new Snippet
                {
                    Id = reader.GetInt64(0),
                    Title = reader.GetString(1),
                    Content = reader.GetString(2),
                    Collection = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Tags = reader.IsDBNull(4) ? null : reader.GetString(4),
                    CreatedAt = DateTime.Parse(reader.GetString(5)),
                    ModifiedAt = DateTime.Parse(reader.GetString(6)),
                    SortOrder = reader.GetInt32(7)
                });
            }
            return snippets;
        }
        finally
        {
            _lock.Release();
        }
    }

    // ==================== Private Helpers ====================

    private static async Task<List<ClipboardEntry>> ReadEntriesAsync(SqliteCommand cmd)
    {
        var entries = new List<ClipboardEntry>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new ClipboardEntry
            {
                Id = reader.GetInt64(0),
                ContentHash = reader.GetString(1),
                ContentType = (ClipboardContentType)reader.GetInt32(2),
                PlainText = reader.IsDBNull(3) ? null : reader.GetString(3),
                RichTextData = reader.IsDBNull(4) ? null : reader.GetString(4),
                HtmlData = reader.IsDBNull(5) ? null : reader.GetString(5),
                ImageData = reader.IsDBNull(6) ? null : (byte[])reader[6],
                ThumbnailData = reader.IsDBNull(7) ? null : (byte[])reader[7],
                FilePaths = reader.IsDBNull(8) ? null : reader.GetString(8),
                SourceApplication = reader.IsDBNull(9) ? null : reader.GetString(9),
                CapturedAt = DateTime.Parse(reader.GetString(10)),
                IsPinned = reader.GetInt32(11) == 1,
                IsFavorite = reader.GetInt32(12) == 1,
                Tags = reader.IsDBNull(13) ? null : reader.GetString(13),
                ContentSizeBytes = reader.GetInt64(14)
            });
        }
        return entries;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}
