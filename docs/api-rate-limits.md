# Aurora Cloud Storage - API Rate Limits

## Overview
The Aurora REST API is available to all paid plans. Requests are authenticated
using an API key passed in the `Authorization` header.

## Rate Limits by Plan
Starter plan API keys are limited to 60 requests per minute and 20,000 requests per
day. Business plan API keys are limited to 300 requests per minute and 200,000
requests per day. Enterprise plan API keys have no fixed daily cap and instead use
an adaptive limit based on sustained account throughput, negotiated at contract time.

## Rate Limit Headers
Every API response includes `X-RateLimit-Limit`, `X-RateLimit-Remaining`, and
`X-RateLimit-Reset` headers so clients can back off before hitting the limit.

## Exceeding the Limit
Requests made after the limit is exceeded receive an HTTP 429 Too Many Requests
response with a `Retry-After` header indicating how many seconds to wait.

## Bulk Uploads
For uploading more than 10,000 files at once, Aurora recommends using the batch
upload endpoint (`POST /v1/batch/upload`) instead of individual file requests, since
batch requests are counted as a single request against the rate limit.
