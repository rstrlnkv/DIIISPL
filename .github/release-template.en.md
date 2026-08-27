DIIISPL {VERSION} — {KIND}. Users are encouraged to upgrade.

DIIISPL is a monitor layout manager for Windows 11: the layout is built inside the
program, monitors are identified by EDID — not by the graphics card output number — and
a profile is switched by a hotkey. HDR is switched together with the layout.

## What's new

{CHANGES}

## Installation and upgrading

Download the package below and run it. The program is installed into
`C:\Program Files\DIIISPL` and appears in the list of installed programs, which is where
you uninstall it from; a running copy is closed by the installer itself.

- [{MSI}](https://github.com/rstrlnkv/DIIISPL/releases/download/v{VERSION}/{MSI}) — SHA-256 `{SHA256}`

The package is signed with a self-made certificate whose thumbprint is `{THUMBPRINT}`.
The program checks updates against that same thumbprint and will not install a package
signed by anyone else — but Windows will show the publisher as unknown and SmartScreen
may warn you, because the root of this certificate is not installed on your machine.

A copy already installed updates itself: it looks at the releases once a day and offers
the new one in the About window. Profiles live in `%AppData%\DIIISPL` and survive
reinstalling, uninstalling included.

What the program does and how to use it — see the
[README](https://github.com/rstrlnkv/DIIISPL#readme).

## What's next

- {NEXTPATCH} — fixes only
- {NEXTMINOR} — the next one with new features
