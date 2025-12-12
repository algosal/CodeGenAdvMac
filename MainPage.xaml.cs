using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Maui.Controls;
using CodeGenApp.Services;

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
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    // -----------------------------------------
    // Generate API-only template
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
                var resourceName = (pack.ResourceName ?? "").Trim();
                if (string.IsNullOrWhiteSpace(resourceName))
                    resourceName = $"resource{pack.Index}";

                // Normalize to valid logical-id-ish name: letters/digits only
                resourceName = LogicalName.Sanitize(resourceName);

                var basePath = (pack.BasePath ?? "").Trim();
                if (string.IsNullOrWhiteSpace(basePath))
                    basePath = "/resource" + pack.Index;

                if (!basePath.StartsWith("/"))
                    basePath = "/" + basePath;

                var outputsJson = (pack.OutputsJson ?? "").Trim();
                if (string.IsNullOrWhiteSpace(outputsJson))
                {
                    StatusEditor.Text = $"{pack.Title}: Outputs JSON is empty.";
                    return;
                }

                // Robust parse: accepts raw array or "array + extra pasted text"
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
                    ResourceName = resourceName,
                    BasePath = basePath,
                    CreateArn = createArn,
                    GetListArn = getListArn,
                    GetOneArn = getOneArn,
                    UpdateDeleteArn = updateDeleteArn,
                    DeletePermanentArn = deletePermanentArn,
                    PackAuthorizerArn = packAuthArn
                });
            }

            // Use explicit authorizer if provided; else first pack that has one
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

            var outPath = Path.Combine(outDir, "template.yaml"); // IMPORTANT: template.yaml
            File.WriteAllText(outPath, yaml, Encoding.UTF8);

            StatusEditor.Text =
                $"Generated template.yaml\n\nPath:\n{outPath}\n\nNext:\n1) sam build\n2) sam deploy --guided";

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
// UI Models
// -------------------------------
public class TablePackInput : INotifyPropertyChanged
{
    private int _index;
    private string _title = "";
    private string _resourceName = "";
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

    public string ResourceName
    {
        get => _resourceName;
        set { _resourceName = value; OnPropertyChanged(); }
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
        if (string.IsNullOrWhiteSpace(ResourceName))
            ResourceName = $"resource{index}";
        if (string.IsNullOrWhiteSpace(BasePath))
            BasePath = $"/resource{index}";
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
