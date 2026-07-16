# Forge Campus membership scaffold

The membership workflow deliberately separates the Campus decision from external
identity provisioning.

## Current development implementations

- `InMemoryMembershipApplicationStore` loses data when the application restarts.
- `DevelopmentMemberIdentityProvisioner` records a synthetic external identity.
- In Development, subjects listed under
  `Membership:DevelopmentAdministratorSubjects` satisfy the
  `ForgeAdministrator` policy.

These implementations exist to exercise the application and review workflow. They
are not production persistence or authentication.

## Intended adapters

1. Replace `IMembershipApplicationStore` with durable persistence.
2. Configure OpenID Connect authentication against Keycloak.
3. Map the Keycloak `/forge-admins` group into the token's `groups` claim. The
   existing `ForgeAdministrator` policy recognizes both `/forge-admins` and
   `forge-admins` claim values.
4. Implement `IMemberIdentityProvisioner` with the Keycloak Admin API. Provisioning
   should be idempotent, create or explicitly link the approved identity, add it to
   `/campus-members`, and request Keycloak email actions for verification and
   initial password setup.
5. Add a retry operation for applications whose decision is `Approved` and whose
   provisioning state is `Failed`.

Administrative authorization must protect server-side actions as well as the
review page. Never rely on the navigation link or rendered UI as the access check.
