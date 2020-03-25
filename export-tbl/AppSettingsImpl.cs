using AppSettings;
using System.Collections.Generic;
using System.IO;

namespace export_tbl
{
    /// <summary>
    /// Extends the AppSettingsBase class with settings needed by the utility
    /// </summary>

    class AppSettingsImpl : AppSettingsBase
    {
        /// <summary>
        /// Specifies the target database to load the data into
        /// </summary>
        public static StringSetting Db { get { return (StringSetting)SettingsDict["Db"]; } }

        /// <summary>
        /// Specifies the target table to load the data into. Can be a bare table name, or in the
        /// form schema.tablename.
        /// </summary>
        public static StringSetting Tbl { get { return (StringSetting)SettingsDict["Tbl"]; } }

        /// <summary>
        /// Specifies the pathname of the file to export to
        /// </summary>
        public static StringSetting File { get { return (StringSetting)SettingsDict["File"]; } }

        /// <summary>
        /// "delimited" or "fixed"
        /// </summary>
        public static StringSetting Type { get { return (StringSetting)SettingsDict["Type"]; } }

        /// <summary>
        /// Specifies the server name (if not specified, localhost is used)
        /// </summary>
        public static StringSetting Server { get { return (StringSetting)SettingsDict["Server"]; } }

        /// <summary>
        /// Specifies the field delimiter to use
        /// </summary>
        public static StringSetting Delimiter { get { return (StringSetting)SettingsDict["Delimiter"]; } }

        /// <summary>
        /// Causes output fields to be quote-enclosed if they contain the specified delimiter
        /// </summary>
        public static BoolSetting Quote { get { return (BoolSetting)SettingsDict["Quote"]; } }

        /// <summary>
        /// Indicates that the utility should append to the specified output file if it already exists
        /// </summary>
        public static BoolSetting Append { get { return (BoolSetting)SettingsDict["Append"]; } }

        /// <summary>
        /// Specifies the max data rows in the source file to process
        /// </summary>
        public static IntSetting MaxRows { get { return (IntSetting)SettingsDict["MaxRows"]; } }

        /// <summary>
        /// Specifies a max column width
        /// </summary>
        public static IntSetting MaxWidth { get { return (IntSetting)SettingsDict["MaxWidth"]; } }

        /// <summary>
        /// Specifies a max column width
        /// </summary>
        public static IntSetting Pad { get { return (IntSetting)SettingsDict["Pad"]; } }

        /// <summary>
        /// Specifies the one-relative row in the file that contains a header.
        /// </summary>
        public static StringSetting Header { get { return (StringSetting)SettingsDict["Header"]; } }

        /// <summary>
        /// Directs the utility to display the column widths of the exported data file
        /// </summary>
        public static BoolSetting Widths { get { return (BoolSetting)SettingsDict["Widths"]; } }

        /// <summary>
        /// Directs the utility to output the minimum width on fixed width output
        /// </summary>
        public static BoolSetting Trim { get { return (BoolSetting)SettingsDict["Trim"]; } }

        /// <summary>
        /// Defines a SQL timeout in hours
        /// </summary>
        public static StringSetting SQLTimeout { get { return (StringSetting)SettingsDict["SQLTimeout"]; } }

        /// <summary>
        /// Logging target (e.g. file, database, console)
        /// </summary>
        public static StringSetting Log { get { return (StringSetting)SettingsDict["Log"]; } }

        /// <summary>
        /// Specifies which type of events are logged
        /// </summary>
        public static StringSetting LogLevel { get { return (StringSetting)SettingsDict["LogLevel"]; } }

        /// <summary>
        /// Specifies the Job ID
        /// </summary>
        public static StringSetting JobID { get { return (StringSetting)SettingsDict["JobID"]; } }

        /// <summary>
        /// Initializes the instance with an array of settings that the utility supports, as well as usage instructions
        /// </summary>

