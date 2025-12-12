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
        // Make handler base safe for Python module names
        var handlerBase = safeBaseName.Replace("-", string.Empty);

        var createHandler          = $"{handlerBase}_create.lambda_handler";
        var getListHandler         = $"{handlerBase}_get_list.lambda_handler";
        var updateDeleteHandler    = $"{handlerBase}_update_delete.lambda_handler";
        var getOneHandler          = $"{handlerBase}_get_one.lambda_handler";
        var deletePermanentHandler = $"{handlerBase}_delete_permanent.lambda_handler";
        var authorizerHandler      = $"{handlerBase}_authorizer.lambda_handler";

        var stackNameSuggestion = $"{safeTableName}-stack";

        // API + Authorizer name
        var apiLogicalId = $"{logicalBase}Api";

        // Base path for routes. Example: if logicalBase = "AGCodeGenL", you get /agcodegenl
        // You can change this convention if you prefer a different prefix.
        var basePath = "/" + safeBaseName.Trim().Trim('/');

        // DynamoDB: define KeySchema/AttributeDefinitions depending on skName
        var hasSk = !string.IsNullOrWhiteSpace(skName);

        var attributeDefinitions = hasSk
            ? $@"
        AttributeDefinitions:
          - AttributeName: {pkName}
            AttributeType: S
          - AttributeName: {skName}
            AttributeType: S
        KeySchema:
          - AttributeName: {pkName}
            KeyType: HASH
          - AttributeName: {skName}
            KeyType: RANGE"
            : $@"
        AttributeDefinitions:
          - AttributeName: {pkName}
            AttributeType: S
        KeySchema:
          - AttributeName: {pkName}
            KeyType: HASH";

        var template = $@"
# Auto-generated SAM Template (API + Lambda + DynamoDB)
# Stack suggestion: {stackNameSuggestion}
AWSTemplateFormatVersion: '2010-09-09'
Transform: AWS::Serverless-2016-10-31
Description: API + Lambda + DynamoDB for table {tableName}

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
  # DynamoDB Table
  # ------------------------------------------------
  {logicalBase}Table:
    Type: AWS::DynamoDB::Table
    Properties:
      TableName: {tableName}
{attributeDefinitions}
      BillingMode: PAY_PER_REQUEST

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
  # API (SAM REST API) - Authorizer enforced here
  # ------------------------------------------------
  {apiLogicalId}:
    Type: AWS::Serverless::Api
    Properties:
      Name: ""{safeBaseName}-api""
      StageName: prod
      Cors:
        AllowMethods: ""'GET,POST,PUT,DELETE,OPTIONS'""
        AllowHeaders: ""'Content-Type,Authorization,X-Requested-With'""
        AllowOrigin: ""'*'""
      Auth:
        DefaultAuthorizer: LambdaAuth
        Authorizers:
          LambdaAuth:
            FunctionArn: !GetAtt {logicalBase}AuthorizerFunction.Arn
            FunctionPayloadType: REQUEST
            Identity:
              Headers:
                - Authorization
            AuthorizerResultTtlInSeconds: 0

  # ------------------------------------------------
  # CREATE
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
              Resource: !GetAtt {logicalBase}Table.Arn
      Events:
        CreateRoute:
          Type: Api
          Properties:
            RestApiId: !Ref {apiLogicalId}
            Path: {basePath}
            Method: POST

  # ------------------------------------------------
  # GET LIST
  # ------------------------------------------------
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
                - !GetAtt {logicalBase}Table.Arn
                - !Sub ""${{{logicalBase}Table.Arn}}/index/*""
      Events:
        GetListRoute:
          Type: Api
          Properties:
            RestApiId: !Ref {apiLogicalId}
            Path: {basePath}
            Method: GET

  # ------------------------------------------------
  # GET ONE
  # ------------------------------------------------
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
                - !GetAtt {logicalBase}Table.Arn
                - !Sub ""${{{logicalBase}Table.Arn}}/index/*""
      Events:
        GetOneRoute:
          Type: Api
          Properties:
            RestApiId: !Ref {apiLogicalId}
            Path: {basePath}/{{id}}
            Method: GET

  # ------------------------------------------------
  # UPDATE / SOFT DELETE
  # ------------------------------------------------
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
              Resource: !GetAtt {logicalBase}Table.Arn
      Events:
        UpdateRoute:
          Type: Api
          Properties:
            RestApiId: !Ref {apiLogicalId}
            Path: {basePath}/{{id}}
            Method: PUT
        SoftDeleteRoute:
          Type: Api
          Properties:
            RestApiId: !Ref {apiLogicalId}
            Path: {basePath}/{{id}}
            Method: DELETE

  # ------------------------------------------------
  # PERMANENT DELETE
  # ------------------------------------------------
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
              Resource: !GetAtt {logicalBase}Table.Arn
      Events:
        PermanentDeleteRoute:
          Type: Api
          Properties:
            RestApiId: !Ref {apiLogicalId}
            Path: {basePath}/{{id}}/permanent
            Method: DELETE

Outputs:
  {logicalBase}ApiUrl:
    Description: Base URL for this API
    Value: !Sub ""https://${{{apiLogicalId}}}.execute-api.${{AWS::Region}}.amazonaws.com/prod""

  {logicalBase}AuthorizerArn:
    Description: ARN of the authorizer Lambda for {tableName}
    Value: !GetAtt {logicalBase}AuthorizerFunction.Arn

  {logicalBase}CreateArn:
    Description: ARN of the CREATE Lambda for {tableName}
    Value: !GetAtt {logicalBase}CreateFunction.Arn

  {logicalBase}GetListArn:
    Description: ARN of the GET LIST Lambda for {tableName}
    Value: !GetAtt {logicalBase}GetListFunction.Arn

  {logicalBase}GetOneArn:
    Description: ARN of the GET ONE Lambda for {tableName}
    Value: !GetAtt {logicalBase}GetOneFunction.Arn

  {logicalBase}UpdateDeleteArn:
    Description: ARN of the UPDATE/DELETE Lambda for {tableName}
    Value: !GetAtt {logicalBase}UpdateDeleteFunction.Arn

  {logicalBase}DeletePermanentArn:
    Description: ARN of the PERMANENT DELETE Lambda for {tableName}
    Value: !GetAtt {logicalBase}DeletePermanentFunction.Arn
";

        return template.TrimStart('\n', '\r');
    }
}
