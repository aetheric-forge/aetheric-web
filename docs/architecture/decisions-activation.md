# Decisions activation

Decisions retains its existing Administration faculty owner. The shell mounts its
organization under `IAdministrationFaculty`; Architecture remains a separate faculty.
The factory, request-scoped owner resolver, campus status, and navigation must agree.

## Observed production inventory (2026-10-15)

| Entry | Deployment identity |
| --- | --- |
| University | `c6db5109a09570e5afdaa9b43caf79da6f7fdbf68891f8349afadf5c50c4ac55` |
| Campus | `2fcd3d5e0090991c984aed0ebc6c2b1c34fa88258596bdfc0c23482086e38e1c` |
| Administration | `585a1b4fc7d725844c4a1b82f51412f5b372acbf182bac9edb0072bffcdd24a5` |
| Existing Decisions | `1022fe3c460a03e27e52b986b53dada4fda0dfb275bda2854c19e2cd687ed65f` |

Decisions points to Administration, Administration to Campus, and Campus to University.
Campus records Library, Archive, Post Office, and Workbench resources; University records
Registry. These are existing catalog identities, not new deployment requests.

## Remaining activation work

1. Use the newly selected canonical ADR office described below. Retain the older
   form-backed deployment and its private workspace; this is a new office, not a
   migration or identity rewrite.
2. Use the verified new organization and group IDs recorded below. Provisioning
   requires all four `DecisionsConsumer` fields, including display name.
3. Use the pinned production bindings and validate them against the existing
   Administration ancestry. The generated `example` bindings are not production bindings.
4. Prepare the full-campus host configuration, including scoped Registry, Post Office,
   Workbench, campus Data Protection, Membership, and Talent settings consumed by that
   mode. Configure `Decisions:ExpectedOwnerDeploymentId` to the Administration identity
   above. Preserve the existing public sign-in and Parallel You configuration.
5. Deliver the approved complete Decisions consumer artifact to
   `aethericforge-decisions-consumer` through the provisioning worker. Its owner identity
   must match the independently configured expected owner. Retain canonical identity
   and complete-profile validation; do not bypass these checks for the legacy entry.
6. Validate the candidate image and configuration together before changing production
   `PublicSite:Enabled`. Full-campus mode loads all four modules and enables additional
   campus services. Merely setting `Decisions:Enabled=true` in public mode is rejected.
7. After rollout, verify SSO, directory bootstrap, member draft creation and proposal,
   maintainer review, parent resource routing, and existing Library/Parallel You behavior.

Ownership was corrected in web PR #52. This follow-up prepares the new office and
parent workspace routing. It does not migrate catalog entries, publish a consumer
artifact, change production hosting mode, or claim authenticated live workflow
verification. See [module configuration](git-institution-modules.md).

## New office selected (2026-10-15)

The operator selected a new office under the existing Administration faculty and
`brian@aethericforge.ca` as its initial maintainer. The legacy deployment and its
`decisions-decisions-workspace` stage remain separate. No legacy data is adopted.

The new organization is `administration-decisions` (Administration Decisions).
Its canonical production deployment ID is
`acd89aae84c4757e04128aa135bd5fbddaa609396ef4fbbec1646f99e0b1023f`.
The member group is `6f0f5163-0e9e-418e-9525-9248a9f627b3`; the maintainer group is
`bb673f44-117f-4480-83d1-2132d9394201`. Both groups were created in
`int.aethericforge.ca`, tagged with the canonical deployment and organization IDs,
and Brian's existing enabled account was verified as a member of both. No user
account was created and no realm-wide administrator roles were granted.

[The request parameters](decisions-production-request.json) pin the production
bindings and existing Administration parent. They deliberately omit `RequestId`,
`RequestedAtUtc`, and `RootCredentials`: construct those in the trusted submitting
operator, using the existing encrypted credential store. The file is not a complete
message to publish directly. Catalog parent resolution supplies resource locations
from the actual ancestry rather than trusting this file to select a different owner.

The campus Workbench resource uses `orchard-sky-workbench`. Set
`Decisions:Workbench:Stage=orchard-sky-workbench` and configure `Workbench:Redis`
against that resource's endpoint and database. Both the plugin draft repository and
the shell staging provider use this setting, including locks, recovery, and writes.
The default `adr-campus-workbench` is retained for previously configured hosts.
Selecting a stage does not register a workspace or move existing drafts.

The plugin contributes its Decisions Mongo scheme to the parent's Library and its
Decisions S3 scheme to the parent's Archive; those scoped settings come from the
campus ancestry. The separately provisioned Library's own settings must stay intact.
Different databases for these two schemes are supported; matching them is not an
activation prerequisite.

**Do not submit the production request yet.** The current worker writes its consumer
artifact into the mounted production Decisions Secret. A public-mode shell rejects
that artifact, and its Secret watcher restarts the host after a change. Prepare and
validate the complete campus image/configuration and coordinate consumer delivery
with that rollout before publishing. Creating groups alone does not activate routes.
The full-campus credentials and authenticated end-to-end verification listed above
remain required.

## Membership setup tool

`tools/Decisions.Activation` runs alongside the admin's existing encrypted root store.
Publish it with `dotnet publish tools/Decisions.Activation -c Release -o <output>`.
On that trusted host, run:

```sh
dotnet Decisions.Activation.dll inspect brian@aethericforge.ca
dotnet Decisions.Activation.dll prepare brian@aethericforge.ca
```

`inspect` only reads identity information. `prepare` creates the two office groups
and adds the selected existing enabled user to both. It refuses to reuse matching
group names without the exact office ownership attributes and verifies membership
after writing. Repeating it preserves group IDs. The output contains IDs and setup
status, never credentials or tokens. `RootCredentials__Directory` and
`RootCredentials__KeyDirectory` override the default admin store paths.
Neither command submits a provisioning request or changes web hosting mode.
