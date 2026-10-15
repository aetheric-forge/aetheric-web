# Operations and Talent workspaces

Full-campus mode exposes administrator workspaces for the existing institutions:

| Faculty | Routes | Existing service |
| --- | --- | --- |
| Design / Talent | `/talent`, `/talent/roles`, `/talent/personas`, `/talent/recruitment` | `ITalentSteward`, `IPersonaDirectory`, `IRecruitmentOffice` |
| Operations / Issue Reports | `/operations/issue-reports` | `IIssueReports.Warden` |
| Operations / Security | `/operations/security` | `ISecurity.Sentinel` |

`/operations` links the Operations workspaces. Existing `/campus/admin/issue-reports`
and `/campus/admin/security` addresses remain aliases. All these routes use the
existing `ForgeAdministrator` policy. Navigation hides their links while viewing as
a user; page initialization and mutations also respect that toggle. Public-only
hosting returns 404 for these routes before resolving campus services.

Talent remains under Design with the existing scoped `Talent:MongoDb` configuration
and Mongo collections. The host adapts the role, persona, and recruitment screens
from talent-campus to its routes and authorization. It does not create another Talent
instance or use the standalone campus's JSON files. Recruitment destinations come
from `Talent:Recruitment:DecisionsOffices`, entries with `Id` and `Name`. Empty
configuration disables preparing considerations while candidate registration remains
available. Preparing a consideration saves its snapshot; delivery to Decisions is
not implemented by the existing Talent contract.

Operations resolves the existing institutions under the Operations faculty. Its
workspace service checks authorization for every read/write and derives reporter and
security audit actor IDs from the signed-in subject. The custody domain is
`aetheric-web`; callers cannot supply another domain or actor.

Warden and Sentinel receive `OperationsLedgerStore` instead of the generic in-memory
Workbench store. This adapter serializes their existing catalogs through the campus's
Redis staging provider under `operations/ledger-v1/`, using the configured owner stage
(`Decisions:Workbench:Stage`, default `adr-campus-workbench`). It uses no expiration.
Existing Workbench/Parallel You/Decisions data and the generic Workbench service are
unchanged. Records survive web process restarts; their durability still depends on
the existing Redis persistence and backup configuration.

The existing Warden/Sentinel implementations serialize writes within one process;
they do not provide distributed transactions. Keep this host at one writer replica
until the runtime adds distributed catalog coordination. Sentinel's record and history
writes are separate operations; after a storage error, refresh before retrying.

Validation covers route policies and aliases, navigation visibility, Talent rendering
from provider data, Operations authorization, domain separation, subject attribution,
status changes, audit history, and serialized reload through new service instances.
No production workflow records are created by these tests.
