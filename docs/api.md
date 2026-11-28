# OpenShort Integration API

This document covers the OpenShort API surface that is relevant for integrations with external services.

It intentionally focuses on API key usage, not on the JWT-protected dashboard endpoints used internally by the web UI.

## Base URLs

The API is served by the backend port of the container (`8080`), the same one that answers the short links. The base URL depends on how you published that port:

- `http://<server-ip>/api` with the provided `docker-compose.yml`, which maps it to port `80`
- `http://<server-ip>:8080/api` with the `docker run` examples of the README

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

The API key is generated from the OpenShort dashboard by a signed-in user.

To get one:

1. Sign in to the dashboard
2. Open the **Security** section
3. Generate the API key and save it securely: it is shown only once

Use that key in every integration request through the `X-Api-Key` header.

Important:

- An installation has one API key, shared by all its users and integrations
- Store the API key securely and treat it like a secret
- Regenerating the key invalidates the previous one
- The key cannot be generated or read through the API itself

## What Is Covered Here

This integration guide documents:

- API key authentication format
- Link and domain endpoints, which accept the API key

It does **not** document the endpoints that accept the dashboard login only:

- Dashboard login with JWT and first-run admin setup
- User management
- Two-factor authentication and password change
- Application settings

## Common Response Behavior

- `200 OK` for successful reads and for a domain update
- `201 Created` for newly created resources, with the resource in the body
- `204 No Content` for successful deletes and for a link update
- `400 Bad Request` for invalid input or validation issues
- `401 Unauthorized` for a missing or invalid API key
- `404 Not Found` when a resource does not exist
- `409 Conflict` when the request conflicts with existing data
- `500 Internal Server Error` for unexpected server-side failures

Errors are returned as RFC 7807 problem details. The reason is in the `detail` field. When a field of the request body is missing, too long or cannot be read, `errors` also lists the messages field by field.

## Link Management Endpoints

These endpoints are the most relevant for integrations that create or manage short links programmatically.

A link is returned in this form:

```json
{
  "id": 12,
  "slug": "promo",
  "destinationUrl": "https://example.com/landing-page",
  "domain": "short.example.com",
  "createdAt": "2026-11-28T10:15:00.1078548",
  "expiresAt": null,
  "isActive": true,
  "redirectType": 301,
  "title": "Campaign landing page",
  "notes": "Spring campaign",
  "clickCount": 42,
  "lastAccessedAt": "2026-11-28T11:02:31.0747732"
}
```

The short URL of a link is `https://<domain>/<slug>`. `clickCount` and `lastAccessedAt` are updated shortly after each visit. `expiresAt` cannot be set through the API yet.

Dates and times are in UTC, also when they come without the `Z` suffix.

### List Links

```http
GET /api/links
X-Api-Key: <api-key>
```

Returns all the links.

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

- `destinationUrl` is required and must be an absolute `http://` or `https://` URL: any other scheme is rejected with `400 Bad Request`. It is stored, and returned, as it is sent to the visitor: with ASCII characters only. `https://example.com/caffè` becomes `https://example.com/caff%C3%A8`, an international host its `xn--` form. The maximum length is checked on this form
- `slug` is optional: when it is missing or empty, OpenShort generates one. It can contain only the letters `a`-`z` and `A`-`Z`, digits, `-` and `_`: any other character is rejected with `400 Bad Request`. It is stored in lower case and is unique within its domain. In a short URL the case of the slug does not matter: `/Promo` and `/promo` are the same link. A slug with other characters, stored by a version before 0.4.1, keeps working. For those slugs only: a letter with an accent is another slug with SQLite, while MySQL may read it as the plain letter
- `domain` is required, and must already exist in OpenShort and be active
- `redirectType: 301` means permanent redirect and is the default
- `redirectType: 302` means temporary redirect
- `title` and `notes` are optional
- Maximum lengths: `slug` 50 characters, `destinationUrl` 2048, `title` 100, `notes` 500. A longer value is rejected with `400 Bad Request`
- `isActive` controls whether the short link can be resolved publicly; the default is `true`
- a link that is not active or has expired, and every link of a domain that is not active, answers like a link that does not exist: the 404 page, or the custom URL set for the domain

The response is `201 Created` with the link, including its `id` and the slug that was generated.

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

