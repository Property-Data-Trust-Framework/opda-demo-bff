/* ============================================================
   OPDA Property Data Visualiser — STATIC layer
   Icons · block renderers · custom node bodies · the five-role
   dependency graph (nodes + branches). Loaded before app.js.
   ============================================================ */

const VERSION = '2.0';

/* ---------- icon set (inline SVG, 24-grid, stroke) ---------- */
const I = {
  home:'<path d="M3 11l9-7 9 7"/><path d="M5 10v10h14V10"/>',
  key:'<circle cx="8" cy="9" r="3.5"/><path d="M10.6 11.4L20 20.8"/><path d="M17.5 18.3l1.8-1.8M15.2 16l1.8-1.8"/>',
  user:'<circle cx="12" cy="8" r="3.6"/><path d="M5 20c0-3.6 3.1-6 7-6s7 2.4 7 6"/>',
  scale:'<path d="M12 3v18"/><path d="M5 7h14l-2.6-2"/><path d="M5 7l-2.5 6a3 3 0 005 0z"/><path d="M19 7l-2.5 6a3 3 0 005 0z"/><path d="M8.5 21h7"/>',
  id:'<rect x="3" y="5" width="18" height="14" rx="2.2"/><circle cx="8.5" cy="11" r="2.1"/><path d="M13 9.5h5M13 12.5h5M5.3 15.6c.5-1.2 1.7-2 3.2-2s2.7.8 3.2 2"/>',
  fingerprint:'<path d="M12 5a7 7 0 00-7 7v3"/><path d="M19 14v-2a7 7 0 00-3.5-6"/><path d="M9 13a3 3 0 016 0v2a4 4 0 01-1 3"/><path d="M12 13v3"/><path d="M7 18a8 8 0 001 3"/>',
  cart:'<circle cx="9.5" cy="20" r="1.4"/><circle cx="17" cy="20" r="1.4"/><path d="M3 4h2.2l2.3 12h9.6l1.9-8.5H6.2"/>',
  package:'<path d="M12 3l8 4.4v9.2L12 21l-8-4.4V7.4z"/><path d="M4 7.6l8 4.4 8-4.4"/><path d="M12 12v9"/>',
  edit:'<path d="M4 20h4L18 10l-4-4L4 16z"/><path d="M13 7l4 4"/>',
  search:'<circle cx="11" cy="11" r="6.5"/><path d="M20.5 20.5l-4-4"/>',
  grid:'<rect x="4" y="4" width="7" height="7" rx="1.4"/><rect x="13" y="4" width="7" height="7" rx="1.4"/><rect x="4" y="13" width="7" height="7" rx="1.4"/><rect x="13" y="13" width="7" height="7" rx="1.4"/>',
  send:'<path d="M21 3L3 10.5l7 2.8L12.8 21z"/><path d="M21 3L10 14"/>',
  download:'<path d="M12 4v11"/><path d="M8 11.5l4 4 4-4"/><path d="M5 20h14"/>',
  doc:'<path d="M7 3h7l5 5v13H7z"/><path d="M14 3v5h5"/><path d="M10 13h6M10 17h6"/>',
  eye:'<path d="M2 12s4-7 10-7 10 7 10 7-4 7-10 7-10-7-10-7z"/><circle cx="12" cy="12" r="3"/>',
  refresh:'<path d="M20.5 12a8.5 8.5 0 10-2.6 6.1"/><path d="M20.5 5.5V11h-5.5"/>',
  pound:'<path d="M8 21h9"/><path d="M9.5 21c1.8-1.8 1.8-3.6 1.8-5.5V8.2A3.2 3.2 0 0117.5 8"/><path d="M7.5 13.5H14"/>',
  shield:'<path d="M12 3l7 3v6c0 4.2-3 7.4-7 9-4-1.6-7-4.8-7-9V6z"/><path d="M9 12l2.2 2.2L15.5 10"/>',
  check:'<path d="M5 12l5 5L20 7"/>',
  bolt:'<path d="M13 3L5 14h6l-1 7 8-11h-6z"/>',
  pin:'<path d="M12 21s7-5.6 7-11A7 7 0 005 10c0 5.4 7 11 7 11z"/><circle cx="12" cy="10" r="2.5"/>',
  info:'<circle cx="12" cy="12" r="9"/><path d="M12 11v5M12 7.5v.5"/>',
  bank:'<path d="M3 9l9-5 9 5"/><path d="M5 9v9M19 9v9M9 9v8M15 9v8"/><path d="M3 21h18"/>',
  lock:'<rect x="4.5" y="10" width="15" height="10.5" rx="2.2"/><path d="M8 10V7a4 4 0 018 0v3"/><circle cx="12" cy="15" r="1.3"/>',
  clock:'<circle cx="12" cy="12" r="9"/><path d="M12 7.5V12l3 1.8"/>',
  handoff:'<path d="M3 9h13l-3.5-3.5M21 15H8l3.5 3.5"/>',
  mail:'<rect x="3" y="5" width="18" height="14" rx="2"/><path d="M4 7l8 6 8-6"/>',
  arrow:'<path d="M5 12h14"/><path d="M13 6l6 6-6 6"/>',
  cal:'<rect x="3.5" y="5" width="17" height="15" rx="2"/><path d="M3.5 9.5h17M8 3.5v3M16 3.5v3"/>',
  braces:'<path d="M9 4c-1.7 0-2 1.3-2 3v1.4C7 9.7 6.3 10.4 5 10.4c1.3 0 2 .7 2 2V14c0 1.7.3 3 2 3"/><path d="M15 4c1.7 0 2 1.3 2 3v1.4c0 1.3.7 2 2 2-1.3 0-2 .7-2 2V14c0 1.7-.3 3-2 3"/>',
};
const svg = (name, sw=1.85) => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="${sw}" stroke-linecap="round" stroke-linejoin="round">${I[name]||''}</svg>`;
const sealSvg = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12l5 5L20 7"/></svg>`;
const dashSvg = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round"><path d="M6 12h12"/></svg>`;
const seal = (state, cls='') => `<span class="seal ${state==='warn'?'warn':''} ${cls}">${state==='warn'?dashSvg:sealSvg}</span>`;
const ep = (method, path) => `<span class="ep"><b>${method}</b> ${path}</span>`;
const nowHM = () => { const d=new Date(); return String(d.getHours()).padStart(2,'0')+':'+String(d.getMinutes()).padStart(2,'0'); };

/* ---------- conveyancing events (labels for the shared stream) ---------- */
const TRIGGER_EVENTS = [
  { id:'completion_set',      label:'Completion set',      event:'completion.date.set', owner:'sconv' },
  { id:'completion_actioned', label:'Completion actioned', event:'completion.actioned', owner:'bconv' },
  { id:'tid_received',        label:'TID received',        event:'tid.received',        owner:'bconv' }
];

/* ---------- consent gate ---------- */
const GATES = {
  seller_consent:{ name:'Seller consent', owner:'seller',
    desc:'The Seller must consent before the sourced Property Pack is released to the buyer side.' }
};

const SHORT = {agent:'Agent',seller:'Seller',buyer:'Buyer',sconv:'Seller Conv.',bconv:'Buyer Conv.'};

/* ============================================================
   BLOCK RENDERERS (rich detail cards)
   ============================================================ */
