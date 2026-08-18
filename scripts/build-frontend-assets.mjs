import { copyFile, mkdir, readFile, rm, writeFile } from "node:fs/promises";
import path from "node:path";

const webRoot = path.join("src", "LeadRelay.Web", "wwwroot");
const cssDirectory = path.join(webRoot, "css");
const alpineDirectory = path.join(webRoot, "vendor", "alpine");
const lucideDirectory = path.join(webRoot, "vendor", "lucide");

await Promise.all([
  mkdir(cssDirectory, { recursive: true }),
  mkdir(alpineDirectory, { recursive: true }),
  mkdir(lucideDirectory, { recursive: true })
]);

await copyFile(
  path.join("node_modules", "alpinejs", "dist", "cdn.min.js"),
  path.join(alpineDirectory, "alpine.min.js")
);

const lucideSource = await readFile(
  path.join("node_modules", "lucide", "dist", "umd", "lucide.min.js"),
  "utf8"
);
const lucideWithoutSourceMap = lucideSource.replace(/\n?\/\/# sourceMappingURL=lucide\.min\.js\.map\s*$/, "\n");
await writeFile(path.join(lucideDirectory, "lucide.min.js"), lucideWithoutSourceMap, "utf8");
await rm(path.join(lucideDirectory, "lucide.min.js.map"), { force: true });
