# Identity Integration Architecture

This document captures the high-level architecture for packaging identity integration across Azure AD, Okta, and other OIDC/SAML providers.

## Objectives

- Support SSO authentication for GUI/CLI clients using the organisation's IdP.
- Issue scoped access tokens for automation (policy approvals, remote agent orchestration, repository publishing).
- Enforce MFA and conditional access policies when required by governance.
- Provide RBAC aligned with PackagingTools personas (Developer, Release Engineer, Security Officer, Administrator).

## Core Components

1. **Identity Service Abstraction** – `PackagingTools.Core.Security.Identity.IIdentityService` mediates authentication flows. Implementations can delegate to Azure AD (MSAL) or Okta (OIDC/OAuth). The default implementation returns a service principal for offline scenarios.
2. **Identity Models** – `IdentityRequest`, `IdentityResult`, `IdentityToken`, and `IdentityPrincipal` carry provider metadata, tokens, claims, and role assignments.
3. **Role Model** – Roles map to permissions:
   - `Admin` – Manage policies, identity configuration, environment settings.
   - `SecurityOfficer` – Approve releases, manage signing material, view audit evidence.
   - `ReleaseEngineer` – Execute packaging runs, upload artifacts, manage pipelines.
   - `Developer` – Run local packaging, view run history.
4. **Token Handling** – Access tokens stored in secure store (e.g., FileSecureStore/local secrets) with short TTL and refresh tokens when workflows require long-lived sessions.
5. **MFA Enforcement** – `IdentityRequest.RequireMfa` indicates operations requiring MFA (e.g., policy approval). Providers surface MFA claims which the policy engine validates before continuing.

## Provider Integration

### Azure Active Directory
- Use OAuth 2.0/OIDC with MSAL.
- Acquire tokens for API scopes such as `api://packagingtools/run`, `api://packagingtools/approve`.
- Support device code flow in CLI and interactive flow in GUI.
- Validate Conditional Access claims; ensure `mfa` or `deviceid` present when required.

### Okta
- Use OIDC PKCE flow.
- Map Okta groups to PackagingTools roles.
- Retrieve `id_token` for profile info, `access_token` for API calls.
- Evaluate Okta ThreatInsight events to block risky logins.

## Integration Points

- **CLI / SDK** – Service collection registers `IIdentityService`. Consumers pass `IdentityRequest` specifying provider, scopes, and MFA requirement. Default fallback ensures backwards compatibility.
- **CLI** – `packagingtools identity login` acquires tokens for Azure AD or Okta and persists them into the shared secure store for subsequent runs.
- **Policy Engine** – Validates identity claims for approvals and RBAC.
- **Remote Agents** – Tokens forwarded to remote execution environment for signing/repository publishing.
- **Audit Trail** – Identity principal metadata attached to packaging run logs.

## Configuration

- `identity.provider` – default provider key (`azuread`, `okta`, `local`).
- `identity.azuread.tenantId`, `identity.azuread.clientId`, `identity.azuread.clientSecret`.
- `identity.okta.domain`, `identity.okta.clientId`, `identity.okta.clientSecret`.
- CLI/GUI expose login command to bootstrap tokens and persist refresh tokens in secure store.

## Next Steps

1. Implement Azure AD and Okta identity services using MSAL/Okta SDKs.
2. Persist tokens via secure store with rotation and revocation workflows.
3. Integrate identity claims with policy evaluation (e.g., ensure role membership before approvals).
4. Provide admin UI for role assignments, MFA policy toggles, and audit reporting.
