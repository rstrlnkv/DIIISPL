# Security policy

## Supported versions

| Channel | Supported version |
|---------|-------------------|
| Stable  | The latest release |
| Dev     | The latest `-dev.N` prerelease |

Older versions get no fixes. Releases are listed on the
[releases page](https://github.com/rstrlnkv/DIIISPL/releases).

## Reporting a vulnerability

Report it privately through
[GitHub security advisories](https://github.com/rstrlnkv/DIIISPL/security/advisories/new).
Do not open a public issue for a vulnerability.

Please include:

- the version and the channel (Stable or Dev);
- the steps that reproduce the problem;
- the impact you see.

## What to expect

A fix ships in a new release and is described in CHANGELOG.md. There is no
promised response time.

## Scope

In scope:

- the program (`DIIISPL.exe`);
- the MSI installer;
- the update check. The updater compares the signing-certificate thumbprint of a downloaded
  package with the one built into the program and installs nothing that does not match;
- in the Dev channel, `DIIISPL.ExplorerTAP.dll`, which is loaded into `explorer.exe`.
