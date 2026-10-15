# Git-backed institution modules

The web shell hosts independently built, pinned modules. It reads their definitions and
bindings through `PublicGitHubSource`, checks revision and document/binary hashes, and
uses `InstitutionPluginLoader` to discover the package's factory and web adapter.

| Module | Repository | Composition | Web routes |
| --- | --- | --- | --- |
| Parallel You | `aetheric-forge/parallel-you` | Institution | `/parallel-you/*` |
| Library | `aetheric-forge/library` | Institution | `/library`, `/library/articles/*` |
| Archive | `aetheric-forge/archive` | Backend institution | None |
| Decisions | `aetheric-forge/adr-campus` | Organization under the Administration faculty | `/decisions/*` |

`institution-modules.json` and the matching `institutions/*` Git submodules identify
exact source revisions. The build script verifies those checkouts and builds opaque
bundles under `AethericForge.Web/modules`. It can collect assets from a published host
or directly from a Razor class library. A backend-only bundle has no asset directory.
The generated output is ignored by Git and included in build/publish output.

```sh
git submodule update --init --recursive
python3 scripts/build-institution-modules.py
dotnet test tests/AethericForge.Web.Tests --configuration Release
docker build -t aethericforge-web:modules-review .
```

All bundles are required when building the image. Only selected modules load at runtime.
A revision change requires a new bundle and image. Selected modules require outbound
HTTPS access to public GitHub at startup; there is no SDK or repository build in the pod.
The shell rejects a bundle assigned to the wrong configured module slot.

## Ownership and services

Modules retain their collectible assembly contexts throughout discovery and hosting.
The shell registers module objects by their lowercase IDs, and enumerates those objects
for Razor route discovery and stylesheet links. Framework and core runtime contracts
bind to the shell's assemblies. Library, Archive, Workbench, Post Office, and Decisions
institution/organization contracts are also shared where needed for owner resolution;
the module bundle excludes those explicitly listed contract assemblies.

Library and Archive use keyed capability/provider registries under `Library` and
`Archive`. Decisions contributes its existing scoped providers to those owner facilities.
Library contexts resolve their own knowledge service; an unrelated module's unkeyed
knowledge service is not replaced. The shell's campus maps its existing shared facilities
to these capabilities, then registers the loaded institution factories. Decisions uses
`IOrganizationFactory` and `RegisterOrganization`, with its existing owner's Library and
Workbench. Its package owns review dispatch, authenticated callers, and member/maintainer
policies. It is not replaced with a sovereign Decisions institution.

## Hosting and configuration

Full campus mode (`PublicSite:Enabled=false`) loads all four modules and mounts Decisions
under the Administration faculty. Library and Archive replace their direct campus
composition; the copied Library pages and ADR implementation have been removed from
the shell. The remaining Post Office and Workbench composition supplies Decisions'
existing maintenance and staging capabilities.

In public mode, Parallel You retains its existing consumer artifact. Library and Archive
can be selected with `Library:Enabled=true` and `Archive:Enabled=true`. Their own scoped
settings are required under `Library:MongoDb` and `Archive:S3`. Library uses the shared
web authentication and persistent protection key directory. No public Archive UI is added.
Disabled modules' paths return 404 and their Git definitions are not fetched.

Decisions requires an existing owning institution; setting `Decisions:Enabled=true` in
public-only mode is rejected before module loading. Use the existing full-campus owner,
with its Library/Workbench and organization-directory configuration. This integration
neither creates a new owning campus nor provisions infrastructure or changes production
module flags. Decisions continues using `Organization:*`, `Decisions:MongoDb`,
`Decisions:S3`, `Decisions:RabbitMq`, and its owner's `Workbench:Redis` settings. Its
provisioned directory client uses `Decisions:Directory:Keycloak`; the existing
`Registry:Keycloak` configuration remains a fallback for older installations.

The existing deployment-managed Parallel You consumer configuration and OIDC client are
unchanged. No root provisioning credentials or public binding secrets are introduced.

## Provisioning handoff

Library, Archive, and Decisions now receive complete, private consumer artifacts from
the provisioning worker after a successful institution deployment. The shell reads
`Provisioning:Consumers:Directory/{library|archive|decisions}/configuration.json` and
checks the module, canonical repository deployment identity, environment, plan, and
exact profile settings. Missing files in empty Secret slots leave modules disabled.
Changed projected Secrets trigger a graceful host restart to apply a complete revision.

Library receives its scoped Mongo credentials. Archive uses SeaweedFS bucket-scoped
credentials; the legacy archive reference store key is preserved for existing records.
Decisions requires its catalog-backed owner and existing membership groups. Its
directory service account has read-only query roles and does not replace shell sign-in.
The host must already run in full-campus mode, with
`Decisions:ExpectedOwnerDeploymentId` matching the selected owner and that owner's
Workbench settings configured. Provisioning does not create a sovereign Decisions campus.

See [the worker contract](../../runtime/docs/provisioning/web-module-consumers.md) for
request settings, Secret targets, readiness recording, and SeaweedFS IAM requirements.
Shipping the handoff does not itself submit provisioning requests or migrate data.

## Existing records

| Facility | Preserved identity |
| --- | --- |
| Library articles | `ForgeCampus` knowledge scheme, `knowledge` collection, `forge-library` authority, canonical IDs and revisions |
| Archive | `MinIO` store and configured bucket |
| Decisions record | `adr-campus` knowledge scheme and existing organization IDs |
| Decisions administration | `adr-campus` Archive store, `adr-campus` prefix and existing catalog keys |
| Decisions drafts | `adr-campus-workbench` stage and `adr-campus/drafts/catalog-v1` key |
| Parallel You | Existing provisioned instance, personal Library, Workbench and authentication storage |

Tests cover old article/Archive reads and revisions, strict definition loading,
shared-contract mounting, unrelated-service isolation, authenticated module routes,
and draft creation/proposal publication across the loaded Decisions plugin and its
owner's Workbench and Library. Container checks exercise live Git loading, assets,
and anonymous SSO redirects. Interactive authenticated production workflows are verified
after source review and deployment.

For the existing Administration owner and production activation prerequisites, see
[Decisions activation](decisions-activation.md).
