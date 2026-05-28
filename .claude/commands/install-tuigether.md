---
description: Build tuigether in Release mode and install it to ~/.local/bin/tuigether
allowed-tools: Bash, Read
---

Build the tuigether project in Release mode as a single-file framework-dependent executable, then copy the binary to `~/.local/bin/tuigether` (the conventional location for per-user CLI tools on Linux, already on this user's `PATH`).

Steps:

1. Run the publish:
   ```
   dotnet publish src/tuigether -c Release -r linux-x64 --self-contained false -p:PublishSingleFile=true
   ```
2. Copy the resulting binary from `src/tuigether/bin/Release/net10.0/linux-x64/publish/Tuigether` to `~/.local/bin/tuigether` and ensure it is executable (`chmod +x`).
3. Verify the install by running `~/.local/bin/tuigether --version` or `which tuigether` (don't actually launch the TUI).
4. Report the install path and the binary size.

If the publish fails, surface the error and stop — do not attempt to copy a stale binary.
