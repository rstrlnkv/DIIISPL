A monitor layout manager for Windows 11: the layout is built inside the program,
monitors are identified by EDID — not by the graphics card output number — and
a profile is switched by a hotkey. HDR is switched together with the layout.

**Install** it from the `{MSI}` file below.
The program and the installer speak the language of Windows — English or Russian;
the language is switched in the About window. Your own language is added by a
file, without a rebuild: `DIIISPL.exe /lang cs` writes a template to translate.

Windows will show the publisher as unknown and SmartScreen may warn you: the
program is signed with a self-made certificate whose root is not installed on
your machine. Updates are still checked against the thumbprint of that same
certificate, so a package signed by anyone else will not be installed.

Profiles live in `%AppData%\DIIISPL`. Uninstalling the program does not touch them.
