using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace CodeGenApp.Services;

public static class AwsOutputsParser
{
    // Matches AWS CLI describe-stacks --query "Stacks[0].Outputs" JSON
    private sealed class AwsOutputItem
    {
        public string? OutputKey { get; set; }
        public string? OutputValue { get; set; }
    }

    public sealed record ResourceArns(
        string Name,
        string CreateArn,
        string GetListArn,
        string GetOneArn,
        string UpdateDeleteArn,
        string DeletePermanentArn,
        string? AuthorizerArn // optional (you can also pass authorizer separately)
    );

    /// <summary>
    /// Parses a single stack's Outputs JSON array.
    /// Robust against accidental terminal noise like "(END)" or duplicated trailing text,
    /// by trimming everything before the first '[' and after the last ']'.
    /// </summary>
    public static ResourceArns ParseSingleTableOutputs(
        string awsOutputsJson,
        string resourceName // e.g. "CGAdv"
    )
    {
        if (string.IsNullOrWhiteSpace(awsOutputsJson))
            throw new ArgumentException("AWS outputs JSON is empty.");

        // ---- Robust cleanup: keep only the JSON array portion ----
        var cleaned = ExtractJsonArray(awsOutputsJson);

        List<AwsOutputItem>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<AwsOutputItem>>(
                cleaned,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Could not parse AWS Outputs JSON. Paste the full JSON array from AWS CLI (Outputs). " +
                "Tip: ensure it starts with '[' and ends with ']' (no '(END)' or extra lines).",
                ex
            );
        }

        if (items == null || items.Count == 0)
            throw new InvalidOperationException("AWS Outputs JSON did not contain any items.");

        var dict = items
            .Where(i => !string.IsNullOrWhiteSpace(i.OutputKey) && !string.IsNullOrWhiteSpace(i.OutputValue))
            .ToDictionary(
                i => i.OutputKey!.Trim(),
                i => i.OutputValue!.Trim(),
                StringComparer.OrdinalIgnoreCase
            );

        string GetRequiredArn(string keySuffix)
        {
            // Keys are like AGCodeGenLCreateArn, CGAdvLCreateArn, etc. => match by suffix
            var match = dict.FirstOrDefault(kv => kv.Key.EndsWith(keySuffix, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(match.Value))
                throw new InvalidOperationException($"Missing required OutputKey ending with '{keySuffix}'.");
            return match.Value;
        }

        string? GetOptionalArn(string keySuffix)
        {
            var match = dict.FirstOrDefault(kv => kv.Key.EndsWith(keySuffix, StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(match.Value) ? null : match.Value;
        }

        return new ResourceArns(
            Name: resourceName,
            CreateArn: GetRequiredArn("CreateArn"),
            GetListArn: GetRequiredArn("GetListArn"),
            GetOneArn: GetRequiredArn("GetOneArn"),
            UpdateDeleteArn: GetRequiredArn("UpdateDeleteArn"),
            DeletePermanentArn: GetRequiredArn("DeletePermanentArn"),
            AuthorizerArn: GetOptionalArn("AuthorizerArn")
        );
    }

    /// <summary>
    /// Extracts the first JSON array found in the input by taking substring from first '[' to last ']'.
    /// This prevents failures when the pasted text includes terminal noise like "(END)" or extra repeated blocks.
    /// </summary>
    private static string ExtractJsonArray(string input)
    {
        var s = (input ?? "").Trim();

        int start = s.IndexOf('[');
        int end = s.LastIndexOf(']');

        if (start < 0 || end < 0 || end <= start)
            throw new InvalidOperationException("Outputs JSON must contain a JSON array (start with '[' and end with ']').");

        return s.Substring(start, end - start + 1).Trim();
    }
}
