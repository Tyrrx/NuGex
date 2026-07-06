### Requirement: Best target framework group is selected by framework precedence
When a NuGet package contains lib items for multiple target frameworks, the server SHALL select the group to extract by comparing parsed target framework precedence (e.g. via `NuGet.Frameworks`), not by comparing target framework moniker strings.

#### Scenario: Package contains both netstandard2.0 and a modern net target
- **WHEN** a package's lib items include both a `netstandard2.0` group and a `net8.0` group
- **THEN** the server selects the `net8.0` group, not the `netstandard2.0` group

#### Scenario: Package contains only one target framework
- **WHEN** a package's lib items include exactly one target framework group
- **THEN** the server selects that group

#### Scenario: Package contains multiple modern targets
- **WHEN** a package's lib items include both `net6.0` and `net8.0` groups
- **THEN** the server selects the `net8.0` group as the higher-precedence framework
