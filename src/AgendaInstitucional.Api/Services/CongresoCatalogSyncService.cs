using AgendaInstitucional.Infrastructure.Data;
using AgendaInstitucional.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgendaInstitucional.Api.Services;

public sealed class CongresoCatalogSyncService
{
    private const int DefaultActiveVariableId = 40;
    private static readonly string[] ComisionNameCandidates = ["comision", "nombre", "nombre_comision", "descripcion", "title"];
    private static readonly string[] ComisionStatusCandidates = ["variable_id"];
    private static readonly string[] DiputadoNameCandidates = ["diputado", "nombre", "nombre_completo", "nombre_diputado", "full_name"];
    private static readonly string[] DiputadoActualCandidates = ["actual"];
    private static readonly string[] DiputadoVariableIdCandidates = ["variable_id"];
    private static readonly string[] RelComisionIdCandidates = ["comision_id", "id_comision", "comision"];
    private static readonly string[] RelDiputadoIdCandidates = ["diputado_id", "id_diputado", "diputado"];
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public CongresoCatalogSyncService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<CongresoCatalogSyncResponse> SyncAsync(int? activeVariableId, CancellationToken cancellationToken)
    {
        var currentVariableId = NormalizeActiveVariableId(activeVariableId);

        var connectionString = _configuration.GetConnectionString("GacetaReadOnlyConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("No se encontro la cadena de conexion 'GacetaReadOnlyConnection'.");
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var comisiones = await ReadComisionesAsync(connection, currentVariableId, cancellationToken);
        var diputados = await ReadDiputadosAsync(connection, currentVariableId, cancellationToken);
        var relaciones = await ReadComisionesDiputadosAsync(connection, cancellationToken);

        var diputadoIds = diputados.Select(x => x.Id).ToHashSet();
        var comisionIds = comisiones.Select(x => x.Id).ToHashSet();
        relaciones = relaciones
            .Where(x => comisionIds.Contains(x.ComisionId) && diputadoIds.Contains(x.DiputadoId))
            .DistinctBy(x => new { x.ComisionId, x.DiputadoId })
            .ToList();

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var localComisiones = await _context.catComisiones.ToDictionaryAsync(x => x.id, cancellationToken);
        foreach (var comision in comisiones)
        {
            if (localComisiones.TryGetValue(comision.Id, out var existingComision))
            {
                existingComision.comision = comision.Nombre;
                existingComision.estatus = comision.Estatus;
            }
            else
            {
                await InsertComisionAsync(comision, cancellationToken);
            }
        }

        foreach (var extraComision in localComisiones.Values.Where(x => !comisionIds.Contains(x.id)))
        {
            extraComision.estatus = false;
        }

        var relacionesLocales = await _context.ComisionesDiputados.ToListAsync(cancellationToken);
        var diputadosLocales = await _context.Diputados.ToListAsync(cancellationToken);
        _context.ComisionesDiputados.RemoveRange(relacionesLocales);
        _context.Diputados.RemoveRange(diputadosLocales);
        await _context.SaveChangesAsync(cancellationToken);

        _context.Diputados.AddRange(diputados.Select(x => new Diputado
        {
            Id = x.Id,
            Nombre = x.Nombre,
            Estatus = x.Estatus,
            Actual = x.Actual
        }));

        _context.ComisionesDiputados.AddRange(relaciones.Select(x => new ComisionDiputado
        {
            ComisionId = x.ComisionId,
            DiputadoId = x.DiputadoId
        }));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CongresoCatalogSyncResponse
        {
            ComisionesSincronizadas = comisiones.Count,
            DiputadosSincronizados = diputados.Count,
            RelacionesSincronizadas = relaciones.Count,
            ActiveVariableId = currentVariableId,
            SincronizadoEn = DateTimeOffset.UtcNow
        };
    }

    public async Task<CongresoCatalogDiffResponse> GetDiffAsync(int? activeVariableId, CancellationToken cancellationToken)
    {
        var currentVariableId = NormalizeActiveVariableId(activeVariableId);

        var connectionString = _configuration.GetConnectionString("GacetaReadOnlyConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("No se encontro la cadena de conexion 'GacetaReadOnlyConnection'.");
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var remoteComisiones = await ReadComisionesAsync(connection, currentVariableId, cancellationToken);
        var remoteDiputados = await ReadDiputadosAsync(connection, currentVariableId, cancellationToken);
        var remoteRelaciones = await ReadComisionesDiputadosAsync(connection, cancellationToken);

        var localComisiones = await _context.catComisiones
            .AsNoTracking()
            .Select(x => new LocalComisionRow
            {
                Id = x.id,
                Nombre = x.comision,
                Estatus = x.estatus ?? false
            })
            .ToListAsync(cancellationToken);

        var localDiputados = await _context.Diputados
            .AsNoTracking()
            .Select(x => new LocalDiputadoRow
            {
                Id = x.Id,
                Nombre = x.Nombre,
                Actual = x.Actual
            })
            .ToListAsync(cancellationToken);

        var localRelaciones = await _context.ComisionesDiputados
            .AsNoTracking()
            .Select(x => new LocalComisionDiputadoRow
            {
                ComisionId = x.ComisionId,
                DiputadoId = x.DiputadoId
            })
            .ToListAsync(cancellationToken);

        var comisionesDiff = BuildComisionesDiff(localComisiones, remoteComisiones);
        var diputadosDiff = BuildDiputadosDiff(localDiputados, remoteDiputados);
        var relacionesDiff = BuildRelacionesDiff(localRelaciones, remoteRelaciones);

        var hasDifferences = comisionesDiff.HasDifferences || diputadosDiff.HasDifferences || relacionesDiff.HasDifferences;

        return new CongresoCatalogDiffResponse
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            HasDifferences = hasDifferences,
            SuggestSync = hasDifferences,
            Comisiones = comisionesDiff,
            Diputados = diputadosDiff,
            ComisionesDiputados = relacionesDiff
        };
    }

    private async Task InsertComisionAsync(CongresoComisionRow comision, CancellationToken cancellationToken)
    {
        var isIdentity = await IsIdentityColumnAsync("catComisiones", "id", cancellationToken);
        if (isIdentity)
        {
            await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.catComisiones ON;", cancellationToken);
        }

        try
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO dbo.catComisiones (id, comision, estatus) VALUES ({comision.Id}, {comision.Nombre}, {comision.Estatus});",
                cancellationToken);
        }
        finally
        {
            if (isIdentity)
            {
                await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo.catComisiones OFF;", cancellationToken);
            }
        }
    }

    private async Task<bool> IsIdentityColumnAsync(string tableName, string columnName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COLUMNPROPERTY(OBJECT_ID(@p0), @p1, 'IsIdentity') AS Value
            """;

        var result = await _context.Database.SqlQueryRaw<int?>(sql, $"dbo.{tableName}", columnName).FirstAsync(cancellationToken);
        return result == 1;
    }

    private static int NormalizeActiveVariableId(int? activeVariableId)
    {
        var value = activeVariableId ?? DefaultActiveVariableId;
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(activeVariableId), "El valor de variable_id debe ser mayor a 0.");
        }

        return value;
    }

    private static async Task<List<CongresoComisionRow>> ReadComisionesAsync(NpgsqlConnection connection, int activeVariableId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT * FROM public.comisiones ORDER BY id;";

        await using var command = new NpgsqlCommand(sql, connection);
        var result = new List<CongresoComisionRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = ReadRow(reader);
            var id = GetIntValue(row, ["id", "comision_id", "id_comision"]);
            if (!id.HasValue)
            {
                continue;
            }

            result.Add(new CongresoComisionRow
            {
                Id = id.Value,
                Nombre = GetTextValue(row, ComisionNameCandidates),
                Estatus = GetIntValue(row, ComisionStatusCandidates) == activeVariableId
            });
        }

        return result;
    }

    private static async Task<List<CongresoDiputadoRow>> ReadDiputadosAsync(NpgsqlConnection connection, int activeVariableId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT * FROM public.diputados ORDER BY id;";

        await using var command = new NpgsqlCommand(sql, connection);
        var result = new List<CongresoDiputadoRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = ReadRow(reader);
            var id = GetIntValue(row, ["id", "diputado_id", "id_diputado"]);
            if (!id.HasValue)
            {
                continue;
            }

            var variableId = GetIntValue(row, DiputadoVariableIdCandidates);
            var actual = variableId.HasValue
                ? variableId.Value == activeVariableId
                : ParseBoolean(GetColumnValue(row, DiputadoActualCandidates));

            result.Add(new CongresoDiputadoRow
            {
                Id = id.Value,
                Nombre = GetTextValue(row, DiputadoNameCandidates),
                Actual = actual,
                Estatus = actual ?? false
            });
        }

        return result;
    }

    private static async Task<List<CongresoComisionDiputadoRow>> ReadComisionesDiputadosAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = "SELECT * FROM public.comisiones_diputados;";

        await using var command = new NpgsqlCommand(sql, connection);
        var result = new List<CongresoComisionDiputadoRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = ReadRow(reader);
            var comisionId = GetIntValue(row, RelComisionIdCandidates);
            var diputadoId = GetIntValue(row, RelDiputadoIdCandidates);
            if (!comisionId.HasValue || !diputadoId.HasValue)
            {
                continue;
            }

            result.Add(new CongresoComisionDiputadoRow
            {
                ComisionId = comisionId.Value,
                DiputadoId = diputadoId.Value
            });
        }

        return result;
    }

    private static Dictionary<string, object?> ReadRow(NpgsqlDataReader reader)
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        return row;
    }

    private static object? GetColumnValue(IReadOnlyDictionary<string, object?> row, IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            var key = row.Keys.FirstOrDefault(x => string.Equals(x, candidate, StringComparison.OrdinalIgnoreCase));
            if (key is not null)
            {
                return row[key];
            }
        }

        return null;
    }

    private static int? GetIntValue(IReadOnlyDictionary<string, object?> row, IEnumerable<string> candidates)
    {
        var value = GetColumnValue(row, candidates);
        return value switch
        {
            int intValue => intValue,
            long longValue when longValue is >= int.MinValue and <= int.MaxValue => (int)longValue,
            short shortValue => shortValue,
            decimal decimalValue when decimalValue % 1 == 0 && decimalValue is >= int.MinValue and <= int.MaxValue => (int)decimalValue,
            string stringValue when int.TryParse(stringValue.Trim(), out var parsed) => parsed,
            _ => null
        };
    }

    private static string? GetTextValue(IReadOnlyDictionary<string, object?> row, IEnumerable<string> candidates)
    {
        var value = GetColumnValue(row, candidates);
        if (value is null)
        {
            return null;
        }

        var text = value.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static bool? ParseBoolean(object? value)
    {
        return value switch
        {
            bool booleanValue => booleanValue,
            string stringValue when string.Equals(stringValue.Trim(), "true", StringComparison.OrdinalIgnoreCase) => true,
            string stringValue when string.Equals(stringValue.Trim(), "t", StringComparison.OrdinalIgnoreCase) => true,
            string stringValue when string.Equals(stringValue.Trim(), "false", StringComparison.OrdinalIgnoreCase) => false,
            string stringValue when string.Equals(stringValue.Trim(), "f", StringComparison.OrdinalIgnoreCase) => false,
            _ => null
        };
    }

    private static DiffTableSummary BuildComisionesDiff(
        IReadOnlyCollection<LocalComisionRow> local,
        IReadOnlyCollection<CongresoComisionRow> remote)
    {
        var localById = local.ToDictionary(x => x.Id);
        var remoteById = remote.ToDictionary(x => x.Id);

        var onlyLocal = localById.Keys.Except(remoteById.Keys).OrderBy(x => x).Take(20).ToList();
        var onlyRemote = remoteById.Keys.Except(localById.Keys).OrderBy(x => x).Take(20).ToList();

        var changedRows = localById.Keys
            .Intersect(remoteById.Keys)
            .Where(id =>
                !string.Equals(Normalize(localById[id].Nombre), Normalize(remoteById[id].Nombre), StringComparison.Ordinal)
                || localById[id].Estatus != remoteById[id].Estatus)
            .OrderBy(id => id)
            .Take(20)
            .ToList();

        return new DiffTableSummary
        {
            LocalCount = local.Count,
            RemoteCount = remote.Count,
            OnlyLocalIds = onlyLocal,
            OnlyRemoteIds = onlyRemote,
            ChangedIds = changedRows,
            HasDifferences = onlyLocal.Count > 0 || onlyRemote.Count > 0 || changedRows.Count > 0
        };
    }

    private static DiffTableSummary BuildDiputadosDiff(
        IReadOnlyCollection<LocalDiputadoRow> local,
        IReadOnlyCollection<CongresoDiputadoRow> remote)
    {
        var localById = local.ToDictionary(x => x.Id);
        var remoteById = remote.ToDictionary(x => x.Id);

        var onlyLocal = localById.Keys.Except(remoteById.Keys).OrderBy(x => x).Take(20).ToList();
        var onlyRemote = remoteById.Keys.Except(localById.Keys).OrderBy(x => x).Take(20).ToList();

        var changedRows = localById.Keys
            .Intersect(remoteById.Keys)
            .Where(id =>
                !string.Equals(Normalize(localById[id].Nombre), Normalize(remoteById[id].Nombre), StringComparison.Ordinal)
                || localById[id].Actual != remoteById[id].Actual)
            .OrderBy(id => id)
            .Take(20)
            .ToList();

        return new DiffTableSummary
        {
            LocalCount = local.Count,
            RemoteCount = remote.Count,
            OnlyLocalIds = onlyLocal,
            OnlyRemoteIds = onlyRemote,
            ChangedIds = changedRows,
            HasDifferences = onlyLocal.Count > 0 || onlyRemote.Count > 0 || changedRows.Count > 0
        };
    }

    private static DiffTableSummary BuildRelacionesDiff(
        IReadOnlyCollection<LocalComisionDiputadoRow> local,
        IReadOnlyCollection<CongresoComisionDiputadoRow> remote)
    {
        var localSet = local.Select(x => $"{x.ComisionId}-{x.DiputadoId}").ToHashSet(StringComparer.Ordinal);
        var remoteSet = remote.Select(x => $"{x.ComisionId}-{x.DiputadoId}").ToHashSet(StringComparer.Ordinal);

        var onlyLocal = localSet.Except(remoteSet).OrderBy(x => x).Take(20).ToList();
        var onlyRemote = remoteSet.Except(localSet).OrderBy(x => x).Take(20).ToList();

        return new DiffTableSummary
        {
            LocalCount = local.Count,
            RemoteCount = remote.Count,
            OnlyLocalKeys = onlyLocal,
            OnlyRemoteKeys = onlyRemote,
            ChangedIds = [],
            HasDifferences = onlyLocal.Count > 0 || onlyRemote.Count > 0
        };
    }

    private static string Normalize(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private sealed class CongresoComisionRow
    {
        public int Id { get; init; }
        public string? Nombre { get; init; }
        public bool Estatus { get; init; }
    }

    private sealed class CongresoDiputadoRow
    {
        public int Id { get; init; }
        public string? Nombre { get; init; }
        public bool Estatus { get; init; }
        public bool? Actual { get; init; }
    }

    private sealed class CongresoComisionDiputadoRow
    {
        public int ComisionId { get; init; }
        public int DiputadoId { get; init; }
    }

    private sealed class LocalComisionRow
    {
        public int Id { get; init; }
        public string? Nombre { get; init; }
        public bool Estatus { get; init; }
    }

    private sealed class LocalDiputadoRow
    {
        public int Id { get; init; }
        public string? Nombre { get; init; }
        public bool? Actual { get; init; }
    }

    private sealed class LocalComisionDiputadoRow
    {
        public int ComisionId { get; init; }
        public int DiputadoId { get; init; }
    }
}

public sealed class CongresoCatalogSyncResponse
{
    public int ComisionesSincronizadas { get; set; }
    public int DiputadosSincronizados { get; set; }
    public int RelacionesSincronizadas { get; set; }
    public int ActiveVariableId { get; set; }
    public DateTimeOffset SincronizadoEn { get; set; }
}

public sealed class CongresoCatalogDiffResponse
{
    public DateTimeOffset GeneratedAt { get; set; }
    public bool HasDifferences { get; set; }
    public bool SuggestSync { get; set; }
    public DiffTableSummary Comisiones { get; set; } = new();
    public DiffTableSummary Diputados { get; set; } = new();
    public DiffTableSummary ComisionesDiputados { get; set; } = new();
}

public sealed class DiffTableSummary
{
    public int LocalCount { get; set; }
    public int RemoteCount { get; set; }
    public bool HasDifferences { get; set; }
    public List<int> OnlyLocalIds { get; set; } = [];
    public List<int> OnlyRemoteIds { get; set; } = [];
    public List<int> ChangedIds { get; set; } = [];
    public List<string> OnlyLocalKeys { get; set; } = [];
    public List<string> OnlyRemoteKeys { get; set; } = [];
}