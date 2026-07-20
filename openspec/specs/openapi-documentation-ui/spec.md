# openapi-documentation-ui Specification

## Purpose
TBD - created by archiving change et005-auth-refresh. Update Purpose after archive.
## Requirements
### Requirement: Scalar Serves Interactive OpenAPI Documentation
The `Api` layer SHALL serve Scalar as the interactive OpenAPI documentation UI, rendering the same
OpenAPI document ASP.NET Core already generates, replacing Swagger UI as the browsing tool
(`docs/decisions/ADR-0004`). The underlying OpenAPI document/contract SHALL NOT change as a result
of this switch.

#### Scenario: Scalar UI is reachable and renders the OpenAPI document
- **WHEN** a developer navigates to the configured Scalar documentation route on a running API
  instance
- **THEN** the response SHALL render the Scalar UI populated from the API's generated OpenAPI
  document

#### Scenario: OpenAPI document content is unchanged by the UI switch
- **WHEN** the generated OpenAPI JSON/YAML document is compared before and after replacing Swagger
  UI with Scalar
- **THEN** the set of documented paths, operations, and schemas SHALL be identical — only the
  browsing UI changes

