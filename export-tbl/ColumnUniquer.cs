using System.Collections.Generic;

namespace export_tbl
{
    /// <summary>
    /// Handles making column names unique. In cases where column names might be truncated on output, this is necessary
    /// </summary>
    class ColumnUniquer
    {
        /// <summary>
        /// Narrows each header in the passed Dictionary to match the width of the dictionary entry. Replaces
        /// the dictionary with a new dictionary.
        /// </summary>
        /// <param name="Cols">A dictionary in which each key is a column name and each value
        /// is the max width of that column</param>

        public static void NarrowHeaders(Dictionary<string, ColHeader> Cols)
        {
            // first, narrow the headers. This could create dups
            foreach (string ColName in Cols.Keys)
            {
                Cols[ColName].TruncatedColName = ColName.Left(Cols[ColName].FieldWidth);
            }

            int Safety = 10000;
            // now try to shorten
            string[] TruncatedColNames = GetTruncatedColNames(Cols);
            while (new HashSet<string>(TruncatedColNames).Count != TruncatedColNames.Length)
            {
                foreach (string ColName in Cols.Keys)
                {
                    List<string> Dups;
                    if ((Dups = GetDups(Cols, ColName)).Count != 0)
                    {
                        MakeUnique(Cols, ColName, Dups);
                    }
                }
                if (--Safety < 0)
                {
                    throw new ExportException("Failed to narrow column headers");
                }
                TruncatedColNames = GetTruncatedColNames(Cols);
            }

            //Dictionary<string, ColHeader> NewCols = new Dictionary<string, ColHeader>();
            //foreach (string ColName in Cols.Keys)
            //{
            //    Cols[ColName].TruncatedColName = 
            //    //NewCols.Add(ColHeader, new ColHeader(ColHeader.Length);
            //}
            //Cols = NewCols;
        }

        private static string [] GetTruncatedColNames(Dictionary<string, ColHeader> Cols)
        {
            string[] Names = new string[Cols.Count];
            int i = 0;
            foreach (ColHeader Hdr in Cols.Values)
            {
                Names[i++] = Hdr.TruncatedColName;
            }
            return Names;
        }

        private static void MakeUnique(Dictionary<string, ColHeader> Cols, string DupColName, List<string> Dups)
        {
            //List<string> Uniques = GenerateUniques(Cols[DupColName].TruncatedColName, Dups.Count); // will populate with same # of entries as Dups has
            int Positions = DigitsNeeded(Dups.Count);
            if (Positions > DupColName.Length)
            {
                throw new ExportException("Headers are not long enough to make unique");
            }
            //int UniqueWidth = Uniques[0].Length;
            //int UniqueIndex = 0;
            int UniqueValue = 0;
            string Formatter = "{0:" + new string('0', Positions) + "}";

            foreach (string ColName in Dups)
            {
                string Unique = string.Format(Formatter, UniqueValue);
                string Hdr = Cols[ColName].TruncatedColName; // get the dup column name
                string LeftPart = Hdr.Left(Hdr.Length - Unique.Length);
                string RightPart = Unique;
                Cols[ColName].TruncatedColName = LeftPart + RightPart; // make the column name unique within this set (could create non-uniques, in turn)
                ++UniqueValue;
            }
        }

        //private static void MakeUnique(string[] ColHeaders, int Index, List<int> Dups)
        //{
        //    List<string> Uniques = GenerateUniques(ColHeaders[Index], Dups.Count); // will populate with same # of entries as Dups has
        //    int UniqueWidth = Uniques[0].Length;
        //    int UniqueIndex = 0;
        //    foreach (int i in Dups)
        //    {
        //        string Hdr = ColHeaders[i]; // get the dup column name
        //        string LeftPart = Hdr.Left(Hdr.Length - UniqueWidth);
        //        string RightPart = Uniques[UniqueIndex++]; // work through the uniques
        //        ColHeaders[i] = LeftPart + RightPart; // make the column name unique within this set (could create non-uniques, in turn)
        //    }
        //}

        /// <summary>
        /// Generates a list of suffixes to replace the right portion of the ToKeep arg. Does this by
        /// figuring out how many characters are needed to make Count unique strings, then generates the
        /// strings from the right portion of the ToKeep arg. E.g. if ToKeep is "colname" and Count is 10 
        /// then returns a List like {"mf", "mg", "mh", "mi", "mj", "mk", "ml", "mm", "mn", "mo"}
        /// </summary>
        /// <remarks>
        /// Uses 0-9 and a-z so its a base-36 numbering scheme e.g. if ToKeep is "xyz" and Count = 4,
        /// returns "0", "1", "2", "3" if count is large, like > 100, would return "00", "01", or if
        /// even larger, would return "000", "001", etc.
        /// </remarks>
        /// <param name="ToKeep">A column name that is duplicated with other column names</param>
        /// <param name="Count">The number of duplicated columns</param>
        /// <returns></returns>

        public static List<string> GenerateUniques(string ToKeep, int Count)
        {
            List<string> Uniques = new List<string>();

            int Positions = DigitsNeeded(Count);
            if (Positions > ToKeep.Length)
            {
                throw new ExportException("Header is not long enough to make unique");
            }
            char [] Unique = ToKeep.Right(Positions).ToCharArray();
            for (int i = 0; i < Count; ++i)
            {
                for (int j = Unique.Length - 1; j >= 0; --j)
                {
                    Unique[j] = IncrementWithWrap(Unique[j]);
                    if (Unique[j] != '0')
                    {
                        break;
                    }
                }
                Uniques.Add(new string(Unique));
            }
            return Uniques;
        }

        /// <summary>
        /// Figures out how many character positions are needed to make CountRequired unique strings. E.g.
        /// if CountRequired is 9999, then returns 4.
        /// </summary>
        /// <param name="CountRequired">The number of unique values needed</param>
        /// <returns>The number of digits required to represent that quantity of unique values. </returns>
        private static int DigitsNeeded(int CountRequired)
        {
            long Base = 10;
            int Digits = 1;
            while (true)
            {
                if (CountRequired < Base)
                {
                    break;
                }
                Base *= 10;
                Digits++;
            }
            return Digits;
        }

        /// <summary>
        /// "Increments" the passed character using the characters 0-9 and a-z. If the increment would produce
        /// 'z' + 1, then wraps to '0'.
        /// </summary>
        /// <param name="TheChar">The character to increment</param>
        /// <returns>The incremented, or wrapped, character</returns>

        private static char IncrementWithWrap(char TheChar)
        {
            char NewChar = (char)(TheChar + 1);
            NewChar = NewChar == ':' ? 'a' : NewChar; // 0-9 then a-z
            return char.IsLetterOrDigit(NewChar) ? NewChar : '0';
        }

        /// <summary>
        /// Finds all truncated column names in the passed dictionary that are duplicates of the truncated
        /// column name in the dictionary value keyed by the DupColName arg
        /// </summary>
        /// <param name="Cols">A dictionary. The key is the original column name. The value is a ColHeader
        /// instance that contains a truncated column name</param>
        /// <param name="DupColName">The dictionary key whose value contains a truncated column name that
        /// is duped by other entries in the dictionary.</param>
        /// <returns>A List of all the keys - except DupColName - of the dups</returns>

        private static List<string> GetDups(Dictionary<string, ColHeader> Cols, string DupColName)
        {
            List<string> Dups = new List<string>();
            foreach (string ColName in Cols.Keys)
            {
                if (ColName != DupColName && Cols[ColName].TruncatedColName == Cols[DupColName].TruncatedColName)
                {
                    Dups.Add(ColName);
                }
            }
            return Dups;
        }
    }
}
