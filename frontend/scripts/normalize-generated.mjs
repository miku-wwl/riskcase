import { readFile, writeFile } from "node:fs/promises";

const path = new URL("../src/generated/riskcase-api.ts", import.meta.url);
const source = await readFile(path, "utf8");
const normalized = `${source.replace(/\r\n/g, "\n").replace(/[\t ]+$/gm, "").replace(/\n*$/, "\n")}`;
await writeFile(path, normalized, "utf8");
