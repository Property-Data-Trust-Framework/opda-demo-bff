# Extending the Property Data Visualiser — adding a role, and wiring it to VC verification

This is for a developer working on this fork of the sandbox: how to add a new role
to the SPA's five-role transaction flow, and how to wire one of its steps to the
`/demo-api/wallet/*` endpoints from ADR-0013. It touches things documented
elsewhere (`WALLET_INTEGRATION.md`, `ADR-0013-wallet-credential-verification.md`,
`KEY_LEARNINGS.md`) without repeating them — this is the "how do I actually change
the code" layer underneath those.

Two files matter: `spa/src/data.js` (the role/step **content** — the dependency
graph, card renderers, static payloads) and `spa/src/app.js` (the **engine** —
state, rendering, BFF calls, click handling). Everything below lives in one or
the other.

## Part 1 — Adding a new role

The whole tab bar, branching tracker, and node panels are *generated* from one
array — `ROLES` in `data.js`. You don't touch any rendering code to add a role;
you add data and, if a step needs a custom UI, a render function it points to.

### 1. Add an entry to `ROLES`

```js
{ id:'lender', n:'Role 5', name:'Lender', icon:'bank', avatar:'bank',
  desc:"Requests the mortgage offer and identity credentials from the buyer's wallet before releasing funds.",
  stats:[{v:'2',l:'graph steps'},{v:'VC',l:'wallet check',ok:true}],
  nodes:[ /* see step 2 */ ],
  branches:[ /* see step 3 — only if this role gates, or is gated by, another */ ]
}
```

`id` is the routing key used everywhere (`state.role`, `data-role`, `data-jump`,
`realData.<id>`). `n` is just the "Role N" label shown in the tab — it doesn't
have to be sequential, but every other role is, so keep it that way unless you
have a reason not to.

### 2. Define its `nodes[]` — the steps

Four kinds, and they determine which fields you fill in:

| kind | meaning | needs |
|---|---|---|
| `origin` | flow entry point, always "done" | nothing else |
| `input` | needs a person to act | `done()` (bool), `body()` (renders the card) |
| `auto` | fires itself once its prereqs are met | `fired()` (renders the completed card), `pend` (waiting-copy) |
| `merge` | like `auto`, but its prereqs include a cross-role `@branch` | same as `auto` |

`prereqs` is an array of sibling node ids (must complete first) and/or
`@branchId` refs (a cross-role dependency — see step 3). A node with no
prereqs — usually the first real step after `origin` — starts unlocked.

```js
nodes:[
  {id:'start', kind:'origin', ln:'Instructed'},
  {id:'request', kind:'input', ln:'Request mortgage offer', sub:'via wallet',
    api:'POST /demo-api/wallet/presentation-request',
    prereqs:['start'], done:()=>!!state.walletPresentation, body:lenderRequestBody},
  {id:'verified', kind:'input', ln:'Credentials verified', prereqs:['request'],
    done:()=>state.walletResult?.status==='verified',
    lock:'request the presentation first', body:lenderVerifyBody}
]
```

`ln` is the short label on the tracker rail; `api` is just the descriptive text
shown in the node panel header, not something the engine calls.

### 3. Define `branches[]` — only if this role depends on another, or is depended on

A branch is a labelled arrow between two nodes, on this role or another, that
the tracker draws. Look at any existing role for the shape — e.g. `bconv`'s
`sconv_set` branch: `active()` says whether the arrow is "in flight" yet,
`resolved()` says whether it's landed. If your new role's steps unlock purely
from its own prereqs, you don't need this at all.

### 4. Write the render functions

`body()` (input nodes) and `fired()` (auto/merge nodes) return an HTML string.
Reuse the existing helpers rather than hand-rolling markup — `card()`, `grid()`,
`actionCard()`, `doneCard()`, `seal()`, `svg()` are all in `data.js` and every
existing node uses them. `actionCard`/`doneCard` in particular give you the
idle → pending → done states for free; see `setBody()`/`actionBody()` for the
pattern (Part 2 uses the same shape).

### 5. Add any new `state` fields in **all three** places

This is the trap that actually bit this codebase once (`KEY_LEARNINGS.md` /
memory: a `conveyPending` field added to the initial state literal but not to
`resetAll()` threw a `TypeError` on the first reset after the change). A new
field needs to exist in:

1. The initial literal — `let state = {...}` near the top of `app.js`
2. The post-load normalisation block — `state.flags=state.flags||{}; ...` right
   before `renderRail()` runs, so a stale `localStorage` blob from before your
   change doesn't leave the field `undefined`
3. `resetAll()` — same reason, for the in-session reset button

For the lender example: `walletPresentation` and `walletResult` need adding to
all three (or just default them to `null`/`undefined` and use `?.` everywhere
you read them, which is what the existing `advid`/`sof`/`published` fields do —
simpler than tracking down every call site).

### 6. Nothing else for the tab bar / tracker

`renderRail()` maps over `ROLES` directly; `renderTracker()` reads
`roleObj(state.role).nodes`/`.branches`. Add the array entry and the new role's
tab, tracker, and node panels all just appear.

### 7. Wire up any new buttons

Every clickable `data-*` attribute is dispatched from **one** delegation
listener near the bottom of `app.js` (`document.body.addEventListener('click', ...)`).
Add a line there for each new attribute, and a handler function alongside the
existing ones (`traceFunds`, `inviteSeller`, `fireConveyEvent`, ...) — see Part 2
for the actual handler shape you need.

---

## Part 2 — Wiring a step to the wallet-verification BFF methods

