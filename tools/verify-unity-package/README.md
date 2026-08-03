# Exact-source Unity package compile

Compile the repository's current Connector and TestRunner sources against an
existing Unity project's Bee response files without changing or launching the
Editor:

```powershell
pwsh tools/verify-unity-package/compile-exact-source.ps1 `
  -ProjectPath $env:UNITY_PROJECT
```

Run this once for each supported compatibility bucket before a Connector
release. The project must already have compiled the Hera package so the two Bee
response files and package references exist.
