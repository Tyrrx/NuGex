## Purpose

Automated CI/CD for NuGex: a build pipeline that validates every push to `main`, and a release pipeline that publishes NuGex as a cross-platform .NET tool to nuget.org when a GitHub Release is created.

### Requirement: Build pipeline runs on main-only pushes
The system SHALL run a build pipeline automatically on every push to the `main` branch. The pipeline MUST NOT run on other branches or on pull requests.

#### Scenario: Push to main triggers build
- **WHEN** a commit is pushed to `main`
- **THEN** the build pipeline runs a restore and build of the solution

#### Scenario: Push to a feature branch does not trigger build
- **WHEN** a commit is pushed to a non-`main` branch
- **THEN** the build pipeline does not run

### Requirement: Release pipeline runs on release creation
The system SHALL run a release pipeline automatically when a GitHub Release is created. The release pipeline MUST NOT run on pushes or pull requests.

#### Scenario: A GitHub Release is created
- **WHEN** a GitHub Release is created
- **THEN** the release pipeline builds the solution and publishes the tool package

### Requirement: Release publishes a cross-platform .NET tool to nuget.org
The release pipeline SHALL package NuGex as a .NET tool (`PackAsTool`) and publish it to nuget.org. The tool package SHALL be framework-dependent so it can be installed and run on any platform with a compatible .NET runtime (Windows, macOS, Linux).

#### Scenario: User installs the tool on any platform
- **WHEN** a release has been published to nuget.org
- **THEN** a user can run `dotnet tool install -g nugex` on Windows, macOS, or Linux and run the `nugex` command

### Requirement: Release version derived from release tag
The release pipeline SHALL derive the package version from the release tag (e.g., tag `v1.2.3` produces tool version `1.2.3`).

#### Scenario: Version derived from tag
- **WHEN** a release is created with tag `v0.3.0`
- **THEN** the published tool package is version `0.3.0`

### Requirement: Tool build is framework-dependent and untrimmed
The tool package SHALL be built framework-dependent (not self-contained), untrimmed, and with no runtime identifier baked in. The existing Nix flake channel SHALL remain unaffected and continue to produce its self-contained, trimmed Linux build.

#### Scenario: Tool package is not platform-bound
- **WHEN** the release pipeline packs the tool
- **THEN** the resulting package has no runtime identifier and runs on the user's platform

#### Scenario: Nix build is unaffected
- **WHEN** the Nix flake builds NuGex
- **THEN** it continues to produce a self-contained, trimmed `linux-x64` binary with its own RID

### Requirement: No binary artifacts attached to releases
The release pipeline SHALL NOT attach NuGex binary artifacts (e.g., a Nix-built binary) to GitHub Releases. Nix users continue to consume the flake from source.

#### Scenario: Release contains only the tool package output
- **WHEN** a release is created
- **THEN** the release pipeline does not attach standalone binary artifacts
