using System;

namespace CodeGenApp.Services;

public interface ISamTemplateGenerator
{
    string GenerateTemplate(
        string tableName,
        string pkName,
        string? skName,
        string logicalBase,
        string safeBaseName,
        string safeTableName);
}

public class SamTemplateGenerator : ISamTemplateGenerator
{
    public string GenerateTemplate(
        string tableName,
        string pkName,
        string? skName,
        string logicalBase,
        string safeBaseName,
        string safeTableName)
    {
        // Ensure handler base is safe for Python module names (no hyphens).
        var handlerBase = safeBaseName.Replace("-", string.Empty);

        // Handlers are based on filenames without .py
        var createHandler          = $"{handlerBase}_create.lambda_handler";
        var getListHandler         = $"{handlerBase}_get_list.lambda_handler";
        var updateDeleteHandler    = $"{handlerBase}_update_delete.lambda_handler";
        var getOneHandler          = $"{handlerBase}_get_one.lambda_handler";
        var deletePermanentHandler = $"{handlerBase}_delete_permanent.lambda_handler";
        var authorizerHandler      = $"{handlerBase}_authorizer.lambda_handler";

        var stackNameSuggestion = $"{safeTableName}-stack";
        var apiLogicalName = $"{logicalBase}Api";
        var pathSegment = safeBaseName.ToLowerInvariant();

        var template = $@"
# Auto-generated SAM template for table: {tableName}
AWSTemplateFormatVersion: '2010-09-09'
Transform: AWS::Serverless-2016-10-31
Description: SAM stack for table {tableName}

Globals:
  Function:
    Runtime: python3.11
    Timeout: 10
    MemorySize: 256
    Environment:
      Variables:
        TABLE_NAME: ""{tableName}""

Resources:
  # ------------------------------------------------
  # REST API (API Gateway v1) with CORS + Auth
  # ------------------------------------------------
  {apiLogicalName}:
    Type: AWS::Serverless::Api
    Properties:
      Name: {logicalBase}RestApi
      StageName: prod
      Cors:
        AllowMethods: ""'GET,POST,PUT,DELETE,OPTIONS'""
        AllowHeaders: ""'Content-Type,Authorization,X-Requested-With'""
        AllowOrigin: ""'*'""
      Auth:
        DefaultAuthorizer: LambdaAuth
        AddDefaultAuthorizerToCorsPreflight: false
        Authorizers:
          LambdaAuth:
            FunctionArn: !GetAtt {logicalBase}AuthorizerFunction.Arn
            Identity:
              Header: Authorization
              ReauthorizeEvery: 0

  # ------------------------------------------------
  # AUTHORIZER LAMBDA
  # ------------------------------------------------
  {logicalBase}AuthorizerFunction:
    Type: AWS::Serverless::Function
    Properties:
      CodeUri: .
      Handler: {authorizerHandler}
      Layers:
        - arn:aws:lambda:us-east-2:954976294160:layer:JWTLayer:1
      Environment:
        Variables:
          JWT_SECRET: ""replace-me""
      Policies:
        - AWSLambdaBasicExecutionRole

  # ------------------------------------------------
  # LAMBDAS
  # ------------------------------------------------
  {logicalBase}CreateFunction:
    Type: AWS::Serverless::Function
    Properties:
      CodeUri: .
      Handler: {createHandler}
      Policies:
        - Statement:
            - Effect: Allow
              Action:
                - dynamodb:PutItem
              Resource: !Sub 'arn:aws:dynamodb:${{AWS::Region}}:${{AWS::AccountId}}:table/{tableName}'
      Events:
        ApiPost:
          Type: Api
          Properties:
            RestApiId: !Ref {apiLogicalName}
            Path: /{pathSegment}
            Method: POST

  {logicalBase}GetListFunction:
    Type: AWS::Serverless::Function
    Properties:
      CodeUri: .
      Handler: {getListHandler}
      Policies:
        - Statement:
            - Effect: Allow
              Action:
                - dynamodb:Scan
                - dynamodb:Query
                - dynamodb:GetItem
                - dynamodb:DescribeTable
              Resource:
                - !Sub 'arn:aws:dynamodb:${{AWS::Region}}:${{AWS::AccountId}}:table/{tableName}'
                - !Sub 'arn:aws:dynamodb:${{AWS::Region}}:${{AWS::AccountId}}:table/{tableName}/index/*'
      Events:
        ApiGetList:
          Type: Api
          Properties:
            RestApiId: !Ref {apiLogicalName}
            Path: /{pathSegment}
            Method: GET

  {logicalBase}GetOneFunction:
    Type: AWS::Serverless::Function
    Properties:
      CodeUri: .
      Handler: {getOneHandler}
      Policies:
        - Statement:
            - Effect: Allow
              Action:
                - dynamodb:GetItem
                - dynamodb:Query
                - dynamodb:DescribeTable
              Resource:
                - !Sub 'arn:aws:dynamodb:${{AWS::Region}}:${{AWS::AccountId}}:table/{tableName}'
                - !Sub 'arn:aws:dynamodb:${{AWS::Region}}:${{AWS::AccountId}}:table/{tableName}/index/*'
      Events:
        ApiGetOne:
          Type: Api
          Properties:
            RestApiId: !Ref {apiLogicalName}
            Path: /{pathSegment}/{{id}}
            Method: GET

  {logicalBase}UpdateDeleteFunction:
    Type: AWS::Serverless::Function
    Properties:
      CodeUri: .
      Handler: {updateDeleteHandler}
      Policies:
        - Statement:
            - Effect: Allow
              Action:
                - dynamodb:UpdateItem
                - dynamodb:GetItem
                - dynamodb:DescribeTable
              Resource: !Sub 'arn:aws:dynamodb:${{AWS::Region}}:${{AWS::AccountId}}:table/{tableName}'
      Events:
        ApiUpdate:
          Type: Api
          Properties:
            RestApiId: !Ref {apiLogicalName}
            Path: /{pathSegment}/{{id}}
            Method: PUT

  {logicalBase}DeletePermanentFunction:
    Type: AWS::Serverless::Function
    Properties:
      CodeUri: .
      Handler: {deletePermanentHandler}
      Policies:
        - Statement:
            - Effect: Allow
              Action:
                - dynamodb:DeleteItem
                - dynamodb:GetItem
                - dynamodb:DescribeTable
              Resource: !Sub 'arn:aws:dynamodb:${{AWS::Region}}:${{AWS::AccountId}}:table/{tableName}'
      Events:
        ApiDeletePermanent:
          Type: Api
          Properties:
            RestApiId: !Ref {apiLogicalName}
            Path: /{pathSegment}/{{id}}/permanent
            Method: DELETE

Outputs:
  {logicalBase}ApiUrl:
    Description: Base URL for the generated REST API
    Value: !Sub 'https://${{{apiLogicalName}}}.execute-api.${{AWS::Region}}.amazonaws.com/prod'
";

        return template.TrimStart('\n', '\r');
    }
}
