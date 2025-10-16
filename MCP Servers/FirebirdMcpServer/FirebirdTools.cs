using System.Data;
using System.Text;
using System.Text.Json;
using FirebirdSql.Data.FirebirdClient;

namespace FirebirdMcpServer
{
    public class FirebirdTools
    {
        public async Task<object> ConnectDatabase(JsonElement args)
        {
            var connectionString = args.GetProperty("connectionString").GetString()!;

            try
            {
                using var connection = new FbConnection(connectionString);
                await connection.OpenAsync();

                return new
                {
                    success = true,
                    serverVersion = connection.ServerVersion,
                    database = connection.Database,
                    dataSource = connection.DataSource
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    success = false,
                    error = ex.Message
                };
            }
        }

        public async Task<object> TestConnection(JsonElement args)
        {
            var connectionString = args.GetProperty("connectionString").GetString()!;

            try
            {
                using var connection = new FbConnection(connectionString);
                await connection.OpenAsync();

                return new
                {
                    success = true,
                    connected = true,
                    message = "Connection successful"
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    success = false,
                    connected = false,
                    error = ex.Message
                };
            }
        }

        public async Task<object> GetDatabaseMetadata(JsonElement args)
        {
            var connectionString = args.GetProperty("connectionString").GetString()!;

            using var connection = new FbConnection(connectionString);
            await connection.OpenAsync();

            var metadata = new
            {
                serverVersion = connection.ServerVersion,
                database = connection.Database,
                dataSource = connection.DataSource,
                pageSize = await GetDatabasePageSize(connection),
                charset = await GetDatabaseCharset(connection)
            };

            return new
            {
                success = true,
                metadata
            };
        }

        public async Task<object> ListTables(JsonElement args)
        {
            var connectionString = args.GetProperty("connectionString").GetString()!;
            var includeSystem = args.TryGetProperty("includeSystem", out var sys) && sys.GetBoolean();

            using var connection = new FbConnection(connectionString);
            await connection.OpenAsync();

            var tables = new List<object>();
            var schema = await connection.GetSchemaAsync("Tables");

            foreach (DataRow row in schema.Rows)
            {
                var tableName = row["TABLE_NAME"].ToString()!;
                var tableType = row["TABLE_TYPE"].ToString()!;
                
                if (!includeSystem && (tableName.StartsWith("RDB$") || tableName.StartsWith("MON$")))
                    continue;

                tables.Add(new
                {
                    tableName,
                    tableType,
                    description = row["DESCRIPTION"]?.ToString()
                });
            }

            return new
            {
                success = true,
                tableCount = tables.Count,
                tables
            };
        }

        public async Task<object> GetTableSchema(JsonElement args)
        {
            var connectionString = args.GetProperty("connectionString").GetString()!;
            var tableName = args.GetProperty("tableName").GetString()!;

            using var connection = new FbConnection(connectionString);
            await connection.OpenAsync();

            var columns = await GetTableColumns(connection, tableName);
            var indexes = await GetTableIndexes(connection, tableName);
            var constraints = await GetTableConstraints(connection, tableName);

            return new
            {
                success = true,
                tableName,
                columns,
                indexes,
                constraints
            };
        }

        public async Task<object> GetTableColumns(JsonElement args)
        {
            var connectionString = args.GetProperty("connectionString").GetString()!;
            var tableName = args.GetProperty("tableName").GetString()!;

            using var connection = new FbConnection(connectionString);
            await connection.OpenAsync();

            var columns = await GetTableColumns(connection, tableName);

            return new
            {
                success = true,
                tableName,
                columnCount = columns.Count,
                columns
            };
        }

        public async Task<object> GetTableIndexes(JsonElement args)
        {
            var connectionString = args.GetProperty("connectionString").GetString()!;
            var tableName = args.GetProperty("tableName").GetString()!;

            using var connection = new FbConnection(connectionString);
            await connection.OpenAsync();

            var indexes = await GetTableIndexes(connection, tableName);

            return new
            {
                success = true,
                tableName,
                indexCount = indexes.Count,
                indexes
            };
        }

        public async Task<object> GetTableConstraints(JsonElement args)
        {
            var connectionString = args.GetProperty("connectionString").GetString()!;
            var tableName = args.GetProperty("tableName").GetString()!;

            using var connection = new FbConnection(connectionString);
            await connection.OpenAsync();

            var constraints = await GetTableConstraints(connection, tableName);

            return new
            {
                success = true,
                tableName,
                constraintCount = constraints.Count,
                constraints
            };
        }

