## Purpose

Loads package sources and their credentials from the user's local NuGet configuration so package download and version resolution can reach private and authenticated feeds, not just nuget.org.

## ADDED Requirements

### Requirement: Package source config in the working directory tree is used
The package processor SHALL include a `NuGet.Config` located in the current working directory or any subdirectory of it when loading package sources, in addition to the user's global, machine, and user-level configuration. Sources and credentials from such a directory-level config SHALL be usable for package download and version resolution, layered with the user-level config as NuGet's settings precedence dictates.

#### Scenario: Repo-local NuGet.Config defines a private feed
- **WHEN** the current working directory (or a subdirectory of it) contains a `NuGet.Config` that lists a private feed as a package source with credentials
- **THEN** the processor is able to resolve versions and download packages from that private feed using its credentials

#### Scenario: No NuGet.Config in the working directory tree
- **WHEN** neither the current working directory nor any subdirectory of it contains a `NuGet.Config`
- **THEN** the processor falls back to the user's global, machine, and user-level configuration (and nuget.org default) exactly as before, with no behavior change