The wallet flow is inherently asynchronous — a holder has to act on a separate
device — so it does **not** fit the optimistic pattern used for e.g. `traceFunds()`
(mark done immediately, fetch in the background). It fits the pattern already
built for conveyancing events: **fire a request, mark it pending, poll for the
outcome.** `fireConveyEvent()` + `pollBffEvents()` is the template; below is the
same shape pointed at `/demo-api/wallet/*` instead of `/demo-api/conveyancing/*`.

### 1. Trigger function — `app.js`, alongside `fireConveyEvent`

```js
async function requestWalletPresentation(credentialTypes){
  state.walletPresentation = {pending:true};
  sync();
  const r = await bffFetch(
    `/demo-api/wallet/presentation-request/${state.transactionDid}`,
    {method:'POST', headers:{'Content-Type':'application/json'},
     body: JSON.stringify({credentialTypes})});
  if(!r){ state.walletPresentation = null; sync(); return; }
  state.walletPresentation = {pending:false, state:r.state, requestUri:r.requestUri};
  sync();
}
```

Note this returns `state` (the OpenID4VP correlation id) and `requestUri` —
**not** `transactionDid`. `transactionDid` only got us the presentation created;
everything downstream (the wallet's fetch, the poll below) keys on `r.state`.

### 2. Render the `requestUri` for the holder to act on

The existing demo has no QR renderer. Simplest path: render `requestUri` as a
plain link/copyable string (fine for a sandbox demo where you're the one
scanning it with a test wallet) — or drop in a QR library later. This is the
`body()` function for the "request" node:

```js
function lenderRequestBody(){
  const p = state.walletPresentation;
  if(p?.state) return actionCard({icon:'qr', amber:true, title:'Waiting for the wallet…',
    sub:"Scan or open this on the holder's device:",
    body:`<div class="mono epline">${p.requestUri}</div>`,
    btn:'Waiting…', btnCls:'pen', bIcon:'clock', attr:'disabled'});
  return actionCard({icon:'wallet', amber:true, title:'Request mortgage offer + identity',
    sub:'Opens an OpenID4VP presentation request for the wallet to fetch.',
    body:'<div class="mono epline">POST /demo-api/wallet/presentation-request</div>',
    btn:'Request presentation', btnCls:'pen', bIcon:'wallet',
    attr:'data-walletrequest'});
}
```

### 3. Poll for the outcome — fold into the existing `pollBffEvents()` loop

Don't add a second `setInterval` — piggyback on the one that already runs every
5s. Add this near the top of `pollBffEvents()`, guarded the same way the
Smoove-event handling is:

```js
// inside pollBffEvents(), alongside the existing transactionDid guard
if(state.walletPresentation?.state && !state.walletPresentation.pending
   && state.walletResult?.state !== state.walletPresentation.state){
  const r = await fetch(`/demo-api/wallet/result/${state.walletPresentation.state}`);
  if(r.ok){
    const result = await r.json();
    if(result.status !== 'pending'){
      state.walletResult = result;
      changed = true;
    }
  }
}
```

(`changed` is the same flag `pollBffEvents()` already uses to decide whether to
call `sync()` at the end of the function — reuse it rather than calling `sync()`
twice per tick.)

### 4. The "done" check and the result card

```js
function lenderVerifyBody(){
  const r = state.walletResult;
  if(r?.status==='verified') return doneCard({title:'Credentials verified',
    lines:[`Verified at <b style="color:var(--ink)">${r.verifiedAt}</b>`,
           ...r.credentials.map(c=>`<code>${c.credentialType}</code> — signature ${c.signatureVerified?'valid':'NOT valid'}`)],
    reset:'data-walletreset', resetLabel:'reset'});
  if(r?.status==='failed') return actionCard({icon:'alert', amber:false, title:'Verification failed',
    sub: r.failureReason || 'unknown reason', body:'', btn:'Retry', btnCls:'pen', bIcon:'refresh',
    attr:'data-walletrequest'});
  return actionCard({icon:'clock', amber:true, title:'Waiting on the wallet…',
    sub:'The step advances automatically once the presentation lands.',
    body:'', btn:'Waiting…', btnCls:'pen', bIcon:'clock', attr:'disabled'});
}
```

### 5. Register the click handler

In the delegation block:

```js
const wreq=e.target.closest('[data-walletrequest]');
if(wreq){ requestWalletPresentation(['mortgage-offer','person-identity']); return; }
if(e.target.closest('[data-walletreset]')){ state.walletPresentation=null; state.walletResult=null; sync(); return; }
```

### 6. One thing that's different from every other `/demo-api/*` route

Every other BFF route is only ever called by our own SPA, same-origin. These
two are not: `GET /demo-api/wallet/request/{state}` and
`POST /demo-api/wallet/callback` are fetched **directly by the holder's wallet**
— a genuinely external, cross-origin caller. Don't assume the same trust
boundary as the rest of `/demo-api/*` when touching those two routes; they're
the one deliberately public surface this BFF exposes to a third party (see
`WALLET_INTEGRATION.md`'s Prerequisites section for what that third party needs
from us first).

## Worked example

`terraform`/`Program.cs` already have everything Part 2 needs deployed — adding
the `lender` role above end-to-end is: one `ROLES` entry (Part 1, steps 1–3),
two render functions (`lenderRequestBody`, `lenderVerifyBody` — Part 2, steps
2 & 4), one trigger function (`requestWalletPresentation` — Part 2, step 1),
one poll addition (Part 2, step 3), two delegation lines (Part 2, step 5), and
three lines of `state` bookkeeping (Part 1, step 5). No backend changes at all
— the BFF side of this was the point of ADR-0013.
