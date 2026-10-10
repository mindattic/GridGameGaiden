// Browser smoke test for the WebGL build: boots the game in Chromium, walks title → new game → camp,
// reloads the page and checks the save survived (IndexedDB). Screenshots → artifacts/webgl/.
//   python -m http.server 8099 --directory Build/WebGL   (in another shell)
//   node tools/webcheck.mjs [url]
import { chromium } from "playwright";
import fs from "node:fs";
import path from "node:path";

const url = process.argv[2] ?? "http://127.0.0.1:8099/index.html";
const out = path.resolve("artifacts/webgl");
fs.mkdirSync(out, { recursive: true });
const results = [];
const check = (name, ok, detail = "") => { results.push({ name, ok }); console.log(`${ok ? "PASS" : "FAIL"} ${name}${!ok && detail ? " — " + detail : ""}`); };

const browser = await chromium.launch({ args: ["--use-angle=swiftshader", "--enable-unsafe-swiftshader"] });
const context = await browser.newContext({ viewport: { width: 585, height: 1266 } });
const page = await context.newPage();
const logs = [];
page.on("console", (m) => logs.push(m.text()));
page.on("pageerror", (e) => logs.push("PAGEERROR " + e.message));

async function bootAndWait(label) {
  await page.goto(url);
  const t0 = Date.now();
  while (Date.now() - t0 < 120000) {
    if (logs.some((l) => l.includes("[GameApp] content:"))) break;
    await page.waitForTimeout(500);
  }
  await page.waitForTimeout(2500);
  await page.screenshot({ path: path.join(out, `${label}.png`) });
}

// The canvas fills the viewport; design coords (1170×2532) map ×0.5 onto 585×1266.
async function tapDesign(x, y) {
  const box = await page.locator("canvas").boundingBox();
  const k = box.width / 1170;
  await page.mouse.click(box.x + x * k, box.y + y * k);
  await page.waitForTimeout(900);
}

await bootAndWait("01-title");
check("game boots in the browser", logs.some((l) => l.includes("[GameApp] content:")), logs.slice(-5).join(" | "));
check("no page errors", !logs.some((l) => l.startsWith("PAGEERROR")), logs.filter((l) => l.startsWith("PAGEERROR")).join(" | "));
await tapDesign(410, 1486); // New Game
await page.screenshot({ path: path.join(out, "02-slots.png") });
await tapDesign(500, 360); // Slot 1
await page.waitForTimeout(1500);
await page.screenshot({ path: path.join(out, "03-camp.png") });
logs.length = 0;
await bootAndWait("04-after-reload");
check("save survives a page reload (IndexedDB)", logs.some((l) => l.includes("most recent save slot: 0")), logs.filter((l) => l.includes("[Title]")).join(" | "));

// Into a battle with real browser mouse input.
await tapDesign(585, 1434);  // Continue
await page.waitForTimeout(1200);
await tapDesign(225, 732);   // Adventure
await tapDesign(500, 234);   // Green Valley I
await page.waitForTimeout(1500);
await tapDesign(1090, 2440); // Skip crawl (no-op if none)
await page.waitForTimeout(2500);
await page.screenshot({ path: path.join(out, "05-battle.png") });
const box = await page.locator("canvas").boundingBox();
const k = box.width / 1170;
const tile = (x, y) => [box.x + (45 + 180 * x + 90) * k, box.y + (450 + 180 * y + 90) * k];
logs.length = 0;
let [sx, sy] = tile(4, 6);
await page.mouse.move(sx, sy); await page.mouse.down();
for (const [x, y] of [[4, 5.5], [4, 5], [4, 4.5], [4, 4]]) { const [px, py] = tile(x, y); await page.mouse.move(px, py, { steps: 6 }); await page.waitForTimeout(120); }
await page.screenshot({ path: path.join(out, "06-dragging.png") });
await page.mouse.up();
await page.waitForTimeout(2000);
await page.screenshot({ path: path.join(out, "07-after-drag.png") });
check("a browser mouse drag commits a hero move", logs.some((l) => l.includes("[Battle] Move")), logs.filter((l) => l.includes("[Battle]")).join(" | "));
fs.writeFileSync(path.join(out, "console.log"), logs.join("\n"));
await browser.close();
console.log(`${results.filter((r) => r.ok).length}/${results.length} browser checks passed`);
process.exit(results.every((r) => r.ok) ? 0 : 1);
