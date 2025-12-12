using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeGenApp.Services;

public record ApiPack(
    string PackName,
    string BasePath,
    string CreateArn,
    string GetListArn,
    string GetOneArn,
    string UpdateDeleteArn,
    string DeletePermanentArn);

public interface IApiOnlySamTemplateGenerator
{
    string Generate(string apiLogicalName, string stageName, string authorizerArn, List<ApiPack> packs);
}

public class ApiOnlySamTemplateGenerator : IApiOnlySamTemplateGenerator
{
    public string Generate(string apiLogicalName, string stageName, string authorizerArn, List<ApiPack> packs)
    {
        if (string.IsNullOrWhiteSpace(apiLogicalName)) apiLogicalName = "AlbaGoldApi";
        if (string.IsNullOrWhiteSpace(stageName)) stageName = "prod";

        // Normalize
        apiLogicalName = SafeLogicalId(apiLogicalName);
        stageName = stageName.Trim();

        // Swagger (REST API v1)
        var swagger = BuildSwagger(stageName, authorizerArn, packs);

        // Lambda permissions for ALL lambdas referenced
        var perms = BuildLambdaPermissions(apiLogicalName, stageName, authorizerArn, packs);

        var sb = new StringBuilder();
        sb.AppendLine("AWSTemplateFormatVersion: '2010-09-09'");
        sb.AppendLine("Transform: AWS::Serverless-2016-10-31");
        sb.AppendLine("Description: API-only SAM stack (existing Lambdas) with mandatory Lambda Authorizer");
        sb.AppendLine();
        sb.AppendLine("Resources:");
        sb.AppendLine($"  {apiLogicalName}:");
        sb.AppendLine("    Type: AWS::Serverless::Api");
        sb.AppendLine("    Properties:");
        sb.AppendLine($"      Name: {apiLogicalName}-RestApi");
        sb.AppendLine($"      StageName: {stageName}");
        sb.AppendLine("      Cors:");
        sb.AppendLine("        AllowMethods: \"'GET,POST,PUT,DELETE,OPTIONS'\"");
        sb.AppendLine("        AllowHeaders: \"'Content-Type,Authorization,X-Requested-With'\"");
        sb.AppendLine("        AllowOrigin: \"'*'\"");
        sb.AppendLine("      DefinitionBody:");
        sb.Append(swagger);

        sb.AppendLine();
        sb.AppendLine(perms);

        sb.AppendLine();
        sb.AppendLine("Outputs:");
        sb.AppendLine($"  {apiLogicalName}ApiUrl:");
        sb.AppendLine("    Description: Base URL for the REST API");
        sb.AppendLine($"    Value: !Sub 'https://${{{apiLogicalName}}}.execute-api.${{AWS::Region}}.amazonaws.com/{stageName}'");

        return sb.ToString();
    }

