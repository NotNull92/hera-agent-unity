#!/usr/bin/env node

const fs = require("node:fs");
const https = require("node:https");
const path = require("node:path");

const packageJson = require("../package.json");

const SUPPORTED_TARGETS = new Map([
  ["linux-x64", ["linux", "amd64", ""]],
  ["linux-arm64", ["linux", "arm64", ""]],
  ["darwin-x64", ["darwin", "amd64", ""]],
  ["darwin-arm64", ["darwin", "arm64", ""]],
  ["win32-x64", ["windows", "amd64", ".exe"]],
]);

function resolveTarget(platform = process.platform, arch = process.arch) {
  const target = SUPPORTED_TARGETS.get(`${platform}-${arch}`);
  if (!target) {
    throw new Error(`Unsupported platform: ${platform}-${arch}`);
  }

  const [goos, goarch, extension] = target;
  return {
    assetName: `hera-agent-unity-${goos}-${goarch}${extension}`,
    binaryName: `hera-agent-unity${extension}`,
  };
}

function download(url, destination, redirectsLeft = 5) {
  return new Promise((resolve, reject) => {
    const request = https.get(url, {
      headers: { "User-Agent": "hera-agent-unity-npm-installer" },
    }, (response) => {
      if (response.statusCode >= 300 && response.statusCode < 400 && response.headers.location) {
        response.resume();
        if (redirectsLeft === 0) {
          reject(new Error("Too many redirects while downloading the Hera CLI"));
          return;
        }
        resolve(download(response.headers.location, destination, redirectsLeft - 1));
        return;
      }

      if (response.statusCode !== 200) {
        response.resume();
        reject(new Error(`Download failed with HTTP ${response.statusCode}`));
        return;
      }

      const temporary = `${destination}.download`;
      const output = fs.createWriteStream(temporary, { mode: 0o755 });
      response.pipe(output);
      output.on("finish", () => {
        output.close(() => {
          fs.renameSync(temporary, destination);
          fs.chmodSync(destination, 0o755);
          resolve();
        });
      });
      output.on("error", (error) => {
        fs.rmSync(temporary, { force: true });
        reject(error);
      });
    });
    request.on("error", reject);
  });
}

async function install() {
  if (process.env.HERA_AGENT_UNITY_SKIP_DOWNLOAD === "1") return;

  const target = resolveTarget();
  const destinationDirectory = path.join(__dirname, "..", "bin", "native");
  const destination = path.join(destinationDirectory, target.binaryName);
  const tag = `v${packageJson.version}`;
  const url = `https://github.com/NotNull92/hera-agent-unity/releases/download/${tag}/${target.assetName}`;

  fs.mkdirSync(destinationDirectory, { recursive: true });
  await download(url, destination);
  process.stdout.write(`Installed Hera Agent Unity ${tag} for ${process.platform}-${process.arch}.\n`);
}

if (require.main === module) {
  install().catch((error) => {
    process.stderr.write(`hera-agent-unity install failed: ${error.message}\n`);
    process.exitCode = 1;
  });
}

module.exports = { install, resolveTarget };
