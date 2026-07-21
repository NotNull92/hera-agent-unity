#!/usr/bin/env node

const path = require("node:path");
const { spawnSync } = require("node:child_process");
const { resolveTarget } = require("../scripts/install");

let target;
try {
  target = resolveTarget();
} catch (error) {
  process.stderr.write(`${error.message}\n`);
  process.exit(1);
}

const binary = path.join(__dirname, "native", target.binaryName);
const result = spawnSync(binary, process.argv.slice(2), { stdio: "inherit" });

if (result.error) {
  process.stderr.write(`Failed to start Hera Agent Unity: ${result.error.message}\n`);
  process.exit(1);
}

process.exit(result.status === null ? 1 : result.status);
