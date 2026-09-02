## Purpose

Loads package sources and their credentials from the user's local NuGet configuration so package download and version resolution can reach private and authenticated feeds, not just nuget.org.

### Requirement: Package sources loaded from local NuGet configuration
The package processor SHALL load package sources from the user's NuGet configuration (global, machine-level, and user `NuGet.Config` files) via NuGet's settings machinery, rather than hardcoding a single source. Each configured source SHALL be usable for package download and version resolution, carrying its configured credentials.

#### Scenario: Private feed is configured in user NuGet.Config
- **WHEN** the user's NuGet configuration lists a private feed as a package source with credentials
- **THEN** the processor is able to download packages and resolve versions from that private feed using its credentials

#### Scenario: Default nuget.org is the only configured source
- **WHEN** the user's NuGet configuration contains only the default `nuget.org` source (or none, so the default applies)
- **THEN** the processor continues to download packages from nuget.org as before

### Requirement: Multiple sources are tried in order with fallback
When more than one package source is configured, the processor SHALL attempt to resolve and download a package across the configured sources, continuing to the next source when the package is absent from or the source is unreachable on the current one. If no configured source yields the package, the operation SHALL fail gracefully rather than crash the caller.

#### Scenario: Package exists only on a later-configured source
- **WHEN** a package is absent from the first configured source but present on a later one
- **THEN** the processor resolves and downloads the package from the later source

#### Scenario: All sources fail to provide the package
- **WHEN** the package cannot be found on any configured source
- **THEN** the operation returns a not-found outcome without throwing and without aborting the host process

### Requirement: Package source config in the working directory tree is used
The package processor SHALL include a `NuGet.Config` located in the current working directory or any subdirectory of it when loading package sources, in addition to the user's global, machine, and user-level configuration. Sources and credentials from such a directory-level config SHALL be usable for package download and version resolution, layered with the user-level config as NuGet's settings precedence dictates.

#### Scenario: Repo-local NuGet.Config defines a private feed
- **WHEN** the current working directory (or a subdirectory of it) contains a `NuGet.Config` that lists a private feed as a package source with credentials
- **THEN** the processor is able to resolve versions and download packages from that private feed using its credentials

#### Scenario: No NuGet.Config in the working directory tree
- **WHEN** neither the current working directory nor any subdirectory of it contains a `NuGet.Config`
- **THEN** the processor falls back to the user's global, machine, and user-level configuration (and nuget.org default) exactly as before, with no behavior change