using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Common;
using System.Data.SqlClient;
using System.Data;
using System.Collections.Generic;
using System;
using System.IO;
using static export_tbl.Globals;
using System.Text;

namespace export_tbl
{
    /// <summary>
    /// Methods that support interacting with the database server
    /// </summary>

    class ServerUtils
    {
        private const string ONE_DBL_QUOTE = "\"";
        private const string TWO_DBL_QUOTES = "\"\"";

        /// <summary>
        /// Exports data from a database table to a file
        /// </summary>
        /// <param name="ServerName">The server name</param>
        /// <param name="DatabaseName">The database name</param>
        /// <param name="TableName">the table name</param>
        /// <param name="FileName">The file to export to</param>
        /// <param name="HeadersDict">the Columns to export</param>
        /// <param name="Fixed">true if field-length export</param>
        /// <param name="Delimiter">Delimiter, if not fixed length</param>
        /// <param name="Pad">The number of characters of padding to add to each column if fixed. Zero if no padding</param>
        /// <param name="Append">True to append to the output file. Otherwise re-create the output file</param>
        /// <param name="ShouldWriteHeader">True to write a header</param>
        /// <param name="MaxRows">Max rows (excl. header) to write</param>
        /// <param name="Quote">True to quote fields if fields contain a delimiter character</param>
        /// <returns>true if delimiters were found in the fields, and quoting was not specified</returns>

