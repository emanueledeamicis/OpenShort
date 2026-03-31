# OpenShort Integration API

This document covers the OpenShort API surface that is relevant for integrations with external services.

It intentionally focuses on API key usage, not on the JWT-protected dashboard endpoints used internally by the web UI.

## Base URLs

- Integration API base URL: `http://<server-ip>/api`

Examples below assume:

```text
http://localhost/api
```

## Authentication

Integration requests use an API key sent in the `X-Api-Key` header:

```http
X-Api-Key: <api-key>
```

## Getting an API Key

API keys are generated from the OpenShort dashboard by an authenticated administrator.

To get one:

1. Sign in to the dashboard
2. Open the **Security** section
3. Generate the API key and save it securely

Use that key in every integration request through the `X-Api-Key` header.

Once generated, use the key in external services, scripts, automation tools, internal portals, or custom apps.

Important:

- Store the API key securely
- Treat it like a secret
- Regenerating the key invalidates the previous one

## What Is Covered Here

This integration guide documents:

- API key authentication format
- Link management endpoints that support API key authentication

It does **not** document:

- Dashboard login with JWT
- First-run admin setup
- Admin user management
- Dashboard-only settings and maintenance flows

## Common Response Behavior

- `200 OK` for successful reads and most successful writes
- `201 Created` for newly created resources
- `204 No Content` for successful deletes or updates with no response body
- `400 Bad Request` for invalid input or validation issues
- `401 Unauthorized` for a missing or invalid API key
- `404 Not Found` when a resource does not exist
- `409 Conflict` when the request conflicts with existing data
- `500 Internal Server Error` for unexpected server-side failures

OpenShort often returns RFC 7807-style problem details for API errors.

## Link Management Endpoints

These endpoints are the most relevant for integrations that create or manage short links programmatically.

### List Links

```http
GET /api/links
X-Api-Key: <api-key>
```

### Get Link by ID

```http
GET /api/links/{id}
X-Api-Key: <api-key>
```

### Create Link

```http
POST /api/links
X-Api-Key: <api-key>
Content-Type: application/json
```

Request body:

```json
{
  "destinationUrl": "https://example.com/landing-page",
  "slug": "promo",
  "domain": "short.example.com",
  "redirectType": 301,
  "title": "Campaign landing page",
  "notes": "Spring campaign",
  "isActive": true
}
```

Field notes:

- `destinationUrl` must be an absolute `http://` or `https://` URL
- `slug` is optional
- `domain` must already exist in OpenShort and be active
- `redirectType: 301` means permanent redirect
- `redirectType: 302` means temporary redirect
- `isActive` controls whether the short link can be resolved publicly

### Update Link

```http
PUT /api/links/{id}
X-Api-Key: <api-key>
Content-Type: application/json
```

Request body:

```json
{
  "destinationUrl": "https://example.com/new-target",
  "redirectType": 302,
  "title": "Updated title",
  "notes": "Temporary redirect for testing",
  "isActive": true
}
```

### Delete Link

```http
DELETE /api/links/{id}
X-Api-Key: <api-key>
```

## Domain Endpoints

These endpoints are useful if your integration also manages custom domains inside OpenShort.

### List Domains

```http
GET /api/domains
X-Api-Key: <api-key>
```

### Get Domain by ID

```http
GET /api/domains/{id}
X-Api-Key: <api-key>
```

### Create Domain

```http
POST /api/domains
X-Api-Key: <api-key>
Content-Type: application/json
```

Request body:

```json
{
  "host": "short.example.com"
}
```

### Get Link Count for a Domain

```http
GET /api/domains/{id}/link-count
X-Api-Key: <api-key>
```

### Delete Domain

```http
DELETE /api/domains/{id}
X-Api-Key: <api-key>
```

## Typical Integration Flow

### 1. Create or verify the domain

```bash
curl -X POST http://localhost/api/domains \
  -H "X-Api-Key: <api-key>" \
  -H "Content-Type: application/json" \
  -d "{\"host\":\"short.example.com\"}"
```

### 2. Create a short link

```bash
curl -X POST http://localhost/api/links \
  -H "X-Api-Key: <api-key>" \
  -H "Content-Type: application/json" \
  -d "{\"destinationUrl\":\"https://example.com\",\"slug\":\"promo\",\"domain\":\"short.example.com\",\"redirectType\":301,\"isActive\":true}"
```

## Example Error Cases

### Invalid destination URL

You may receive:

```json
{
  "detail": "Invalid Destination URL format."
}
```

### Domain not authorized

If the requested domain does not exist in OpenShort:

```json
{
  "detail": "Domain 'short.example.com' is not authorized."
}
```

### Slug conflict

If the slug is already used on the same domain:

```json
{
  "detail": "Slug already in use for this domain."
}
```

## Notes

- Endpoint behavior can evolve between releases
- For the most precise schema, use the running API and Swagger/OpenAPI output during development
- This document is intended as an integration guide for API key usage
