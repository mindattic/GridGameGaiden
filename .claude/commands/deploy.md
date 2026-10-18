---
description: Build the Windows Release player and publish it to C:\Apps\GridGameGaiden — this repo's own /deploy, which overrides the generic MindAttic router (tools\deploy.ps1 lives under tools\, not at root, so the router wouldn't find it on its own).
argument-hint: "[-Launch]"
allowed-tools: Read, Bash, PowerShell
---

# /deploy — GridGameGaiden

`deploy.bat` (repo root) forwards to `tools\deploy.ps1`: stops any running instance launched from
this repo or from `C:\Apps\GridGameGaiden\`, clears `Build\WindowsRelease` (build output only,
never `Library\`), rebuilds the Windows Release player via Unity batch mode, and publishes it to
`C:\Apps\GridGameGaiden\`. Requires the Unity Editor closed first — batchmode fails otherwise; ask
the owner to quit it, never kill it.

Run `deploy.bat` from the repo root in a single tool call, forwarding `$ARGUMENTS` (e.g. `-Launch`
to also launch the published exe once it's copied). Don't invoke the `unthrottle` skill yourself:
the context gate only writes its exemption marker when the user types `/unthrottle`, so a
self-invoked one does nothing. If the user wants this to skip the pace hold, they type
`/unthrottle` first.

Report `deploy.bat`'s own output; don't summarize away a failure.

The Desktop shortcut `GridGameGaiden.lnk` (-> `C:\Apps\GridGameGaiden\GridGameGaiden.exe`) is this
repo's `/run` — it launches whatever is currently published, with no rebuild.