function card(b, inner){
  const provHtml = b.seal
    ? `<span class="prov ${b.seal==='warn'?'warn':''}">${b.provLabel?`<span class="pl">${b.provLabel}</span>`:''}${seal(b.seal)}</span>`
    : '';
  return `<div class="card s${b.span||6}">
      ${b.title?`<div class="chead"><span class="ct">${b.title}</span>${provHtml}</div>`:''}
      ${inner}
    </div>`;
}
const Blocks = {
  kpis(b){
    const tiles = b.items.map(k=>`
      <div class="kpi">
        <div class="kl"><span>${k.label}</span>${k.seal?seal(k.seal,'sm'):''}</div>
        <div class="kv ${k.small?'sm':''}">${k.value}</div>
        ${k.sub?`<div class="ks">${k.sub}</div>`:''}
      </div>`).join('');
    return card(b,`<div class="kpis c${b.cols||b.items.length}">${tiles}</div>`);
  },
  epc(b){
    const bars = [['A','#1f9b63','55'],['B','#43b35f','68'],['C','#9bc63f','80'],['D','#f5d23f','92'],['E','#f5a93f','78'],['F','#f57a3f','64'],['G','#ef5a4a','50']];
    const rows = bars.map(([g,c,w])=>`<div class="b ${g===b.band?'on':''}" style="width:${w}%;background:${c};">${g}</div>`).join('');
    return card(b,`<div class="epc"><div><div class="kv" style="font-size:34px;font-weight:800;">${b.value}</div><div class="ks">potential ${b.potential}</div></div><div class="scale">${rows}</div></div>`);
  },
  docs(b){
    const rows = b.items.map(d=>`
      <div class="doc">
        <div class="dico">${svg('doc')}</div>
        <div><div class="dn">${d.name}</div><div class="dm">${d.meta}</div></div>
        <div class="dl">${seal(d.seal,'sm')}${d.url
          ? `<a href="${d.url}" target="_blank" rel="noopener" class="dbtn">${svg('download')}</a>`
          : `<button class="dbtn" disabled>${svg('download')}</button>`
        }</div>
      </div>`).join('');
    return card(b, rows + (b.action ? `<div class="trig-foot">${b.action}</div>` : ''));
  },
  rollup(b){
    const chips = b.items.map(c=>`<span class="chip">${seal(c.seal,'sm')}${c.label}</span>`).join('');
    return card(b,`<div class="rollup"><div class="big">${b.ratio.split('/')[0]}<span class="of"> / ${b.ratio.split('/')[1]} signed</span></div><div class="chips" style="margin-top:14px;">${chips}</div></div>`);
  },
  form(b){
    const rows = b.items.map(f=>`<div class="frow"><span class="fl">${f.label}</span><span class="fv ${f.ph?'ph':''}">${f.value}</span></div>`).join('');
    return card(b,rows);
  },
  status(b){
    const lines = (b.lines||[]).map(l=>`<li>${l}</li>`).join('');
    const prov = b.seal
      ? `<span class="prov ${b.seal==='warn'?'warn':''}" style="position:absolute;top:16px;right:16px;">${b.provLabel?`<span class="pl">${b.provLabel}</span>`:''}${seal(b.seal)}</span>`
      : '';
    return `<div class="card s${b.span||6}" style="position:relative;">${prov}
      <div class="status ${b.tone}"><div class="sico">${svg(b.icon||(b.tone==='ok'?'shield':'info'))}</div>
      <div><div class="stit">${b.title}</div>${lines?`<ul>${lines}</ul>`:''}${b.action?`<div style="margin-top:13px;">${b.action}</div>`:''}</div></div></div>`;
  },
  note(b){
    return `<div class="card s${b.span||12}" style="background:none;border:0;padding:0;box-shadow:none;"><div class="note"><span class="ni">${svg('info')}</span><div>${b.text}${b.action?`<div style="margin-top:12px;">${b.action}</div>`:''}</div></div></div>`;
  },
  map(b){
    return `<div class="card s${b.span||6} mapcard">
      ${b.title?`<div class="chead"><span class="ct">${b.title}</span></div>`:''}
      <div class="mapslot" style="height:200px;border-radius:8px;overflow:hidden;"></div>
    </div>`;
  },
  stream(b){
    const rows = buildStream().map(r=>{
      const fresh = state.lastKey===(r[0]+'|'+r[1]);
      return `<div class="ev ${fresh?'just-fired':''}"><span class="dot done ${fresh?'fresh':''}"></span><span class="t">${r[0]}</span><span class="name">${r[1]}</span><span class="by">${r[2]}</span></div>`;
    }).join('');
    return card(b,`<div class="feedhead"><span class="live"><span class="d"></span>LIVE</span> · shared transaction stream · JWT verified via /jwks</div>${rows||'<div class="feed-empty">No events yet — actions stream in here.</div>'}`);
  },
  idform(b){
    const role=b.role, done=state.id[role];
    if(done){
      return card(b,`<div class="status ok"><div class="sico">${sealSvg}</div><div><div class="stit">Identity verified</div><ul><li>Signed at <b style="color:var(--ink)">${done.time}</b> · <code>${role}.identity.verified</code> streamed</li><li>Document &amp; liveness checks passed</li></ul><div style="margin-top:12px;"><button class="linkbtn" data-idedit="${role}">${svg('refresh',2)} re-enter</button></div></div></div>`);
    }
    return card(b,`<div class="idform">
      <div class="idfield"><label>Full legal name</label><input placeholder="e.g. Alex Morgan"></div>
      <div class="idfield"><label>Date of birth</label><input placeholder="DD / MM / YYYY"></div>
      <div class="idfield wide"><label>Current address</label><input placeholder="14 Elm Grove, Bristol BS6 5DB"></div>
      <div class="idfield"><label>Document type</label><select><option>UK Passport</option><option>Driving licence</option></select></div>
      <div class="idfield"><label>Document number</label><input placeholder="•••••••••"></div>
      <div class="idfoot"><span class="demo">Demo only — nothing entered is stored or sent.</span><button class="btn amber" data-idsubmit="${role}">${svg('shield')} Verify identity</button></div>
    </div>`);
  },
  search(b){
    if(state.addr){
      const addrData = typeof realData!=='undefined' && realData.address?.data?.[0];
      if(!addrData){
        const results = typeof realData!=='undefined' && realData.addressResults;
        if(!results) return card(b,`<div class="searchwrap"><div style="color:var(--ink-3);font-size:13.5px;padding:6px 0;display:flex;align-items:center;gap:8px;"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 3"/></svg>Searching…</div></div>`);
        const items = results.map((r,i)=>`<button class="addrbtn" data-addrpick="${i}"><span class="addrline">${r.address}</span><span class="chip mono" style="font-size:10.5px;margin-left:auto;flex:none;">${r.uprn}</span></button>`).join('');
        return card(b,`<div class="addrpick"><div class="pickhead">${svg('pin')} ${results.length} properties found — select the correct one</div><div class="addrlist">${items}</div><div style="margin-top:10px;"><button class="linkbtn" data-searchreset>${svg('refresh',2)} new search</button></div></div>`);
      }
      const displayAddr = addrData.address || '14 Elm Grove, Redland, Bristol BS6 5DB';
      const displayUprn = addrData.uprn || '—';
      return card(b,`<div class="resolved"><div class="bigaddr">${displayAddr}</div>
        <div class="chips"><span class="chip">${seal('ok','sm')}UPRN <b class="mono" style="margin-left:3px">${displayUprn}</b></span><span class="chip">format valid · resolved ${state.addr.time}</span></div>
        <div style="margin-top:13px;"><button class="linkbtn" data-searchreset>${svg('refresh',2)} new search</button></div></div>`);
    }
    return card(b,`<div class="searchwrap"><div class="searchin"><span class="ico">${svg('search',2)}</span><input id="addrInput" placeholder="Search an address or postcode…  e.g. 14 Elm Grove, Bristol BS6"></div><button class="btn" data-search>${svg('search',2)} Search</button></div>
      <div class="sugg-row">try:<button class="sugg" data-search>33 Evelyn Road, E17 9HE</button><button class="sugg" data-search>5 Cavendish Road, CH45 2NX</button><button class="sugg" data-search>Capel Isaac, SA4 3JQ</button></div>`);
  },
  surveys(b){
    const role=b.role, done=state.surv[role];
    const fmtExpiry = iso => { if(!iso) return 'pre-signed S3'; const d=new Date(iso); return 'expires '+d.toLocaleTimeString('en-GB',{hour:'2-digit',minute:'2-digit'}); };
    const rawDocs = typeof realData!=='undefined' && (realData.surveys?.documents ?? realData.surveys?.data?.documents);
    const docs = rawDocs && rawDocs.length
      ? rawDocs.map(d=>`<div class="doc"><div class="dico">${svg('doc')}</div><div><div class="dn">${d.filename||'Document'}</div><div class="dm">${fmtExpiry(d.expiresAt)}</div></div><div class="dl">${seal('ok','sm')}${d.url?`<a href="${d.url}" target="_blank" rel="noopener" class="dbtn">${svg('download')}</a>`:`<button class="dbtn" disabled>${svg('download')}</button>`}</div></div>`).join('')
      : `<div class="doc"><div class="dico">${svg('clock')}</div><div><div class="dn">Retrieving documents…</div><div class="dm">loading</div></div></div>`;
    if(done){
      return card(b,`<div class="turnline done">${sealSvg}<span>Surveys retrieved at ${done.time} · <span class="mono">documents.surveys.retrieved</span> streamed</span></div>${docs}<div class="trig-foot"><span class="tf-l">Each survey carries its own provenance seal</span><button class="linkbtn" data-survget="${role}">${svg('refresh',2)} re-fetch</button></div>`);
    }
    const who = role==='sconv'?'Acting for the seller, pull any surveys filed against the property':"Pull the surveys for your client's review";
    return card(b,`<div class="status ok" style="margin-bottom:15px;"><div class="sico" style="background:var(--amber-bg);color:var(--amber-d);border-color:var(--amber-ring)">${svg('doc')}</div><div><div class="stit">Retrieve surveys on demand</div><ul><li>${who}</li><li>Served from the documents service, each with its own seal</li></ul></div></div><button class="btn amber" data-survget="${role}">${svg('download')} Retrieve surveys</button>`);
  },
  consentRequest(b){
    const st = gateStatus(b.gate), c = (state.gates&&state.gates[b.gate])||{};
    if(st==='requested'){
      return `<div class="card gatecard s12 gate-wait"><div class="gatebar">
        <span class="gicon">${svg('clock',2)}</span>
        <div class="gtxt"><div class="gt">Requested — waiting on the Seller</div>
          <div class="gs">Sent at <b>${c.reqTime}</b> · the Seller has been prompted to confirm or deny. Everything downstream is held until they grant consent.</div></div>
        <button class="linkbtn" data-withdraw="${b.gate}">${svg('refresh',2)} withdraw</button></div></div>`;
    }
    if(st==='granted'){
      return `<div class="card gatecard s12 gate-ok"><div class="gatebar">
        <span class="gicon">${sealSvg}</span>
        <div class="gtxt"><div class="gt">Consent granted — pack released</div>
          <div class="gs">Released at <b>${c.decTime}</b> · the sourced pack carries across untouched, seals and all. Nothing is re-sourced.</div></div>
        <span class="ghint ok">${sealSvg} handoff complete</span></div></div>`;
    }
    if(st==='denied'){
      return `<div class="card gatecard s12 gate-deny"><div class="gatebar">
        <span class="gicon">${svg('lock')}</span>
        <div class="gtxt"><div class="gt">The Seller denied the request</div>
          <div class="gs">Declined at <b>${c.decTime}</b> · the pack stays locked. You can send the request again.</div></div>
        <button class="btn amber" data-request="${b.gate}">${svg('send')} Request again</button></div></div>`;
    }
    return `<div class="card gatecard s12 gate-rel"><div class="gatebar">
      <span class="gicon">${svg('download')}</span>
      <div class="gtxt"><div class="gt">Request the Seller's Property Pack</div>
        <div class="gs">Sends a just-in-time consent request to the Seller to release their signed pack. Nothing is re-sourced or re-paid — you inherit their seals.</div></div>
      <button class="btn" data-request="${b.gate}">${svg('send')} Request pack</button></div></div>`;
  },
  consentInbound(b){
    const g = GATES[b.gate], st = gateStatus(b.gate), c = (state.gates&&state.gates[b.gate])||{};
    if(st==='requested'){
      return `<div class="card gatecard s12 gate-rel"><div class="gatebar">
        <span class="gicon">${svg('bolt',2)}</span>
        <div class="gtxt"><div class="gt">A verified buyer has requested your Property Pack</div>
          <div class="gs">Requested at <b>${c.reqTime}</b> · the buyer side is <b>waiting on you</b>. Confirm to release the signed pack, or deny.</div></div>
        <div class="cbtns"><button class="btn amber" data-decide="yes" data-gate="${b.gate}">${svg('shield')} Confirm — release</button>
          <button class="btn ghost" data-decide="no" data-gate="${b.gate}">Deny</button></div></div></div>`;
    }
    if(st==='granted'){
      return `<div class="card gatecard s12 gate-ok"><div class="gatebar">
        <span class="gicon">${sealSvg}</span>
        <div class="gtxt"><div class="gt">Released — consent granted</div>
          <div class="gs">You released the pack at <b>${c.decTime}</b> · the buyer side is unblocked and inherits every seal.</div></div>
        <button class="linkbtn" data-revoke="${b.gate}">${svg('refresh',2)} revoke</button></div></div>`;
    }
    if(st==='denied'){
      return `<div class="card gatecard s12 gate-deny"><div class="gatebar">
        <span class="gicon">${svg('lock')}</span>
        <div class="gtxt"><div class="gt">Request denied</div>
          <div class="gs">Denied at <b>${c.decTime}</b> · the pack stays locked. You can change your mind and release it.</div></div>
        <button class="btn amber" data-decide="yes" data-gate="${b.gate}">${svg('shield')} Change — release</button></div></div>`;
    }
    return `<div class="card gatecard s12 gate-idle"><div class="gatebar">
      <span class="gicon">${svg('clock',2)}</span>
      <div class="gtxt"><div class="gt">No pending consent requests</div>
        <div class="gs">${g.desc} When a verified buyer requests it, you'll be prompted to confirm or deny — right here.</div></div>
      <span class="ghint">${svg('clock',2)} awaiting request</span></div></div>`;
  }
};
function renderBlocks(blocks){ return blocks.map(b=> (Blocks[b.type]?Blocks[b.type](b):'') ).join(''); }
function grid(...blocks){ return `<div class="grid">${renderBlocks(blocks)}</div>`; }

