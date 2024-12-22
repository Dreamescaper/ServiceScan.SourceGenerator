using System;

namespace ServiceScan.SourceGenerator.Extensions;

internal static class StringExtensions
{
    public static string ReplaceLineEndings(this string input)
    {
#if NET6_0_OR_GREATER
            return input.ReplaceLineEndings();
#else
#pragma warning disable RS1035 // Do not use APIs banned for analyzers
        return ReplaceLineEndings(input, Environment.NewLine);
#pragma warning restore RS1035 // Do not use APIs banned for analyzers
#endif
    }

    public static string ReplaceLineEndings(this string input, string replacementText)
    {
#if NET6_0_OR_GREATER
            return input.ReplaceLineEndings(replacementText);
#else
        // First normalize to LF
        var lineFeedInput = input
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");

        // Then normalize to the replacement text
        return lineFeedInput.Replace("\n", replacementText);
#endif
    }
}
