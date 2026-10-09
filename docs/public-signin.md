# Public site sign-in

Public content remains independent of the campus services with `PublicSite__Enabled=true`.
Set `PublicSite__SignInEnabled=true` to enable `/account/login`, `/account/logout` and the
OIDC callback without enabling campus workers, Membership MongoDB or Redis.

Configure `Keycloak__Authority` and `Keycloak__Realm`, plus the institution-owned
`Registry__Keycloak__ClientId` and `Registry__Keycloak__ClientSecret`. Use a dedicated
confidential web client with standard authorization code flow, PKCE and exact callback
`https://aethericforge.ca/signin-oidc`. The logout callback is
`https://aethericforge.ca/signout-callback-oidc`. A www-host deployment also needs its exact
callbacks registered. Public sign-in validates OIDC identity directly; campus mode keeps
its existing registrar/Person validation.

Set `PublicSite__ProtectionKeyDirectory` to a persistent private mounted directory.
The public host uses its own Data Protection application name. Never mount the admin
bootstrap root credentials or encryption key into the public host. Use the saved
provisioning credential to create/read the dedicated client, then store only that
client's secret in the web configuration Secret. Keycloak requires internal CA trust
and private network access in this deployment.

Disabling `PublicSite__SignInEnabled` preserves the standalone content mode. Full campus
mode (`PublicSite__Enabled=false`) continues to require all campus service configuration.
