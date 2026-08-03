#!/usr/bin/env node

const https = require("node:https");
const packageJson = require("../package.json");
const { resolveTarget } = require("./install");

const SUPPORTED = [
  ["linux", "x64"],
  ["linux", "arm64"],
  ["darwin", "x64"],
  ["darwin", "arm64"],
  ["win32", "x64"],
];

function requestJson(url, redirectsLeft = 5) {
  return new Promise((resolve, reject) => {
    const headers = {
      Accept: "application/vnd.github+json",
      "User-Agent": "hera-agent-unity-npm-release-check",
      "X-GitHub-Api-Version": "2022-11-28",
    };
    if (process.env.GITHUB_TOKEN) {
      headers.Authorization = `Bearer ${process.env.GITHUB_TOKEN}`;
    }
    const request = https.get(url, { headers }, (response) => {
      if (response.statusCode >= 300 && response.statusCode < 400 && response.headers.location) {
        response.resume();
        if (redirectsLeft === 0) {
          reject(new Error("Too many redirects while checking the GitHub release"));
          return;
        }
        resolve(requestJson(response.headers.location, redirectsLeft - 1));
        return;
      }
      let body = "";
      response.setEncoding("utf8");
      response.on("data", (chunk) => { body += chunk; });
      response.on("end", () => {
        if (response.statusCode !== 200) {
          reject(new Error(`GitHub release lookup failed with HTTP ${response.statusCode}: ${body.trim()}`));
          return;
        }
        try {
          resolve(JSON.parse(body));
        } catch (error) {
          reject(new Error(`GitHub release response was not valid JSON: ${error.message}`));
        }
      });
    });
    request.on("error", reject);
  });
}

async function main() {
  const expectedTag = `v${packageJson.version}`;
  const requestedTag = process.env.HERA_AGENT_UNITY_RELEASE_TAG || expectedTag;
  if (requestedTag !== expectedTag) {
    throw new Error(`npm package ${packageJson.version} must publish against ${expectedTag}, not ${requestedTag}`);
  }

  const release = await requestJson(
    `https://api.github.com/repos/NotNull92/hera-agent-unity/releases/tags/${encodeURIComponent(requestedTag)}`,
  );
  if (release.draft || release.prerelease) {
    throw new Error(`${requestedTag} must be a published stable GitHub release`);
  }

  const actual = new Set((release.assets || []).map((asset) => asset.name));
  const required = SUPPORTED.map(([platform, arch]) => resolveTarget(platform, arch).assetName);
  const missing = required.filter((name) => !actual.has(name));
  if (missing.length > 0) {
    throw new Error(`${requestedTag} is missing release assets: ${missing.join(", ")}`);
  }

  process.stdout.write(
    `Verified npm ${packageJson.version} against ${requestedTag}: ${required.length}/${required.length} native assets present.\n`,
  );
}

main().catch((error) => {
  process.stderr.write(`npm release verification failed: ${error.message}\n`);
  process.exitCode = 1;
});
