using System;

namespace export_tbl
{
    class ExportException : Exception
    {
        public ExportException(string Message) : base(Message) { }
    }
}
