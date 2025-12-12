using System;

namespace CodeGenApp.Services;

public class LambdaCodeGenerator : ILambdaCodeGenerator
{
    public string GenerateCreatePython(string tableName, string pkName, string? skName)
    {
        const string template = @"
# Auto-generated Lambda for DynamoDB
# Purpose: CREATE items
# Table: __TABLE_NAME__
# PK: __PK_NAME__
# Sort key: __SK_COMMENT__

import json
import os
from datetime import datetime
from decimal import Decimal

import boto3

dynamodb = boto3.resource('dynamodb')
TABLE_NAME = os.environ.get('TABLE_NAME', '__TABLE_NAME__')
table = dynamodb.Table(TABLE_NAME)

PK = '__PK_NAME__'
SK = __SK_VALUE__

class DecimalEncoder(json.JSONEncoder):
    def default(self, o):
        if isinstance(o, Decimal):
            return float(o)
        return super().default(o)

def build_response(status_code, body):
    return {
        'statusCode': status_code,
        'headers': {
            'Content-Type': 'application/json',
            'Access-Control-Allow-Origin': '*',
            'Access-Control-Allow-Headers': 'Content-Type,Authorization,X-Requested-With',
            'Access-Control-Allow-Methods': 'GET,POST,PUT,DELETE,OPTIONS',
            'Access-Control-Allow-Credentials': 'true',
        },
        'body': json.dumps(body, cls=DecimalEncoder),
    }

def parse_body(event):
    try:
        if event.get('body'):
            return json.loads(event['body'])
        return event.get('body_dict', {}) or {}
    except Exception:
        return {}

def get_jwt_from_event(event):
    try:
        headers = event.get('headers') or {}
        auth = headers.get('Authorization') or headers.get('authorization')
        if not auth:
            return None
        # Support ""Bearer <token>"" and raw token
        if auth.lower().startswith('bearer '):
            return auth[7:]
        return auth
    except Exception:
        return None

def lambda_handler(event, context):
    print('CREATE event:', json.dumps(event))

    jwt_token = get_jwt_from_event(event)
    if jwt_token:
        print('JWT token received (truncated):', jwt_token[:50])

    body = parse_body(event)
    if not body:
        return build_response(400, {'error': 'Empty body'})

    if PK not in body:
        return build_response(400, {'error': 'Missing partition key ' + PK + ' in body'})

    item = dict(body)

    # Default timestamps
    now_iso = datetime.utcnow().isoformat()
    if 'created_at' not in item:
        item['created_at'] = now_iso
    if 'updated_at' not in item:
        item['updated_at'] = now_iso

    # Default is_active
    if 'is_active' not in item:
        item['is_active'] = True

    try:
        table.put_item(Item=item)
        return build_response(201, item)
    except Exception as e:
        print('DynamoDB put_item error:', str(e))
        return build_response(500, {'error': 'DynamoDB put_item failed', 'details': str(e)})
";

        var skComment = skName ?? "none";
        var skValue = skName is null ? "None  # no sort key" : $"'{skName}'  # sort key";

        var code = template
            .Replace("__TABLE_NAME__", tableName)
            .Replace("__PK_NAME__", pkName)
            .Replace("__SK_COMMENT__", skComment)
            .Replace("__SK_VALUE__", skValue);

        return code.TrimStart('\n', '\r');
    }