        static AppSettingsImpl()
        {
            SettingList = new Setting[] {
                new StringSetting("Db", "database", null, Setting.ArgTyp.Mandatory, true, false,
                    "The database to export from."),
                new StringSetting("Tbl", "schema.table", null,  Setting.ArgTyp.Mandatory, true, false,
                    "The table to export. If a dotted schema prefix is provided, then it is used, otherwise the table is accessed in the " +
                    "dbo schema."),
                new StringSetting("File", "filespec", null,  Setting.ArgTyp.Mandatory, true, false,
                    "The path name of the file to export to. Only a single file at a time is supported. If the file does not exist, it is " +
                    "created. If it exists, it can be replaced, or appended to. (See the -append arg.)"),
                new StringSetting("Type", "fixed|delimited", null,  Setting.ArgTyp.Mandatory, true, false,
                    "The export type. Allowed literals are 'fixed' and 'delimited'."),
                new StringSetting("Server", "server", "localhost",  Setting.ArgTyp.Optional, true, false,
                    "The server to export from. If not specified, localhost is used. Only Windows Integrated login is supported."),
                new StringSetting("Delimiter", "delim", "tab",  Setting.ArgTyp.Optional, true, false,
                    "Specifies the field delimiter for the output file. Allowed literals are 'pipe', 'comma', and " +
                    "'tab'. E.g. '-delimiter tab'. If not supplied, then tab is used as the default. Ignored unless -type " +
                    "is 'delimited'."),
                new BoolSetting("Quote", false, Setting.ArgTyp.Optional, true, false,
                    "Directs the utility to quote-enclose fields containing the specified delimiter. Should not be used for fixed " +
                    "field output. If a database field contains the specified delimiter - or a double-quote character - then the field will " +
                    "be enclosed in double quotes in the output file. (Double-quotes in data will in turn be quoted.)"),
                new BoolSetting("Append", false, Setting.ArgTyp.Optional, true, false,
                    "Causes the utility to append to the output file if it already exists. If not specified, then the output file - if it exists - is " +
                    "replaced. This option overrides the -header arg. I.e., if this option is specified, then no header is output. Note: the utility makes " +
                    "no effort to ensure that the appended data is in the same format as the existing data in the output file."),
                new IntSetting("MaxRows", "n", int.MaxValue, Setting.ArgTyp.Optional, true, false,
                    "Process at most n data rows from the table. (The count does not include the header.) The default is to " +
                    "export all rows. Note that the -trim option doesn't take this option into account so if this option is specified" +
                    "together with '-trim', the widths in the output will be still based on all the data in the table."),
                new IntSetting("MaxWidth", "n", null, Setting.ArgTyp.Optional, true, false,
                    "Ignored unless the output file type is fixed. " +
                    "Specifies a maximum column width to restrict varchar(max) fields. Useful in cases where a column may contain " +
                    "inconsequential data, but may have rows with excessively wide values, resulting in the output file becoming " +
                    "extremely large. By specifying this value, all varchar(max) columns whose width exceeds this value will be " +
                    "truncated to this size. Note: if this argument is specified, then the utility will not attempt to determine " +
                    "the column width of varchar(max) fields. This can substantially improve performance for large tables since " +
                    "the utility must otherwise execute a SQL query to determine the max column length of all varchar(max) fields " +
                    "in the source table - which can be expensive for large tables. However, specifying this option can result in truncation."),
                new IntSetting("Pad", "n", 0, Setting.ArgTyp.Optional, true, false,
                    "Adds n blanks to the end of every field (including the header.) Useful when testing new fixed-width exports. By adding padding " +
                    "character(s) to each field, the output file is more easily parsed visually since there is whitespace between each field in the " +
                    "output file. To elaborate: if a fixed with table is being exported and there are two adjacent columns are fixed width columns " +
                    "with alphanumeric output it could be hard to discern whether the data is exported correctly. Adding a single space can help there."),
                new StringSetting("Header", "trunc|fit", null, Setting.ArgTyp.Optional, true, false,
                    "Outputs a header line at the head of the exported file, using the column names of the exported table. If not " +
                    "specified, then no header is output. Valid values are 'trunc', and 'fit'. Note: these values come into " +
                    "effect only for fixed width output, and only in cases where the header name is wider than the data for the corresponding column. " +
                    "In this case, if 'trunc' is specified, then the header name is shortened and made unique to fit the data. If 'fit' is specified, then " +
                    "the data column is widened to fit the header name. Again - If the header name is shorter than the corresponding column data width, then has no effect. " +
                    "For delimited exports, the param value - while required - is ignored and the directive is taken as instruction to simply output a header. " +
                    "The header in this case is delimited in exactly the same way as the data."),
                new BoolSetting("Widths", false, Setting.ArgTyp.Optional, true, false,
                    "Ignored unless the export type is \"fixed\". Directs the utility to display the field widths of the generated file upon " +
                    "completion of the export. The widths are displayed as a comma-separated list of values. E.g.: \"10,4,1,20,255...\". Note: " +
                    "the -log setting controls where the widths appear (console, file, database.) The widths are displayed as an INFO-level message."),
                new BoolSetting("Trim", false, Setting.ArgTyp.Optional, true, false,
                    "Ignored unless the export type is \"fixed\". Directs the utility to determine the max width to output for char and varchar" +
                    "fields, and then set that width for all rows. For example, if a column width is defined as VARCHAR(255) then by default that will " +
                    "be the width in the output file. If this option is specified, then the utility will query the database for the maximum column width " +
                    "and set the output file width to that. Again - this may be expensive for large tables, but may result in a substantially smaller " +
                    "output file. Ignored if MaxWidth is specified."),
                new StringSetting("SQLTimeout", "hrs", "1",  Setting.ArgTyp.Optional, true, false,
                    "Specifies the command timeout for executing SQL commands against the server. The value specified is in hours. Decimal values are " +
                    "allowed (e.g. .25 for 15 minutes). If this arg is not provided, then one (1) hour is used as the default."),
                new StringSetting("Log", "file|db|con", "con",  Setting.ArgTyp.Optional, true, false,
                    "Determines how the utility communicates errors, status, etc. If not supplied, then all output goes to the console. " +
                    "If 'file' is specified, then the utility logs to a log file in the same directory that the utility is run from. " +
                    "The log file will be named load-file.log. " +
                    "If 'db' is specified, then logging occurs to the database. If 'con' is specified, then output goes to the console " +
                    "(same as if the arg were omitted.) If logging to file or db is specified then the utility runs silently " +
                    "with no console output. If db logging is specified, then the required logging components must be " +
                    "installed in the database. If the components are not installed and db logging is specified, then the utility " +
                    "will automatically fail over to file-based logging."),
                new StringSetting("LogLevel", "err|warn|info", "info",  Setting.ArgTyp.Optional, true, false,
                    "Defines the logging level. 'err' specifies that only errors will be reported. 'warn' means errors and warnings, " +
                    "and 'info' means all messages. The default is 'info'."),
                new StringSetting("JobID", "guid", null,  Setting.ArgTyp.Optional, true, false,
                    "Defines a job ID for the logging subsystem. A GUID value is supplied in the canonical 8-4-4-4-12 form. If provided, " +
                    "then the logging subsystem is initialized with the provided GUID. The default behavior is for the logging subsystem " +
                    "to generate its own GUID.")
            };

            Usage =
                "Exports a database table to a flat file. Supports fixed width and delimited exports. If fixed width export is used, " +
                "obtains the data definitions from the table and uses that to format the output data. For fixed width export, If the table to be " +
                "exported contains VARCHAR(MAX) fields, then the utility has to determine the maximum length for each such column as " +
                "a pre-processing step. For large tables, this can take some time.";
        }

