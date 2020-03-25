using AppSettings;
using System;
using System.Collections.Generic;
using static export_tbl.Globals;

/*
 * TODO: show total rows in addition to time
 */

namespace export_tbl
{
    /// <summary>
    /// Defines the exit codes supported by the utility
    /// </summary>

    enum ExitCode : int
    {
        /// <summary>
        /// Indicates successful completion
        /// </summary>
        Success = 0,
        /// <summary>
        /// Indicates that invalid settings or command line args were provided
        /// </summary>
        InvalidParameters = 1,
        /// <summary>
        /// Indicates the source file specified by the user does not exist
        /// </summary>
        SrcFileDoesNotExist = 2,
        /// <summary>
        /// Indicates an inability to connect to the database server
        /// </summary>
        DBConnectFailed = 3,
        /// <summary>
        /// Indicates the supplied database name does not exist
        /// </summary>
        InvalidDatabaseName = 4,
        /// <summary>
        /// Indicates the schema name embedded in the table name does not exist
        /// </summary>
        InvalidSchemaName = 5,
        /// <summary>
        /// Indicates the bulk load failed
        /// </summary>
        LoadTableFailed = 6,
        /// <summary>
        /// Indicates the source file specified by the user does not exist
        /// </summary>
        ColFileDoesNotExist = 7,
        /// <summary>
        /// Indicates the source file is not a text file
        /// </summary>
        SrcFileIsNotText = 8,
        /// <summary>
        /// Indicates the colname file is not a text file
        /// </summary>
        ColFileIsNotText = 9,
        /// <summary>
        /// Indicates the column splitting file specified by the user does not exist
        /// </summary>
        SplitFileDoesNotExist = 10,
        /// <summary>
        /// Indicates the target table data types will not accommodate the incoming file data types
        /// </summary>
        TargetTableDataTypesNotCompatible = 11,
        /// <summary>
        /// Indicates the target table data types will not accommodate the incoming file data types
        /// </summary>
        CouldNotDetermineDelimiter = 12,
        /// <summary>
        /// Indicates that some other error occurred
        /// </summary>
        OtherError = 99
    }

    /// <summary>
    /// Entry point
    /// </summary>
    class Program
    {
        /// <summary>
        /// Entry point. Does initialization and then calls the DoWork method to actually do the work
        /// </summary>
        /// <param name="args">provided by .Net</param>

        static void Main(string[] args)
        {
#if DEBUG
            args = new string[] {
                "-file", @".\test.txt"
                , "-server", "localhost"
                , "-db", "mdb"
                , "-tbl", "lkp_fips"
                , "-type", "delimited"
//              , "-trim"
//              , "-delimiter", "comma"
//              , "-append"
                , "-quote"
//              , "-maxrows", "10"
//              , "-maxwidth", "255"
//              , "-pad", "1"
//              , "-header", "fit"
//              , "-widths"
                , "-sqltimeout", "2"
                , "-log", "con"
                , "-loglevel", "info"
            };
#endif
            try
            {
                if (!ParseArgs(args))
                {
                    Environment.ExitCode = (int)ExitCode.InvalidParameters;
                    return;
                }
                Log.InitLoggingSettings();
                Log.InformationMessage("Started");
                if (DoValidations())
                {
                    DoWork(); // sets the exit code
                }
                Log.InformationMessage("Normal completion");
            }
            catch (Exception Ex)
            {
                if (Ex is ExportException)
                {
                    Log.ErrorMessage(Ex.Message);
                }
                else
                {
                    Log.ErrorMessage("An unhandled exception occurred. The exception was: {0}. Stack trace follows:\n{1}", Ex.Message, Ex.StackTrace);
                }
                Environment.ExitCode = (int)ExitCode.OtherError;
            }
        }

        /// <summary>
        /// Executes a number of start-up validations and initializations. Emanates the appropriate error message
        /// and sets the ExitCode if unable to proceed
        /// </summary>
        /// <returns>true if the utility can proceed, else false: the utility is unable to proceed</returns>

