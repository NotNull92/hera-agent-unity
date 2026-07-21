const assert = require("node:assert/strict");
const { resolveTarget } = require("../scripts/install");

assert.deepEqual(resolveTarget("linux", "x64"), {
  assetName: "hera-agent-unity-linux-amd64",
  binaryName: "hera-agent-unity",
});
assert.deepEqual(resolveTarget("darwin", "arm64"), {
  assetName: "hera-agent-unity-darwin-arm64",
  binaryName: "hera-agent-unity",
});
assert.deepEqual(resolveTarget("win32", "x64"), {
  assetName: "hera-agent-unity-windows-amd64.exe",
  binaryName: "hera-agent-unity.exe",
});
assert.throws(() => resolveTarget("win32", "arm64"), /Unsupported platform/);

process.stdout.write("npm installer target tests passed\n");
