namespace CodeGenApp.Services;

public interface ILambdaCodeGenerator
{
    string GenerateCreatePython(string tableName, string pkName, string? skName);
    string GenerateGetListPython(string tableName, string pkName, string? skName);
    string GenerateGetOnePython(string tableName, string pkName, string? skName);
    string GenerateUpdateDeletePython(string tableName, string pkName, string? skName);
    string GenerateDeletePermanentPython(string tableName, string pkName, string? skName);

    // NEW: authorizer generator
    string GenerateAuthorizerPython(string tableName, string pkName, string? skName, string safeBaseName);
}
