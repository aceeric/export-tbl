using System;

namespace export_tbl
{
    /// <summary>
    /// Various extension methods
    /// </summary>

    static class Extensions
    {
        /// <summary>
        /// Looks up a character in a char array and returns the corresponding string representation. E.g.
        /// 'x'.Xlat(new char[] { 'x', 'y', 'z' }, new string[] { "a", "b", "c" }) will produce string "a"
        /// </summary>
        /// <param name="this">this character</param>
        /// <param name="Lookup">the char array to search</param>
        /// <param name="ReplaceWith">the string to replace this character with</param>
        /// <returns>the replacement string, as described, or null if no match found</returns>

        public static string Xlat(this char @this, char[] Lookup, string [] ReplaceWith)
        {
            for (int i = 0; i < Lookup.Length; ++i)
            {
                if (@this == Lookup[i])
                {
                    return ReplaceWith[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Looks up a string in a string array and returns the corresponding character representation. E.g.
        /// "x".Xlat(new string[] { "x", "y", "z" }, new char[] { 'a', 'b', 'c' }) will produce character 'a'. A
        /// case-insensitive comparison is performed, but the translation character is returned exactly as is.
        /// </summary>
        /// <param name="this">this string</param>
        /// <param name="Lookup">the string array to search</param>
        /// <param name="ReplaceWith">the character to replace this string with</param>
        /// <returns>the replacement character, as described, or zero if no match found</returns>

        public static char Xlat(this string @this, string [] Lookup, char [] ReplaceWith)
        {
            for (int i = 0; i < Lookup.Length; ++i)
            {
                if (@this.ToLower() == Lookup[i].ToLower())
                {
                    return ReplaceWith[i];
                }
            }
            return (char) 0;
        }

        /// <summary>
        /// Looks up a string in a string array and returns the replacement from another array. E.g.
        /// "x".Xlat(new string[] { "x", "y", "z" }, new string[] { "a", "b", "c" }) will produce string "a". A
        /// case-insensitive comparison is performed, but the translation string is returned exactly as is.
        /// </summary>
        /// <param name="this">this string</param>
        /// <param name="Lookup">the string array to search</param>
        /// <param name="ReplaceWith">the string to replace this string with</param>
        /// <returns>the replacement string, as described, or null if no match found</returns>

        public static string Xlat(this string @this, string[] Lookup, string[] ReplaceWith)
        {
            for (int i = 0; i < Lookup.Length; ++i)
            {
                if (@this.ToLower() == Lookup[i].ToLower())
                {
                    return ReplaceWith[i];
                }
            }
            return null;
        }

        /// <summary>
        /// returns the rightmost number of characters from a string. E.g. "meetwo".right(3) produces "two"
        /// </summary>
        /// <param name="this">the string to process</param>
        /// <param name="Chars">number of characters from the right to return</param>
        /// <returns></returns>

        public static string Right(this string @this, int Chars)
        {
            if (@this.Length < Chars)
            {
                return @this;
            }
            else
            {
                return @this.Substring(@this.Length - Chars);
            }
        }


        /// <summary>
        /// returns the leftmost number of characters from a string. E.g. "twothree".left(3) produces "two"
        /// </summary>
        /// <param name="this">the string to process</param>
        /// <param name="Chars">number of characters from the right to return</param>
        /// <returns></returns>

        public static string Left(this string @this, int Chars)
        {
            if (@this.Length < Chars)
            {
                return @this;
            }
            else
            {
                return @this.Substring(0, Chars);
            }
        }

        /// <summary>
        /// Returns TRUE if the string is found in the specified list. This is a case-insensitive comparison
        /// </summary>
        /// <param name="this">string to search</param>
        /// <param name="List">list to search for this string in</param>
        /// <returns></returns>

        public static bool In(this string @this, params string[] List)
        {
            foreach (string s in List)
            {
                if (@this.ToLower() == s.ToLower())
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns TRUE if the string is a valid GUID. Otherwise returns FALSE
        /// </summary>
        /// <param name="this">The string to validate</param>
        /// <returns></returns>

        public static bool IsGuid(this string @this)
        {
            Guid TmpGuid;
            return Guid.TryParse(@this, out TmpGuid);
        }
    }
}