    public string GenerateGetListPython(string tableName, string pkName, string? skName)
    {
        const string template = @"
# Auto-generated Lambda for DynamoDB
# Purpose: GET ALL active items (or full scan)
# Table: __TABLE_NAME__
# PK: __PK_NAME__
# Sort key: __SK_COMMENT__

import json
import os
from decimal import Decimal

import boto3

dynamodb = boto3.resource('dynamodb')
TABLE_NAME = os.environ.get('TABLE_NAME', '__TABLE_NAME__')
table = dynamodb.Table(TABLE_NAME)

class DecimalEncoder(json.JSONEncoder):
    def default(self, o):
        if isinstance(o, Decimal):
            return float(o)
        return super().default(o)

def build_response(status_code, body):
    return {
        'statusCode': status_code,
        'headers': {
            'Content-Type': 'application/json',
            'Access-Control-Allow-Origin': '*',
            'Access-Control-Allow-Headers': 'Content-Type,Authorization,X-Requested-With',
            'Access-Control-Allow-Methods': 'GET,POST,PUT,DELETE,OPTIONS',
            'Access-Control-Allow-Credentials': 'true',
        },
        'body': json.dumps(body, cls=DecimalEncoder),
    }

def get_jwt_from_event(event):
    try:
        headers = event.get('headers') or {}
        auth = headers.get('Authorization') or headers.get('authorization')
        if not auth:
            return None
        if auth.lower().startswith('bearer '):
            return auth[7:]
        return auth
    except Exception:
        return None

def lambda_handler(event, context):
    print('GET LIST event:', json.dumps(event))

    jwt_token = get_jwt_from_event(event)
    if jwt_token:
        print('JWT token received (truncated):', jwt_token[:50])

    try:
        # Simple full scan; you can add filters later (e.g., is_active = true)
        resp = table.scan()
        items = resp.get('Items', [])
        return build_response(200, items)
    except Exception as e:
        print('DynamoDB scan error:', str(e))
        return build_response(500, {'error': 'DynamoDB scan failed', 'details': str(e)})
";

        var skComment = skName ?? "none";

        var code = template
            .Replace("__TABLE_NAME__", tableName)
            .Replace("__PK_NAME__", pkName)
            .Replace("__SK_COMMENT__", skComment);

        return code.TrimStart('\n', '\r');
    }

    public string GenerateGetOnePython(string tableName, string pkName, string? skName)
    {
        const string template = @"
# Auto-generated Lambda for DynamoDB
# Purpose: GET exactly one item by PK (+ optional SK via ?sk=)
# Table: __TABLE_NAME__
# PK: __PK_NAME__
# Sort key: __SK_COMMENT__

import json
import os
from decimal import Decimal

import boto3

dynamodb = boto3.resource('dynamodb')
TABLE_NAME = os.environ.get('TABLE_NAME', '__TABLE_NAME__')
table = dynamodb.Table(TABLE_NAME)

PK = '__PK_NAME__'   # partition key
SK = __SK_VALUE__

class DecimalEncoder(json.JSONEncoder):
    def default(self, o):
        if isinstance(o, Decimal):
            return float(o)
        return super().default(o)

def build_response(status_code, body):
    return {
        'statusCode': status_code,
        'headers': {
            'Content-Type': 'application/json',
            'Access-Control-Allow-Origin': '*',
            'Access-Control-Allow-Headers': 'Content-Type,Authorization,X-Requested-With',
            'Access-Control-Allow-Methods': 'GET,POST,PUT,DELETE,OPTIONS',
            'Access-Control-Allow-Credentials': 'true',
        },
        'body': json.dumps(body, cls=DecimalEncoder),
    }

def get_jwt_from_event(event):
    try:
        headers = event.get('headers') or {}
        auth = headers.get('Authorization') or headers.get('authorization')
        if not auth:
            return None
        if auth.lower().startswith('bearer '):
            return auth[7:]
        return auth
    except Exception:
        return None

def lambda_handler(event, context):
    print('GET ONE event:', json.dumps(event))

    jwt_token = get_jwt_from_event(event)
    if jwt_token:
        print('JWT token received (truncated):', jwt_token[:50])

    path_params = event.get('pathParameters') or {}
    query_params = event.get('queryStringParameters') or {}

    # PK from path /resource/{id}
    record_id = path_params.get('id')
    if not record_id:
        return build_response(400, {'error': 'Missing id path parameter (/{id})'})

    sort_value = None
    if SK:
        if not query_params or 'sk' not in query_params:
            return build_response(
                400,
                {'error': 'Missing sort key ' + SK + '. Provide ?sk=... in query string.'}
            )
        sort_value = query_params['sk']

    key = {PK: record_id}
    if SK:
        key[SK] = sort_value

    print('DDB KEY:', key)

    try:
        resp = table.get_item(Key=key)
        item = resp.get('Item')
        print('DDB RESP:', resp)

        if not item:
            return build_response(404, {'error': 'Not found'})

        return build_response(200, item)
    except Exception as e:
        print('DynamoDB get_item error:', repr(e))
        return build_response(
            500,
            {'error': 'DynamoDB get_item failed', 'details': str(e)}
        )
";

        var skComment = skName ?? "none";
        var skValue = skName is null ? "None  # no sort key" : $"'{skName}'  # sort key";

        var code = template
            .Replace("__TABLE_NAME__", tableName)
            .Replace("__PK_NAME__", pkName)
            .Replace("__SK_COMMENT__", skComment)
            .Replace("__SK_VALUE__", skValue);

        return code.TrimStart('\n', '\r');
    }

