using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using System.Data;
using System.Linq.Expressions;
using System.Text;

namespace EfficientBulkOperations;

public static class BulkOperations
{
    public static async Task BulkInsertToTempTableAsync<T>(
    this DbContext context,
    IEnumerable<T> entities,
    string tempTableName,
    BulkOptions<T>? options = null)
    {
        options ??= new BulkOptions<T>();
        
        // Open connection and keep it open
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        var connectionOpenedHere = false;
        if (connection.State != ConnectionState.Open)
        {
            await context.Database.OpenConnectionAsync();
            connectionOpenedHere = true;
        }

        try
        {
            // Step 1: Create Temp Table
            var entityType = context.Model.FindEntityType(typeof(T));
            var targetTableName = entityType!.GetTableName();
            var schema = entityType!.GetSchema();

            // Create temp table with all properties
            string createTempTableSql = string.IsNullOrEmpty(schema) ?
                $"CREATE TEMP TABLE {tempTableName} (LIKE \"{targetTableName}\" INCLUDING ALL);"
            :
                $"CREATE TEMP TABLE {tempTableName} (LIKE {schema}.\"{targetTableName}\" INCLUDING ALL);";

            // Remove identity property from "id" column
            string alterTempTableSql = $@"ALTER TABLE {tempTableName} ALTER COLUMN ""Id"" DROP IDENTITY;";

            // Execute both commands
            await using var cmd = connection.CreateCommand();

            cmd.CommandText = createTempTableSql;
            await cmd.ExecuteNonQueryAsync();

            cmd.CommandText = alterTempTableSql;
            await cmd.ExecuteNonQueryAsync();

            // Step 2: Bulk Insert into Temp Table using Npgsql COPY

            var properties = entityType.GetProperties();
            var columnNames = properties
                .Select(p => $"\"{p.GetColumnName(StoreObjectIdentifier.Table(targetTableName, schema))}\"")
                .ToArray();
            var copyCommand = $"COPY {tempTableName} ({string.Join(", ", columnNames)}) FROM STDIN (FORMAT BINARY)";

            {
                await using var writer = connection.BeginBinaryImport(copyCommand);
                foreach (var entity in entities)
                {
                    writer.StartRow();
                    foreach (var property in properties)
                    {
                        var value = property.PropertyInfo.GetValue(entity);
                        writer.Write(value);
                    }
                }
                await writer.CompleteAsync();
            }

            await using var countCmd = connection.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(*) FROM \"{tempTableName}\";";
            var count = (long)await countCmd.ExecuteScalarAsync();
            Console.WriteLine($"Number of rows inserted into temp table: {count}");
        }
        finally
        {
            if (connectionOpenedHere)
            {
                await context.Database.CloseConnectionAsync();
            }
        }
    }