        static bool DoValidations()
        {
            if (!ServerUtils.CanConnect(AppSettingsImpl.Server.Value))
            {
                Log.ErrorMessage("Unable to connect to the specified SQL Server: {0}", AppSettingsImpl.Server.Value);
                Environment.ExitCode = (int)ExitCode.DBConnectFailed;
                return false;
            }

            if (!ServerUtils.IsValidDatabaseName(AppSettingsImpl.Server.Value, AppSettingsImpl.Db.Value))
            {
                Log.ErrorMessage("Specified database does not exist on the server: {0}", AppSettingsImpl.Db.Value);
                Environment.ExitCode = (int)ExitCode.InvalidDatabaseName;
                return false;
            }

            string Schema = ServerUtils.SchemaFromTableName(AppSettingsImpl.Tbl.Value);
            if (!ServerUtils.IsValidSchemaName(AppSettingsImpl.Server.Value, AppSettingsImpl.Db.Value, Schema))
            {
                Log.ErrorMessage("Specified schema {0} is invalid in database {1}", Schema, AppSettingsImpl.Db.Value);
                Environment.ExitCode = (int)ExitCode.InvalidSchemaName;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Main worker method
        /// </summary>

        static void DoWork()
        {
            Dictionary<string, ColHeader> Cols = null;
            string ServerName = AppSettingsImpl.Server.Value;
            string DatabaseName = AppSettingsImpl.Db.Value;
            string TableName = AppSettingsImpl.Tbl.Value;
            string FileName = AppSettingsImpl.File.Value;
            bool Fixed = AppSettingsImpl.Type.Value.ToLower() == "fixed";
            int MaxWidth = AppSettingsImpl.MaxWidth.Initialized ? AppSettingsImpl.MaxWidth.Value : 0;

            DateTime Start = DateTime.Now;

            if (AppSettingsImpl.Type.Value.ToLower() == "fixed")
            {
                Log.InformationMessage("Computing column widths");
                bool FitToHeader = ShouldFitDataToHeader(); // if true, widen data. If false, truncat headers
                Cols = ServerUtils.GetColInfo(ServerName, DatabaseName, TableName, ShouldFitDataToHeader(), true, MaxWidth); // column names will be lower case
                if (!FitToHeader)
                {
                    Log.InformationMessage("Narrowing headers");
                    ColumnUniquer.NarrowHeaders(Cols); // modifies Cols
                }
            }
            else
            {
                Cols = ServerUtils.GetColInfo(ServerName, DatabaseName, TableName, false, false, MaxWidth);
            }
            string Delimiter = AppSettingsImpl.Delimiter.Value.Xlat(new string[] {"pipe", "comma", "tab"}, new string[] {"|", ",", "\t"});

            Log.InformationMessage("Exporting data");

            int Pad = AppSettingsImpl.Pad.Value;
            bool Append = AppSettingsImpl.Append.Value;
            bool ShouldWriteHeader = AppSettingsImpl.Header.Initialized && !Append;
            int MaxRows = AppSettingsImpl.MaxRows.Value;
            bool Quote = AppSettingsImpl.Quote.Value;
            int RecordsWritten;

            bool FoundDelimiterInData = ServerUtils.DoExport(ServerName, DatabaseName, TableName, FileName, Cols, Fixed,
                Delimiter, Pad, Append, ShouldWriteHeader, MaxRows, Quote, out RecordsWritten);

            if (!Fixed && FoundDelimiterInData && !Quote)
            {
                Log.InformationMessage("Note: The specified delimiter was found in the data, but the -quote option was not specified. " +
                    "The generated file may not be able to be successfully parsed.");
            }
            if (AppSettingsImpl.Type.Value == "fixed" && AppSettingsImpl.Widths.Value)
            {
                List<ColHeader> Hdrs = ColHeader.ToList(Cols);
                int[] Lengths = new int[Hdrs.Count];
                for (int i = 0; i < Hdrs.Count; ++i)
                {
                    Lengths[i] = Hdrs[i].FieldWidth;
                }
                Log.InformationMessage("Exported field widths: {0}", string.Join(",", Lengths));
            }
            Log.InformationMessage("Export completed -- Records written: {0} Elapsed time (HH:MM:SS.Milli): {1}", RecordsWritten, DateTime.Now - Start);
        }

        /// <summary>
        /// Determines whether the data should be fit to headers, or the headers should be truncated to fit the data
        /// </summary>
        /// <returns>True if the data should be expanded to fit the header column name width, else false to truncate
        /// header column names to fit the data</returns>
        static private bool ShouldFitDataToHeader()
        {
            return AppSettingsImpl.Header.Initialized && AppSettingsImpl.Header.Value.ToLower() == "fit";
        }

        /// <summary>
        /// Parses the command line args
        /// </summary>
        /// <param name="args">from .Net</param>
        /// <returns>true if the args parsed ok, else false. If false, prints the usage instructions</returns>

        static private bool ParseArgs(string[] args)
        {
            if (!AppSettingsImpl.Parse(SettingsSource.CommandLine, args))
            {
                if (AppSettingsImpl.ParseErrorMessage != null)
                {
                    Console.WriteLine(AppSettingsImpl.ParseErrorMessage);
                }
                else
                {
                    AppSettingsImpl.ShowUsage();
                }
                return false;
            }
            return true;
        }
    }
}
