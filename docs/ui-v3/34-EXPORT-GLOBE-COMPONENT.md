# 34 — Export Globe Component (V3.9)
Reference: `css/fw-globe.js` (the component), `css/export-globe.html` (standalone card), and live uses in `css/overseas-signup.html` (step 1 hero) and `css/supplier-profile.html` (Export block, showing that mill's countries). Original artwork in strict black and white; it is not copied from any other design.

## What it shows
A rotating dotted globe with continents, graticule, soft shading, two orbit rings with satellites, glowing arcs from **Surat, India** to destination cities, travelling comet dots, pulsing nodes and city labels (labels only for far cities, so they do not overlap). It tells one story: "fabric from India to the world".

## Where to use it (and where not)
- Use: overseas signup hero (`29`), mill profile Export block (`27`, `28`), an "Export ready" slide in the home banner (`26`), the buyer sign-in page for overseas buyers.
- Do not use on screens where users do real work (cart, checkout, forms below step 1), and not on every page. It is a hero element, one per page.
- Mobile first. It must never delay page content: it loads after the page, runs only while visible and pauses when the tab is hidden.

## API (as built in the reference)
`FWGlobe.mount(canvas, { dest: [{n:'Dubai', la:25.2, lo:55.3}, ...], labels: true, radius: .3, oscillate: true })` returns `{ toggle(), playing }`.
- `dest` comes from the API in production: the mill's real export countries (city of each country's main port or capital), or the platform's top destinations on general pages.
- `radius` is a fraction of canvas width; keep diameter below the canvas height so nothing is cropped (about 0.30 for height 560, 0.33 for height 720).
- The canvas has `role="img"` and an `aria-label` that lists the destinations. Because a canvas is invisible to screen readers, **the same destinations must also appear as real text** (chips or the "Exports to" row).

## Rules
1. **Decorative only.** No information exists only in the globe. Counts and countries are always in text next to it.
2. **Reduced motion:** if the device asks for reduced motion, the globe does not animate; it draws once and can still be dragged.
3. **Pause control:** where the globe animates for more than 5 seconds on a page, show a Pause/Play button (WCAG 2.2.2). Small instances inside forms or cards may omit it only if they stop on their own after 5 seconds (add this when wiring) or are static.
4. **Performance:** pauses off screen (`IntersectionObserver`) and when the tab is hidden; canvas resolution capped at 2x; no libraries. Test on a low-end Android phone; if frame rate is poor, fall back to a static image.
5. **Colour:** black, white and grey only. A coloured version is an owner decision (the approved direction takes colour only from fabric photos).
6. **Never claim more than the data says.** Numbers like "12 countries" and "180+ export mills" come from the database, never typed by hand; the reference numbers are demo values.

## Known limits
- **Land outlines are approximate.** The land mask (`fw-globe.js`, 2-degree grid) was drawn by hand from simplified outlines, because map data could not be downloaded in the build environment. Continents are recognisable but small islands and coasts are rough. **Codex task:** generate the mask from Natural Earth 110m land data (public domain) with a small script and replace the string in `fw-globe.js`; keep the same 180×90 bit-mask format so nothing else changes.
- Drag rotates left/right only. The globe swings around India so India stays in view; it is not a free-spin earth.
- Labels are English only.

## Acceptance checks
Page loads and is usable with the globe script blocked; no console errors; reduced-motion shows a still globe; Pause stops the animation; destination text is present without the canvas; globe never overlaps form fields; works at 360 px width; CPU stays low when scrolled off screen.