/* ---------- mini action card helper (custom input nodes) ---------- */
function actionCard(opts){
  // opts: {icon, title, sub, btn, btnCls, attr, span}
  return `<div class="card s${opts.span||12} actcard"><div class="status ${opts.tone||'ok'}">
    <div class="sico" style="${opts.amber?'background:var(--amber-bg);color:var(--amber-d);border-color:var(--amber-ring)':''}">${svg(opts.icon)}</div>
    <div style="flex:1"><div class="stit">${opts.title}</div>${opts.sub?`<div class="acsub">${opts.sub}</div>`:''}
    ${opts.body||''}
    <div style="margin-top:14px;"><button class="btn ${opts.btnCls||'amber'}" ${opts.attr}>${svg(opts.bIcon||'arrow')} ${opts.btn}</button></div></div></div></div>`;
}
function doneCard(opts){
  return `<div class="card s${opts.span||12}"><div class="status ok"><div class="sico">${sealSvg}</div>
    <div><div class="stit">${opts.title}</div>${opts.lines?`<ul>${opts.lines.map(l=>`<li>${l}</li>`).join('')}</ul>`:''}
    ${opts.jump?`<div class="acjump" style="margin-top:12px;">${opts.jump}</div>`:''}
    ${opts.reset?`<div style="margin-top:10px;"><button class="linkbtn" ${opts.reset}>${svg('refresh',2)} ${opts.resetLabel||'reset'}</button></div>`:''}</div></div></div>`;
}
const jumpBtn = (tab,label)=>`<button class="jumpbtn" data-jump="${tab}">${label} ${svg('arrow',2)}</button>`;

