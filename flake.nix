{
  description = "NuGex: A .NET analysis tool and MCP server";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixpkgs-unstable";
    flake-parts.url = "github:hercules-ci/flake-parts";
    flake-parts.inputs.nixpkgs-lib.follows = "nixpkgs";
    systems.url = "github:nix-systems/default";
  };

  outputs = inputs@{ self, nixpkgs, flake-parts, systems, ... }:
    flake-parts.lib.mkFlake { inherit inputs; } {
      systems = import systems;

      perSystem = { system, ... }:
        let
          pkgs = import nixpkgs { inherit system; };

          # Use .NET 10 SDK
          dotnet-sdk = pkgs.dotnetCorePackages.sdk_10_0;

          # Binary name
          pname = "nugex";
          version = "0.1.0";
        in
        {
          packages.default = pkgs.buildDotnetModule {
            inherit pname version;

            src = ./.;
            projectFile = "NuGex/NuGex.fsproj";

            nugetDeps = ./nix/deps.json;

            dotnet-sdk = dotnet-sdk;
            dotnet-runtime = pkgs.dotnetCorePackages.runtime_10_0;

            # Use host RID for build if we want it to run on the host
            # Or linux-x64 for standard glibc distribution
            runtimeId = "linux-x64";
            selfContained = true;

            # Build flags for single file, no trimming, and invariant globalization
            extraPublishFlags = [
              "-p:PublishSingleFile=true"
              "-p:PublishTrimmed=false"
              "-p:InvariantGlobalization=true"
            ];

            # Post-install logic to ensure the binary is named correctly and executable
            postInstall = ''
              mkdir -p $out/bin
              if [ -f $out/lib/${pname}/NuGex ]; then
                ln -s $out/lib/${pname}/NuGex $out/bin/${pname}
              fi
            '';

            meta = with pkgs.lib; {
              description = "NuGex MCP Server and .NET analysis tool";
              homepage = "https://github.com/Tyrrx/NuGex";
              license = licenses.mit;
              platforms = platforms.linux;
            };
          };

          devShells.default = pkgs.mkShell {
            buildInputs = [
              dotnet-sdk
              pkgs.fsautocomplete
              pkgs.fantomas
            ];

            shellHook = ''
              echo "NuGex development environment (using .NET 10)"
              export DOTNET_ROOT=${dotnet-sdk}
            '';
          };
        };
    };
}
