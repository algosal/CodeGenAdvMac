using System.IO;
using CodeGenApp.Utils;

namespace CodeGenApp.Services;

public class CodeGenerationService : ICodeGenerationService
{
    private readonly IDynamoTerraformGenerator _dynamo;
    private readonly ILambdaCodeGenerator _lambda;
    private readonly ISamTemplateGenerator _sam;
    private readonly IOutputPathService _paths;

    public CodeGenerationService(
        IDynamoTerraformGenerator dynamo,
        ILambdaCodeGenerator lambda,
        ISamTemplateGenerator sam,
        IOutputPathService paths)
    {
        _dynamo = dynamo;
        _lambda = lambda;
        _sam = sam;
        _paths = paths;
        ;
    }

    public string GenerateDynamoTerraformFile(string tableName, string pkName, string? skName, bool isOnDemand)
    {
        var content = _dynamo.GenerateTerraform(tableName, pkName, skName, isOnDemand);

        var safeTableName = NameSanitizer.MakeSafeName(tableName);

        var tableDir = Path.Combine(_paths.DynamoDir, safeTableName);
        Directory.CreateDirectory(tableDir);

        var tfPath = Path.Combine(tableDir, $"{safeTableName}.tf");

        File.WriteAllText(tfPath, content);
        return tfPath;
    }

    // Core implementation: 4-arg version
    public LambdaGenerationResult GenerateLambdaFiles(
        string tableName,
        string pkName,
        string? skName,
        string baseName)
    {
        var safeTableName = NameSanitizer.MakeSafeName(tableName);    // folder name
        var safeBaseName  = NameSanitizer.MakeSafeName(baseName);     // base for filenames & handlers
        var logicalBase   = NameSanitizer.ToPascalSafe(safeBaseName); // CloudFormation logical IDs

        // Per-table folder: <root>/lambda/<tableName>/
        var tableLambdaDir = Path.Combine(_paths.LambdaDir, safeTableName);
        Directory.CreateDirectory(tableLambdaDir);

        // Filenames
        var createFileName          = $"{safeBaseName}_create.py";
        var getListFileName         = $"{safeBaseName}_get_list.py";
        var updateDeleteFileName    = $"{safeBaseName}_update_delete.py";
        var getOneFileName          = $"{safeBaseName}_get_one.py";
        var deletePermanentFileName = $"{safeBaseName}_delete_permanent.py";
        var authorizerFileName      = $"{safeBaseName}_authorizer.py";

        var createPath          = Path.Combine(tableLambdaDir, createFileName);
        var getListPath         = Path.Combine(tableLambdaDir, getListFileName);
        var updateDeletePath    = Path.Combine(tableLambdaDir, updateDeleteFileName);
        var getOnePath          = Path.Combine(tableLambdaDir, getOneFileName);
        var deletePermanentPath = Path.Combine(tableLambdaDir, deletePermanentFileName);
        var authorizerPath      = Path.Combine(tableLambdaDir, authorizerFileName);
        var samTemplatePath     = Path.Combine(tableLambdaDir, "template.yaml");

        // Generate Python lambdas
        var createContent          = _lambda.GenerateCreatePython(tableName, pkName, skName);
        var getListContent         = _lambda.GenerateGetListPython(tableName, pkName, skName);
        var updateDeleteContent    = _lambda.GenerateUpdateDeletePython(tableName, pkName, skName);
        var getOneContent          = _lambda.GenerateGetOnePython(tableName, pkName, skName);
        var deletePermanentContent = _lambda.GenerateDeletePermanentPython(tableName, pkName, skName);
        var authorizerContent      = _lambda.GenerateAuthorizerPython(tableName, pkName, skName, safeBaseName);

        File.WriteAllText(createPath,          createContent);
        File.WriteAllText(getListPath,         getListContent);
        File.WriteAllText(updateDeletePath,    updateDeleteContent);
        File.WriteAllText(getOnePath,          getOneContent);
        File.WriteAllText(deletePermanentPath, deletePermanentContent);
        File.WriteAllText(authorizerPath,      authorizerContent);

        // Generate SAM template (YAML) – always owns its own API for now
        var samContent = _sam.GenerateTemplate(
            tableName:     tableName,
            pkName:        pkName,
            skName:        skName,
            logicalBase:   logicalBase,
            safeBaseName:  safeBaseName,
            safeTableName: safeTableName
        );

        File.WriteAllText(samTemplatePath, samContent);

        return new LambdaGenerationResult
        {
            FolderPath              = tableLambdaDir,
            CreateFileName          = createFileName,
            GetListFileName         = getListFileName,
            GetOneFileName          = getOneFileName,
            UpdateDeleteFileName    = updateDeleteFileName,
            DeletePermanentFileName = deletePermanentFileName,
            SamTemplateFileName     = "template.yaml"
        };
    }

    // 5-arg version for callers that want to pass appName / apiName today
    public LambdaGenerationResult GenerateLambdaFiles(
        string tableName,
        string pkName,
        string? skName,
        string baseName,
        string? appName)
    {
        // For now, appName is reserved for future shared-API wiring.
        // We simply delegate to the 4-arg implementation.
        return GenerateLambdaFiles(tableName, pkName, skName, baseName);
    }
}
