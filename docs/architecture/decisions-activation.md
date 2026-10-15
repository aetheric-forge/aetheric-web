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

1. Reconcile the existing form-backed Decisions source identity with the canonical
   `https://github.com/aetheric-forge/adr-campus` identity required by consumer delivery.
   Confirm the approved definition's resources against the existing deployment before
   adopting or migrating it. A fresh canonical request is a different deployment identity;
   do not assume it updates the existing office or preserves all resource routing.
2. Resolve the existing organization ID and member/maintainer group IDs. Preserve these
   IDs and existing draft/catalog keys. Provisioning requires all four
   `DecisionsConsumer` fields, including display name.
3. Validate production bindings against the existing Administration ancestry. The ADR
   package's generated `example` bindings are not production bindings.
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

This source change corrects ownership. It does not migrate catalog entries, publish a
consumer artifact, change production hosting mode, or claim authenticated live workflow
verification. See [module configuration](git-institution-modules.md).
