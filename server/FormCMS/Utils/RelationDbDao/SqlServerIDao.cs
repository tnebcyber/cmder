using System.Data;
using FormCMS.Utils.EnumExt;
using Microsoft.Data.SqlClient;
using SqlKata.Compilers;
using SqlKata.Execution;

namespace FormCMS.Utils.RelationDbDao;

public class SqlServerIDao(SqlConnection connection, ILogger<SqlServerIDao> logger ) : IDao
{
    private readonly Compiler _compiler = new SqlServerCompiler();
    private  SqlTransaction? _transaction = null;

    public async Task<T> ExecuteKateQuery<T>(Func<QueryFactory, IDbTransaction?, Task<T>> queryFunc)
    {
        var db = new QueryFactory(connection, _compiler);
        db.Logger = result => logger.LogInformation(result.ToString());
        return await queryFunc(db,_transaction);
    }

    public async ValueTask<IDbTransaction> BeginTransaction()
    {
        _transaction= await connection.BeginTransactionAsync() as SqlTransaction;
        return _transaction!;
    }

    public void EndTransaction() => _transaction = null;

    public bool TryParseDataType(string s, ColumnType type, out DatabaseTypeValue? result)
    {
        result = type switch
        {
            ColumnType.Datetime or ColumnType.String or ColumnType.Text => new DatabaseTypeValue(s),
            ColumnType.Int when int.TryParse(s, out var resultInt) => new DatabaseTypeValue(I: resultInt),
            _ => null
        };
        return result != null;
    }

    public async Task CreateTable(string table, IEnumerable<Column> cols,  CancellationToken ct)
    {
        var strs = cols.Select(column => column switch
        {
            _ when column.Name == DefaultColumnNames.Id.ToCamelCase() => $"[{DefaultColumnNames.Id.ToCamelCase()}] INT IDENTITY(1,1) PRIMARY KEY",
            _ when column.Name == DefaultColumnNames.Deleted.ToCamelCase() => $"[{DefaultColumnNames.Deleted.ToCamelCase()}] BIT DEFAULT 0",
            
            _ when column.Name == DefaultColumnNames.CreatedAt.ToCamelCase() => $"[{DefaultColumnNames.CreatedAt.ToCamelCase()}] DATETIME DEFAULT GETDATE()",
            _ when column.Name == DefaultColumnNames.UpdatedAt.ToCamelCase() => $"[{DefaultColumnNames.UpdatedAt.ToCamelCase()}] DATETIME DEFAULT GETDATE()",
            
            _ => $"[{column.Name}] {DataTypeToString(column.Type)}"
        });

        var sql = $"CREATE TABLE [{table}] ({string.Join(", ", strs)});";
        
        await ExecuteQuery(sql, async cmd => await cmd.ExecuteNonQueryAsync(ct));
        sql = $"""
               CREATE TRIGGER trg_{table}_updatedAt 
               ON [{table}] 
               AFTER UPDATE
               AS 
               BEGIN
                   SET NOCOUNT ON;
                   UPDATE [{table}]
                   SET [{DefaultColumnNames.UpdatedAt.ToCamelCase()}] = GETDATE()
                   FROM inserted i
                   WHERE [{table}].[id] = i.[id];
               END;
               """;

        await ExecuteQuery(sql, async cmd => await cmd.ExecuteNonQueryAsync(ct));
    }

    public async Task AddColumns(string table, IEnumerable<Column> cols, CancellationToken ct)
    {
        var parts = cols.Select(x =>
            $"ALTER TABLE [{table}] ADD [{x.Name}] {DataTypeToString(x.Type)}"
        );
        var sql = string.Join(";", parts);
        await ExecuteQuery(sql, async cmd => await cmd.ExecuteNonQueryAsync(ct));
    }
    public async Task CreateForeignKey(string table, string col, string refTable, string refCol, CancellationToken ct)
    {
        var sql = $"""
                   IF NOT EXISTS (
                           SELECT 1 FROM sys.foreign_keys
                           WHERE name = 'fk_{table}_{col}' AND parent_object_id = OBJECT_ID('{table}')
                               )
                           BEGIN
                               ALTER TABLE {table} ADD CONSTRAINT fk_{table}_{col} FOREIGN KEY ([{col}]) REFERENCES {refTable} ([{refCol}]);
                           END
                   """;
        await ExecuteQuery(sql, cmd => cmd.ExecuteNonQueryAsync(ct));
    }
    public async Task<Column[]> GetColumnDefinitions(string table, CancellationToken ct)
    {
        var sql = @"
                SELECT COLUMN_NAME, DATA_TYPE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @tableName";

        return await ExecuteQuery(sql, async command =>
        {
            var columnDefinitions = new List<Column>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                columnDefinitions.Add(new Column
                ( 
                    Name : reader.GetString(0),
                    Type : StringToDataType(reader.GetString(1))
                ));
            }

            return columnDefinitions.ToArray();
        }, ("tableName", table));
    }

    private static string DataTypeToString(ColumnType dataType)
        => dataType switch
        {
            ColumnType.Int => "INT",
            ColumnType.Text => "TEXT",
            ColumnType.Datetime => "DATETIME",
            ColumnType.String => "NVARCHAR(255)",
            _ => throw new NotSupportedException($"Type {dataType} is not supported")
        };

    private ColumnType StringToDataType(string s)
    {
        s = s.ToLower();
        return s switch
        {
            "int" => ColumnType.Int,
            "text" => ColumnType.Text,
            "datetime" => ColumnType.Datetime,
            _ => ColumnType.String
        };
    }

    // Use callback instead of return QueryFactory to ensure proper disposing connection
    private async Task<T> ExecuteQuery<T>(
        string sql, 
        Func<SqlCommand, Task<T>> executeFunc, 
        params (string, object)[] parameters)
    {
        logger.LogInformation(sql);
        await using var command = new SqlCommand(sql, connection);
        command.Transaction = _transaction as SqlTransaction;

        foreach (var (paramName, paramValue) in parameters)
        {
            command.Parameters.AddWithValue(paramName, paramValue);
        }

        return await executeFunc(command);
    }
}