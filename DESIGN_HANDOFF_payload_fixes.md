# Design handoff — payload field path fixes (June 2026)

## What changed and why

A payload wiring audit found that several fields in `spa/src/data.js` and `spa/src/app.js`
were reading the wrong field names (or non-existent paths) from the BFF API responses.
All five bugs were silent: they never threw errors, they just silently fell through to
hardcoded defaults so live API data was never shown.

All fixes are in **`spa/src/data.js`** and **`spa/src/app.js`** — no HTML or CSS was
touched. The dist was rebuilt (`node spa/build.mjs`).

---

## Files changed

| File | What changed |
|---|---|
| `spa/src/data.js` | 5 field path fixes in the `pack` node's `fired()` function |
| `spa/src/app.js` | `bngToLatLng()` helper added; `initMap()` updated to use OS Places coords |
| `spa/dist/` | Rebuilt — do not hand-edit |

---

## Change 1 — Council Tax band field name (`data.js` line ~349)

```diff
- const ctBand=p?.councilTax?.data?.band??'D';
+ const ctBand=p?.councilTax?.data?.councilTaxBand??'D';
```

`/v1/council-tax/{uprn}` returns `{ data: { uprn, councilTaxBand: "D" } }`.
The field is `councilTaxBand`, not `band`.

---

## Change 2 — Coalfield status field name + value mapping (`data.js` line ~350)

```diff
- const coalStatus=p?.coalfield?.data?.status??'OFF';
+ const coalfieldRaw=p?.coalfield?.data?.coalfieldStatus;
+ const coalStatus=coalfieldRaw==='ON_COALFIELD'?'ON':coalfieldRaw==='OFF_COALFIELD'?'OFF':(coalfieldRaw??'—');
```

`/v1/coalfield/{uprn}` returns `{ data: { uprn, coalfieldStatus: "OFF_COALFIELD" } }`.
The field is `coalfieldStatus` and values are the full enum strings (`ON_COALFIELD`,
`OFF_COALFIELD`, `UNKNOWN`), not short forms. The mapping keeps the chip label short.

---

## Change 3 — EPC score removed (`data.js` lines ~348, ~353)

```diff
- const epcScore=p?.epc?.data?.currentEnergyEfficiencyScore??72;
  ...
- <span class="chip">${seal('ok','sm')}EPC ${epcBand} · ${epcScore}</span>
+ <span class="chip">${seal('ok','sm')}EPC ${epcBand}</span>
```

`EpcCertificate` only has `currentEnergyEfficiencyBand` — there is no score field.
The chip now shows `EPC B` rather than `EPC B · 72`.

---

## Change 4 — Title Register tenure path (`data.js` line ~351)

```diff
- const tenure=p?.titleRegister?.data?.OCSummaryData?.TitleDetails?.TenureType??'Freehold';
+ const isLeasehold=p?.titleRegister?.data?.OCSummaryData?.RegisterEntryIndicators?.LeaseHoldTitleIndicator;
+ const tenure=isLeasehold?'Leasehold':'Freehold';
```

`TitleDetails` and `TenureType` do not exist in the LR facade `TitleDeed` model.
Tenure is derived from `RegisterEntryIndicators.LeaseHoldTitleIndicator` (bool), which
is the correct path in the `OCSummaryData` struct.

---

## Change 5 — Map coordinates (`app.js` lines ~615-621)

```diff
+ function bngToLatLng(E,N){ /* OSGB36→WGS84 Transverse Mercator inversion */ ... }
  let _map = null;
  function initMap(){
    const el = document.getElementById('leaflet-map');
    if(!el || !window.L) return;
-   const lat = (typeof realData!=='undefined' && realData.uprn?.data?.lat) || 51.4712;
-   const lng = (typeof realData!=='undefined' && realData.uprn?.data?.lng) || -2.6003;
+   let lat=51.4712, lng=-2.6003;
+   const addr=typeof realData!=='undefined'&&realData.address?.data?.[0];
+   if(addr?.xCoordinate&&addr?.yCoordinate){
+     try{[lat,lng]=bngToLatLng(addr.xCoordinate,addr.yCoordinate);}catch(e){}
+   }
```

The UPRN validator only returns `{ data: { valid: bool } }` — no coordinates.
OS Places address results (`realData.address.data[0]`) carry `xCoordinate` and
`yCoordinate` in British National Grid (BNG/OSGB36). The new `bngToLatLng()` function
converts these to WGS84 lat/lng so the Leaflet map centres on the actual property.
Bristol (51.4712, -2.6003) remains the fallback if no address has been resolved.

---

## What was NOT changed (confirmed correct)

- `realData.pack.epc.data.currentEnergyEfficiencyBand` — path is correct ✓
- `realData.sellerPack?.source` — path is correct ✓
- `realData.address?.data?.[0]?.uprn` / `.address` — correct ✓
- `realData.chain?.data?.data?.[0]` — double `data` is correct; VMC wraps in
  `SignedResponse<{data:[...]}>` so the path is `chain.data.data[0]` ✓
- `realData.chain?.data?.data?.[0].milestones[].label` / `.date` — correct ✓

---

## How to avoid merge conflicts

These changes are **logic-only** — no HTML structure, no CSS class names, no IDs were
added or removed. The design agent's CSS work on `spa/src/opda.css` will not conflict
with these changes. After the design agent finishes, rebuild dist once with
`node spa/build.mjs` and the combined changes will be in `spa/dist/`.