        public async Task<object> ListStoredProcedures(JsonElement args)
        {
            var connectionString = args.GetProperty("connectionString").GetString()!;

            using var connection = new FbConnection(connectionString);
            await connection.OpenAsync();

            var procedures = new List<object>();
            var schema = await connection.GetSchemaAsync("Procedures");

            foreach (DataRow row in schema.Rows)
            {
                procedures.Add(new
                {
                    procedureName = row["PROCEDURE_NAME"].ToString(),
                    description = row["DESCRIPTION"]?.ToString()
                });
            }

            return new
            {
                success = true,
                procedureCount = procedures.Count,
                procedures
            };
        }

        public async Task<object> GetProcedureDefinition(JsonElement args)
        {
            var connectionString = args.GetProperty("connectionString").GetString()!;
            var procedureName = args.GetProperty("procedureName").GetString()!;

            using var connection = new FbConnection(connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT RDB$PROCEDURE_SOURCE 
                FROM RDB$PROCEDURES 
                WHERE RDB$PROCEDURE_NAME = @ProcName";

            using var cmd = new FbCommand(sql, connection);
            cmd.Parameters.AddWithValue("@ProcName", procedureName);

            var source = await cmd.ExecuteScalarAsync() as string;

            return new
            {
                success = true,
                procedureName,
                source = source?.Trim()
            };
        }

        public async Task<object> GetProcedureParameters(JsonElement args)
        {
            var connectionString = args.GetProperty("connectionString").GetString()!;
            var procedureName = args.GetProperty("procedureName").GetString()!;

            using var connection = new FbConnection(connectionString);
            await connection.OpenAsync();

            var parameters = new List<object>();
            var schema = await connection.GetSchemaAsync("ProcedureParameters");

            foreach (DataRow row in schema.Rows)
            {
                if (row["PROCEDURE_NAME"].ToString()?.Trim() == procedureName)
                {
                    parameters.Add(new
                    {
                        parameterName = row["PARAMETER_NAME"].ToString(),
                        parameterType = row["PARAMETER_DIRECTION"].ToString(),
                        dataType = row["PARAMETER_DATA_TYPE"].ToString(),
                        parameterSize = row["PARAMETER_SIZE"],
                        position = row["ORDINAL_POSITION"]
                    });
                }
            }

            return new
            {
                success = true,
                procedureName,
                parameterCount = parameters.Count,
                parameters
            };
        }

        public async Task<object> ListTriggers(JsonElement args)
        {
            var connectionString = args.GetProperty("connectionString").GetString()!;
            var tableName = args.TryGetProperty("tableName", out var tbl) ? tbl.GetString() : null;

            using var connection = new FbConnection(connectionString);
            await connection.OpenAsync();

            var sql = tableName != null
                ? "SELECT RDB$TRIGGER_NAME, RDB$RELATION_NAME, RDB$TRIGGER_TYPE, RDB$TRIGGER_INACTIVE FROM RDB$TRIGGERS WHERE RDB$RELATION_NAME = @TableName AND RDB$SYSTEM_FLAG = 0"
                : "SELECT RDB$TRIGGER_NAME, RDB$RELATION_NAME, RDB$TRIGGER_TYPE, RDB$TRIGGER_INACTIVE FROM RDB$TRIGGERS WHERE RDB$SYSTEM_FLAG = 0";

            using var cmd = new FbCommand(sql, connection);
            if (tableName != null)
                cmd.Parameters.AddWithValue("@TableName", tableName);

            var triggers = new List<object>();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                triggers.Add(new
                {
                    triggerName = reader["RDB$TRIGGER_NAME"].ToString()?.Trim(),
                    tableName = reader["RDB$RELATION_NAME"].ToString()?.Trim(),
                    triggerType = reader["RDB$TRIGGER_TYPE"],
                    isActive = reader["RDB$TRIGGER_INACTIVE"].ToString() == "0"
                });
            }

            return new
            {
                success = true,
                triggerCount = triggers.Count,
                triggers
            };
        }

        public async Task<object> GetTriggerDefinition(JsonElement args)
        {
            var connectionString = args.GetProperty("connectionString").GetString()!;
            var triggerName = args.GetProperty("triggerName").GetString()!;

            using var connection = new FbConnection(connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT RDB$TRIGGER_SOURCE, RDB$RELATION_NAME, RDB$TRIGGER_TYPE 
                FROM RDB$TRIGGERS 
                WHERE RDB$TRIGGER_NAME = @TriggerName";

            using var cmd = new FbCommand(sql, connection);
            cmd.Parameters.AddWithValue("@TriggerName", triggerName);

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new
                {
                    success = true,
                    triggerName,
                    tableName = reader["RDB$RELATION_NAME"].ToString()?.Trim(),
                    triggerType = reader["RDB$TRIGGER_TYPE"],
                    source = reader["RDB$TRIGGER_SOURCE"].ToString()?.Trim()
                };
            }

            throw new Exception($"Trigger not found: {triggerName}");
        }

