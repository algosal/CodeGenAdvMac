using System;
using System.IO;
using System.Text;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Graphics;
using CodeGenApp.Services;
using CodeGenApp.Utils;

namespace CodeGenApp;

public partial class MainPage : ContentPage
{
    private readonly IOutputPathService _paths;
    private readonly ICodeGenerationService _codeGen;

    public MainPage()
    {
        InitializeComponent();

        // Services
        _paths = new OutputPathService();
        _codeGen = new CodeGenerationService(
            new DynamoTerraformGenerator(),
            new LambdaCodeGenerator(),
            new SamTemplateGenerator(),
            _paths);

        // 🔍 Test write so we can confirm sandbox + symlink work
        try
        {
            var testFile = Path.Combine(_paths.DynamoDir, "startup_test.txt");
            File.WriteAllText(testFile, $"Hello from MAUI at {DateTime.Now:O}");
        }
        catch (Exception ex)
        {
            ShowError($"❌ Startup write error: {ex.Message}");
        }

        RootPathLabel.Text = $"Root: {_paths.RootDir}";
        ShowSuccess("Ready.");
    }

    // ====== DYNAMODB BUTTON HANDLER ======

    private void OnGenerateDynamoClicked(object sender, EventArgs e)
    {
        try
        {
            var tableName = TableNameEntry.Text?.Trim() ?? string.Empty;
            var pkName    = PkNameEntry.Text?.Trim() ?? string.Empty;
            var skRaw     = SkNameEntry.Text?.Trim();
            var skName    = string.IsNullOrWhiteSpace(skRaw) ? null : skRaw;

            var isOnDemand = OnDemandSwitch.IsToggled;

            if (string.IsNullOrWhiteSpace(tableName) ||
                string.IsNullOrWhiteSpace(pkName))
            {
                ShowError("❌ Table Name and PK are required.");
                return;
            }

            var tfPath = _codeGen.GenerateDynamoTerraformFile(tableName, pkName, skName, isOnDemand);

            ShowSuccess($"✅ DynamoDB Terraform generated:\n{tfPath}");
        }
        catch (Exception ex)
        {
            ShowError($"❌ Error: {ex.Message}");
        }
    }

    // ====== LAMBDA BUTTON HANDLER ======

    private void OnGenerateLambdaClicked(object sender, EventArgs e)
    {
        try
        {
            var tableName = TableNameEntry.Text?.Trim() ?? string.Empty;
            var pkName    = PkNameEntry.Text?.Trim() ?? string.Empty;
            var skRaw     = SkNameEntry.Text?.Trim();
            var skName    = string.IsNullOrWhiteSpace(skRaw) ? null : skRaw;
            var baseName  = BaseLambdaEntry.Text?.Trim() ?? string.Empty;

            // This field is now: "Existing API URL or ID (optional)"
            var existingApiInput = AppNameEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(existingApiInput))
            {
                existingApiInput = null; // means: create a NEW API in the SAM template
            }

            if (string.IsNullOrWhiteSpace(tableName) ||
                string.IsNullOrWhiteSpace(pkName) ||
                string.IsNullOrWhiteSpace(baseName))
            {
                ShowError("❌ Table Name, PK, and Base Lambda Name are required.");
                return;
            }

            // existingApiInput is either null (new API) or URL/ID (attach to existing)
            var result = _codeGen.GenerateLambdaFiles(tableName, pkName, skName, baseName, existingApiInput);

            var sb = new StringBuilder();
            sb.AppendLine($"✅ Lambda files + SAM template generated in:");
            sb.AppendLine(result.FolderPath);
            sb.AppendLine();
            sb.AppendLine($"Create:           {result.CreateFileName}");
            sb.AppendLine($"Get/List:         {result.GetListFileName}");
            sb.AppendLine($"Get One:          {result.GetOneFileName}");
            sb.AppendLine($"Update/Delete:    {result.UpdateDeleteFileName}");
            sb.AppendLine($"Delete Permanent: {result.DeletePermanentFileName}");
            sb.AppendLine($"SAM:              {result.SamTemplateFileName}");
            sb.AppendLine();
            sb.AppendLine("Suggested deploy:");
            sb.AppendLine("sam build");
            sb.AppendLine($"sam deploy --stack-name {tableName}-stack --guided");

            StatusEditor.TextColor = Colors.Green;
            StatusEditor.Text = sb.ToString();
        }
        catch (Exception ex)
        {
            ShowError($"❌ Error: {ex.Message}");
        }
    }

    // ====== CLOUDFORMATION HELPER BUTTONS ======

    private void OnShowListStacksCommandsClicked(object sender, EventArgs e)
    {
        var sb = new StringBuilder();

        sb.AppendLine("CloudFormation list-stacks commands:");
        sb.AppendLine();
        sb.AppendLine("aws cloudformation list-stacks \\");
        sb.AppendLine("  --query \"StackSummaries[].{Name:StackName,Status:StackStatus}\" \\");
        sb.AppendLine("  --output table");
        sb.AppendLine();
        sb.AppendLine("aws cloudformation list-stacks \\");
        sb.AppendLine("  --stack-status-filter CREATE_COMPLETE UPDATE_COMPLETE UPDATE_ROLLBACK_COMPLETE \\");
        sb.AppendLine("  --query \"StackSummaries[].StackName\" \\");
        sb.AppendLine("  --output text");
        sb.AppendLine();
        sb.AppendLine("aws cloudformation list-stacks \\");
        sb.AppendLine("  --stack-status-filter DELETE_COMPLETE DELETE_FAILED \\");
        sb.AppendLine("  --query \"StackSummaries[].StackName\" \\");
        sb.AppendLine("  --output text");

        StatusEditor.TextColor = Colors.Blue;
        StatusEditor.Text = sb.ToString();
    }

    private void OnShowDeleteStackCommandsClicked(object sender, EventArgs e)
    {
        var stackName = StackNameToDeleteEntry.Text?.Trim();

        if (string.IsNullOrEmpty(stackName))
        {
            ShowError("❌ Please enter a stack name to delete.");
            return;
        }

        var sb = new StringBuilder();

        sb.AppendLine($"Delete CloudFormation stack commands for: {stackName}");
        sb.AppendLine();
        sb.AppendLine($"aws cloudformation delete-stack --stack-name {stackName}");
        sb.AppendLine();
        sb.AppendLine($"aws cloudformation wait stack-delete-complete --stack-name {stackName}");

        StatusEditor.TextColor = Colors.Orange;
        StatusEditor.Text = sb.ToString();
    }

    private void OnStatusEditorFocused(object sender, FocusEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(StatusEditor.Text))
        {
            StatusEditor.CursorPosition = 0;
        }
    }

    // ====== STATUS HELPERS ======

    private void ShowError(string message)
    {
        StatusEditor.TextColor = Colors.Red;
        StatusEditor.Text = message;
    }

    private void ShowSuccess(string message)
    {
        StatusEditor.TextColor = Colors.Green;
        StatusEditor.Text = message;
    }
}