        public static bool DoExport(string ServerName, string DatabaseName, string TableName, string FileName,
            Dictionary<string, ColHeader> HeadersDict, bool Fixed, string Delimiter, int Pad, bool Append, bool ShouldWriteHeader, int MaxRows, bool Quote,
            out int RecordsWritten)
        {
            List<ColHeader> ColHeaders = ColHeader.ToList(HeadersDict);
            string Sql = BuildSelect(TableName, ColHeaders);
            bool FoundDelimitersInField = false;
            string Padding = Pad != 0 ? new string(' ', Pad) : string.Empty;
            int RecordCount = 0;

            using (SqlConnection Cnct = GetSqlConnection(ServerName, DatabaseName))
            using (StreamWriter Writer = new StreamWriter(FileName, Append))
            {
                SqlCommand command = new SqlCommand(Sql, Cnct)
                {
                    CommandTimeout = GetSQLTimeout()
                };
                command.Connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (RecordCount == 0 && ShouldWriteHeader)
                        {
                            Writer.WriteLine(BuildHeader(ColHeaders, Fixed, Delimiter, Pad));
                        }
                        foreach (ColHeader Hdr in ColHeaders)
                        {
                            if (!Fixed && Hdr.OrdinalPosition > 0)
                            {
                                Writer.Write(Delimiter);
                            }
                            string Fld = Hdr.ValueFrom(reader, Fixed);
                            if (!Fixed && (Fld.Contains(Delimiter) || Fld.Contains(ONE_DBL_QUOTE)))
                            {
                                // if the field contains a delimiter, quote it. If it contains any quotes, also
                                // quote it so the reader can strip the quotes and get the original contents back
                                if (Quote)
                                {
                                    Fld = ONE_DBL_QUOTE + Fld.Replace(ONE_DBL_QUOTE, TWO_DBL_QUOTES) + ONE_DBL_QUOTE;
                                }
                                FoundDelimitersInField = true;
                            }
                            Writer.Write(Fld);
                            if (Pad != 0)
                            {
                                Writer.Write(Padding);
                            }
                        }
                        Writer.WriteLine();
                        if (++RecordCount >= MaxRows)
                        {
                            command.Cancel();
                            break;
                        }
                        if (RecordCount % 10000 == 0)
                        {
                            Log.InformationMessage("Records: {0}", RecordCount);
                        }
                    } // while (reader.Read())
                } // using SqlDataReader
            } // using SqlConnection & StreamWriter
            RecordsWritten = RecordCount;
            return FoundDelimitersInField;
        }

        /// <summary>
        /// Builds a header for the output file
        /// </summary>
        /// <param name="ColHeaders">Column headers containing metadata</param>
        /// <param name="Fixed">True if fixed width, else delimited</param>
        /// <param name="Delimiter">Delimiter, if not fixed length</param>
        /// <param name="Pad"></param>
        /// <returns>the header line. (Not newline-terminated)</returns>

        private static string BuildHeader(List<ColHeader> ColHeaders, bool Fixed, string Delimiter, int Pad)
        {
            StringBuilder Builder = new StringBuilder();
            string Padding = string.Empty;
            if (Pad != 0)
            {
                Padding = new string(' ', Pad);
            }
            foreach (ColHeader Hdr in ColHeaders)
            {
                Builder.Append(Fixed || Hdr.OrdinalPosition == 0 ? string.Empty : Delimiter);
                string ColumnName = Hdr.TruncatedColName ?? Hdr.ColName;
                Builder.Append(Fixed ? ColumnName.PadRight(Hdr.FieldWidth) : ColumnName);
                if (Pad != 0)
                {
                    Builder.Append(Padding);
                }
            }
            return Builder.ToString();
        }

        /// <summary>
        /// Builds a select statement from the passed headers, with the columns in ordinal position as obtained
        /// from the server
        /// </summary>
        /// <param name="TableName">The table name to select from</param>
        /// <param name="ColHeaders">Headers wit metadata</param>
        /// <returns>A select statement</returns>

        private static string BuildSelect(string TableName, List<ColHeader> ColHeaders)
        {
            StringBuilder Sql = new StringBuilder();
            Sql.Append("select ");
            foreach (ColHeader Hdr in ColHeaders)
            {
                Sql.Append(Hdr.OrdinalPosition == 0 ? string.Empty : ", ");
                Sql.Append(Hdr.ColName);
            }
            Sql.Append(" from ");
            Sql.Append(TableName);
            return Sql.ToString();
        }

        /// <summary>
        /// Gets table metadata from the server
        /// </summary>
        /// <param name="ServerName">The server name</param>
        /// <param name="DatabaseName">The database name</param>
        /// <param name="TableName">the table name</param>
        /// <param name="FitToHeader">If true, expands the width of columns so that the column name in the header does not need to be
        /// truncated. Otherwise, does not do this.</param>
        /// <param name="ComputeWidths">True to compute columns widths. (Does not need to be done for a delimited export</param>
        /// <param name="MaxWidth">The max width for output columns. If zero, then don't impose a maximum.</param>
        /// <returns></returns>

        public static Dictionary<string, ColHeader> GetColInfo(string ServerName, string DatabaseName, string TableName, bool FitToHeader, bool ComputeWidths, int MaxWidth)
        {
            using (SqlConnection SqlCnct = GetSqlConnection(ServerName, DatabaseName))
            {
                List<Column> ServerCols = new List<Column>();
                ServerConnection SrvrConn = new ServerConnection(SqlCnct);
                Server Srvr = new Server(SrvrConn);
                Database Db = Srvr.Databases[DatabaseName];
                string SchemaName = "dbo";
                if (TableName.Contains("."))
                {
                    string[] tmp = TableName.Split('.');
                    SchemaName = tmp[0];
                    TableName = tmp[1];
                }
                Table TargetTable = Db.Tables[TableName, SchemaName];
                foreach (Column SqlCol in TargetTable.Columns)
                {
                    if (!IsSupportedType(SqlCol.DataType))
                    {
                        throw new ExportException("Unsupported data type: " + SqlCol.DataType.Name);
                    }
                    ServerCols.Add(SqlCol);
                }
                if (ComputeWidths)
                {
                    return ComputeColWidths(ServerName, DatabaseName, TableName, ServerCols, MaxWidth);
                }
                else
                {
                    Dictionary<string, ColHeader> Cols = new Dictionary<string, ColHeader>();
                    int OrdinalPosition = 0;
                    foreach (Column Col in ServerCols) // now zip them back together
                    {
                        Cols.Add(Col.Name.ToLower(), new ColHeader(Col.Name.ToLower(), 0, Col.DataType, OrdinalPosition++));
                    }
                    return Cols;

                }
            }
        }

        /// <summary>
        /// Determines whether the passed SQL Server data type is supported by the utility
        /// </summary>
        /// <param name="Dt">SQL Server data type </param>
        /// <returns>True if supported, else false</returns>

        private static bool IsSupportedType(DataType Dt)
        {
            // TODO "binary", "varbinary", "text", "ntext", "image", "real", "float"
            return Dt.Name.ToLower().In("varchar", "char", "nvarchar", "nchar", "decimal", "numeric", "date", "datetime", "money", 
                "smallmoney", "int", "bigint", "bit", "tinyint", "smallint", "uniqueidentifier");
        }

        /// <summary>
        /// Computes column widths. For varchar max fields, issues a select statement to determine the widths. Can be time-consuming for
        /// a large table. Otherwise uses metadata to compute the widths
        /// </summary>
        /// <param name="ServerName">The server name</param>
        /// <param name="DatabaseName">The database name</param>
        /// <param name="TableName">the table name</param>
        /// <param name="ServerCols">A list of table columns with metadata</param>
        /// <param name="MaxWidth">The max width for output columns. If zero, then don't impose a maximum.</param>
        /// <returns>A dictionary keyed by column name containing corresponding column metadata</returns>

        private static Dictionary<string, ColHeader> ComputeColWidths(string ServerName, string DatabaseName, string TableName,
            List<Column> ServerCols, int MaxWidth)
        {
            Dictionary<Column, int> VariableCols = new Dictionary<Column, int> ();
            Dictionary<Column, int> NonVariableCols = new Dictionary<Column, int>();
            foreach (Column Col in ServerCols)
            {
                if (MaxWidth == 0 && Col.DataType.Name.ToLower().In("varchar", "nvarchar") && Col.DataType.MaximumLength == -1)
                {
                    // if there's a max width then we don't do varchar max calculation from the DB
                    VariableCols.Add(Col, 0);
                }
                else if (MaxWidth == 0 && Col.DataType.Name.ToLower().In("varchar", "nvarchar", "char", "nchar") && AppSettingsImpl.Trim)
                {
                    VariableCols.Add(Col, 0);
                }
                else
                {
                    NonVariableCols.Add(Col, 0);
                }
            }
            VariableCols = ComputeCharColWidths(ServerName, DatabaseName, TableName, VariableCols);
            NonVariableCols = ComputeNonVarcharMaxColWidths(ServerName, DatabaseName, TableName, NonVariableCols, MaxWidth);

            // zip everything back together
            Dictionary<string, ColHeader> Cols = new Dictionary<string, ColHeader>();
            int OrdinalPosition = 0;
            foreach (Column Col in ServerCols) // now zip them back together
            {
                int ColWidth = VariableCols.ContainsKey(Col) ? VariableCols[Col] : NonVariableCols[Col];
                ColHeader Hdr = new ColHeader(Col.Name.ToLower(), ColWidth, Col.DataType, OrdinalPosition++);
                Cols.Add(Col.Name.ToLower(), Hdr);
            }
            return Cols;
        }

        /// <summary>
        /// Computes column widths for non-VARCHAR(MAX) columns
        /// </summary>
        /// <param name="ServerName">The server name</param>
        /// <param name="DatabaseName">The database name</param>
        /// <param name="TableName">the table name</param>
        /// <param name="NonVarCharMaxCols">Columns that are not VARCHAR(MAX)</param>
        /// <param name="MaxWidth">The max width for output columns. If zero, then don't impose a maximum.</param>
        /// <returns>A new dictionary, derived from the passed dictionary, with column widths computed</returns>

        private static Dictionary<Column, int> ComputeNonVarcharMaxColWidths(string ServerName, string DatabaseName, string TableName, 
            Dictionary<Column, int> NonVarCharMaxCols, int MaxWidth)
        {
            if (NonVarCharMaxCols.Count == 0)
            {
                return NonVarCharMaxCols;
            }
            Dictionary<Column, int> NewCols = new Dictionary<Column, int>();
            foreach (Column Col in NonVarCharMaxCols.Keys)
            {
                switch (Col.DataType.Name.ToLower())
                {
                    case "varchar":
                    case "nvarchar":
                    case "char":
                    case "nchar":
                        int Len = Col.DataType.MaximumLength;
                        if (Len == -1 && MaxWidth == 0)
                        {
                            throw new ExportException("Max width not initialized for VARCHAR(MAX) field"); // just a guard - should never happen
                        }
                        if (MaxWidth != 0)
                        {
                            Len = Len == -1 ? MaxWidth : (Len > MaxWidth ? MaxWidth : Len);
                        }
                        NewCols.Add(Col, Len);
                        break;
                    case "decimal":
                    case "numeric":
                        NewCols.Add(Col, Col.DataType.NumericPrecision + 2); // +1 for decimal +1 for sign
                        break;
                    case "date":
                    case "datetime":
                        NewCols.Add(Col, 23); // 2017-01-01 12:31:31.000
                        break;
                    case "money":
                        NewCols.Add(Col, 21); // +-922,337,203,685,477.5808
                        break;
                    case "smallmoney":
                        NewCols.Add(Col, 12); // +-214,748.3647
                        break;
                    case "int":
                        NewCols.Add(Col, 11); // +-922,337,203,685,477.5808
                        break;
                    case "bigint":
                        NewCols.Add(Col, 20); // +-9,223,372,036,854,775,808
                        break;
                    case "bit":
                        NewCols.Add(Col, 1);
                        break;
                    case "tinyint":
                        NewCols.Add(Col, 3); // 255
                        break;
                    case "smallint":
                        NewCols.Add(Col, 6); // +-32,767
                        break;
                    case "uniqueidentifier":
                        NewCols.Add(Col, 36); // 6F9619FF-8B86-D011-B42D-00C04FC964FF
                        break;
                    default:
                        throw new ExportException("Unsupported data type: " + Col.DataType.Name);
                }
            }
            return NewCols;
        }

        /// <summary>
        /// Computes columns widths for character columns. Issues a select statement to get the widths from the database. This
        /// is necessary in order to generate a fixed-width file for a table containing varchar(max) columns and may optionally be
        /// performed for tables with CHAR(n) columns based on command line options.
        /// </summary>
        /// <param name="ServerName">The server name</param>
        /// <param name="DatabaseName">The database name</param>
        /// <param name="TableName">the table name</param>
        /// <param name="VarCharMaxCols">Columns that are VARCHAR(MAX)</param>
        /// <returns>A new dictionary, derived from the passed dictionary, with column widths computed</returns>

        private static Dictionary<Column, int> ComputeCharColWidths(string ServerName, string DatabaseName, string TableName,
            Dictionary<Column, int> VarCharMaxCols)
        {
            if (VarCharMaxCols.Count == 0)
            {
                return VarCharMaxCols;
            }
            string Sql = "select ";
            string Max = "max(len({0}))";
            int ColIdx = 0;
            foreach (Column Col in VarCharMaxCols.Keys)
            {
                Sql += ColIdx++ == 0 ? string.Empty : ", ";
                Sql += string.Format(Max, Col.Name.ToLower());
            }
            Sql += " from " + TableName;
            DataSet Ds = ExecSql(ServerName, DatabaseName, Sql);
            DataRow Row = Ds.Tables[0].Rows[0];
            ColIdx = 0;
            Dictionary<Column, int> NewCols = new Dictionary<Column, int>();
            foreach (Column Col in VarCharMaxCols.Keys)
            {
                int Len = int.Parse(Row[ColIdx++].ToString());
                NewCols.Add(Col, Len);
            }
            return NewCols;
        }

        /// <summary>
        /// Translates the data type in the passed server column to a string representation. E.g. "varchar(100), or "numeric(10,2)"
        /// </summary>
        /// <param name="Col">The server column</param>
        /// <returns>The string representation of the data type</returns>

        private static string XlatDatatype(Column Col)
        {
            if (Col.Computed)
            {
                return "as " + Col.ComputedText.Replace("[", string.Empty).Replace("]", string.Empty);
            }
            DataType Dt = Col.DataType;
            string DtName = Dt.Name.ToLower();
            if (DtName == "varchar" || DtName == "char" || DtName == "varbinary" ||
                DtName == "nvarchar" || DtName == "nchar" || DtName == "nvarbinary")
            {
                return Dt.Name + "(" + (Dt.MaximumLength == -1 ? "max" : Dt.MaximumLength.ToString()) + ")";
            }
            if (DtName == "decimal" || DtName == "numeric")
            {
                return Dt.Name + "(" + Dt.NumericPrecision + ", " + Dt.NumericScale + ")";
            }
            if (DtName == "date" || DtName == "datetime" || DtName == "money" || DtName == "int" || DtName == "bigint"
                || DtName == "bit" || DtName == "tinyint" || DtName == "float" || DtName == "smallint" || DtName == "text"
                || DtName == "uniqueidentifier" || DtName == "ntext")
            {
                return Dt.Name;
            }
            throw new Exception("Unhandled data type: " + Dt.Name);
        }

        /// <summary>
        /// Executes the passed statement as a non-query
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <param name="DatabaseName">The database</param>
        /// <param name="Sql">A SQL Statement</param>

        public static void ExecStatement(string ServerName, string DatabaseName, string Sql)
        {
            using (SqlConnection Cnct = GetSqlConnection(ServerName, DatabaseName))
            {
                SqlCommand command = new SqlCommand(Sql, Cnct);
                command.CommandTimeout = GetSQLTimeout();
                command.Connection.Open();
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Executes the passed statement as a query and returns the results
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <param name="DatabaseName">The database</param>
        /// <param name="Sql">A SQL Statement</param>
        /// <returns>A DataSet with the results</returns>

        public static DataSet ExecSql(string ServerName, string DatabaseName, string Sql)
        {
            using (SqlConnection Cnct = GetSqlConnection(ServerName, DatabaseName))
            using (SqlDataAdapter adapter = new SqlDataAdapter())
            using (adapter.SelectCommand = new SqlCommand(Sql, Cnct))
            using (DataSet dataset = new DataSet())
            {
                adapter.SelectCommand.CommandTimeout = GetSQLTimeout();
                adapter.Fill(dataset);
                return dataset;
            }
        }

        /// <summary>
        /// Translates the SQL timeout provided on the command line to seconds
        /// </summary>
        /// <returns>the timeout in seconds</returns>

        private static int GetSQLTimeout()
        {
            const float SECONDS_PER_HOUR = 60 * 60;
            return (int)(SECONDS_PER_HOUR * float.Parse(AppSettingsImpl.SQLTimeout.Value));
        }

        /// <summary>
        /// Builds a connection string and creates a new connection. The connection is not opened
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <param name="DatabaseName">The database. If null, the connection is just to the server, otherwise the database
        /// is specified in the initial catalog parameter of the connection string</param>
        /// <returns>The connection</returns>

        private static SqlConnection GetSqlConnection(string ServerName, string DatabaseName = null)
        {
            string CnctStr = string.Format("Data Source={0};Integrated Security=true;Connection Timeout={1}", ServerName, GetSQLTimeout());
            if (DatabaseName != null)
            {
                CnctStr += string.Format(";Initial Catalog={0}", DatabaseName);
            }
            return new SqlConnection(CnctStr);
        }

        /// <summary>
        /// Determines if the passed table exists in the passed database
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <param name="DatabaseName">The database</param>
        /// <param name="TableName">The table</param>
        /// <returns>True if the table exists, else false</returns>

        public static bool TableExists(string ServerName, string DatabaseName, string TableName)
        {
            using (SqlConnection SqlCnct = GetSqlConnection(ServerName, DatabaseName))
            {
                ServerConnection SrvrConn = new ServerConnection(SqlCnct);
                Server Srvr = new Server(SrvrConn);
                Database Db = Srvr.Databases[DatabaseName];
                string SchemaName = SchemaFromTableName(TableName);
                string TableNameWrk = TableFromTableName(TableName);
                return Db.Tables.Contains(TableNameWrk, SchemaName);
            }
        }

        /// <summary>
        /// Drops the passed table
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <param name="DatabaseName">The database</param>
        /// <param name="TableName">The table</param>

        public static void DropTable(string ServerName, string DatabaseName, string TableName)
        {
            using (SqlConnection SqlCnct = GetSqlConnection(ServerName, DatabaseName))
            {
                ServerConnection SrvrConn = new ServerConnection(SqlCnct);
                Server Srvr = new Server(SrvrConn);
                Database Db = Srvr.Databases[DatabaseName];
                string SchemaName = SchemaFromTableName(TableName);
                string TableNameWrk = TableFromTableName(TableName);
                if (!Db.Tables.Contains(TableNameWrk, SchemaName))
                {
                    Log.InformationMessage("Settings indicated to drop table '{0}', but it does not exist. Bypassing this step. (Not an error.)", TableName);
                    return;
                }
                Log.InformationMessage("Dropping table: {0}", TableName);
                Db.Tables[TableNameWrk, SchemaName].DropIfExists();
            }
        }

        /// <summary>
        /// Truncates the passed table
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <param name="DatabaseName">The database</param>
        /// <param name="TableName">The table</param>

        public static void TruncateTable(string ServerName, string DatabaseName, string TableName)
        {
            using (SqlConnection SqlCnct = GetSqlConnection(ServerName, DatabaseName))
            {
                ServerConnection SrvrConn = new ServerConnection(SqlCnct);
                Server Srvr = new Server(SrvrConn);
                Database Db = Srvr.Databases[DatabaseName];
                string SchemaName = SchemaFromTableName(TableName);
                string TableNameWrk = TableFromTableName(TableName);
                if (Db.Tables.Contains(TableNameWrk, SchemaName))
                {
                    Log.InformationMessage("Settings indicated to truncate table '{0}', but it does not exist. Bypassing this step. (Not an error.)", TableName);
                    return;
                }
                Log.InformationMessage("Truncating table: {0}", TableName);
                Db.Tables[TableNameWrk, SchemaName].TruncateData();
            }
        }


        /// <summary>
        /// Determines if the passed database name is valid
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <param name="DatabaseName">The database</param>
        /// <returns>True if the database exists on the server</returns>

        public static bool IsValidDatabaseName(string ServerName, string DatabaseName)
        {
            using (SqlConnection SqlCnct = GetSqlConnection(ServerName))
            {
                ServerConnection SrvrConn = new ServerConnection(SqlCnct);
                Server Srvr = new Server(SrvrConn);
                return Srvr.Databases.Contains(DatabaseName);
            }
        }

        /// <summary>
        /// Tests whether it is possible to connect to the passed server
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <returns>True if a connection can be established, else false</returns>

        public static bool CanConnect(string ServerName)
        {
            using (SqlConnection SqlCnct = GetSqlConnection(ServerName))
            {
                try
                {
                    SqlCnct.Open();
                }
                catch
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Determines whether the passed schema is valid in the passed database
        /// </summary>
        /// <param name="ServerName">The server</param>
        /// <param name="DatabaseName">The database</param>
        /// <param name="SchemaName">The schema name</param>
        /// <returns>True if the schema is valid, else false</returns>

        public static bool IsValidSchemaName(string ServerName, string DatabaseName, string SchemaName)
        {
            using (SqlConnection SqlCnct = GetSqlConnection(ServerName, DatabaseName))
            {
                ServerConnection SrvrConn = new ServerConnection(SqlCnct);
                Server Srvr = new Server(SrvrConn);
                Database Db = Srvr.Databases[DatabaseName];
                return Db.Schemas.Contains(SchemaName);
            }
        }

        /// <summary>
        /// Parses the table name - if it contains a schema prefix then returns it, else returns "dbo"
        /// </summary>
        /// <param name="TableName">Table name to parse</param>
        /// <returns>The schema from the passed table name, or "dbo" if the tables does not contain a schema specifier</returns>

        public static string SchemaFromTableName(string TableName)
        {
            string SchemaName = "dbo";
            if (TableName.Contains("."))
            {
                string[] tmp = TableName.Split('.');
                SchemaName = tmp[0];
                TableName = tmp[1];
            }
            return SchemaName;
        }

        /// <summary>
        /// Parses the table name and returns it. E.g. TableFromTableName("foo.bar") produces "foo"
        /// </summary>
        /// <param name="TableName">Table name to parse</param>
        /// <returns>The table portion it the passed table name contains a schema specifer, else simply returns the table name</returns>

        public static string TableFromTableName(string TableName)
        {
            if (TableName.Contains("."))
            {
                string[] tmp = TableName.Split('.');
                TableName = tmp[0];
            }
            return TableName;
        }
    }
}
