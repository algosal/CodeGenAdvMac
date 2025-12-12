using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Controls;

namespace CodeGenApp;

public partial class MainPage : ContentPage
{
    private string _rootPath = "";
    public ObservableCollection<TablePackInput> Packs { get; } = new();

    public MainPage()
    {
        InitializeComponent();

        _rootPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        RootPathLabel.Text = $"Root: {_rootPath}";

        ApiNameEntry.Text = "AlbaGold-MultiTable-API";
        StageNameEntry.Text = "prod";

        AwsRegionEntry.Text = "us-east-2";

        Packs.Add(new TablePackInput(1));
        BindingContext = this;

        CommandsEditor.Text =
@"Use these buttons to copy commands.
1) Copy Outputs JSON command → run in Terminal → paste JSON into Pack #1
2) Generate template
3) Copy sam commands → run in Terminal";
    }

    private void OnAddPackClicked(object sender, EventArgs e)
    {
        Packs.Add(new TablePackInput(Packs.Count + 1));
        StatusEditor.Text = $"Added Pack #{Packs.Count}.";
    }

    private void OnRemovePackClicked(object sender, EventArgs e)
    {
        if (Packs.Count == 0)
        {
            StatusEditor.Text = "No packs to remove.";
            return;
        }

        Packs.RemoveAt(Packs.Count - 1);

        for (int i = 0; i < Packs.Count; i++)
            Packs[i].SetIndex(i + 1);

        StatusEditor.Text = $"Removed last pack. Remaining packs: {Packs.Count}.";
    }

    // -----------------------------------------
    // COMMAND GENERATOR BUTTONS (no AWS calls)
    // -----------------------------------------

    private async void OnCopyDescribeOutputsCommandClicked(object sender, EventArgs e)
    {
        var stackName = (StackNameEntry.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(stackName))
        {
            StatusEditor.Text = "Enter a CloudFormation stack name first.";
            return;
        }

        var region = (AwsRegionEntry.Text ?? "").Trim();

        var cmd = string.IsNullOrWhiteSpace(region)
            ? $"aws cloudformation describe-stacks --stack-name {EscapeShellArg(stackName)} --query \"Stacks[0].Outputs\" --output json"
            : $"aws cloudformation describe-stacks --stack-name {EscapeShellArg(stackName)} --region {EscapeShellArg(region)} --query \"Stacks[0].Outputs\" --output json";

        await Clipboard.Default.SetTextAsync(cmd);

        CommandsEditor.Text = cmd;
        StatusEditor.Text = "Copied Outputs JSON command to clipboard. Paste into Terminal.";
    }

    private async void OnCopyListLambdasCommandClicked(object sender, EventArgs e)
    {
        var stackName = (StackNameEntry.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(stackName))
        {
            StatusEditor.Text = "Enter a CloudFormation stack name first.";
            return;
        }

        var region = (AwsRegionEntry.Text ?? "").Trim();

        var cmd = string.IsNullOrWhiteSpace(region)
            ? $"aws cloudformation list-stack-resources --stack-name {EscapeShellArg(stackName)} --query \"StackResourceSummaries[?ResourceType=='AWS::Lambda::Function'].[LogicalResourceId,PhysicalResourceId]\" --output table"
            : $"aws cloudformation list-stack-resources --stack-name {EscapeShellArg(stackName)} --region {EscapeShellArg(region)} --query \"StackResourceSummaries[?ResourceType=='AWS::Lambda::Function'].[LogicalResourceId,PhysicalResourceId]\" --output table";

        await Clipboard.Default.SetTextAsync(cmd);

        CommandsEditor.Text = cmd;
        StatusEditor.Text = "Copied List Lambdas command to clipboard. Paste into Terminal.";
    }

    private async void OnCopyGetLambdaArnCommandClicked(object sender, EventArgs e)
    {
        var lambdaName = (LambdaNameEntry.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(lambdaName))
        {
            StatusEditor.Text = "Enter a Lambda Function Name first (PhysicalResourceId from the list-lambdas command).";
            return;
        }

        var region = (AwsRegionEntry.Text ?? "").Trim();

        var cmd = string.IsNullOrWhiteSpace(region)
            ? $"aws lambda get-function --function-name {EscapeShellArg(lambdaName)} --query \"Configuration.FunctionArn\" --output text"
            : $"aws lambda get-function --function-name {EscapeShellArg(lambdaName)} --region {EscapeShellArg(region)} --query \"Configuration.FunctionArn\" --output text";

        await Clipboard.Default.SetTextAsync(cmd);

        CommandsEditor.Text = cmd;
        StatusEditor.Text = "Copied Get Lambda ARN command to clipboard. Paste into Terminal.";
    }

    private async void OnCopySamCommandsClicked(object sender, EventArgs e)
    {
        var docsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var outDir = Path.Combine(docsDir, "Output");

        var cmd =
$@"cd ""{outDir}""
sam build
sam deploy --guided";

        await Clipboard.Default.SetTextAsync(cmd);

        CommandsEditor.Text = cmd;
        StatusEditor.Text = "Copied SAM commands to clipboard. Paste into Terminal.";
    }

    private static string EscapeShellArg(string value)
    {
        // Simple shell-safe quoting for common cases
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        // Wrap in quotes; escape internal quotes
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    // -----------------------------------------
    // Existing: Generate API-only template
    // -----------------------------------------
    private async void OnGenerateApiTemplateClicked(object sender, EventArgs e)
    {
        try
        {
            var apiName = (ApiNameEntry.Text ?? "").Trim();
            var stageName = (StageNameEntry.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(apiName))
                apiName = "AlbaGold-MultiTable-API";

            if (string.IsNullOrWhiteSpace(stageName))
                stageName = "prod";

            if (Packs.Count == 0)
            {
                StatusEditor.Text = "Add at least 1 pack.";
                return;
            }

            var parsedPacks = new List<ParsedPack>();

            foreach (var pack in Packs)
            {
                var basePath = (pack.BasePath ?? "").Trim();
                if (string.IsNullOrWhiteSpace(basePath))
                    basePath = "/pack" + pack.Index;

                if (!basePath.StartsWith("/"))
                    basePath = "/" + basePath;

                var outputsJson = (pack.OutputsJson ?? "").Trim();
                if (string.IsNullOrWhiteSpace(outputsJson))
                {
                    StatusEditor.Text = $"{pack.Title}: Outputs JSON is empty.";
                    return;
                }

                var outputs = OutputsParser.Parse(outputsJson);

                string createArn = OutputsParser.FindArnBySuffix(outputs, "CreateArn");
                string getListArn = OutputsParser.FindArnBySuffix(outputs, "GetListArn");
                string getOneArn = OutputsParser.FindArnBySuffix(outputs, "GetOneArn");
                string updateDeleteArn = OutputsParser.FindArnBySuffix(outputs, "UpdateDeleteArn");
                string deletePermanentArn = OutputsParser.FindArnBySuffix(outputs, "DeletePermanentArn");

                var missing = new List<string>();
                if (string.IsNullOrWhiteSpace(createArn)) missing.Add("CreateArn");
                if (string.IsNullOrWhiteSpace(getListArn)) missing.Add("GetListArn");
                if (string.IsNullOrWhiteSpace(getOneArn)) missing.Add("GetOneArn");
                if (string.IsNullOrWhiteSpace(updateDeleteArn)) missing.Add("UpdateDeleteArn");
                if (string.IsNullOrWhiteSpace(deletePermanentArn)) missing.Add("DeletePermanentArn");

                if (missing.Count > 0)
                {
                    StatusEditor.Text =
                        $"{pack.Title}: Missing OutputKey(s) ending with: {string.Join(", ", missing)}";
                    return;
                }

                string packAuthArn = OutputsParser.FindArnBySuffix(outputs, "AuthorizerArn");

                parsedPacks.Add(new ParsedPack
                {
                    Title = pack.Title,
                    BasePath = basePath,
                    CreateArn = createArn,
                    GetListArn = getListArn,
                    GetOneArn = getOneArn,
                    UpdateDeleteArn = updateDeleteArn,
                    DeletePermanentArn = deletePermanentArn,
                    PackAuthorizerArn = packAuthArn
                });
            }

            var authorizerArn = (AuthorizerArnEntry.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(authorizerArn))
            {
                authorizerArn = parsedPacks
                    .Select(p => p.PackAuthorizerArn)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "";
            }

            if (string.IsNullOrWhiteSpace(authorizerArn))
            {
                StatusEditor.Text =
                    "Authorizer ARN is required. Paste it in the Authorizer ARN field or ensure Pack #1 outputs includes AuthorizerArn.";
                return;
            }

            var generator = new ApiOnlySamTemplateGenerator();
            var yaml = generator.Generate(apiName, stageName, authorizerArn, parsedPacks);

            var docsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var outDir = Path.Combine(docsDir, "Output");
            Directory.CreateDirectory(outDir);

            var outPath = Path.Combine(outDir, "template.yaml");
            File.WriteAllText(outPath, yaml, Encoding.UTF8);

            StatusEditor.Text =
                $"Generated template-api.yaml\n\nPath:\n{outPath}\n\nNext:\n1) sam build\n2) sam deploy --guided";

            await DisplayAlert("Done", $"Generated:\n{outPath}", "OK");
        }
        catch (Exception ex)
        {
            StatusEditor.Text = "ERROR:\n" + ex;
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }
}

// -------------------------------
// Models / Helpers
// -------------------------------
public class TablePackInput : INotifyPropertyChanged
{
    private int _index;
    private string _title = "";
    private string _basePath = "";
    private string _outputsJson = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Index
    {
        get => _index;
        private set { _index = value; OnPropertyChanged(); }
    }

    public string Title
    {
        get => _title;
        private set { _title = value; OnPropertyChanged(); }
    }

    public string BasePath
    {
        get => _basePath;
        set { _basePath = value; OnPropertyChanged(); }
    }

    public string OutputsJson
    {
        get => _outputsJson;
        set { _outputsJson = value; OnPropertyChanged(); }
    }

    public TablePackInput(int index) => SetIndex(index);

    public void SetIndex(int index)
    {
        Index = index;
        Title = $"Pack #{index}";
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class ParsedPack
{
    public string Title { get; set; } = "";
    public string BasePath { get; set; } = "";

    public string CreateArn { get; set; } = "";
    public string GetListArn { get; set; } = "";
    public string GetOneArn { get; set; } = "";
    public string UpdateDeleteArn { get; set; } = "";
    public string DeletePermanentArn { get; set; } = "";

    public string? PackAuthorizerArn { get; set; }
}

public record CfnOutput(string OutputKey, string OutputValue);

public static class OutputsParser
{
    public static List<CfnOutput> Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

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
}

public class ApiOnlySamTemplateGenerator
{
    public string Generate(string apiName, string stageName, string authorizerArn, List<ParsedPack> packs)
    {
        var apiLogicalId = "MultiTableApi";

        var pathsYaml = BuildPathsYaml(packs);
        var permsYaml = BuildPermissionsYaml(apiLogicalId, authorizerArn, packs);

        var yaml = $@"
AWSTemplateFormatVersion: '2010-09-09'
Transform: AWS::Serverless-2016-10-31
Description: API-only SAM template. Attaches existing Lambda ARNs to a single API with a Lambda Authorizer.

Resources:
  {apiLogicalId}:
    Type: AWS::Serverless::Api
    Properties:
      Name: {EscapeScalar(apiName)}
      StageName: {EscapeScalar(stageName)}
      Cors:
        AllowMethods: ""'GET,POST,PUT,DELETE,OPTIONS'""
        AllowHeaders: ""'Content-Type,Authorization,X-Requested-With'""
        AllowOrigin: ""'*'""
      DefinitionBody:
        openapi: 3.0.1
        info:
          title: {EscapeScalar(apiName)}
          version: '1.0'
        components:
          securitySchemes:
            LambdaAuth:
              type: apiKey
              name: Authorization
              in: header
              x-amazon-apigateway-authorizer:
                type: request
                identitySource: method.request.header.Authorization
                authorizerUri:
                  Fn::Sub: arn:aws:apigateway:${{AWS::Region}}:lambda:path/2015-03-31/functions/{authorizerArn}/invocations
                authorizerResultTtlInSeconds: 0
        security:
          - LambdaAuth: []
        paths:
{Indent(pathsYaml, 10)}

{permsYaml}

Outputs:
  ApiUrl:
    Description: Base URL for this API
    Value:
      Fn::Sub: https://${{{apiLogicalId}}}.execute-api.${{AWS::Region}}.amazonaws.com/{stageName}
".TrimStart('\n', '\r');

        return yaml;
    }

    private static string BuildPathsYaml(List<ParsedPack> packs)
    {
        var sb = new StringBuilder();

        foreach (var p in packs)
        {
            var basePath = p.BasePath.Trim();
            if (!basePath.StartsWith("/")) basePath = "/" + basePath;

            sb.AppendLine($"{basePath}:");
            sb.AppendLine("  options:");
            sb.AppendLine("    x-amazon-apigateway-integration:");
            sb.AppendLine("      type: mock");
            sb.AppendLine("      requestTemplates:");
            sb.AppendLine("        application/json: '{\"statusCode\": 200}'");
            sb.AppendLine("      responses:");
            sb.AppendLine("        default:");
            sb.AppendLine("          statusCode: '200'");
            sb.AppendLine("      passthroughBehavior: when_no_match");

            sb.AppendLine("  post:");
            sb.AppendLine("    security:");
            sb.AppendLine("      - LambdaAuth: []");
            sb.AppendLine("    x-amazon-apigateway-integration:");
            sb.AppendLine("      httpMethod: POST");
            sb.AppendLine("      type: aws_proxy");
            sb.AppendLine("      uri:");
            sb.AppendLine($"        Fn::Sub: arn:aws:apigateway:${{AWS::Region}}:lambda:path/2015-03-31/functions/{p.CreateArn}/invocations");

            sb.AppendLine("  get:");
            sb.AppendLine("    security:");
            sb.AppendLine("      - LambdaAuth: []");
            sb.AppendLine("    x-amazon-apigateway-integration:");
            sb.AppendLine("      httpMethod: POST");
            sb.AppendLine("      type: aws_proxy");
            sb.AppendLine("      uri:");
            sb.AppendLine($"        Fn::Sub: arn:aws:apigateway:${{AWS::Region}}:lambda:path/2015-03-31/functions/{p.GetListArn}/invocations");

            sb.AppendLine($"{basePath}/{{id}}:");
            sb.AppendLine("  options:");
            sb.AppendLine("    x-amazon-apigateway-integration:");
            sb.AppendLine("      type: mock");
            sb.AppendLine("      requestTemplates:");
            sb.AppendLine("        application/json: '{\"statusCode\": 200}'");
            sb.AppendLine("      responses:");
            sb.AppendLine("        default:");
            sb.AppendLine("          statusCode: '200'");
            sb.AppendLine("      passthroughBehavior: when_no_match");

            sb.AppendLine("  get:");
            sb.AppendLine("    security:");
            sb.AppendLine("      - LambdaAuth: []");
            sb.AppendLine("    x-amazon-apigateway-integration:");
            sb.AppendLine("      httpMethod: POST");
            sb.AppendLine("      type: aws_proxy");
            sb.AppendLine("      uri:");
            sb.AppendLine($"        Fn::Sub: arn:aws:apigateway:${{AWS::Region}}:lambda:path/2015-03-31/functions/{p.GetOneArn}/invocations");

            sb.AppendLine("  put:");
            sb.AppendLine("    security:");
            sb.AppendLine("      - LambdaAuth: []");
            sb.AppendLine("    x-amazon-apigateway-integration:");
            sb.AppendLine("      httpMethod: POST");
            sb.AppendLine("      type: aws_proxy");
            sb.AppendLine("      uri:");
            sb.AppendLine($"        Fn::Sub: arn:aws:apigateway:${{AWS::Region}}:lambda:path/2015-03-31/functions/{p.UpdateDeleteArn}/invocations");

            sb.AppendLine($"{basePath}/{{id}}/permanent:");
            sb.AppendLine("  options:");
            sb.AppendLine("    x-amazon-apigateway-integration:");
            sb.AppendLine("      type: mock");
            sb.AppendLine("      requestTemplates:");
            sb.AppendLine("        application/json: '{\"statusCode\": 200}'");
            sb.AppendLine("      responses:");
            sb.AppendLine("        default:");
            sb.AppendLine("          statusCode: '200'");
            sb.AppendLine("      passthroughBehavior: when_no_match");

            sb.AppendLine("  delete:");
            sb.AppendLine("    security:");
            sb.AppendLine("      - LambdaAuth: []");
            sb.AppendLine("    x-amazon-apigateway-integration:");
            sb.AppendLine("      httpMethod: POST");
            sb.AppendLine("      type: aws_proxy");
            sb.AppendLine("      uri:");
            sb.AppendLine($"        Fn::Sub: arn:aws:apigateway:${{AWS::Region}}:lambda:path/2015-03-31/functions/{p.DeletePermanentArn}/invocations");
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildPermissionsYaml(string apiLogicalId, string authorizerArn, List<ParsedPack> packs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("  # ------------------------------------------------");
        sb.AppendLine("  # Lambda invoke permissions for API Gateway");
        sb.AppendLine("  # ------------------------------------------------");

        sb.AppendLine("  AuthorizerInvokePermission:");
        sb.AppendLine("    Type: AWS::Lambda::Permission");
        sb.AppendLine("    Properties:");
        sb.AppendLine("      Action: lambda:InvokeFunction");
        sb.AppendLine("      FunctionName: " + EscapeScalar(authorizerArn));
        sb.AppendLine("      Principal: apigateway.amazonaws.com");
        sb.AppendLine("      SourceArn:");
        sb.AppendLine($"        Fn::Sub: arn:aws:execute-api:${{AWS::Region}}:${{AWS::AccountId}}:${{{apiLogicalId}}}/*/*/*");

        int i = 1;
        foreach (var p in packs)
        {
            AppendPerm(sb, $"Pack{i}CreateInvokePermission", apiLogicalId, p.CreateArn);
            AppendPerm(sb, $"Pack{i}GetListInvokePermission", apiLogicalId, p.GetListArn);
            AppendPerm(sb, $"Pack{i}GetOneInvokePermission", apiLogicalId, p.GetOneArn);
            AppendPerm(sb, $"Pack{i}UpdateDeleteInvokePermission", apiLogicalId, p.UpdateDeleteArn);
            AppendPerm(sb, $"Pack{i}DeletePermanentInvokePermission", apiLogicalId, p.DeletePermanentArn);
            i++;
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendPerm(StringBuilder sb, string logicalId, string apiLogicalId, string functionArn)
    {
        sb.AppendLine($"  {logicalId}:");
        sb.AppendLine("    Type: AWS::Lambda::Permission");
        sb.AppendLine("    Properties:");
        sb.AppendLine("      Action: lambda:InvokeFunction");
        sb.AppendLine("      FunctionName: " + EscapeScalar(functionArn));
        sb.AppendLine("      Principal: apigateway.amazonaws.com");
        sb.AppendLine("      SourceArn:");
        sb.AppendLine($"        Fn::Sub: arn:aws:execute-api:${{AWS::Region}}:${{AWS::AccountId}}:${{{apiLogicalId}}}/*/*/*");
    }

    private static string EscapeScalar(string value)
    {
        var safe = (value ?? "").Replace("\"", "\\\"");
        return $"\"{safe}\"";
    }

    private static string Indent(string text, int spaces)
    {
        var pad = new string(' ', spaces);
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        return string.Join("\n", lines.Select(l => pad + l));
    }
}
