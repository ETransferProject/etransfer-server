# Token Configuration API Documentation

## Overview

The Token Configuration API provides management interfaces for controlling deposit, withdraw, and transfer functionality switches for tokens on specific chains.

## API Endpoints

### Base URL
```
/api/etransfer/token-configuration
```

### Interface List

#### 1. Get Token Configuration Status

**Method**: `GET`  
**Path**: `/api/etransfer/token-configuration`  
**Parameters**: Query Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| chainId | string | Yes | Chain ID, e.g., "AELF" |
| symbol | string | Yes | Token symbol, e.g., "USDT" |

**Request Example**:
```http
GET /api/etransfer/token-configuration?chainId=AELF&symbol=USDT
```

**Response Example**:
```json
{
  "chainId": "AELF",
  "symbol": "USDT",
  "depositEnabled": true,
  "withdrawEnabled": true,
  "transferEnabled": true,
  "lastUpdatedTime": "2024-01-15T10:30:00Z"
}
```

#### 2. Set Deposit Enabled Status

**Method**: `POST`  
**Path**: `/api/etransfer/token-configuration/deposit/{chainId}/{symbol}`  
**Parameters**: Path Parameters + Request Body

**Path Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| chainId | string | Yes | Chain ID |
| symbol | string | Yes | Token symbol |

**Request Body**:
```json
{
  "enabled": false
}
```

**Request Example**:
```http
POST /api/etransfer/token-configuration/deposit/AELF/USDT
Content-Type: application/json

{
  "enabled": false
}
```

**Response Example**:
```json
{
  "chainId": "AELF",
  "symbol": "USDT",
  "depositEnabled": false,
  "withdrawEnabled": true,
  "transferEnabled": true,
  "lastUpdatedTime": "2024-01-15T10:35:00Z"
}
```

#### 3. Set Withdraw Enabled Status

**Method**: `POST`  
**Path**: `/api/etransfer/token-configuration/withdraw/{chainId}/{symbol}`  
**Parameters**: Same as deposit interface

**Request Example**:
```http
POST /api/etransfer/token-configuration/withdraw/AELF/USDT
Content-Type: application/json

{
  "enabled": false
}
```

#### 4. Set Transfer Enabled Status

**Method**: `POST`  
**Path**: `/api/etransfer/token-configuration/transfer/{chainId}/{symbol}`  
**Parameters**: Same as deposit interface

**Request Example**:
```http
POST /api/etransfer/token-configuration/transfer/AELF/USDT
Content-Type: application/json

{
  "enabled": true
}
```

#### 5. Batch Set Token Configuration

**Method**: `POST`  
**Path**: `/api/etransfer/token-configuration`  
**Parameters**: Request Body

**Request Body**:
```json
{
  "chainId": "AELF",
  "symbol": "USDT",
  "depositEnabled": false,
  "withdrawEnabled": true,
  "transferEnabled": null
}
```

> **Note**: `null` values mean no change to that configuration

**Request Example**:
```http
POST /api/etransfer/token-configuration
Content-Type: application/json

{
  "chainId": "AELF",
  "symbol": "USDT",
  "depositEnabled": false,
  "withdrawEnabled": true,
  "transferEnabled": null
}
```

#### 6. Batch Enable/Disable All Functions

**Method**: `POST`  
**Path**: `/api/etransfer/token-configuration/all/{chainId}/{symbol}`  
**Parameters**: Path Parameters + Request Body

**Request Example** (Disable all functions):
```http
POST /api/etransfer/token-configuration/all/AELF/USDT
Content-Type: application/json

{
  "enabled": false
}
```

**Request Example** (Enable all functions):
```http
POST /api/etransfer/token-configuration/all/AELF/USDT
Content-Type: application/json

{
  "enabled": true
}
```

## Response Format

### Success Response

All interfaces return the updated token configuration object on success:

```json
{
  "chainId": "string",
  "symbol": "string", 
  "depositEnabled": "boolean",
  "withdrawEnabled": "boolean",
  "transferEnabled": "boolean",
  "lastUpdatedTime": "datetime"
}
```

### Error Response

```json
{
  "error": {
    "code": "ERROR_CODE",
    "message": "Error description",
    "details": "Additional error details"
  }
}
```

## Use Cases

### 1. System Maintenance
Disable all functions during system maintenance:
```bash
curl -X POST "http://localhost:8080/api/etransfer/token-configuration/all/AELF/USDT" \
  -H "Content-Type: application/json" \
  -d '{"enabled": false}'
```

### 2. Emergency Risk Control
Quickly disable related functions when security risks are discovered:
```bash
# Disable USDT withdraw on AELF chain
curl -X POST "http://localhost:8080/api/etransfer/token-configuration/withdraw/AELF/USDT" \
  -H "Content-Type: application/json" \
  -d '{"enabled": false}'
```

### 3. Phased Function Recovery
Gradually restore functions after maintenance:
```bash
# First restore deposit
curl -X POST "http://localhost:8080/api/etransfer/token-configuration/deposit/AELF/USDT" \
  -H "Content-Type: application/json" \
  -d '{"enabled": true}'

# Then restore withdraw
curl -X POST "http://localhost:8080/api/etransfer/token-configuration/withdraw/AELF/USDT" \
  -H "Content-Type: application/json" \
  -d '{"enabled": true}'
```

### 4. Batch Configuration Update
Use batch interface for fine-grained configuration:
```bash
curl -X POST "http://localhost:8080/api/etransfer/token-configuration" \
  -H "Content-Type: application/json" \
  -d '{
    "chainId": "AELF",
    "symbol": "USDT", 
    "depositEnabled": true,
    "withdrawEnabled": false,
    "transferEnabled": null
  }'
```

## Notes

1. **Access Control**: These interfaces typically require administrator privileges; ensure proper authentication and authorization in production
2. **Operation Logs**: All configuration changes are logged in application logs
3. **Cache Updates**: Configuration changes take effect immediately without service restart
4. **Distributed Consistency**: Orleans Grain ensures configuration consistency in distributed environments
5. **Parameter Validation**: ChainId and Symbol parameters cannot be empty, otherwise validation errors will be returned

## Status Codes

- `200 OK`: Operation successful
- `400 Bad Request`: Invalid request parameters
- `401 Unauthorized`: Unauthorized access
- `403 Forbidden`: Insufficient permissions
- `500 Internal Server Error`: Internal server error
