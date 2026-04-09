using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeGenApp.Services
{
    // ------------------------------------------------
    // Model used by MainPage.xaml.cs
    // ------------------------------------------------
    public class ParsedPack
    {
        public string Title { get; set; } = "";

        // Real API resource name: vendors, stores, corporates, etc.
        public string ResourceName { get; set; } = "";

        // Backward compatibility
        public string BasePath { get; set; } = "";

        public string CreateArn { get; set; } = "";
        public string GetListArn { get; set; } = "";
        public string GetOneArn { get; set; } = "";
        public string UpdateDeleteArn { get; set; } = "";
        public string DeletePermanentArn { get; set; } = "";

        public string? PackAuthorizerArn { get; set; }
    }

    // ------------------------------------------------
    // API-ONLY SAM TEMPLATE GENERATOR
    // ------------------------------------------------
    public class ApiOnlySamTemplateGenerator
    {
        public string Generate(
            string apiName,
            string stageName,
            string authorizerArn,
            List<ParsedPack> packs)
        {
            const string apiLogicalId = "MultiTableApi";

            var pathsYaml = BuildPathsYaml(packs);
            var permsYaml = BuildPermissionsYaml(apiLogicalId, authorizerArn, packs);

            // IMPORTANT:
            // - Use TOKEN authorizer for compatibility with most existing Lambda authorizers.
            // - Add x-amazon-apigateway-authtype: custom so Console shows Authorization properly.
            var yaml = $@"
AWSTemplateFormatVersion: '2010-09-09'
Transform: AWS::Serverless-2016-10-31
Description: API-only SAM template. Attaches existing Lambda ARNs with a Lambda Authorizer.

Resources:
  {apiLogicalId}:
    Type: AWS::Serverless::Api
    Properties:
      Name: {Q(apiName)}
      StageName: {Q(stageName)}
      EndpointConfiguration: REGIONAL
      Cors:
        AllowMethods: ""'GET,POST,PUT,DELETE,OPTIONS'""
        AllowHeaders: ""'Content-Type,Authorization,X-Requested-With'""
        AllowOrigin: ""'*'""
      DefinitionBody:
        openapi: 3.0.1
        info:
          title: {Q(apiName)}
          version: '1.0'
        components:
          securitySchemes:
            LambdaAuth:
              type: apiKey
              name: Authorization
              in: header
              x-amazon-apigateway-authtype: custom
              x-amazon-apigateway-authorizer:
                type: token
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
".TrimStart();

            return yaml;
        }

        // ------------------------------------------------
        // PATHS (USES REAL RESOURCE NAMES)
        // ------------------------------------------------
        private static string BuildPathsYaml(List<ParsedPack> packs)
        {
            var sb = new StringBuilder();

            foreach (var p in packs)
            {
                var basePath = ResolveBasePath(p);

                // /resource
                sb.AppendLine($"{basePath}:");
                AddOptions(sb);
                AddMethod(sb, "post", p.CreateArn);
                AddMethod(sb, "get", p.GetListArn);

                // /resource/{id}
                sb.AppendLine($"{basePath}/{{id}}:");
                AddOptions(sb);
                AddMethod(sb, "get", p.GetOneArn);
                AddMethod(sb, "put", p.UpdateDeleteArn);
                AddMethod(sb, "delete", p.UpdateDeleteArn);

                // /resource/{id}/permanent
                sb.AppendLine($"{basePath}/{{id}}/permanent:");
                AddOptions(sb);
                AddMethod(sb, "delete", p.DeletePermanentArn);
            }

            return sb.ToString().TrimEnd();
        }

        private static string ResolveBasePath(ParsedPack p)
        {
            var rn = (p.ResourceName ?? "").Trim().Trim('/');

            if (!string.IsNullOrWhiteSpace(rn))
                return "/" + rn;

            var bp = (p.BasePath ?? "").Trim();
            if (string.IsNullOrWhiteSpace(bp))
                throw new Exception("Each pack requires ResourceName (preferred) or BasePath.");

            if (!bp.StartsWith("/")) bp = "/" + bp;
            return bp;
        }

        private static void AddOptions(StringBuilder sb)
        {
            // No auth on OPTIONS to keep CORS clean.
            sb.AppendLine("  options:");
            sb.AppendLine("    x-amazon-apigateway-integration:");
            sb.AppendLine("      type: mock");
            sb.AppendLine("      requestTemplates:");
            sb.AppendLine("        application/json: '{\"statusCode\": 200}'");
            sb.AppendLine("      responses:");
            sb.AppendLine("        default:");
            sb.AppendLine("          statusCode: '200'");
            sb.AppendLine("      passthroughBehavior: when_no_match");
        }

        private static void AddMethod(StringBuilder sb, string method, string lambdaArn)
        {
            sb.AppendLine($"  {method}:");
            sb.AppendLine("    security:");
            sb.AppendLine("      - LambdaAuth: []");
            sb.AppendLine("    x-amazon-apigateway-integration:");
            sb.AppendLine("      httpMethod: POST");
            sb.AppendLine("      type: aws_proxy");
            sb.AppendLine("      uri:");
            sb.AppendLine(
                $"        Fn::Sub: arn:aws:apigateway:${{AWS::Region}}:lambda:path/2015-03-31/functions/{lambdaArn}/invocations");
        }

        // ------------------------------------------------
        // PERMISSIONS
        // ------------------------------------------------
        private static string BuildPermissionsYaml(
            string apiLogicalId,
            string authorizerArn,
            List<ParsedPack> packs)
        {
            var sb = new StringBuilder();

            // CRITICAL FIX:
            // Lambda authorizers are invoked from:
            // arn:aws:execute-api:region:account:apiId/authorizers/authorizerId
            // Not from /*/*/*
            sb.AppendLine("  AuthorizerInvokePermission:");
            sb.AppendLine("    Type: AWS::Lambda::Permission");
            sb.AppendLine("    Properties:");
            sb.AppendLine("      Action: lambda:InvokeFunction");
            sb.AppendLine($"      FunctionName: {Q(authorizerArn)}");
            sb.AppendLine("      Principal: apigateway.amazonaws.com");
            sb.AppendLine("      SourceArn:");
            sb.AppendLine($"        Fn::Sub: arn:aws:execute-api:${{AWS::Region}}:${{AWS::AccountId}}:${{{apiLogicalId}}}/authorizers/*");

            int i = 1;
            foreach (var p in packs)
            {
                AddPerm(sb, $"Pack{i}CreatePerm", apiLogicalId, p.CreateArn);
                AddPerm(sb, $"Pack{i}GetListPerm", apiLogicalId, p.GetListArn);
                AddPerm(sb, $"Pack{i}GetOnePerm", apiLogicalId, p.GetOneArn);
                AddPerm(sb, $"Pack{i}UpdateDeletePerm", apiLogicalId, p.UpdateDeleteArn);
                AddPerm(sb, $"Pack{i}DeletePermanentPerm", apiLogicalId, p.DeletePermanentArn);
                i++;
            }

            return sb.ToString();
        }

        private static void AddPerm(
            StringBuilder sb,
            string logicalId,
            string apiLogicalId,
            string functionArn)
        {
            sb.AppendLine($"  {logicalId}:");
            sb.AppendLine("    Type: AWS::Lambda::Permission");
            sb.AppendLine("    Properties:");
            sb.AppendLine("      Action: lambda:InvokeFunction");
            sb.AppendLine($"      FunctionName: {Q(functionArn)}");
            sb.AppendLine("      Principal: apigateway.amazonaws.com");
            sb.AppendLine("      SourceArn:");
            sb.AppendLine($"        Fn::Sub: arn:aws:execute-api:${{AWS::Region}}:${{AWS::AccountId}}:${{{apiLogicalId}}}/*/*/*");
        }

        // ------------------------------------------------
        // HELPERS
        // ------------------------------------------------
        private static string Q(string value)
            => $"\"{(value ?? "").Replace("\"", "\\\"")}\"";

        private static string Indent(string text, int spaces)
        {
            var pad = new string(' ', spaces);
            return string.Join("\n", text.Split('\n').Select(l => pad + l));
        }
    }
}