    private static string BuildSwagger(string stageName, string authorizerArn, List<ApiPack> packs)
    {
        // We embed stageName only for info; API GW uses StageName from SAM.
        // AuthorizerUri for REST API custom authorizer:
        // arn:aws:apigateway:{region}:lambda:path/2015-03-31/functions/{lambdaArn}/invocations
        var sb = new StringBuilder();

        sb.AppendLine("        swagger: '2.0'");
        sb.AppendLine("        info:");
        sb.AppendLine("          version: '1.0'");
        sb.AppendLine($"          title: 'Generated API ({stageName})'");
        sb.AppendLine("        schemes:");
        sb.AppendLine("          - https");
        sb.AppendLine("        consumes:");
        sb.AppendLine("          - application/json");
        sb.AppendLine("        produces:");
        sb.AppendLine("          - application/json");
        sb.AppendLine("        securityDefinitions:");
        sb.AppendLine("          LambdaAuth:");
        sb.AppendLine("            type: apiKey");
        sb.AppendLine("            name: Authorization");
        sb.AppendLine("            in: header");
        sb.AppendLine("            x-amazon-apigateway-authtype: custom");
        sb.AppendLine("            x-amazon-apigateway-authorizer:");
        sb.AppendLine("              type: token");
        sb.AppendLine("              identitySource: method.request.header.Authorization");
        sb.AppendLine("              authorizerResultTtlInSeconds: 0");
        sb.AppendLine($"              authorizerUri: !Sub 'arn:aws:apigateway:${{AWS::Region}}:lambda:path/2015-03-31/functions/{authorizerArn}/invocations'");
        sb.AppendLine("        paths:");

        foreach (var p in packs)
        {
            var basePath = "/" + p.BasePath.Trim().Trim('/');

            // POST /base
            sb.AppendLine($"          {basePath}:");
            sb.AppendLine("            options:");
            sb.AppendLine("              x-amazon-apigateway-integration:");
            sb.AppendLine("                type: mock");
            sb.AppendLine("                requestTemplates:");
            sb.AppendLine("                  application/json: '{\"statusCode\": 200}'");
            sb.AppendLine("                responses:");
            sb.AppendLine("                  default:");
            sb.AppendLine("                    statusCode: '200'");
            sb.AppendLine("              responses:");
            sb.AppendLine("                '200':");
            sb.AppendLine("                  description: CORS preflight");
            sb.AppendLine("            post:");
            sb.AppendLine("              security:");
            sb.AppendLine("                - LambdaAuth: []");
            sb.AppendLine("              responses:");
            sb.AppendLine("                '200': { description: OK }");
            sb.AppendLine("              x-amazon-apigateway-integration:");
            sb.AppendLine($"                uri: !Sub 'arn:aws:apigateway:${{AWS::Region}}:lambda:path/2015-03-31/functions/{p.CreateArn}/invocations'");
            sb.AppendLine("                httpMethod: POST");
            sb.AppendLine("                type: aws_proxy");

            // GET /base
            sb.AppendLine("            get:");
            sb.AppendLine("              security:");
            sb.AppendLine("                - LambdaAuth: []");
            sb.AppendLine("              responses:");
            sb.AppendLine("                '200': { description: OK }");
            sb.AppendLine("              x-amazon-apigateway-integration:");
            sb.AppendLine($"                uri: !Sub 'arn:aws:apigateway:${{AWS::Region}}:lambda:path/2015-03-31/functions/{p.GetListArn}/invocations'");
            sb.AppendLine("                httpMethod: POST");
            sb.AppendLine("                type: aws_proxy");

            // GET/PUT/DELETE /base/{id}
            var idPath = $"{basePath}/{{id}}";
            sb.AppendLine($"          {idPath}:");
            sb.AppendLine("            options:");
            sb.AppendLine("              x-amazon-apigateway-integration:");
            sb.AppendLine("                type: mock");
            sb.AppendLine("                requestTemplates:");
            sb.AppendLine("                  application/json: '{\"statusCode\": 200}'");
            sb.AppendLine("                responses:");
            sb.AppendLine("                  default:");
            sb.AppendLine("                    statusCode: '200'");
            sb.AppendLine("              responses:");
            sb.AppendLine("                '200':");
            sb.AppendLine("                  description: CORS preflight");
            sb.AppendLine("            get:");
            sb.AppendLine("              security:");
            sb.AppendLine("                - LambdaAuth: []");
            sb.AppendLine("              responses:");
            sb.AppendLine("                '200': { description: OK }");
            sb.AppendLine("              x-amazon-apigateway-integration:");
            sb.AppendLine($"                uri: !Sub 'arn:aws:apigateway:${{AWS::Region}}:lambda:path/2015-03-31/functions/{p.GetOneArn}/invocations'");
            sb.AppendLine("                httpMethod: POST");
            sb.AppendLine("                type: aws_proxy");

            sb.AppendLine("            put:");
            sb.AppendLine("              security:");
            sb.AppendLine("                - LambdaAuth: []");
            sb.AppendLine("              responses:");
            sb.AppendLine("                '200': { description: OK }");
            sb.AppendLine("              x-amazon-apigateway-integration:");
            sb.AppendLine($"                uri: !Sub 'arn:aws:apigateway:${{AWS::Region}}:lambda:path/2015-03-31/functions/{p.UpdateDeleteArn}/invocations'");
            sb.AppendLine("                httpMethod: POST");
            sb.AppendLine("                type: aws_proxy");

            // DELETE /base/{id}/permanent
            var permPath = $"{basePath}/{{id}}/permanent";
            sb.AppendLine($"          {permPath}:");
            sb.AppendLine("            options:");
            sb.AppendLine("              x-amazon-apigateway-integration:");
            sb.AppendLine("                type: mock");
            sb.AppendLine("                requestTemplates:");
            sb.AppendLine("                  application/json: '{\"statusCode\": 200}'");
            sb.AppendLine("                responses:");
            sb.AppendLine("                  default:");
            sb.AppendLine("                    statusCode: '200'");
            sb.AppendLine("              responses:");
            sb.AppendLine("                '200':");
            sb.AppendLine("                  description: CORS preflight");
            sb.AppendLine("            delete:");
            sb.AppendLine("              security:");
            sb.AppendLine("                - LambdaAuth: []");
            sb.AppendLine("              responses:");
            sb.AppendLine("                '200': { description: OK }");
            sb.AppendLine("              x-amazon-apigateway-integration:");
            sb.AppendLine($"                uri: !Sub 'arn:aws:apigateway:${{AWS::Region}}:lambda:path/2015-03-31/functions/{p.DeletePermanentArn}/invocations'");
            sb.AppendLine("                httpMethod: POST");
            sb.AppendLine("                type: aws_proxy");
        }

        return sb.ToString();
    }