All the fields of the body are replaced: a field left out goes back to its default (`redirectType` 301, `isActive` true, `title` and `notes` empty). The slug and the domain of a link cannot be changed.

`destinationUrl`, `title` and `notes` follow the rules of the creation. One exception: a value equal to the stored one, as the API returns it, is accepted as it is. A link created by an earlier version with another scheme, or with a value over the maximum length, can still be renamed or turned off. A new `title` or new `notes` over the maximum length are rejected with `400 Bad Request` and the reason in `detail` only: "Title must be at most 100 characters." or "Notes must be at most 500 characters."

The response is `204 No Content`.

### Delete Link

```http
DELETE /api/links/{id}
X-Api-Key: <api-key>
```

## Domain Endpoints

These endpoints are useful if your integration also manages custom domains inside OpenShort.

A domain is returned in this form:

```json
{
  "id": 3,
  "host": "short.example.com",
  "isActive": true,
  "notFoundBehavior": 0,
  "notFoundRedirectUrl": null,
  "createdAt": "2026-11-28T10:00:00.9778134"
}
```

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

The host is stored in lower case and can be at most 255 characters: a longer one is rejected with `400 Bad Request`. The domain is created active, and shows the OpenShort "link not found" page for links that do not exist. If the host already exists the response is `409 Conflict`.

### Update Domain Settings

```http
PUT /api/domains/{id}
X-Api-Key: <api-key>
Content-Type: application/json
```

Request body:

```json
{
  "isActive": true,
  "notFoundBehavior": 1,
  "notFoundRedirectUrl": "https://example.com/not-found"
}
```

Field notes:

- `isActive: false` turns the domain off: none of its links redirects until it is active again, and no new link can be created on it. No link is deleted
- `notFoundBehavior: 0` shows the OpenShort "link not found" page with status `404`
- `notFoundBehavior: 1` redirects the visitor to `notFoundRedirectUrl`, which is then required and must be an absolute `http://` or `https://` URL. It is stored with ASCII characters only, like the destination of a link
- All three fields are replaced: send `isActive` every time, or the domain is turned off
- The host of a domain cannot be changed

The response is `200 OK` with the domain.

### Get Link Count for a Domain

```http
GET /api/domains/{id}/link-count
X-Api-Key: <api-key>
```

Returns the number of links of the domain, as a plain number.

### Delete Domain

```http
DELETE /api/domains/{id}
X-Api-Key: <api-key>
```

Deleting a domain also deletes all its links.

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

Each of these is the `detail` field of a problem details response.

### Invalid destination URL

`400 Bad Request`, for a destination that is not an absolute `http://` or `https://` URL:

```json
{
  "detail": "Destination URL must be an absolute http or https URL of at most 2048 characters."
}
```

### Invalid slug

`400 Bad Request`:

```json
{
  "detail": "Slug can contain only letters, digits, '-' and '_'.",
  "errors": {
    "Slug": ["Slug can contain only letters, digits, '-' and '_'."]
  }
}
```

### Field too long

`400 Bad Request`:

```json
{
  "detail": "The field Title must be a string with a maximum length of 100.",
  "errors": {
    "Title": ["The field Title must be a string with a maximum length of 100."]
  }
}
```

### Invalid custom 404 URL

`400 Bad Request`, when `notFoundBehavior` is `1` and `notFoundRedirectUrl` is missing or is not an absolute `http://` or `https://` URL:

```json
{
  "detail": "An absolute http or https custom 404 URL is required when redirect mode is enabled."
}
```

### Domain missing

`400 Bad Request`, if `domain` is missing or empty:

```json
{
  "detail": "Domain is required."
}
```

### Domain not authorized

`400 Bad Request`, if the requested domain does not exist in OpenShort:

```json
{
  "detail": "Domain 'short.example.com' is not authorized."
}
```

### Domain not active

`400 Bad Request`, if the requested domain exists but is turned off:

```json
{
  "detail": "Domain 'short.example.com' is not active."
}
```

### Slug conflict

`409 Conflict`, if the slug is already used on the same domain:

```json
{
  "detail": "Slug already in use for this domain."
}
```

## Notes

- Endpoint behavior can evolve between releases: the release notes list the changes
- The published image does not expose Swagger/OpenAPI. It is available when the backend runs in the `Development` environment
- This document is intended as an integration guide for API key usage
