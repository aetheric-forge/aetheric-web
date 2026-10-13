# Parallel You module and provisioning handoff

Parallel You's reusable composition lives in `parallel-you/ParallelYou.Services/ParallelYouModule.cs`.
It accepts an institution context, an explicit Librarian, and an explicit Artificer. It exports
the institution and its workflow services without replacing the host's Library or Workbench.
The same composition runs independently or as a child of the full Forge Campus. The imported
code in this repository is the starting point: the standalone `aetheric-forge/parallel-you`
repository remains at its v1.0.0 container commit, while this copy has subsequent institution
and shared-host changes.

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

`parallel-you/institution/parallel-you.yaml` and its sibling bindings file use the existing
provisioning definition format. The institution owns a personal Library and inherits Registrar
and Workbench. The example uses the existing Mongo provider to create a database-scoped user.
It is an example deployment, not evidence that an institution has been provisioned.

The next integration is in the provisioning service, rather than another bootstrap wizard:

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

The current module host still consumes configuration sections; the automatic provisioning-to-host
handoff and durable institution inventory are not implemented in this change. The module accepts
an externally supplied institution context so that the later adapter can use the provisioned
definition instead of reconstructing the full campus template.

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
