using Microsoft.SqlServer.Management.Smo;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace export_tbl
{
    /// <summary>
    /// Provides functionality to deterine column types
    /// </summary>

    class ColHeader
    {
        public string ColName;
        public string TruncatedColName;
        public int FieldWidth;
        public DataType SqlDataType;
        public int OrdinalPosition;

        public ColHeader(string ColName, int FieldWidth, DataType SqlDataType, int OrdinalPosition)
        {
            this.ColName = ColName;
            this.FieldWidth = FieldWidth;
            this.SqlDataType = SqlDataType;
            this.OrdinalPosition = OrdinalPosition;
            if (AppSettingsImpl.Header.Initialized && AppSettingsImpl.Header.Value.ToLower() == "fit")
            {
                // if we're fitting the data to the header then, widen the field if the header is wider than the data
                this.FieldWidth = FieldWidth < ColName.Length ? ColName.Length : FieldWidth;
            }
        }

        public string ValueFrom(SqlDataReader reader, bool Fixed)
        {
            string FieldValue;
            if (reader.IsDBNull(OrdinalPosition))
            {
                return Fixed ? string.Empty.PadRight(FieldWidth) : string.Empty;
            }
            switch (SqlDataType.Name.ToLower())
            {
                case "varchar":
                case "nvarchar":
                case "char":
                case "nchar":
                    FieldValue = reader.GetString(OrdinalPosition);
                    break;
                case "decimal":
                case "numeric":
                    FieldValue = reader.GetDecimal(OrdinalPosition).ToString();
                    break;
                case "date":
                    FieldValue = reader.GetDateTime(OrdinalPosition).ToString("yyyy-MM-dd");
                    break;
                case "datetime":
                    FieldValue = reader.GetDateTime(OrdinalPosition).ToString("yyyy-MM-dd HH:mm:ss.fff");
                    break;
                case "money":
                    FieldValue = reader.GetSqlMoney(OrdinalPosition).ToDecimal().ToString();
                    break;
                case "smallmoney":
                    FieldValue = reader.GetSqlMoney(OrdinalPosition).ToDecimal().ToString();
                    break;
                case "int":
                case "tinyint":
                case "smallint":
                    FieldValue = reader.GetInt32(OrdinalPosition).ToString();
                    break;
                case "bigint":
                    FieldValue = reader.GetInt64(OrdinalPosition).ToString();
                    break;
                case "bit":
                    FieldValue = reader.GetInt32(OrdinalPosition) == 0 ? "0" : "1";
                    break;
                case "uniqueidentifier":
                    FieldValue = reader.GetGuid(OrdinalPosition).ToString();
                    break;
                default:
                    throw new ExportException("Unsupported data type: " + SqlDataType.Name);
            }
            return Fixed ? FieldValue.PadRight(FieldWidth).Left(FieldWidth) : FieldValue.Trim();
        }


        /// <summary>
        /// Returns a List of ColHeader instances from the passed dictionary. The list is sorted in order of the
        /// OrdinalPosition field of the ColHeader class in the dictionary.
        /// </summary>
        /// <param name="Cols">A dictionary from which to extract the List</param>
        /// <returns>The List, in OrdinalPosition order</returns>
        public static List<ColHeader> ToList(Dictionary<string, ColHeader> Cols)
        {
            List<ColHeader> Headers = new List<ColHeader>();
            foreach (string Key in Cols.Keys)
            {
                Headers.Add(Cols[Key]);
            }
            return Headers.OrderBy(h => h.OrdinalPosition).ToList();
        }


    }
}