/* ---------- custom node bodies ---------- */
function inviteBody(){
  if(state.invited) return doneCard({title:'ID verification link sent to the seller',
    lines:[`Sent at <b style="color:var(--ink)">${state.invited.time}</b> via a third-party IDV provider`,'The seller\'s "Verify identity" step is now unlocked'],
    jump:jumpBtn('seller','Go to the Seller tab'), reset:'data-invitereset', resetLabel:'re-send'});
  return `<div class="card s12 actcard"><div class="acform">
    <div class="acform-h">${svg('mail')}<div><div class="stit">Invite the seller to verify their identity</div><div class="acsub">In a live deployment this sends a verification link through a third-party IDV provider.</div></div></div>
    <div class="idform" style="margin-top:4px;">
      <div class="idfield"><label>Seller name</label><input placeholder="A. Seller"></div>
      <div class="idfield"><label>Contact (email / phone)</label><input placeholder="seller@email.com"></div>
    </div>
    <div class="idfoot"><span class="demo">Demo only — nothing is actually sent.</span><button class="btn amber" data-invite>${svg('send')} Send ID verification link</button></div>
  </div></div>`;
}
function advidBody(){
  if(state.advid) return doneCard({title:'Advanced identity verification complete',
    lines:[`Signed at <b style="color:var(--ink)">${state.advid.time}</b> · <code>seller.identity.advanced</code> streamed`,'Flows across to the Seller Conveyancer as an inherited, signed check'],
    jump:jumpBtn('sconv','See the Seller Conveyancer'), reset:'data-advidreset', resetLabel:'re-enter'});
  return `<div class="card s12 actcard"><div class="acform">
    <div class="acform-h">${svg('fingerprint')}<div><div class="stit">Advanced ID verification</div><div class="acsub">An enhanced check your conveyancer relies on — runs via a <span class="mono">3rd-party IDV provider</span>, building on your initial verification.</div></div></div>
    <div class="idform" style="margin-top:4px;">
      <div class="idfield"><label>Full legal name</label><input placeholder="A. Seller"></div>
      <div class="idfield"><label>National Insurance no.</label><input placeholder="QQ 12 34 56 C"></div>
      <div class="idfield wide"><label>Document</label><select><option>UK Passport + proof of address</option><option>Driving licence + bank statement</option></select></div>
    </div>
    <div class="idfoot"><span class="demo">Demo only — nothing entered is stored.</span><button class="btn amber" data-advid>${svg('shield')} Complete advanced ID check</button></div>
  </div></div>`;
}
function fundsBody(){
  if(state.sof) return `<div class="grid">
    <div class="card s7"><div class="status ok"><div class="sico">${sealSvg}</div><div><div class="stit">Source of funds verified</div><ul><li>Deposit £62,000 traced to source at <b style="color:var(--ink)">${state.sof.time}</b></li><li>Savings + gifted deposit, both evidenced</li><li>Signed report returned · <code>Armalytix</code></li></ul><div style="margin-top:12px;"><button class="linkbtn" data-fundsreset>${svg('refresh',2)} re-trace</button></div></div></div></div>
    <div class="card s5"><div class="chead"><span class="ct">Report summary</span></div><div class="kpis c2"><div class="kpi"><div class="kl">Deposit traced</div><div class="kv sm">£62,000</div></div><div class="kpi"><div class="kl">Sources</div><div class="kv sm">2 evidenced</div><div class="ks">savings · gift</div></div></div></div></div>`;
  return `<div class="card s12 actcard"><div class="acform">
    <div class="acform-h">${svg('pound')}<div><div class="stit">Trace the deposit to source</div><div class="acsub">Runs an open-banking source-of-funds trace for AML evidence.</div></div></div>
    <div class="idform" style="margin-top:4px;">
      <div class="idfield"><label>Deposit amount</label><input placeholder="£62,000"></div>
      <div class="idfield"><label>Evidenced source</label><input placeholder="Savings + gift"></div>
    </div>
    <div class="idfoot"><span class="demo">GET /v1/source-of-funds/&#123;id&#125;</span><button class="btn amber" data-funds>${svg('pound')} Trace source of funds</button></div>
  </div></div>`;
}
function setBody(){
  if(eventFired('completion_set')) return doneCard({title:'Completion date set',
    lines:[`Locked at <b style="color:var(--ink)">${state.fired.completion_set.time}</b> · <code>completion.date.set</code> streamed`,'The Buyer Conveyancer can now action completion'],
    jump:jumpBtn('bconv','See the Buyer Conveyancer'), reset:'data-eventreset="completion_set"', resetLabel:'reset'});
  if(state.conveyPending?.completion_set) return actionCard({icon:'clock', amber:true, title:'Waiting for Smoove webhook…',
    sub:'Signed event dispatched — the step will advance automatically when the webhook confirms.',
    body:'<div class="mono epline">POST /conveyancing-events/completion-set</div>',
    btn:'Waiting…', btnCls:'pen', bIcon:'clock', attr:'disabled'});
  return actionCard({icon:'cal', amber:true, title:'Set the agreed completion date',
    sub:'Locks the completion date into the transaction as a signed conveyancing event.', body:'<div class="mono epline">POST /conveyancing-events/completion-set</div>',
    btn:'Set completion date', btnCls:'pen', bIcon:'cal', attr:'data-fire="completion_set"'});
}
function actionBody(){
  if(eventFired('completion_actioned')) return doneCard({title:'Completion actioned — funds released',
    lines:[`Confirmed at <b style="color:var(--ink)">${state.fired.completion_actioned.time}</b> · <code>completion.actioned</code> streamed`,"The Seller Conveyancer's completion merges closed; the TID auto-registers next"],
    jump:jumpBtn('sconv','Back to the Seller Conveyancer'), reset:'data-eventreset="completion_actioned"', resetLabel:'reset'});
  if(state.conveyPending?.completion_actioned) return actionCard({icon:'clock', amber:true, title:'Waiting for Smoove webhook…',
    sub:'Signed event dispatched — the step will advance automatically when the webhook confirms.',
    body:'<div class="mono epline">POST /conveyancing-events/completion-actioned</div>',
    btn:'Waiting…', btnCls:'pen', bIcon:'clock', attr:'disabled'});
  return actionCard({icon:'shield', amber:true, title:'Action completion',
    sub:'Confirms funds have moved and executes the deal — the matching event for the date the Seller Conveyancer set.', body:'<div class="mono epline">POST /conveyancing-events/completion-actioned</div>',
    btn:'Action completion', btnCls:'pen', bIcon:'shield', attr:'data-fire="completion_actioned"'});
}
function publishBody(){
  if(state.published) return `<div class="grid">
    <div class="card s7" style="position:relative;"><span class="prov warn" style="position:absolute;top:16px;right:16px;"><span class="pl">output<br>step</span>${seal('warn')}</span><div class="status ok"><div class="sico">${sealSvg}</div><div><div class="stit">Listing published — live on the portal</div><ul><li>Published at <b style="color:var(--ink)">${state.published.time}</b></li><li>Material Information (Parts A–C) attached</li><li>Each fact links back to its source seal</li></ul><div style="margin-top:12px;"><button class="linkbtn" data-publishreset>${svg('refresh',2)} unpublish</button></div></div></div></div>
    <div class="card s5" style="background:none;border:0;padding:0;box-shadow:none;"><div class="note"><span class="ni">${svg('info')}</span><div>Buyers viewing the listing can see <b>which facts are signed at source</b> — trust travels into the portal.</div></div></div></div>`;
  return actionCard({icon:'send', amber:true, title:'Publish the listing',
    sub:'Pushes the sale-ready listing — with Material Information and its source seals — out to the portal feed.', body:'<div class="mono epline">— export / portal</div>',
    btn:'Publish listing', btnCls:'pen', bIcon:'send', attr:'data-publish'});
}
function reviewBody(){
  return `<div class="card s12" style="background:none;border:0;padding:0;box-shadow:none;"><div class="note"><span class="ni">${svg('eye')}</span><div>The Buyer flow opens straight into the full <b>Property Passport</b>, read-only, with every source seal visible — nothing is re-fetched.<div style="margin-top:12px;"><button class="btn" onclick="setView('passport')">${svg('eye')} Open Property Passport</button></div></div></div></div>`;
}