        public async Task<object> ExecuteQuery(JsonElement args)
        {
            var connectionString = args.GetProperty("connectionString").GetString()!;
            var sql = args.GetProperty("sql").GetString()!;
            var parameters = args.TryGetProperty("parameters", out var parms) ? parms : (JsonElement?)null;

            using var connection = new FbConnection(connectionString);
            await connection.OpenAsync();

            using var cmd = new FbCommand(sql, connection);

            if (parameters.HasValue)
            {
                foreach (var param in parameters.Value.EnumerateObject())
                {
                    cmd.Parameters.AddWithValue("@" + param.Name, param.Value.ToString());
                }
            }

            var results = new List<Dictionary<string, object?>>();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                results.Add(row);
            }

            return new
            {
                success = true,
                rowCount = results.Count,
                columns = reader.FieldCount,
                results
            };
        }

        public async Task<object> GetForeignKeys(JsonElement args)
        {
            var connectionString = args.GetProperty("connectionString").GetString()!;
            var tableName = args.GetProperty("tableName").GetString()!;

            using var connection = new FbConnection(connectionString);
            await connection.OpenAsync();

            var foreignKeys = new List<object>();
            var schema = await connection.GetSchemaAsync("ForeignKeys");

            foreach (DataRow row in schema.Rows)
            {
                if (row["TABLE_NAME"].ToString()?.Trim() == tableName)
                {
                    foreignKeys.Add(new
                    {
                        constraintName = row["CONSTRAINT_NAME"].ToString(),
                        columnName = row["COLUMN_NAME"].ToString(),
                        referencedTable = row["REFERENCED_TABLE_NAME"].ToString(),
                        referencedColumn = row["REFERENCED_COLUMN_NAME"].ToString()
                    });
                }
            }

            return new
            {
                success = true,
                tableName,
                foreignKeyCount = foreignKeys.Count,
                foreignKeys
            };
        }

        public async Task<object> GenerateDDL(JsonElement args)
        {
            var connectionString = args.GetProperty("connectionString").GetString()!;
            var objectType = args.GetProperty("objectType").GetString()!;
            var objectName = args.GetProperty("objectName").GetString()!;

            using var connection = new FbConnection(connectionString);
            await connection.OpenAsync();

            string ddl;

            switch (objectType.ToUpperInvariant())
            {
                case "TABLE":
                    ddl = await GenerateTableDDL(connection, objectName);
                    break;
                case "PROCEDURE":
                    ddl = await GenerateProcedureDDL(connection, objectName);
                    break;
                case "TRIGGER":
                    ddl = await GenerateTriggerDDL(connection, objectName);
                    break;
                default:
                    throw new ArgumentException($"Unsupported object type: {objectType}");
            }

            return new
            {
                success = true,
                objectType,
                objectName,
                ddl
            };
        }

        private async Task<List<object>> GetTableColumns(FbConnection connection, string tableName)
        {
            var columns = new List<object>();
            var sql = @"
                SELECT 
                    f.RDB$FIELD_NAME AS COLUMN_NAME,
                    f.RDB$FIELD_TYPE AS FIELD_TYPE,
                    f.RDB$FIELD_LENGTH AS FIELD_LENGTH,
                    f.RDB$NULL_FLAG AS NULL_FLAG,
                    f.RDB$DEFAULT_SOURCE AS DEFAULT_VALUE,
                    t.RDB$TYPE_NAME AS TYPE_NAME
                FROM RDB$RELATION_FIELDS f
                LEFT JOIN RDB$FIELDS fld ON f.RDB$FIELD_SOURCE = fld.RDB$FIELD_NAME
                LEFT JOIN RDB$TYPES t ON fld.RDB$FIELD_TYPE = t.RDB$TYPE AND t.RDB$FIELD_NAME = 'RDB$FIELD_TYPE'
                WHERE f.RDB$RELATION_NAME = @TableName
                ORDER BY f.RDB$FIELD_POSITION";

            using var cmd = new FbCommand(sql, connection);
            cmd.Parameters.AddWithValue("@TableName", tableName);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                columns.Add(new
                {
                    columnName = reader["COLUMN_NAME"].ToString()?.Trim(),
                    dataType = reader["TYPE_NAME"].ToString()?.Trim(),
                    length = reader["FIELD_LENGTH"],
                    isNullable = reader["NULL_FLAG"] == DBNull.Value,
                    defaultValue = reader["DEFAULT_VALUE"]?.ToString()?.Trim()
                });
            }

