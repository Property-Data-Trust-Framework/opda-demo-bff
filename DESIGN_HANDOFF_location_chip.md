# Design handoff — location chip not visible in top search bar

## What this is

The top bar of the OPDA demo SPA (`spa/src/index.html` lines 24-29) contains a `.search` pill
that shows the currently resolved property address and a green UPRN chip:

```
[ 🔍  8  ·  CANADA SQUARE, LONDON, E14 5EQ          ✓ UPRN 6646137 ]
```

The UPRN chip (`#topUprn`, class `.uprn`) is confirmed working in JS — `getComputedStyle`
returns `display:flex` and `textContent` is `UPRN 6646137` after address selection.
Despite this, the chip is **not visible** to the user.

## Root cause (diagnosed)

The `.search` container is a flex row with `overflow:hidden`. The `.sub` span
(showing the secondary address line) has no `min-width:0`, so it cannot shrink below
its min-content width even though it has `text-overflow:ellipsis`. On a long address
like "·  CANADA SQUARE, LONDON, E14 5EQ" it fills the available width and pushes
`.uprn` (which uses `margin-left:auto`) outside the container where it is clipped.

**File:** `spa/src/opda.css` lines 68-76

```css
/* current — broken */
.search{flex:1;max-width:540px;display:flex;align-items:center;gap:11px;height:40px;
  padding:0 8px 0 15px;white-space:nowrap;overflow:hidden; ...}
.search .addr,.search .sub{overflow:hidden;text-overflow:ellipsis;}   /* ← no min-width:0 */
.search .ico{color:var(--ink-3);flex:none;display:flex;}
.search .addr{color:var(--ink);font-weight:600;}
.search .sub{color:var(--ink-3);}
.search .uprn{margin-left:auto;display:flex;align-items:center;gap:6px; ...}  /* ← not flex:none */
```

## Requested fix

```css
/* proposed */
.search .ico{color:var(--ink-3);flex:none;display:flex;}
.search .addr{color:var(--ink);font-weight:600;flex:none;max-width:120px;overflow:hidden;text-overflow:ellipsis;}
.search .sub{color:var(--ink-3);flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;}
.search .uprn{flex:none;margin-left:12px;display:flex;align-items:center;gap:6px; ...}
```

Key changes:
- `.addr` → `flex:none` + `max-width:120px` so it doesn't expand
- `.sub` → `flex:1;min-width:0` so it takes remaining space and clips correctly
- `.uprn` → `flex:none;margin-left:12px` (fixed margin instead of `auto`) so it's always visible

## What the resolved state looks like

After an address is selected, JS sets:
- `#topAddr` textContent = first comma-segment of address e.g. `"8"`
- `#topSub` textContent = `"·  CANADA SQUARE, LONDON, E14 5EQ"`
- `#topUprnNum` textContent = `"6646137"` (a string)
- `#topUprn` style.display = `""` (reverts to CSS flex)

Empty / loading state:
- `#topSearch` has class `search empty` — `.search.empty .addr,.search.empty .sub` are styled italic/muted
- `#topUprn` style.display = `"none"`

## Files to edit

| File | What |
|---|---|
| `spa/src/opda.css` lines 68-76 | CSS fix (described above) |
| `spa/dist/opda.css` | Rebuilt automatically via `node spa/build.mjs` — do not hand-edit |

## Do not change

- `spa/src/index.html` — HTML structure is correct
- `spa/src/app.js` — JS logic is confirmed correct
- `spa/src/data.js` — no changes needed
