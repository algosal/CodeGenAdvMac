public interface ISamTemplateGenerator
{
    string GenerateTemplate(
        string tableName,
        string pkName,
        string? skName,
        string logicalBase,
        string safeBaseName,
        string safeTableName);

    // New overload for reusing an existing API
    string GenerateTemplate(
        string tableName,
        string pkName,
        string? skName,
        string logicalBase,
        string safeBaseName,
        string safeTableName,
        string? existingApiId);
}
