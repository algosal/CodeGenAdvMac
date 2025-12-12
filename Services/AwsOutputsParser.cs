using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeGenApp.Services;

public record CfnOutput(string OutputKey, string OutputValue);

public static class LogicalName
{
    // Keep only letters/digits; ensure it starts with a letter for CloudFormation logical IDs
    public static string Sanitize(string input)
    {
        var raw = (input ?? "").Trim();
        if (raw.Length == 0) return "Resource";

        var cleaned = new string(raw.Where(char.IsLetterOrDigit).ToArray());
        if (cleaned.Length == 0) cleaned = "Resource";

        if (!char.IsLetter(cleaned[0]))
            cleaned = "R" + cleaned;

        return cleaned;
    }
}

public static class OutputsParser
{
    /// <summary>
    /// Robust parser for AWS CLI "Stacks[0].Outputs" JSON.
    /// Accepts either:
    ///  - exact JSON array
    ///  - user paste that includes extra text before/after
    /// It extracts the first JSON array from the text and parses that.
    /// </summary>
    public static List<CfnOutput> Parse(string pastedText)
    {
        if (string.IsNullOrWhiteSpace(pastedText))
            throw new Exception("Outputs JSON is empty.");

        var jsonArrayText = ExtractFirstJsonArray(pastedText);

        try
        {
            using var doc = JsonDocument.Parse(jsonArrayText);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                throw new Exception("Outputs JSON must be a JSON array.");

            var list = new List<CfnOutput>();

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var key = el.TryGetProperty("OutputKey", out var k) ? (k.GetString() ?? "") : "";
                var val = el.TryGetProperty("OutputValue", out var v) ? (v.GetString() ?? "") : "";

                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(val))
                    list.Add(new CfnOutput(key.Trim(), val.Trim()));
            }

            if (list.Count == 0)
                throw new Exception("Outputs JSON parsed but contained no OutputKey/OutputValue pairs.");

            return list;
        }
        catch (JsonException jex)
        {
            throw new Exception("Invalid JSON. Paste the Outputs array exactly as returned by AWS CLI.", jex);
        }
    }

    public static string FindArnBySuffix(IEnumerable<CfnOutput> outputs, string suffix)
    {
        var match = outputs.FirstOrDefault(o =>
            o.OutputKey.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

        return match?.OutputValue ?? "";
    }

    /// <summary>
    /// Extract the first JSON array found in a blob of text.
    /// This fixes the exact problem you hit: "(END)" or duplicated output after the array.
    /// </summary>
    public static string ExtractFirstJsonArray(string text)
    {
        // Simple state machine: find first '[' then parse brackets until balanced.
        int start = text.IndexOf('[');
        if (start < 0)
            throw new Exception("Could not find '[' to start a JSON array. Paste the AWS CLI Outputs JSON array.");

        int depth = 0;
        bool inString = false;
        char prev = '\0';

        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];

            if (c == '"' && prev != '\\')
                inString = !inString;

            if (!inString)
            {
                if (c == '[') depth++;
                if (c == ']') depth--;

                if (depth == 0)
                {
                    var slice = text.Substring(start, i - start + 1).Trim();
                    return slice;
                }
            }

            prev = c;
        }

        throw new Exception("Could not extract a complete JSON array. Ensure you pasted the full Outputs array.");
    }
}
