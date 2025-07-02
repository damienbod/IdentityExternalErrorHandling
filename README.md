# ASP.NET Core Identity External OIDC Error Handling

[![.NET](https://github.com/damienbod/IdentityExternalErrorHandling/actions/workflows/dotnet.yml/badge.svg)](https://github.com/damienbod/IdentityExternalErrorHandling/actions/workflows/dotnet.yml)

[Handling OpenID Connect error events in ASP.NET Core](https://damienbod.com/2025/06/02/handling-openid-connect-error-events-in-asp-net-core/)

[Implement ASP.NET Core OpenID Connect with Keykloak to implement Level of Authentication (LoA) requirements](https://damienbod.com/2025/07/02/implement-asp-net-core-openid-connect-with-keykloak-to-implement-level-of-authentication-loa-requirements/)

## Setup

## Migrations

```
Add-Migration "InitializeApp" -Context ApplicationDbContext
```

```
Update-Database -Context ApplicationDbContext
```

## History

- 2025-05-31 Updated packages
- 2025-05-17 Added LoA
- 2025-05-14 Initial version

## Links

https://www.keycloak.org/docs/latest/server_admin/index.html#features

https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-oidc-web-authentication

https://docs.duendesoftware.com/identityserver/fundamentals/openid-connect-events/

https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.openidconnect.openidconnectevents

https://datatracker.ietf.org/doc/html/rfc9126