        /// <summary>
        /// Performs custom arg validation for the utility, after invoking the base class parser.
        /// </summary>
        /// <param name="Settings">A settings instance to parse</param>
        /// <param name="CmdLineArgs">Command-line args array</param>
        /// <returns>True if args are valid, else False</returns>

        public new static bool Parse(SettingsSource Settings, string[] CmdLineArgs = null)
        {
            if (AppSettingsBase.Parse(Settings, CmdLineArgs))
            {
                if (!Type.Value.In("delimited", "fixed"))
                {
                    ParseErrorMessage = "Invalid value specified for the -type arg";
                    return false;
                }
                if (Header.Initialized && Type.Value == "fixed" && !Header.Value.In("trunc", "fit"))
                {
                    ParseErrorMessage = "Invalid value specified for the -header arg";
                    return false;
                }
                if (Tbl.Initialized && Tbl.Value.Contains("."))
                {
                    if (Tbl.Value.Split('.').Length != 2)
                    {
                        ParseErrorMessage = "Accepted table name forms are \"tablename\" and \"schemaname.tablename\"";
                        return false;
                    }
                }
                if (Delimiter.Initialized)
                {
                    if (!Delimiter.Value.In("pipe", "comma", "tab"))
                    {
                        ParseErrorMessage = "Invalid value specified for the -delimiter arg";
                        return false;
                    }
                }
                if (JobID.Initialized && !JobID.Value.IsGuid())
                {
                    ParseErrorMessage = "-jobid arg must be a GUID (nnnnnnnn-nnnn-nnnn-nnnn-nnnnnnnnnnnn)";
                    return false;
                }
                if (SQLTimeout.Initialized)
                {
                    float Timeout;
                    if (!float.TryParse(SQLTimeout.Value, out Timeout))
                    {
                        ParseErrorMessage = "Uable to parse the specified SQL timeout value as a decimal value: " + SQLTimeout.Value;
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Supports the ability to provide fixed field widths on the command line or from a file
        /// </summary>
        private class FieldWidthsListSetting : StringListSetting
        {
            /// <summary>
            /// Initializes the instance. Simply passes control to the parent class constructor
            /// </summary>
            /// <param name="Key"></param>
            /// <param name="ArgValHint"></param>
            /// <param name="DefaultValue"></param>
            /// <param name="Help"></param>

            public FieldWidthsListSetting(string Key, string ArgValHint, List<string> DefaultValue, ArgTyp ArgType, bool Persist, bool IsInternal, string Help)
                : base(Key, ArgValHint, DefaultValue, ArgType, Persist, IsInternal, Help) { }

            /// <summary>
            /// Accepts a value that is either a comma-separated list of DUNS numbers or in the form @filename in which
            /// filename is a file containing DUNS numbers
            /// </summary>
            /// <param name="Key"></param>
            /// <param name="Value"></param>
            /// <returns></returns>

            public override bool Accept(string Key, string Value)
            {
                if (Key.ToLower() == SettingKey.ToLower())
                {
                    if (Value != string.Empty && Value.Substring(0, 1) == "@")
                    {
                        using (StreamReader sr = new StreamReader(Value.Substring(1)))
                        {
                            while ((Value = sr.ReadLine()) != null)
                            {
                                SettingValue.AddRange(Value.Split(','));
                                SettingInitialized = true;
                            }
                        }
                    }
                    else
                    {
                        SettingValue.AddRange(Value.Split(','));
                        SettingInitialized = true;
                    }
                    SettingValue.RemoveAll(Str => string.IsNullOrEmpty(Str)); // ensure there are no empties
                    return true;
                }
                return false;
            }
        }
    }
}
