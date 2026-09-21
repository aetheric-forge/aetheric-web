# Forge Campus membership scaffold

The membership workflow deliberately separates the Campus decision from external
identity provisioning.

## Current implementations

- `IMembershipApplicationStore`, `MembershipApplication`, and the Mongo-backed
  `MongoMembershipApplicationStore` live in the `aetheric-contracts` submodule,
  shared with `aetheric-admin` (which reads/flags applications this app creates
  via `StaleMembershipApplicationsWorker`).
- `DevelopmentMemberIdentityProvisioner` records a synthetic external identity.
- In Development, subjects listed under
  `Membership:DevelopmentAdministratorSubjects` satisfy the
  `ForgeAdministrator` policy.

`DevelopmentMemberIdentityProvisioner` exists only to exercise the review
workflow locally. It is not production identity provisioning.

## Intended adapters

1. Configure OpenID Connect authentication against Keycloak.
2. Map the Keycloak `/forge-admins` group into the token's `groups` claim. The
   existing `ForgeAdministrator` policy recognizes both `/forge-admins` and
   `forge-admins` claim values.
3. Implement `IMemberIdentityProvisioner` with the Keycloak Admin API. Provisioning
   should be idempotent, create or explicitly link the approved identity, add it to
   `/campus-members`, and request Keycloak email actions for verification and
   initial password setup.
4. Add a retry operation for applications whose decision is `Approved` and whose
   provisioning state is `Failed`.

Administrative authorization must protect server-side actions as well as the
review page. Never rely on the navigation link or rendered UI as the access check.