            return columns;
        }

        private async Task<List<object>> GetTableIndexes(FbConnection connection, string tableName)
        {
            var indexes = new List<object>();
            var schema = await connection.GetSchemaAsync("Indexes");

            foreach (DataRow row in schema.Rows)
            {
                if (row["TABLE_NAME"].ToString()?.Trim() == tableName)
                {
                    indexes.Add(new
                    {
                        indexName = row["INDEX_NAME"].ToString(),
                        isUnique = Convert.ToBoolean(row["IS_UNIQUE"]),
                        columnName = row["COLUMN_NAME"].ToString()
                    });
                }
            }

            return indexes;
        }

        private async Task<List<object>> GetTableConstraints(FbConnection connection, string tableName)
        {
            var constraints = new List<object>();
            var sql = @"
                SELECT 
                    rc.RDB$CONSTRAINT_NAME,
                    rc.RDB$CONSTRAINT_TYPE
                FROM RDB$RELATION_CONSTRAINTS rc
                WHERE rc.RDB$RELATION_NAME = @TableName";

            using var cmd = new FbCommand(sql, connection);
            cmd.Parameters.AddWithValue("@TableName", tableName);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                constraints.Add(new
                {
                    constraintName = reader["RDB$CONSTRAINT_NAME"].ToString()?.Trim(),
                    constraintType = reader["RDB$CONSTRAINT_TYPE"].ToString()?.Trim()
                });
            }

            return constraints;
        }

        private async Task<int> GetDatabasePageSize(FbConnection connection)
        {
            var sql = "SELECT MON$PAGE_SIZE FROM MON$DATABASE";
            using var cmd = new FbCommand(sql, connection);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        private async Task<string> GetDatabaseCharset(FbConnection connection)
        {
            var sql = "SELECT RDB$CHARACTER_SET_NAME FROM RDB$DATABASE";
            using var cmd = new FbCommand(sql, connection);
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString()?.Trim() ?? "NONE";
        }

        private async Task<string> GenerateTableDDL(FbConnection connection, string tableName)
        {
            var ddl = new StringBuilder();
            ddl.AppendLine($"CREATE TABLE {tableName} (");

            var columns = await GetTableColumns(connection, tableName);
            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                var colProps = new Dictionary<string, object?>();
                foreach (var prop in col.GetType().GetProperties())
                {
                    colProps[prop.Name] = prop.GetValue(col);
                }

                ddl.Append($"  {colProps["columnName"]} {colProps["dataType"]}");
                
                if (colProps["length"] != null && Convert.ToInt32(colProps["length"]) > 0)
                    ddl.Append($"({colProps["length"]})");

                if (!(bool)colProps["isNullable"]!)
                    ddl.Append(" NOT NULL");

                if (i < columns.Count - 1)
                    ddl.AppendLine(",");
                else
                    ddl.AppendLine();
            }

            ddl.AppendLine(");");
            return ddl.ToString();
        }

        private async Task<string> GenerateProcedureDDL(FbConnection connection, string procedureName)
        {
            var sql = @"
                SELECT RDB$PROCEDURE_SOURCE 
                FROM RDB$PROCEDURES 
                WHERE RDB$PROCEDURE_NAME = @ProcName";

            using var cmd = new FbCommand(sql, connection);
            cmd.Parameters.AddWithValue("@ProcName", procedureName);

            var source = await cmd.ExecuteScalarAsync() as string;
            return $"CREATE PROCEDURE {procedureName}\n{source?.Trim()}";
        }

        private async Task<string> GenerateTriggerDDL(FbConnection connection, string triggerName)
        {
            var sql = @"
                SELECT RDB$TRIGGER_SOURCE 
                FROM RDB$TRIGGERS 
                WHERE RDB$TRIGGER_NAME = @TriggerName";

            using var cmd = new FbCommand(sql, connection);
            cmd.Parameters.AddWithValue("@TriggerName", triggerName);

            var source = await cmd.ExecuteScalarAsync() as string;
            return $"CREATE TRIGGER {triggerName}\n{source?.Trim()}";
        }
    }
}
