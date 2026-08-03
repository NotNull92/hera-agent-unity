const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const npmRoot = path.resolve(__dirname, "..");
const repositoryRoot = path.resolve(npmRoot, "..");
const npmPackage = require(path.join(npmRoot, "package.json"));
const npmLock = require(path.join(npmRoot, "package-lock.json"));
const marketplace = require(path.join(repositoryRoot, ".agents", "plugins", "marketplace.json"));
const plugin = require(path.join(repositoryRoot, "plugins", "hera-unity", ".codex-plugin", "plugin.json"));

assert.equal(npmLock.version, npmPackage.version, "npm package-lock version must match package.json");
assert.equal(npmLock.packages[""].version, npmPackage.version, "npm root lock version must match package.json");
assert.equal(marketplace.name, "hera-agent-unity");
assert.equal(marketplace.plugins.length, 1);
assert.equal(marketplace.plugins[0].name, plugin.name);
assert.equal(marketplace.plugins[0].source.source, "local");
assert.equal(marketplace.plugins[0].source.path, "./plugins/hera-unity");
assert.equal(marketplace.plugins[0].policy.installation, "AVAILABLE");
assert.match(marketplace.plugins[0].policy.authentication, /^(ON_INSTALL|ON_USE)$/);
assert.match(plugin.version, /^\d+\.\d+\.\d+$/);
assert.ok(fs.existsSync(path.join(repositoryRoot, "plugins", "hera-unity", "skills", "live-editor", "SKILL.md")));
const standaloneSkill = fs.readFileSync(
  path.join(repositoryRoot, ".agents", "skills", "hera-agent-unity", "SKILL.md"),
  "utf8",
);
assert.match(standaloneSkill, /^---\r?\nname: hera-agent-unity\r?\ndescription: .+\r?\n---/);
assert.ok(fs.existsSync(path.join(repositoryRoot, ".github", "workflows", "npm-publish.yml")));

process.stdout.write("distribution metadata tests passed\n");