/* ============================================================
   THE FIVE-ROLE DEPENDENCY GRAPH  (agent-first)
   kinds: origin | input | auto | merge
   prereqs entries: 'nodeId'  or  '@branchId'
   ============================================================ */
const ROLES = [
  { id:'agent', n:'Role 0', name:'Seller Estate Agent', icon:'key', avatar:'key',
    desc:'Triggers the whole transaction. Captures the property, invites the seller to verify, auto-sources listing information once the UPRN validates, and publishes when every detail is ready.',
    stats:[{v:'6',l:'graph steps'},{v:'8',l:'APIs touched'},{v:'auto',l:'sourcing',ok:true}],
    nodes:[
      {id:'enter',kind:'input',ln:'Enter property',sub:'& resolve',api:'GET /v1/places · /uprn/validate',
        prereqs:[], done:()=>!!state.addr,
        body:()=>grid({type:'search',title:'Capture &amp; resolve the address',span:8,seal:'ok',provLabel:'OS Places'},{type:'map',title:'Location',span:4})},
      {id:'invite',kind:'input',ln:'Invite seller',sub:'(ID link)',api:'POST /identity/invite',
        prereqs:['enter'], done:()=>!!state.invited, lock:'capture the property first', body:inviteBody},
      {id:'uprn',kind:'auto',ln:'Validate UPRN',api:'GET /v1/uprn/validate',prereqs:['invite'],
        fired:()=>{
          const uprn=(typeof realData!=='undefined'&&realData.address?.data?.[0]?.uprn)||'—';
          return `<span class="chip">${seal('ok','sm')}UPRN <b class="mono" style="margin-left:3px">${uprn}</b> validated</span>`;
        },
        pend:'fires automatically once the seller is invited'},
      {id:'pack',kind:'auto',ln:'Gather listing info',api:'GET EPC · council-tax · coalfield · title',prereqs:['uprn'],
        fired:()=>{
          const p=typeof realData!=='undefined'&&realData.pack;
          const cl=state.packCleared||{};
          const pp=p?.propertyPack;
          const epcBand=pp?.energyEfficiency?.certificate?.currentEnergyRating??'—';
          const ctBand=pp?.councilTax?.councilTaxBand??'—';
          const coalfieldRaw=pp?.environmentalIssues?.coalMining?.riskIndicator;
          const coalStatus=coalfieldRaw==='Yes'?'ON':coalfieldRaw==='No'?'OFF':'—';
          const isLeasehold=pp?.titlesToBeSold?.[0]?.registerExtract?.ocSummaryData?.registerEntryIndicators?.leaseHoldTitleIndicator;
          const tenure=isLeasehold===true?'Leasehold':isLeasehold===false?'Freehold':'—';
          function vchip(id,sl,label){
            if(cl[id]) return `<span class="chip" style="opacity:.55;">${svg('refresh',1.6)} ${label} <button class="linkbtn" data-restorepackchip="${id}" style="margin-left:2px;">re-fetch</button></span>`;
            return `<span class="chip">${seal(sl,'sm')}${label}<button class="linkbtn" data-resetpackchip="${id}" style="margin-left:5px;opacity:.4;" title="clear">×</button></span>`;
          }
          return `<div class="chips">
            ${vchip('epc','ok',`EPC ${epcBand}`)}
            ${vchip('ct',ctBand==='D'?'warn':'ok',`Council tax ${ctBand}`)}
            ${vchip('coalfield','ok',`Coalfield ${coalStatus}`)}
            ${vchip('title','ok',tenure)}
          </div>`;
        },
        pend:'auto-sources listing information once the UPRN validates'},
      {id:'ready',kind:'merge',ln:'All details ready',api:'internal',prereqs:['pack','@seller_id'],
        fired:()=>`<span class="chip">${seal('ok','sm')}All listing information assembled — property is sale-ready</span>`,
        pend:"needs listing info gathered and the seller's ID verified"},
      {id:'publish',kind:'input',ln:'Publish listing',api:'— export / portal',prereqs:['ready'],
        done:()=>!!state.published, lock:'unlocks once all details are ready', body:publishBody}
    ],
    branches:[{id:'seller_id',label:'Seller verified ID',from:'invite',to:'ready',party:'Seller',tab:'seller',
      active:()=>!!state.invited, resolved:()=>!!(state.id&&state.id.seller)}]
  },

  { id:'seller', n:'Role 1', name:'Seller', icon:'home', avatar:'home',
    desc:'Proves identity, sources their own Property Pack (via Sprift / PDI), completes advanced ID for the conveyancer, and — when a verified buyer asks — releases the pack via the consent gate.',
    stats:[{v:'5',l:'graph steps'},{v:'2',l:'ID checks',ok:true},{v:'JIT',l:'consent',ok:true}],
    nodes:[
      {id:'start',kind:'origin',ln:'Sale opened'},
      {id:'verify',kind:'input',ln:'Verify identity',api:'POST /identity/verify',prereqs:['@invite_recv'],
        done:()=>!!(state.id&&state.id.seller),
        body:()=>grid({type:'idform',role:'seller',title:'Identity details',span:7},{type:'note',span:5,text:'Every identity result carries a <b>signature block</b>, so downstream roles trust it without re-checking. <b>Demo only</b> — entered data goes nowhere.'})},
      {id:'shared',kind:'auto',ln:'Identity shared',sub:'signed',api:'internal',prereqs:['verify'],
        fired:()=>`<span class="chip">${seal('ok','sm')}Signed identity result shared and verified on-chain</span>`,
        pend:'emitted automatically once you verify'},
      {id:'advid',kind:'input',ln:'Advanced ID',sub:'verification',api:'POST /identity/verify · enhanced',prereqs:['shared'],
        done:()=>!!state.advid, lock:'complete your initial verification first', body:advidBody},
      {id:'packSourced',kind:'auto',ln:'Property pack sourced',sub:'Sprift / PDI',api:'GET /demo-api/property-pack',prereqs:['advid'],
        fired:()=>{
          const s=typeof realData!=='undefined'&&realData.sellerPack;
          const sourceLabel=s?.source==='sprift'?'Sprift':s?.source==='pdi'?'PDI':null;
          const label=sourceLabel?`Property pack sourced · ${sourceLabel}`:'Property pack sourced and sealed';
          return `<span class="chip">${seal('ok','sm')}${label} — ready to release to buyers</span>`;
        },
        pend:'auto-sources the full property pack once advanced ID is verified'},
      {id:'consent',kind:'input',ln:'Grant consent',api:'POST /consent/release-pack',prereqs:['packSourced','@buyer_req'],openOnReach:true,
        done:()=>gateReleased('seller_consent'), body:()=>grid({type:'consentInbound',gate:'seller_consent'})}
    ],
    branches:[
      {id:'invite_recv',label:'Invite from Agent',from:'start',to:'verify',party:'Agent',tab:'agent',
        active:()=>true, resolved:()=>!!state.invited},
      {id:'buyer_req',label:'Buyer requested pack',from:'packSourced',to:'consent',party:'Buyer',tab:'buyer',
        active:()=>flagDone('seller.packSourced'), resolved:()=>reqDone()}
    ]
  },

  { id:'buyer', n:'Role 2', name:'Buyer', icon:'user', avatar:'user',
    desc:'Verifies identity, requests the Property Pack the seller already sourced, and reads it as the shared Property Passport once consent lands — nothing is re-sourced.',
    stats:[{v:'3',l:'graph steps'},{v:'0',l:'re-sourced',ok:true},{v:'6/7',l:'inherited seals',ok:true}],
    nodes:[
      {id:'start',kind:'origin',ln:'Listing found'},
      {id:'bid',kind:'input',ln:'Verify identity',api:'POST /identity/verify',prereqs:[],
        done:()=>!!(state.id&&state.id.buyer),
        body:()=>grid({type:'idform',role:'buyer',title:'Identity details',span:7},{type:'note',span:5,text:'The Buyer verifies identity up front — it also flows to the Buyer Conveyancer. <b>Demo only</b> — entered data goes nowhere.'})},
      {id:'request',kind:'input',ln:'Request pack',api:'GET /consent/request',prereqs:['bid'],
        done:()=>reqDone(), lock:'verify your identity first', body:()=>grid({type:'consentRequest',gate:'seller_consent'})},
      {id:'released',kind:'merge',ln:'Pack released',api:'consume pack',prereqs:['request','@seller_consent'],
        fired:()=>`<span class="chip">${seal('ok','sm')}Pack released — inherits the seller's seals, untouched</span>`,
        pend:"waiting on the seller's consent"},
      {id:'review',kind:'auto',ln:'Read the Passport',api:'GET full passport',prereqs:['released'],
        fired:reviewBody, pend:'available once the pack is released'}
    ],
    branches:[{id:'seller_consent',label:'Seller consent',from:'request',to:'released',party:'Seller',tab:'seller',
      active:()=>reqDone(), resolved:()=>gateReleased('seller_consent')}]
  },

  { id:'sconv', n:'Role 3', name:'Seller Conveyancer', icon:'scale', avatar:'scale',
    desc:"Acts for the seller. Inherits the seller's advanced ID, retrieves surveys on demand, sets the completion date — then waits on the buyer side to action it before completion confirms.",
    stats:[{v:'4',l:'graph steps'},{v:'inherited',l:'pack & ID',ok:true},{v:'1',l:'shared stream',ok:true}],
    nodes:[
      {id:'start',kind:'origin',ln:'Instructed'},
      {id:'advid',kind:'merge',ln:'Advanced ID',sub:'verification',api:'POST /identity/verify · enhanced',prereqs:['@seller_advid'],
        fired:()=>`<span class="chip">${seal('ok','sm')}Seller's advanced ID verified — inherited, signed</span>`,
        pend:'the seller completes an advanced ID check (in the Seller tab)'},
      {id:'surveys',kind:'input',ln:'Retrieve surveys',api:'GET /documents?type=survey',prereqs:['advid'],
        done:()=>!!(state.surv&&state.surv.sconv), lock:"the seller's advanced ID must land first",
        body:()=>grid({type:'surveys',role:'sconv',title:'Surveys — documents service',span:7},{type:'note',span:5,text:'Surveys ride the same documents store as the pack. Acting for the seller, the conveyancer pulls them on demand — each carries its own seal.'})},
      {id:'set',kind:'input',ln:'Set completion',sub:'date',api:'POST /conveyancing-events/completion-set',prereqs:['surveys'],
        done:()=>eventFired('completion_set'), lock:'retrieve the surveys first', body:setBody},
      {id:'completed',kind:'merge',ln:'Completion',sub:'confirmed',api:'completion.actioned',prereqs:['set','@bconv_action'],
        fired:()=>`<span class="chip">${seal('ok','sm')}Completion confirmed — funds moved</span>`,
        pend:'waiting on the Buyer Conveyancer to action completion'}
    ],
    branches:[
      {id:'seller_advid',label:'Seller advanced ID',from:'start',to:'advid',party:'Seller',tab:'seller',
        active:()=>true, resolved:()=>!!state.advid},
      {id:'bconv_action',label:'Buyer Conv. actioned',from:'set',to:'completed',party:'Buyer Conv.',tab:'bconv',
        active:()=>eventFired('completion_set'), resolved:()=>eventFired('completion_actioned')}
    ]
  },

  { id:'bconv', n:'Role 4', name:'Buyer Conveyancer', icon:'scale', avatar:'scale',
    desc:"The compliance lens. Inherits the buyer's ID, traces source-of-funds, runs AML — then actions completion once the seller side sets the date, and the TID auto-registers.",
    stats:[{v:'5',l:'graph steps'},{v:'inherited',l:'buyer ID',ok:true},{v:'AML',l:'screened',ok:true}],
    nodes:[
      {id:'start',kind:'origin',ln:'Instructed'},
      {id:'bid',kind:'merge',ln:'Verify identity',api:'POST /identity/verify',prereqs:['@buyer_id'],
        fired:()=>`<span class="chip">${seal('ok','sm')}Buyer's identity verified — inherited, signed</span>`,
        pend:'the buyer verifies their identity (in the Buyer tab)'},
      {id:'funds',kind:'input',ln:'Source of funds',api:'GET /v1/source-of-funds',prereqs:['bid'],
        done:()=>!!state.sof, lock:"the buyer's ID must land first", body:fundsBody},
      {id:'aml',kind:'auto',ln:'AML screening',api:'GET screening',prereqs:['funds'],
        fired:()=>`<span class="chip">${seal('ok','sm')}AML clear — sanctions / PEP no match</span>`,
        pend:'runs automatically after funds are traced'},
      {id:'action',kind:'input',ln:'Action completion',api:'POST /conveyancing-events/completion-actioned',prereqs:['aml','@sconv_set'],
        done:()=>eventFired('completion_actioned'), lock:'AML must clear first', body:actionBody},
      {id:'tid',kind:'auto',ln:'TID received',api:'tid.received',prereqs:['action'],
        effect:()=>{ state.fired=state.fired||{}; if(!state.fired.tid_received){ state.fired.tid_received={time:nowHM()}; state.lastKey=state.fired.tid_received.time+'|tid.received'; } },
        fired:()=>`<span class="chip">${seal('ok','sm')}Title Information Document registered at HMLR</span>`,
        pend:'returned automatically after completion'}
    ],
    branches:[
      {id:'buyer_id',label:'Buyer verified ID',from:'start',to:'bid',party:'Buyer',tab:'buyer',
        active:()=>true, resolved:()=>!!(state.id&&state.id.buyer)},
      {id:'sconv_set',label:'Seller Conv. set completion',from:'aml',to:'action',party:'Seller Conv.',tab:'sconv',
        active:()=>flagDone('bconv.aml'), resolved:()=>eventFired('completion_set')}
    ]
  }
];

