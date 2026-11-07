// Renders SVG -> PNG at exact pixel sizes (vector source, never upscaled raster).
import fs from "node:fs";
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const sharp = require("sharp");
const jobs = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
for (const j of jobs) {
  await sharp(j.svg, { density: 72 }).resize(j.w, j.h, { fit: "fill" }).png({ compressionLevel: 9 }).toFile(j.png);
}
