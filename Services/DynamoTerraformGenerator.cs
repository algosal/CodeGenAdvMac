using System;
using CodeGenApp.Utils;

namespace CodeGenApp.Services;

public interface IDynamoTerraformGenerator
{
    string GenerateTerraform(string tableName, string pkName, string? skName, bool isOnDemand);
}

public class DynamoTerraformGenerator : IDynamoTerraformGenerator
{
    public string GenerateTerraform(
        string tableName,
        string pkName,
        string? skName,
        bool isOnDemand)
    {
        var resourceName = NameSanitizer.MakeSafeName(tableName);

        // Optional SK parts
        string skKeyLine = "";
        string skAttributeBlock = "";

        if (!string.IsNullOrWhiteSpace(skName))
        {
            skKeyLine = $"  range_key     = \"{skName}\"";

            skAttributeBlock = $@"

  attribute {{
    name = ""{skName}""
    type = ""S""
  }}";
        }

        string billingBlock;
        if (isOnDemand)
        {
            billingBlock = "  billing_mode = \"PAY_PER_REQUEST\"";
        }
        else
        {
            billingBlock = @"
  billing_mode   = ""PROVISIONED""
  read_capacity  = 5
  write_capacity = 5";
        }

        var template = @"
# Auto-generated Terraform for DynamoDB table: __TABLE_NAME__

resource ""aws_dynamodb_table"" ""__RESOURCE_NAME__"" {
  name = ""__TABLE_NAME__""
__BILLING_BLOCK__

  hash_key = ""__PK_NAME__""
__SK_KEY_LINE__

  attribute {
    name = ""__PK_NAME__""
    type = ""S""
  }
__SK_ATTRIBUTE_BLOCK__
}
";

        template = template
            .Replace("__TABLE_NAME__", tableName)
            .Replace("__RESOURCE_NAME__", resourceName)
            .Replace("__PK_NAME__", pkName)
            .Replace("__SK_KEY_LINE__", string.IsNullOrWhiteSpace(skKeyLine) ? "" : skKeyLine)
            .Replace("__SK_ATTRIBUTE_BLOCK__", string.IsNullOrWhiteSpace(skAttributeBlock) ? "" : skAttributeBlock)
            .Replace("__BILLING_BLOCK__", billingBlock);

        return template;
    }
}
