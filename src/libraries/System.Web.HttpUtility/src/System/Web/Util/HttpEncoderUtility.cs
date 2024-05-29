// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace System.Web.Util
{
    internal static class HttpEncoderUtility
    {
        // Set of safe chars, from RFC 1738.4 minus '+'
        public static bool IsUrlSafeChar(char ch)
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                return true;
            }

            switch (ch)
            {
                case '-':
                case '_':
                case '.':
                case '!':
                case '*':
                case '(':
                case ')':
                    return true;
            }

            return false;
        }
    }
}