    public string GenerateUpdateDeletePython(string tableName, string pkName, string? skName)
    {
        const string template = @"
# Auto-generated Lambda for DynamoDB
# Purpose: UPDATE or SOFT DELETE items
# Table: __TABLE_NAME__
# PK: __PK_NAME__
# Sort key: __SK_COMMENT__

import json
import os
from datetime import datetime
from decimal import Decimal

import boto3

dynamodb = boto3.resource('dynamodb')
TABLE_NAME = os.environ.get('TABLE_NAME', '__TABLE_NAME__')
table = dynamodb.Table(TABLE_NAME)

PK = '__PK_NAME__'   # partition key
SK = __SK_VALUE__    # sort key or None

class DecimalEncoder(json.JSONEncoder):
    def default(self, o):
        if isinstance(o, Decimal):
            return float(o)
        return super().default(o)

def build_response(status_code, body):
    return {
        'statusCode': status_code,
        'headers': {
            'Content-Type': 'application/json',
            'Access-Control-Allow-Origin': '*',
            'Access-Control-Allow-Headers': 'Content-Type,Authorization,X-Requested-With',
            'Access-Control-Allow-Methods': 'GET,POST,PUT,DELETE,OPTIONS',
            'Access-Control-Allow-Credentials': 'true',
        },
        'body': json.dumps(body, cls=DecimalEncoder),
    }

def parse_body(event):
    try:
        if event.get('body'):
            return json.loads(event['body'])
        return event.get('body_dict', {}) or {}
    except Exception:
        return {}

def get_jwt_from_event(event):
    try:
        headers = event.get('headers') or {}
        auth = headers.get('Authorization') or headers.get('authorization')
        if not auth:
            return None
        if auth.lower().startswith('bearer '):
            return auth[7:]
        return auth
    except Exception:
        return None

def lambda_handler(event, context):
    print('UPDATE/DELETE event:', json.dumps(event))

    jwt_token = get_jwt_from_event(event)
    if jwt_token:
        print('JWT token received (truncated):', jwt_token[:50])

    body = parse_body(event)
    path_params = event.get('pathParameters') or {}
    qs = event.get('queryStringParameters') or {}

    # Operation (default: update)
    operation = (
        (body.get('operation') or (qs or {}).get('operation') or event.get('operation') or 'update')
    ).lower()

    # PK from path /resource/{id}
    record_id = (
        path_params.get('id')
        or body.get(PK)
        or event.get(PK)
    )

    if not record_id:
        return build_response(
            400,
            {
                'error': 'Missing partition key ' + PK + '. It should be in the path /{id} or in the request body.'
            }
        )

    # SK from ?sk=... (API) or body/event for tests
    sort_value = None
    if SK:
        if qs and 'sk' in qs:
            sort_value = qs['sk']
        else:
            sort_value = body.get(SK) or event.get(SK)

        if sort_value is None:
            return build_response(
                400,
                {
                    'error': 'Missing sort key ' + SK + '. Provide ?sk=... in query string or include ' + SK + ' in the body for tests.'
                }
            )

    key = {PK: record_id}
    if SK:
        key[SK] = sort_value

    # ---------- SOFT DELETE ----------
    if operation == 'soft_delete':
        resp = table.update_item(
            Key=key,
            UpdateExpression='SET is_active = :false',
            ExpressionAttributeValues={':false': False},
            ReturnValues='ALL_NEW',
        )
        return build_response(
            200,
            {
                'deleted': True,
                'item': resp.get('Attributes', {}),
            },
        )

    # ---------- UPDATE ----------
    update_payload = {
        k: v for k, v in body.items()
        if k not in {PK, (SK or ''), 'operation'}
    }

    if not update_payload:
        return build_response(400, {'error': 'No fields to update'})

    update_expr_parts = []
    expr_attr_names = {}
    expr_attr_values = {}

    for field_name, value in update_payload.items():
        placeholder_name = '#' + field_name
        placeholder_value = ':' + field_name
        update_expr_parts.append(placeholder_name + ' = ' + placeholder_value)
        expr_attr_names[placeholder_name] = field_name
        expr_attr_values[placeholder_value] = value

    # Always update updated_at
    update_expr_parts.append('#updated_at = :updated_at')
    expr_attr_names['#updated_at'] = 'updated_at'
    expr_attr_values[':updated_at'] = datetime.utcnow().isoformat()

    update_expr = 'SET ' + ', '.join(update_expr_parts)

    resp = table.update_item(
        Key=key,
        UpdateExpression=update_expr,
        ExpressionAttributeNames=expr_attr_names,
        ExpressionAttributeValues=expr_attr_values,
        ReturnValues='ALL_NEW',
    )

    return build_response(200, resp.get('Attributes', {}))
";

        var skComment = skName ?? "none";
        var skValue = skName is null ? "None" : $"'{skName}'";

        var code = template
            .Replace("__TABLE_NAME__", tableName)
            .Replace("__PK_NAME__", pkName)
            .Replace("__SK_COMMENT__", skComment)
            .Replace("__SK_VALUE__", skValue);

        return code.TrimStart('\n', '\r');
    }

