# export-tbl

A C# console  utility to export tables from SQL Server using a command line interface, and with a minimum of hassle.

The basic use case is:

`export-tbl -db my_db -tbl my_table -file .\my_table.tsv -type delimited`

Or:

`export-tbl -db my_db -tbl my_table -file .\my_table.txt -type fixed`

The goal is to get data out of the database table and into a flat file as easily as possible, but also to handle a few special cases. The functionality of the utility is controlled by the command line options:

**-db database**

Mandatory. The database to export from.

**-tbl [schema.]table**

Mandatory. The table to export. If a dotted schema prefix is provided, then it is used, otherwise the table is accessed in the `dbo` schema.

**-file filespec**

Mandatory. The path name of the file to export to. Only a single file at a time is supported. If the file does not exist, it is created. If it exists, it can be replaced, or appended to. (See the `-append` option.)

**-type fixed|delimited**

Mandatory. The export type. Allowed literals are `fixed` and `delimited`.

**-server server**

Optional. The server to export from. If not specified, `localhost` is used. Only Windows Integrated login is supported.

**-delimiter pipe|comma|tab**

Optional. Specifies the field delimiter for the output file. Allowed literals are `pipe`, `comma`, and `tab`. E.g.: `-delimiter tab`. If not supplied, then `tab` is used as the default. Ignored unless `-type` is `delimited`.

**-quote**

Optional. Directs the utility to quote-enclose fields containing the specified delimiter. Should not be used for fixed field output. If a database field contains the specified delimiter - or a double-quote character - then the field will be enclosed in double quotes in the output file. (Double-quotes in data will in turn be quoted.) The goal is to make the exported data loadable by a TSV/CSV reader.

**-append**

Optional. Causes the utility to append to the output file if it already exists. If not specified, then the output file - if it exists - is replaced. This option overrides the `-header` option, meaning if this option is specified, then no header is output. Note: the utility makes no effort to ensure that the appended data is in the same format as the existing data in the output file.

**-maxrows n**

Optional. Process at most `n` data rows from the table. (The count does not include the header.) The default is to export all rows. Note that the `-trim` option doesn't take this option into account when it is determining column widths in the table, so if this option is specified together with `-trim`, the widths in the output will be still based on *all* the data in the table.

**-maxwidth n**

Optional. Ignored unless the output file type is `fixed`. Specifies a maximum column width to restrict `varchar(max)` fields. Useful in cases where a column may contain inconsequential data, but may have rows with excessively wide values, resulting in the output file becoming extremely large. By specifying this value, all `varchar(max)` columns whose width exceeds this value will be truncated to this size. Note: if this argument is specified, then the utility will not attempt to determine the column width of `varchar(max)` fields. This can substantially improve performance for large tables since the utility must otherwise execute a SQL query to determine the max column length of all `varchar(max)` fields in the source table - which can be expensive for large tables. However, specifying this option can result in truncation.

**-pad n**

Optional. Adds `n` blanks to the end of every field in the output file (including the header.) Useful when testing new fixed-width exports. By adding padding character(s) to each field, the output file is more easily parsed visually since there is whitespace between each field in the output file. To elaborate: if a fixed-width table is being exported and there are two adjacent columns are fixed width columns with alphanumeric values filling the entire columns, it could be hard to discern whether the data is exported correctly. Adding a single space can help there.

**-header trunc|fit**

Optional. Outputs a header line at the head of the exported file, using the column names of the exported table. If not specified, then no header is output. Valid values are `trunc`, and `fit`. Note: these values have effect only for fixed width output, and only in cases where the header name is wider than the data for the corresponding column. In this case, if `trunc` is specified, then the header name is shortened and made unique to fit the data. E.g. `some_long_state_name_column` in the database table might be shortened to  `some_long_st` in the output file assuming *Rhode Island* is the longest value in that column. If `fit` is specified, then the data column is widened to fit the header name. Again - If the header name is shorter than the corresponding column data width, then this option has no effect.

For delimited exports, the param value - while required - is ignored and the directive is taken as instruction to simply output a header. The header in this case is delimited in exactly the same way as the data.

**-widths**

Optional. Ignored unless the export type is `fixed`. Directs the utility to display the field widths of the generated file upon completion of the export. The widths are displayed as a comma-separated list of values. E.g.: "10,4,1,20,255...". Note: the `-log` setting controls where the widths appear (console, file, database.) The widths are displayed as an INFO-level message. This option is intended to be used if a flat-file loader will subsequently be used to load the generated file into another table, database, or server.

**-trim**

Optional. Ignored unless the export type is `fixed`. Directs the utility to determine the max width to output for char and varchar fields based on the data, and then set that width for all rows. For example, if a column width is defined as `varchar(255)` then by default that will be the width in the output file. If this option is specified, then the utility will query the database for the maximum width of the values in that column, and then set the output file width to that, rather than using the DDL width. Again - this may be expensive for large tables, but may result in a substantially smaller output file. Ignored if `-maxwidth` is specified.

**-sqltimeout hrs**

Optional. Specifies the command timeout for executing SQL commands against the server. The value specified is in hours. Decimal values are allowed (e.g. .25 for 15 minutes). If this option is not provided, then one (1) hour is used as the default.

**-jobid guid**

Optional. Defines a job ID for the logging subsystem. A GUID value is supplied in the canonical 8-4-4-4-12 form. If provided, then the logging subsystem is initialized with the provided GUID. The default behavior is for the logging subsystem to generate its own GUID.

**-log file|db|con**

Optional. Determines how the application communicates errors, status, etc. If not supplied, then all output goes to the console. If `file` is specified, then the application logs to a log file in the application directory called `export-tbl.log`. If `db` is specified, then logging occurs to the database. If `con` is specified, then output goes to the console (same as if the option were omitted.) If logging to file or database is specified then the application runs silently with no console output.

Note: the C# utilizing this option requires the inclusion of my logging DLL which is also in GitHub: https://github.com/aceeric/logger.

**-loglevel err|warn|info**

Optional. Defines the logging level. `err` specifies that only errors will be reported. `warn` means errors and warnings, and `info` means all messages. The default is `info`.

### Examples:

Here is `.cmd` file to export a FIPS lookup to a CSV:

```DOS
@echo off
export-tbl ^
 -file ".\lkp-fips-csv.txt" ^
 -server localhost ^
 -db mdb ^
 -tbl lkp_fips ^
 -type delimited ^
 -delimiter comma ^
 -quote
```

Here is an example exporting the same table to a fixed width file:

```
@echo off
export-tbl ^
 -file ".\lkp-fips-fixed.txt" ^
 -server localhost ^
 -db mdb ^
 -tbl lkp_fips ^
 -type fixed ^
 -widths ^
 -header fit ^
 -trim ^
 -pad 2
```

The example above will also output the exported widths to the console. Output:

```
Started
Computing column widths
Exporting data
Records: 10000
Records: 20000
Records: 30000
Records: 40000
Exported field widths: 11,10,10,11,5,9,59,34
Export completed -- Records written: 41844 Elapsed time (HH:MM:SS.Milli): 00:00:00.4655194
Normal completion
```

Note - this utility uses my command-line parser `appsettings` for option parsing and displaying usage instructions. That is a DLL project also hosted in GitHub: https://github.com/aceeric/appsettings.

