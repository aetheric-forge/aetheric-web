# Parallel You module and provisioning handoff

Parallel You's reusable composition lives in the external `aetheric-forge/parallel-you`
repository, pinned under `institutions/parallel-you`. It accepts an institution context,
an explicit Librarian, and an explicit Artificer. The web shell loads that composition and
its Razor pages through the institution plugin loader; the implementation is no longer
copied into or directly referenced by the shell project.

## Current host integration

With `PublicSite:Enabled=true`, set `ParallelYou:Enabled=true` to enable the module independently.
This enables OIDC authentication and requires the existing `Registry:Keycloak` settings and a
persistent `PublicSite:ProtectionKeyDirectory`. It does not require `PublicSite:SignInEnabled`.
Use `ParallelYou:MongoDb` for the selected Library database and scoped credentials, and
`Workbench:Redis` for the parent staging connection. The existing institution setting resolver
can inherit endpoint defaults, but credentials remain explicit in their owning sections.
Missing module credentials fail at startup; disabled module routes return 404.

Full-campus mode continues to register the institution under the campus, using the same module
composition. Working pages require authentication. The module's identity accessor uses the
validated OIDC subject; its Person service reads/writes through the module's own Library.

All seven workflow services currently store their artifacts and representations through the
Mongo knowledge provider. They do not directly use Archive or S3. Five staging names are
preserved: ReflectionMapping, TrackingCurrent, IntentionCurrent, PlanCurrent, RecommendationCurrent.
The module's Library contains only the `parallel-you` provider, so publishing cannot fall through
to the shared ForgeCampus provider. Its collection remains `knowledge`.

Before enabling this against existing data, inspect the former ForgeCampus knowledge store.
The old shared Librarian selected the first registered provider for publication, which means
existing personal records may live there. A new dedicated database will not automatically
discover those records or retain their Person IDs. Plan and verify a copy/migration before
switching an existing user's deployment. This change does not migrate data.

## Provisioning contract

`institutions/parallel-you/institution/parallel-you.yaml` and its sibling bindings file use the existing
provisioning definition format. The institution owns a personal Library and inherits Registrar
and Workbench. The example uses the existing Mongo provider to create a database-scoped user.
It is an example deployment, not evidence that an institution has been provisioned.

The provisioning service owns these integration steps:

1. Load a pinned definition and bindings revision, verify the selected parent's capabilities,
   and execute the existing provisioning plan.
2. Persist an institution deployment record containing a stable instance ID, definition ID and
   version, parent instance ID, environment, source commit/hashes, resolved resource locations,
   secret references, plan/run ID, and provisioning outcome. Keep failed/pending attempts distinct
   from a successfully provisioned revision. Names alone are not identities.
3. Resolve successful bindings into a consumer configuration for this module: Mongo endpoint,
   database, generated scoped username/password and authentication database (`admin` for the
   current Mongo provisioner); parent Workbench endpoint/database/ACL grant; and a web OIDC
   client with the web host's callback/logout URLs. The bootstrap administrator's login client
   is not automatically the web application's client.
4. Deliver only the module's consumer settings through deployment-managed secrets or an
   authenticated configuration endpoint. The web host should not mount admin's root credential
   or encryption-key directories. Restart the host when a new configuration revision is applied.

The provisioning worker now records executed institutions in a durable catalog and exports a
Parallel You consumer configuration after creating scoped Mongo, Redis and browser OIDC grants.
It can deliver the artifact to a private file or a named Kubernetes Secret. The module host loads
it with `ParallelYou:ProvisionedConfigurationPath`, verifies the expected `ParallelYou:DeploymentId`
and `ParallelYou:Environment`, and builds its context with the provisioned name/version.
Missing artifacts or incomplete settings fail startup. This artifact is for module hosting inside the public web shell (`PublicSite:Enabled=true`), because its browser client has no Registry management roles.
The full-campus host continues to use its institution configuration.

See `runtime/docs/provisioning/institution-catalog.md` for the inventory query, worker settings,
consumer Secret RBAC example and mount configuration. Operator deployment and existing-data
migration are separate from creating the configuration artifact. Connection and OIDC settings
require a host restart after delivery.

## Later admin interface

Admin should query those deployment records, scoped by parent/environment, and display the
successfully provisioned institution revision with its current observed runtime status. Actions
such as deploy, revise, retry, or retire should go through the provisioning service and update
that same record. Configured modules, sample definitions, failed attempts, and running processes
are different facts and should not be conflated into an institution inventory.

Expose resource locations and secret references to authorized admin callers, not secret values.
Keep provisioning completion separate from runtime readiness: a created database does not prove
that the web host has received usable configuration. This gives admin a durable source of truth
without a second manually maintained list of institutions.

## Git-backed web shell loading

The shell now consumes the external `aetheric-forge/parallel-you` repository pinned in
`institutions/parallel-you` and `institution-modules.json`. It no longer compiles copied
Parallel You implementation projects or pages as part of the shell.

Run `python3 scripts/build-institution-modules.py` after initializing recursive submodules.
The build verifies the pinned Git checkout, publishes the institution independently, and emits
an inventory of its private assemblies and Razor assets under `AethericForge.Web/modules`.
These generated bundles are ignored by Git and copied as opaque build/publish output; their
Razor and JavaScript assets are not recompiled as shell components.

When Parallel You is enabled, the existing `PublicGitHubSource` loads its definition and
bindings from the exact bundled repository commit. The shell verifies the document hashes,
module file inventory, institution identity, and version before using the existing
`InstitutionPluginLoader` to discover its factory. A missing bundle, failed Git load,
revision/hash mismatch, or incompatible web adapter fails startup. Disabled Parallel You
requires neither Git access nor its configuration.

The factory exposes a `ConfigureWebServices(IServiceCollection, IConfiguration,
Func<IServiceProvider, IInstitution?>)` adapter using shared framework/runtime contracts.
The shell registers the module's services through that adapter, adds its assembly to both
Razor routing and endpoint discovery, and serves only its own `_content` assets. The same
factory can mount the institution under the existing full-campus parent. Workflow types and
provider implementations remain private to the plugin load context; institution contracts
and ASP.NET Core framework types are shared with the shell.

The Git loader loads institution definitions; the image build supplies the matching compiled
plugin. No SDK, repository build, or arbitrary branch execution occurs in the running shell.
A revision change requires a new module bundle and web image. Startup requires outbound public
GitHub access and is subject to its unauthenticated API limits.

The deployment-managed Parallel You consumer artifact continues to supply the existing
institution instance, scoped Library/Workbench settings, and web OIDC client. This change
neither reprovisions resources nor changes the instance's catalog identity. An institutional
version mismatch between that artifact, the Git definition, and the plugin is rejected.