    public string GenerateDeletePermanentPython(string tableName, string pkName, string? skName)
    {
        const string template = @"
# Auto-generated Lambda for DynamoDB
# Purpose: PERMANENT DELETE items
# Table: __TABLE_NAME__
# PK: __PK_NAME__
# Sort key: __SK_COMMENT__

import json
import os
from decimal import Decimal

import boto3

dynamodb = boto3.resource('dynamodb')
TABLE_NAME = os.environ.get('TABLE_NAME', '__TABLE_NAME__')
table = dynamodb.Table(TABLE_NAME)

PK = '__PK_NAME__'
SK = __SK_VALUE__

class DecimalEncoder(json.JSONEncoder):
    def default(self, o):
        if isinstance(o, Decimal):
            return float(o)
        return super().default(o)

def build_response(status_code, body):
    return {
        'statusCode': status_code,
        'headers': {
            'Content-Type': 'application/json',
            'Access-Control-Allow-Origin': '*',
            'Access-Control-Allow-Headers': 'Content-Type,Authorization,X-Requested-With',
            'Access-Control-Allow-Methods': 'GET,POST,PUT,DELETE,OPTIONS',
            'Access-Control-Allow-Credentials': 'true',
        },
        'body': json.dumps(body, cls=DecimalEncoder),
    }

def parse_body(event):
    try:
        if event.get('body'):
            return json.loads(event['body'])
        return event.get('body_dict', {}) or {}
    except Exception:
        return {}

def get_jwt_from_event(event):
    try:
        headers = event.get('headers') or {}
        auth = headers.get('Authorization') or headers.get('authorization')
        if not auth:
            return None
        if auth.lower().startswith('bearer '):
            return auth[7:]
        return auth
    except Exception:
        return None

def lambda_handler(event, context):
    '''
    Supports:
    - API Gateway proxy:
        DELETE /resource/{id}/permanent?sk=123
    - Direct invocation (for tests):
        { 'PK': '...', 'SK': '...' }
    '''
    print('DELETE PERMANENT event:', json.dumps(event))

    jwt_token = get_jwt_from_event(event)
    if jwt_token:
        print('JWT token received (truncated):', jwt_token[:50])

    record_id = None
    sort_value = None

    if isinstance(event, dict) and event.get('httpMethod'):
        path_params = event.get('pathParameters') or {}
        qs = event.get('queryStringParameters') or {}

        record_id = path_params.get('id')

        if SK:
            if qs and 'sk' in qs:
                sort_value = qs['sk']
    else:
        body = parse_body(event)
        record_id = body.get(PK) or event.get(PK)
        if SK:
            sort_value = body.get(SK) or event.get(SK)

    if not record_id:
        return build_response(
            400,
            {'error': 'Missing partition key ' + PK + '. Provide /{id} in path or ' + PK + ' in event/body.'}
        )

    if SK and not sort_value:
        return build_response(
            400,
            {'error': 'Missing sort key ' + SK + '. Provide ?sk=... in query string or ' + SK + ' in event/body for tests.'}
        )

    key = {PK: record_id}
    if SK:
        key[SK] = sort_value

    print('DELETE key:', key)

    try:
        resp = table.delete_item(
            Key=key,
            ReturnValues='ALL_OLD',
        )
    except Exception as e:
        print('DynamoDB delete_item error:', str(e))
        return build_response(
            500,
            {'error': 'DynamoDB delete_item failed', 'details': str(e)}
        )

    deleted_item = resp.get('Attributes')
    if not deleted_item:
        return build_response(
            404,
            {'deleted': False, 'error': 'Not found'}
        )

    return build_response(
        200,
        {'deleted': True, 'item': deleted_item}
    )
";

        var skComment = skName ?? "none";
        var skValue = skName is null ? "None" : $"'{skName}'";

        var code = template
            .Replace("__TABLE_NAME__", tableName)
            .Replace("__PK_NAME__", pkName)
            .Replace("__SK_COMMENT__", skComment)
            .Replace("__SK_VALUE__", skValue);

        return code.TrimStart('\n', '\r');
    }