    private static string BuildLambdaPermissions(string apiLogicalName, string stageName, string authorizerArn, List<ApiPack> packs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("  # ------------------------------------------------");
        sb.AppendLine("  # Lambda invoke permissions for API Gateway");
        sb.AppendLine("  # ------------------------------------------------");

        // Authorizer permission (allow API Gateway to invoke authorizer)
        sb.AppendLine($"  {apiLogicalName}AuthorizerInvokePermission:");
        sb.AppendLine("    Type: AWS::Lambda::Permission");
        sb.AppendLine("    Properties:");
        sb.AppendLine("      Action: lambda:InvokeFunction");
        sb.AppendLine($"      FunctionName: '{authorizerArn}'");
        sb.AppendLine("      Principal: apigateway.amazonaws.com");
        sb.AppendLine($"      SourceArn: !Sub 'arn:aws:execute-api:${{AWS::Region}}:${{AWS::AccountId}}:${{{apiLogicalName}}}/*/*/*'");

        int i = 1;
        foreach (var p in packs)
        {
            var basePath = p.BasePath.Trim().Trim('/');
            var packId = SafeLogicalId(p.PackName);

            // Create
            AddPerm(sb, apiLogicalName, $"Pack{i}{packId}CreatePerm", p.CreateArn,
                $"arn:aws:execute-api:${{AWS::Region}}:${{AWS::AccountId}}:${{{apiLogicalName}}}/*/POST/{basePath}");

            // GetList
            AddPerm(sb, apiLogicalName, $"Pack{i}{packId}GetListPerm", p.GetListArn,
                $"arn:aws:execute-api:${{AWS::Region}}:${{AWS::AccountId}}:${{{apiLogicalName}}}/*/GET/{basePath}");

            // GetOne
            AddPerm(sb, apiLogicalName, $"Pack{i}{packId}GetOnePerm", p.GetOneArn,
                $"arn:aws:execute-api:${{AWS::Region}}:${{AWS::AccountId}}:${{{apiLogicalName}}}/*/GET/{basePath}/*");

            // Update
            AddPerm(sb, apiLogicalName, $"Pack{i}{packId}UpdatePerm", p.UpdateDeleteArn,
                $"arn:aws:execute-api:${{AWS::Region}}:${{AWS::AccountId}}:${{{apiLogicalName}}}/*/PUT/{basePath}/*");

            // DeletePermanent
            AddPerm(sb, apiLogicalName, $"Pack{i}{packId}DeletePermanentPerm", p.DeletePermanentArn,
                $"arn:aws:execute-api:${{AWS::Region}}:${{AWS::AccountId}}:${{{apiLogicalName}}}/*/DELETE/{basePath}/*/permanent");

            i++;
        }

        return sb.ToString();
    }

    private static void AddPerm(StringBuilder sb, string apiLogicalName, string logicalId, string functionArn, string sourceArnSub)
    {
        sb.AppendLine($"  {SafeLogicalId(logicalId)}:");
        sb.AppendLine("    Type: AWS::Lambda::Permission");
        sb.AppendLine("    Properties:");
        sb.AppendLine("      Action: lambda:InvokeFunction");
        sb.AppendLine($"      FunctionName: '{functionArn}'");
        sb.AppendLine("      Principal: apigateway.amazonaws.com");
        sb.AppendLine($"      SourceArn: !Sub '{sourceArnSub}'");
    }

    private static string SafeLogicalId(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "X";
        var cleaned = new string(s.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "X" : cleaned;
    }
}
