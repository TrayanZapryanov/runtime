// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace System.Web.Util
{
    internal static class UriUtil
    {
        // Just extracts the query string and fragment from the input path by splitting on the separator characters.
        // Doesn't perform any validation as to whether the input represents a valid URL.
        // Concatenating the pieces back together will form the original input string.
        private static void ExtractQueryAndFragment(string input, out Range pathRange, out Range queryAndFragmentRange)
        {
            int queryFragmentSeparatorPos = input.AsSpan().IndexOfAny('?', '#'); // query fragment separators
            if (queryFragmentSeparatorPos >= 0)
            {
                pathRange = new Range(Index.Start, Index.FromStart(queryFragmentSeparatorPos));
                queryAndFragmentRange = new Range(Index.FromStart(queryFragmentSeparatorPos), Index.End);
            }
            else
            {
                // no query or fragment separator
                pathRange = Range.All;
                queryAndFragmentRange = default(Range);
            }
        }

        // Attempts to split a URI into its constituent pieces.
        // Even if this method returns true, one or more of the out parameters might contain a null or empty string, e.g. if there is no query / fragment.
        // Concatenating the pieces back together will form the original input string.
        internal static bool TrySplitUriForPathEncode(string input, out Range schemeAndAuthority, out Range path, out Range queryAndFragment)
        {
            // Strip off ?query and #fragment if they exist, since we're not going to look at them
            Range inputWithoutQueryFragmentRange;
            ExtractQueryAndFragment(input, out inputWithoutQueryFragmentRange, out queryAndFragment);

            // Use Uri class to parse the url into authority and path, use that to help decide
            // where to split the string. Do not rebuild the url from the Uri instance, as that
            // might have subtle changes from the original string (for example, see below about "://").
            ReadOnlySpan<char> inputWithoutQueryFragmentSpan = input.AsSpan(inputWithoutQueryFragmentRange);
            if (Uri.TryCreate(inputWithoutQueryFragmentSpan.ToString(), UriKind.Absolute, out Uri? uri))
            {
                string authority = uri.Authority; // e.g. "foo:81" in "http://foo:81/bar"
                if (!string.IsNullOrEmpty(authority))
                {
                    // don't make any assumptions about the scheme or the "://" part.
                    // For example, the "//" could be missing, or there could be "///" as in "file:///C:\foo.txt"
                    // To retain the same string as originally given, find the authority in the original url and include
                    // everything up to that.
                    int authorityIndex = inputWithoutQueryFragmentSpan.IndexOf(authority, StringComparison.OrdinalIgnoreCase);
                    if (authorityIndex != -1)
                    {
                        int schemeAndAuthorityLength = authorityIndex + authority.Length;
                        schemeAndAuthority = new Range(inputWithoutQueryFragmentRange.Start, Index.FromStart(schemeAndAuthorityLength));
                        path = new Range(Index.FromStart(schemeAndAuthorityLength), inputWithoutQueryFragmentRange.End);
                        return true;
                    }
                }
            }

            // Not a safe URL
            schemeAndAuthority = default(Range);
            path = default(Range);
            queryAndFragment = default(Range);
            return false;
        }
    }
}
