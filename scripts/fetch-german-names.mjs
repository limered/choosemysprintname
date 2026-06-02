#!/usr/bin/env node
// Fetches all Pokemon species from PokeAPI and extracts their German names.
// Output: backend/Pokemon/Data/german-pokemon-names.json
//
// Usage: node scripts/fetch-german-names.mjs
//
// Notes:
// - ~1300 HTTP requests; concurrency capped at 20 to be polite to PokeAPI.
// - Species without a German `names[]` entry are skipped.
// - Re-run to regenerate the JSON file.

import { writeFile, mkdir } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const OUT_FILE = resolve(__dirname, "..", "backend", "Pokemon", "Data", "german-pokemon-names.json");
const LIST_URL = "https://pokeapi.co/api/v2/pokemon-species?limit=10000";
const CONCURRENCY = 20;

function extractId(url) {
  const m = /\/pokemon-species\/(\d+)\/?$/.exec(url);
  return m ? Number(m[1]) : null;
}

async function getJson(url) {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`${res.status} ${res.statusText} for ${url}`);
  return res.json();
}

async function runPool(items, worker, concurrency) {
  const results = new Array(items.length);
  let i = 0;
  const workers = Array.from({ length: concurrency }, async () => {
    while (true) {
      const idx = i++;
      if (idx >= items.length) return;
      try {
        results[idx] = await worker(items[idx], idx);
      } catch (err) {
        results[idx] = { __error: String(err) };
      }
      if ((idx + 1) % 100 === 0) {
        process.stderr.write(`  fetched ${idx + 1}/${items.length}\n`);
      }
    }
  });
  await Promise.all(workers);
  return results;
}

async function main() {
  process.stderr.write(`Fetching species index from ${LIST_URL}\n`);
  const list = await getJson(LIST_URL);
  const species = list.results.filter(s => extractId(s.url) != null);
  process.stderr.write(`Got ${species.length} species. Fetching details with concurrency ${CONCURRENCY}...\n`);

  const detailed = await runPool(species, async (s) => {
    const id = extractId(s.url);
    const data = await getJson(s.url);
    const de = (data.names || []).find(n => n.language?.name === "de");
    if (!de || !de.name) return null;
    return { id, name: de.name };
  }, CONCURRENCY);

  const errors = detailed.filter(x => x && x.__error);
  const records = detailed.filter(x => x && !x.__error && x.id && x.name);
  records.sort((a, b) => a.id - b.id);

  await mkdir(dirname(OUT_FILE), { recursive: true });
  await writeFile(OUT_FILE, JSON.stringify(records, null, 2) + "\n", "utf8");

  process.stderr.write(`\nWrote ${records.length} entries to ${OUT_FILE}\n`);
  if (errors.length) process.stderr.write(`Errors: ${errors.length}\n`);
  const sample = records.find(r => r.id === 1);
  const pika = records.find(r => r.id === 25);
  const quaxly = records.find(r => r.id === 912);
  process.stderr.write(`Samples: id 1 = ${sample?.name}, id 25 = ${pika?.name}, id 912 = ${quaxly?.name}\n`);
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
