# Hierarchical service configuration

Aetheric Web resolves each infrastructure setting from `<Institution>:<Service>:<Setting>` first, then the platform default `<Service>:<Setting>`. This extends the section prefixes introduced by the Maintenance jobs feature. Resolution is per setting: `Maintenance:MongoDb:DatabaseName` can override the database while inheriting `MongoDb:Host` and `MongoDb:Port`. There is no parent-institution or sibling fallback.

An explicitly present empty/null value is an override, not a request to inherit. Missing optional settings retain the provider builder's existing defaults. Configuration provider precedence (JSON, environment, etc.) applies within each path before hierarchy resolution. Environment variables use `__` in place of `:`.

## Credential policy

Credentials are read **only** from the consuming institution's service section, even when its endpoint comes from platform defaults. Missing, empty, or whitespace credentials fail resolution with the configuration path, without including their values. Flat credentials are ignored and cannot rescue a partial local credential pair. Redis requires a local password; an optional ACL `User` must also be local. Platform and sibling users are never inherited.

| Service | Institution-only fields |
| --- | --- |
| Keycloak | ClientId, ClientSecret |
| MongoDb | Username, Password |
| RabbitMq | Username, Password |
| S3 | AccessKey, SecretKey |
| Redis | User (optional), Password |

The resolver accepts explicitly supported scalar settings only. Opaque connection strings and unknown/nested fields are rejected to prevent credentials from entering through alternate settings. New provider options must be classified in `InstitutionServiceConfiguration` before use. Endpoint URLs must contain endpoints only; credentials belong in their dedicated fields.

## Connection ownership

| Institution | Services |
| --- | --- |
| ForgeCampus | Redis for Data Protection |
| Registry | Keycloak for sign-in, identity provider, and organization directory |
| Archive | S3 for campus archive |
| Library | MongoDb for campus knowledge |
| PostOffice | RabbitMq for campus post |
| Workbench | Redis for all staging, including ParallelYou and Decisions |
| ParallelYou | S3, MongoDb |
| Decisions | S3, MongoDb, RabbitMq |
| Maintenance | MongoDb for jobs and encrypted credential records |

S3 and MongoDB clients use institution-keyed registrations. All staging Redis providers use the Workbench client and credentials; ParallelYou and Decisions access their stages through Workbench. Data Protection retains its separate ForgeCampus Redis connection. Existing archive store names, knowledge schemes, and staging names remain the routing identifiers. Institutions may use a common endpoint, but operators must provision credentials and grants for each institution's intended resources. Configuration scoping alone does not create server-side access controls. Registry provides the shared identity capability; directory callers use Registry's identity service configuration.

Configuration is resolved when the host or client factory consumes it. Invalid credentials fail before that client connects. Existing singleton connections and OIDC options are not hot-reloaded; restart the host after rotating configuration.

## Deployment migration

Keep noncredential defaults under the existing flat service sections. Move each credential pair to the owning institution and provision appropriate grants. Existing `Maintenance:MongoDb:*` overrides continue to work and may now omit fields supplied by `MongoDb:*`. Configure database, bucket, virtual-host, and endpoint overrides wherever the institution's resources differ. Do not copy a credential with narrow Decisions grants into every institution's settings.

For example (secrets supplied through deployment secret configuration):

```text
MongoDb__Host=mongodb.example.com
MongoDb__Port=27017
MongoDb__AuthenticationDatabase=admin
Maintenance__MongoDb__DatabaseName=maintenance
Maintenance__MongoDb__Username=maintenance-service
Maintenance__MongoDb__Password=<secret>
Decisions__MongoDb__DatabaseName=decisions
Decisions__MongoDb__Username=decisions-service
Decisions__MongoDb__Password=<different-secret>
```

Staging requires only `Workbench:Redis` credentials; remove obsolete `ParallelYou:Redis` and `Decisions:Redis` settings after deploying this change. Shared endpoint defaults remain under `Redis`. If the old scopes used different endpoints or databases, migrate their staging data into the Workbench Redis database before switching; stage names and key prefixes are unchanged.

The updated `.env.example` lists all required credential scopes. Old flat-only deployments must migrate before running this version. No secrets are stored in checked-in appsettings, diagnostics, or exception messages.