/* ============================================================
   SIGNED PAYLOADS — the raw, signed source responses behind
   each Passport fact (Inspector lens). Per-source JWS objects
   nested under one property-pack envelope.
   `gate` maps a source to the flow state that "pulls" it.
   ============================================================ */
const UPRN_ID = '100091234567';
// Static fallback payloads for the offline demo. Each `claims` object is shaped
// EXACTLY like the live payload the card shows (v3.5 propertyPack fragments for
// the four pack APIs, the real envelope shapes elsewhere), and each `sig` is
// shaped like a real per-source provenance block. Live data replaces these
// wholesale — never merged — see resolvedClaims/resolvedSig in app.js.
const PAYLOADS = {
  uprn: UPRN_ID,
  envelope: {
    endpoint: 'GET /demo-api/pack/' + UPRN_ID,
    schema: 'PDTF v3.5 propertyPack (@pdtf/schemas v3)',
    propertyPackSections: ['energyEfficiency', 'councilTax', 'environmentalIssues', 'titlesToBeSold'],
    packSignature: 'none — the merged pack is not re-signed; each fragment carries per-source provenance',
    provenance: {
      epc:           { alg:'RS256', kid:'a41f7c02-8f3d-4e19-9a55-0b6c2d8e1f30', signedAt:'2026-06-11T09:14:22Z' },
      councilTax:    { alg:'RS256', kid:'c93b0e77-2a41-4d88-b1c6-7f5e9a3d2b18', signedAt:'2026-06-11T09:14:31Z' },
      coalfield:     { alg:'RS256', kid:'5d28f1a9-6c07-4b3e-8e92-1a4b7c6d5e83', signedAt:'2026-06-11T09:15:03Z' },
      titleRegister: { alg:'RS256', kid:'e07a3d51-4b96-42c8-a7f0-9c2e8b1d6a44', signedAt:'2026-06-11T09:16:48Z' }
    }
  },
  sources: [
    { id:'address', name:'Address & UPRN', service:'OPDA OS Places API', endpoint:'GET /v1/places/find?query=…',
      signed:true, gate:'addr',
      sig:{ alg:'RS256', kid:'7b1e4f92-3c58-4a06-bd77-2f9a8c1e5d40', iss:'(OPDA)', signedAt:'2026-06-11T09:09:02Z',
        value:'kQv2mLf0pZ7gWq3rJZ1cQ4rLl9aYk7mY0v1AeD2pak9wq8Lr3rJpZ0kref2bQ7nVxT1mYc9pL0aQ2KdWgxN0pTr8' },
      claims:{ uprn:UPRN_ID, address:'14, ELM GROVE, REDLAND, BRISTOL, BS6 5DB', udprn:'21929808',
        xCoordinate:358205, yCoordinate:174894, localAuthority:'BRISTOL CITY COUNCIL', propertyType:'Terraced' } },

    { id:'uprn_validation', name:'UPRN validation', service:'OPDA UPRN Validator', endpoint:'GET /v1/uprn/validate/'+UPRN_ID,
      signed:true, gate:'uprn',
      sig:{ alg:'RS256', kid:'2c806e13-9f47-4b2a-8d15-6e3a1c9f7b52', iss:'(OPDA)', signedAt:'2026-06-11T09:09:14Z',
        value:'aQk7mY0v1Ae2Tn0pXc8bWq3rJZ0kKdRfH8nZ1cQ4rLl9pak9wq8Lr3rJpZ0kref2bQ7nVxT1mYc9pL0aQ2KdWg2m' },
      claims:{ valid:true } },

    { id:'epc', name:'Energy Performance (EPC)', service:'OPDA EPC API', endpoint:'GET /v1/epc/'+UPRN_ID,
      signed:true, gate:'pack',
      sig:{ alg:'RS256', kid:'a41f7c02-8f3d-4e19-9a55-0b6c2d8e1f30', iss:'(OPDA)', signedAt:'2026-06-11T09:14:22Z',
        value:'H8nZ1cQ4rLl9aQk7mY0v1Ae2Tn0pXc8bWq3rJZ0kKdRfpZ7gWq3rJZ1cQ4rLl9aYk7mY0v1AkQv2mLf0aQ2KdWgxN' },
      claims:{ propertyPack:{ energyEfficiency:{ certificate:{
        certificateNumber:'8206-7942-1030-8846-9002',
        address:'14 Elm Grove, Redland, Bristol, BS6 5DB', address1:'14 Elm Grove',
        postcode:'BS6 5DB', posttown:'Bristol',
        localAuthorityLabel:'Bristol City Council', constituencyLabel:'Bristol West',
        currentEnergyRating:'C', potentialEnergyRating:'B', lodgementDate:'2021-07-20' } } } } },

    { id:'council_tax', name:'Council tax band', service:'OPDA Council Tax API', endpoint:'GET /v1/council-tax/'+UPRN_ID,
      signed:true, gate:'pack',
      sig:{ alg:'RS256', kid:'c93b0e77-2a41-4d88-b1c6-7f5e9a3d2b18', iss:'(OPDA)', signedAt:'2026-06-11T09:14:31Z',
        value:'xN0pTr8kQv2mLf0pZ7gWq3rJZ1cQ4rLl9aYk7mY0v1AH8nZ1cQ4rLl9aQk7mY0v1Ae2Tn0pXc8bWq3rJZ0kKdRfpZ' },
      claims:{ propertyPack:{ councilTax:{ councilTaxBand:'D' } } } },

    { id:'coalfield', name:'Mining / coalfield', service:'OPDA Mining Remediation API', endpoint:'GET /v1/coalfield/'+UPRN_ID,
      signed:true, gate:'pack',
      sig:{ alg:'RS256', kid:'5d28f1a9-6c07-4b3e-8e92-1a4b7c6d5e83', iss:'(OPDA)', signedAt:'2026-06-11T09:15:03Z',
        value:'pZ7gWq3rJZ1cQ4rLl9aYk7mY0v1AxN0pTr8kQv2mLf0H8nZ1cQ4rLl9aQk7mY0v1Ae2Tn0pXc8bWq3rJZ0kKdRfaQ' },
      claims:{ propertyPack:{ environmentalIssues:{ coalMining:{ riskIndicator:'No' } } } } },

    { id:'chain', name:'Property chain', service:'ViewMyChain', endpoint:'POST /api/v1/opda/chains',
      signed:true, gate:'chain',
      sig:{ alg:'RS256', kid:'vmc-opda-2026-1', iss:'ViewMyChain', signedAt:'2026-06-11T09:10:12Z',
        value:'DvMc3pTr8kQv2mLf0pZ7gWq3rJZ1cQ4rLl9aYk7mY0v1ApZ0kref2bQ7nVxT1mYc9pL0aQ2KdWgH8nZ1cQ4rLl9aQ' },
      claims:{ data:[{
        properties:[
          { uprn:'200001858100', address:'2 Hill View, Bristol', position:1 },
          { uprn:UPRN_ID, address:'14 Elm Grove, Redland, Bristol BS6 5DB', position:2 },
          { uprn:'200001858900', address:'8 Orchard Way, Bristol', position:3 }],
        milestones:[{ label:'Offer Accepted', date:'2026-05-12' },{ label:'SSTC', date:'2026-05-20' }] }] } },

    { id:'surveys', name:'Survey documents', service:'OPDA Survey Shack API', endpoint:'GET /v1/documents/'+UPRN_ID,
      signed:true, gate:'surv',
      sig:{ alg:'RS256', kid:'91c5b2e8-0d74-4f3a-a6b9-8e1f5c2d7a03', iss:'(OPDA)', signedAt:'2026-06-11T11:20:51Z',
        value:'BxW9pLqT3mK7nZ0vQ1f9Wq3rJZ0kref2bYc9pL0aQ2KdRg4rLlH8nZ1cQ4rLl9aQk7mY0v1Ae2Tn0pXc8bWq3rJZ0' },
      claims:{ uprn:UPRN_ID, documents:[
        { documentType:'full_buyer_report', filename:'full_buyer_report-01.pdf',
          url:'https://opda-survey-shack.s3.eu-west-2.amazonaws.com/full_buyer_report-01.pdf?X-Amz-Expires=3600',
          expiresAt:'2026-06-11T12:20:51Z' },
        { documentType:'full_homeowner_report', filename:'full_homeowner_report.pdf',
          url:'https://opda-survey-shack.s3.eu-west-2.amazonaws.com/full_homeowner_report.pdf?X-Amz-Expires=3600',
          expiresAt:'2026-06-11T12:20:51Z' } ] } },

    // Detached JWS (x-jws-signature header) — real value populated by the BFF from realData.sellerPack.jwsSignature
    { id:'property_pack', name:'Property pack', service:'Sprift / PDI', endpoint:'POST /appraisal/v1/property-pack/uprn',
      signed:true, gate:'sellerPack',
      sig:{ alg:'RS256', kid:'(from x-jws-signature header)', iss:'Sprift / PDI', signedAt:'(see header)', value:'' },
      claims:{ propertyPack:{
        address:{ line1:'14 Elm Grove', town:'Bristol', postcode:'BS6 5DB' },
        priceInformation:{ price:475000, priceQualifier:'Guide price' },
        councilTax:{ councilTaxBand:'D' } } } },

    { id:'source_of_funds', name:'Source of funds', service:'OPDA Armalytix API', endpoint:'GET /v1/source-of-funds/{clientRequestId}',
      signed:true, gate:'sof',
      sig:{ alg:'RS256', kid:'4e97d0b6-1a83-4c25-9f60-3b7e8d2c5a19', iss:'(OPDA)', signedAt:'2026-06-11T10:02:15Z',
        value:'C7mB1aWqNf0pV2kZ9rL8Wq3rJZ1cQ4rLl9aYk7mY0v1Ae2H8nZ1cQ4rLl9aQk7mY0v1Ae2Tn0pXc8bWq3rJZ0kKdR' },
      claims:{ reportId:'rep_8f2c41', reportType:'SOURCE_OF_FUNDS', issuedAt:'2026-06-11T10:02:15Z',
        status:'AVAILABLE', applicantName:'Robert Malytix',
        proofOfFunds:{ totalBalance:68450.12, formattedTotalBalance:'£68,450.12', currency:'GBP',
          amountRequired:62000, formattedAmountRequired:'£62,000.00',
          surplus:6450.12, formattedSurplus:'£6,450.12', result:'PASS' },
        accounts:[{ bankName:'Monzo', sortCode:'04-00-04', accountNumber:'••••1234', accountName:'R Malytix' }],
        income:{ averageMonthlyTakeHome:3120.55, formattedAverageMonthlyTakeHome:'£3,120.55',
          sources:[{ type:'SALARY', description:'ACME LTD', averageMonthly:3120.55,
            formattedAverageMonthly:'£3,120.55', verified:true }] },
        flags:[] } },

    { id:'title_register', name:'Title register & ownership', service:'OPDA LR Facade', endpoint:'POST /official-copies/v1/register-extract',
      signed:true, gate:'pack',
      sig:{ alg:'RS256', kid:'e07a3d51-4b96-42c8-a7f0-9c2e8b1d6a44', iss:'(OPDA)', signedAt:'2026-06-11T09:16:48Z',
        value:'DpL3xVbN8Qz9rT4uKpW1cWq3rJZ0kref2bYc9pL0aQ2KdRgH8nZ1cQ4rLl9aQk7mY0v1Ae2Tn0pXc8bWq3rJZ0kKd' },
      claims:{ propertyPack:{ titlesToBeSold:[{ registerExtract:{ ocSummaryData:{
        title:{ titleNumber:'EXC10010', classOfTitleCode:'A' },
        registerEntryIndicators:{ leaseHoldTitleIndicator:false },
        propertyAddress:{ postcodeZone:{ postcode:'BS6 5DB' } },
        proprietorship:{ registeredProprietorParty:[{ name:{ forenamesName:'A N', surname:'Seller' } }] },
        pricePaidEntry:{ infills:{ amount:'£150,000' } } } } }] } } }
  ]
};