    // NEW: Lambda authorizer generator
    public string GenerateAuthorizerPython(string tableName, string pkName, string? skName, string safeBaseName)
    {
        const string template = @"
# Auto-generated Lambda AUTHORIZER
# Tied to API/table: __TABLE_NAME__
# This validates a JWT (HS256) using JWT_SECRET from environment
# and returns an IAM policy for API Gateway (TOKEN authorizer).

import json
import os

import jwt

JWT_SECRET = os.environ.get('JWT_SECRET', 'JWTSecret')

def build_policy(principal_id, effect, resource, context=None):
    auth_response = {
        'principalId': principal_id,
        'policyDocument': {
            'Version': '2012-10-17',
            'Statement': [
                {
                    'Action': 'execute-api:Invoke',
                    'Effect': effect,
                    'Resource': resource,
                }
            ],
        },
    }
    if context:
        auth_response['context'] = context
    return auth_response

def lambda_handler(event, context):
    '''
    Expected event (TOKEN authorizer):
    {
      'type': 'TOKEN',
      'authorizationToken': 'Bearer <JWT>',
      'methodArn': 'arn:aws:execute-api:...'
    }
    '''
    print('AUTHORIZER event:', json.dumps(event))

    token = event.get('authorizationToken')
    method_arn = event.get('methodArn', '*')

    if not token:
        print('No authorizationToken provided.')
        raise Exception('Unauthorized')

    # Strip ""Bearer "" if present
    if token.lower().startswith('bearer '):
        token = token[7:]

    try:
        decoded = jwt.decode(token, JWT_SECRET, algorithms=['HS256'])
        print('Decoded JWT:', json.dumps(decoded))

        principal_id = decoded.get('userId') or decoded.get('sub') or 'user'
        context = {
            'email': decoded.get('email', ''),
            'username': decoded.get('username', ''),
            'role': decoded.get('role', ''),
            'status': decoded.get('status', ''),
        }

        return build_policy(principal_id, 'Allow', method_arn, context=context)

    except Exception as e:
        print('JWT decode/validation failed:', str(e))
        raise Exception('Unauthorized')
";

        var code = template
            .Replace("__TABLE_NAME__", tableName);

        return code.TrimStart('\n', '\r');
    }
}
