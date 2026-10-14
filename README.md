# aetheric-web
Service configuration and credential migration: [hierarchical service configuration](docs/architecture/service-configuration.md).

Parallel You is loaded into the existing web shell through its pinned Git definition and
institution plugin. See [the loading contract](docs/architecture/parallel-you-module.md#git-backed-web-shell-loading).

```bash
git submodule update --init --recursive
python3 scripts/build-institution-modules.py
dotnet test tests/AethericForge.Web.Tests --configuration Release
docker build -t aethericforge-web:local .
```

The module bundle must be built before running or publishing the shell with Parallel You enabled.

Library, Archive, and Decisions also use pinned Git-backed modules. See
[the module registry, ownership, and configuration](docs/architecture/git-institution-modules.md).
