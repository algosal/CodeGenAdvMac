namespace CodeGenApp.Services;

public interface ICodeGenerationService
{
    string GenerateDynamoTerraformFile(string tableName, string pkName, string? skName, bool isOnDemand);

    // 4-arg version (core implementation)
    LambdaGenerationResult GenerateLambdaFiles(string tableName, string pkName, string? skName, string baseName);

    // 5-arg version (includes appName / apiName)
    LambdaGenerationResult GenerateLambdaFiles(string tableName, string pkName, string? skName, string baseName, string? appName);
}

public class LambdaGenerationResult
{
    public string FolderPath { get; init; } = string.Empty;
    public string CreateFileName { get; init; } = string.Empty;
    public string GetListFileName { get; init; } = string.Empty;
    public string GetOneFileName { get; init; } = string.Empty;
    public string UpdateDeleteFileName { get; init; } = string.Empty;
    public string DeletePermanentFileName { get; init; } = string.Empty;
    public string SamTemplateFileName { get; init; } = "template.yaml";
}