    public static async Task MergeTempTableAsync<T>(
    this DbContext context,
    string tempTableName,
    BulkOptions<T> options = null)
    {
        options ??= new BulkOptions<T>();
        var entityType = context.Model.FindEntityType(typeof(T));
        var targetTableName = entityType.GetTableName();
        var schema = entityType.GetSchema();

        var keyProperties = entityType.FindPrimaryKey().Properties;
        var keyColumns = keyProperties
            .Select(p => $"\"{p.GetColumnName(StoreObjectIdentifier.Table(targetTableName, schema))}\"")
            .ToArray();

        var properties = entityType.GetProperties();
        var insertColumns = properties
            .Select(p => $"\"{p.GetColumnName(StoreObjectIdentifier.Table(targetTableName, schema))}\"")
            .ToArray();

        string conflictClause;

        if (options.OnConflict == ConflictAction.DoNothing)
        {
            conflictClause = $"ON CONFLICT ({string.Join(", ", keyColumns)}) DO NOTHING;";
        }
        else
        {
            var nonKeyProperties = properties.Except(keyProperties);
            var updateColumns = nonKeyProperties
                .Select(p => $"\"{p.GetColumnName(StoreObjectIdentifier.Table(targetTableName, schema))}\"")
                .ToArray();

            string conditionSql = "TRUE";

            if (options.MergeCondition != null)
            {
                conditionSql = ConvertExpressionToSql(options.MergeCondition);
            }

            conflictClause = $@"
            ON CONFLICT ({string.Join(", ", keyColumns)}) DO UPDATE
            SET {string.Join(", ", updateColumns.Select(col => $"{col} = EXCLUDED.{col}"))}
            WHERE {conditionSql};";
        }

        // Properly quote schema and table names
        var schemaPart = string.IsNullOrEmpty(schema) ? "" : $"\"{schema}\".";

        var mergeSql = $@"
        INSERT INTO {schemaPart}""{targetTableName}"" ({string.Join(", ", insertColumns)})
        OVERRIDING SYSTEM VALUE
        SELECT {string.Join(", ", insertColumns)}
                FROM ""{tempTableName}""
        {conflictClause}
                ";

        // Execute mergeSql using the same connection
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = mergeSql;

        Console.WriteLine("Generated mergeSql:");
        Console.WriteLine(mergeSql);
        try
        {
            await cmd.ExecuteNonQueryAsync();

            // After the merge operation
            await using var verifyCmd = connection.CreateCommand();
            verifyCmd.CommandText = $"SELECT COUNT(*) FROM \"{targetTableName}\";";
            var mainTableCount = (long)await verifyCmd.ExecuteScalarAsync();
            Console.WriteLine($"Number of rows in main table after merge: {mainTableCount}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during merge operation: {ex.Message}");
            throw;
        }
    }

    private static string ConvertExpressionToSql<T>(Expression<Func<T, T, bool>> expression)
    {
        var visitor = new MergeConditionVisitor();
        visitor.Visit(expression);
        return visitor.Condition;
    }

    public class MergeConditionVisitor : ExpressionVisitor
    {
        private StringBuilder _sb = new StringBuilder();
        public string Condition => _sb.ToString();

        protected override Expression VisitBinary(BinaryExpression node)
        {
            _sb.Append('(');
            Visit(node.Left);

            switch (node.NodeType)
            {
                case ExpressionType.Equal:
                    _sb.Append(" = ");
                    break;

                case ExpressionType.NotEqual:
                    _sb.Append(" <> ");
                    break;

                case ExpressionType.GreaterThan:
                    _sb.Append(" > ");
                    break;

                case ExpressionType.GreaterThanOrEqual:
                    _sb.Append(" >= ");
                    break;

                case ExpressionType.LessThan:
                    _sb.Append(" < ");
                    break;

                case ExpressionType.LessThanOrEqual:
                    _sb.Append(" <= ");
                    break;

                case ExpressionType.AndAlso:
                    _sb.Append(" AND ");
                    break;

                case ExpressionType.OrElse:
                    _sb.Append(" OR ");
                    break;

                default:
                    throw new NotSupportedException($"Operation '{node.NodeType}' is not supported");
            }

            Visit(node.Right);
            _sb.Append(")");
            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ParameterExpression parameter)
            {
                string alias = parameter.Name == "existing" ? "target" : "EXCLUDED";
                _sb.Append($"{alias}.\"{node.Member.Name}\"");
                return node;
            }

            throw new NotSupportedException($"The member '{node.Member.Name}' is not supported");
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            // Handle constants directly in SQL. Be cautious with SQL injection risks.
            if (node.Type == typeof(string))
            {
                _sb.Append($"'{node.Value}'");
            }
            else
            {
                _sb.Append(node.Value);
            }
            return node;
        }
    }

    public static async Task ExecuteBulkOperationAsync<T>(
    this DbContext context,
    IEnumerable<T> entities,
    BulkOptions<T> options = null)
    {
        var tempTable = $"temp_{Guid.NewGuid():N}";
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.BulkInsertToTempTableAsync(entities, tempTable, options);
        await context.MergeTempTableAsync<T>(tempTable, options);
        await transaction.CommitAsync();
    }
}