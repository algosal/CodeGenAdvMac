using System;
using System.IO;
using System.Text;

namespace CodeGenApp.Utils;

public static class NameSanitizer
{
    public static string MakeSafeName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "resource";

        var trimmed = input.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            trimmed = trimmed.Replace(c, '_');
        }

        // spaces to underscore
        trimmed = trimmed.Replace(' ', '_');
        return trimmed;
    }

    public static string ToPascalSafe(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "Lambda";

        var parts = input.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var p in parts)
        {
            var part = p.Trim();
            if (part.Length == 0) continue;

            sb.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1)
                sb.Append(part.Substring(1));
        }

        var result = sb.ToString();
        if (string.IsNullOrEmpty(result))
            result = "Lambda";

        // CloudFormation logical IDs cannot start with a number
        if (char.IsDigit(result[0]))
            result = "_" + result;

        return result;
    }
}
