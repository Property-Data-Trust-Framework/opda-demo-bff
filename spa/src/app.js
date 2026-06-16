/* ============================================================
   OPDA Property Data Visualiser — DYNAMIC layer
   State · shared dependency-graph engine · branching tracker ·
   stacked node panels · actions · passport · chain · stream.
   Loaded after data.js.
   ============================================================ */

/* ---------- state ---------- */
let state = { role:'agent', view:'flows', flags:{}, fired:{}, gates:{}, id:{}, surv:{},
              addr:null, invited:null, published:null, advid:null, sof:null };
let firing = null;

// Real data fetched from the BFF — preferred by renderers where available
let realData = {};

function resolvedUprn(){
  return (typeof realData!=='undefined' && realData.address?.data?.[0]?.uprn) || '100091225620';
}

async function bffFetch(path, opts) {
  try {
    const res = await fetch(path, opts);
    if (!res.ok) return null;
    return await res.json();
  } catch { return null; }
}

/* ---------- BFF live webhook events ---------- */
let bffEvents = [];

function decodeJwtPayload(token) {
  try {
    const b64 = token.split('.')[1].replace(/-/g,'+').replace(/_/g,'/');
    return JSON.parse(atob(b64));
  } catch { return null; }
}
function fmtTime(iso) {
  try {
    const d = new Date(iso);
    return String(d.getHours()).padStart(2,'0') + ':' + String(d.getMinutes()).padStart(2,'0');
  } catch { return '--:--'; }
}
async function pollBffEvents() {
  try {
    const res = await fetch('/demo-api/events?limit=20');
    if (!res.ok) return;
    const events = await res.json();
    bffEvents = events.map(e => {
      const payload = decodeJwtPayload(e.rawBody);
      const label = payload && payload.eventType ? payload.eventType : 'webhook.received';
      return [fmtTime(e.receivedAt), label, 'Smoove'];
    });
    // refresh the stream panel for conveyancers without a full re-render
    if (state.role === 'sconv' || state.role === 'bconv') renderNodes(state.role);
  } catch {}
}

/* ---------- lookups + tiny helpers ---------- */
function roleObj(id){ return ROLES.find(r=>r.id===id); }
function roleName(id){ const r=roleObj(id); return r?r.name:id; }
function shortRole(id){ return SHORT[id]||roleName(id); }
function nodeById(role,id){ const r=roleObj(role); return r&&r.nodes.find(n=>n.id===id); }
function getBranch(role,bid){ const r=roleObj(role); return r&&(r.branches||[]).find(b=>b.id===bid); }
function flagDone(key){ return !!(state.flags && state.flags[key]); }
function gateStatus(id){ const c=state.gates&&state.gates[id]; return c?c.status:'idle'; }
function gateReleased(id){ return gateStatus(id)==='granted'; }
function eventFired(id){ return !!(state.fired && state.fired[id]); }
function reqDone(){ const c=state.gates&&state.gates.seller_consent; return !!(c && c.reqTime); }
function val(x){ return typeof x==='function'?x():(x==null?'':x); }

/* ---------- graph engine ---------- */
function nodeDone(role,id){
  const nd=nodeById(role,id); if(!nd) return false;
  if(nd.kind==='origin') return true;
  if(nd.kind==='input') return nd.done?nd.done():false;
  return flagDone(role+'.'+id);            // auto | merge
}
function branchState(role,b){ if(!b) return 'upcoming'; if(!b.active()) return 'upcoming'; return b.resolved()?'done':'pending'; }
function prereqMet(role,t){
  if(t[0]==='@'){ const b=getBranch(role,t.slice(1)); return b?branchState(role,b)==='done':false; }
  return nodeDone(role,t);
}
function prereqsMet(role,id){ const nd=nodeById(role,id); return (nd.prereqs||[]).every(t=>prereqMet(role,t)); }
function nonBranchMet(role,id){ const nd=nodeById(role,id); return (nd.prereqs||[]).filter(t=>t[0]!=='@').every(t=>prereqMet(role,t)); }
function gatePending(role,id){
  const nd=nodeById(role,id);
  for(const t of (nd.prereqs||[])){ if(t[0]==='@'){ const b=getBranch(role,t.slice(1)); if(b&&branchState(role,b)==='pending') return b; } }
  return null;
}
function nodeState(role,id){
  const nd=nodeById(role,id);
  if(nd.kind==='origin') return 'origin';
  if(nodeDone(role,id)) return 'done';
  if(firing && firing.role===role && firing.id===id) return 'firing';
  if(nd.kind==='auto'||nd.kind==='merge'){
    if(prereqsMet(role,id)) return 'firing';
    return (nonBranchMet(role,id) && gatePending(role,id)) ? 'waiting' : 'upcoming';
  }
  // input
  if(!nonBranchMet(role,id)) return 'upcoming';
  if(gatePending(role,id)) return 'waiting';
  return 'current';
}

/* auto/merge nodes fire on their own once prerequisites land */
function cascade(){
  if(firing) return;
  for(const r of ROLES){
    for(const nd of r.nodes){
      if((nd.kind==='auto'||nd.kind==='merge') && !nodeDone(r.id,nd.id) && prereqsMet(r.id,nd.id)){
        firing={role:r.id,id:nd.id}; render();
        setTimeout(()=>{
          const f=firing; firing=null;
          state.flags=state.flags||{}; state.flags[f.role+'.'+f.id]={time:nowHM()};
          const node=nodeById(f.role,f.id); if(node.effect) node.effect();
          // Trigger real BFF calls for auto nodes that have API counterparts
          if(f.role==='agent'&&f.id==='uprn'){
            bffFetch(`/demo-api/uprn/${resolvedUprn()}`).then(r=>{ if(r){ realData.uprn=r; renderFlow(); initMap(); } });
          }
          if(f.role==='agent'&&f.id==='pack'){
            bffFetch(`/demo-api/pack/${resolvedUprn()}`).then(r=>{ if(r){ realData.pack=r; renderFlow(); } });
          }
          if(f.role==='seller'&&f.id==='packSourced'){
            bffFetch(`/demo-api/property-pack/${resolvedUprn()}`).then(r=>{ if(r){ realData.sellerPack=r; renderFlow(); } });
          }
          persist(); render(); cascade();
        }, 760);
        return;
      }
    }
  }
}

/* ---------- role cue (rail badge + persona chip) ---------- */
function roleCue(role){
  const r=roleObj(role);
  const moveNode=r.nodes.find(n=>n.kind==='input'&&nodeState(role,n.id)==='current');
  if(moveNode) return {kind:'move', text:moveNode.ln+(moveNode.sub?' '+moveNode.sub:'')};
  // any node waiting on a branch
  for(const n of r.nodes){
    if(n.kind==='origin') continue;
    if(nodeState(role,n.id)==='waiting'){ const b=gatePending(role,n.id); if(b) return {kind:'wait', party:b.party, text:b.label}; }
  }
  const real=r.nodes.filter(n=>n.kind!=='origin');
  if(real.every(n=>nodeDone(role,n.id))) return {kind:'done'};
  return null;
}

/* ============================================================
   RAIL (role tabs)
   ============================================================ */
function renderRail(){
  document.getElementById('roleBar').innerHTML = ROLES.map(r=>{
    const cue=roleCue(r.id);
    let badge='';
    if(cue&&cue.kind==='move') badge=`<span class="railwait act" title="Your move — ${cue.text}">${svg('bolt',2.2)}</span>`;
    else if(cue&&cue.kind==='wait') badge=`<span class="railwait" title="Waiting on ${cue.party} — ${cue.text}">${svg('clock',2.2)}</span>`;
    return `
    <button class="rtab ${r.id===state.role?'active':''}" data-role="${r.id}">
      <span class="rb">${badge}</span>
      <span class="rn">${r.n}</span>
      <span class="rnm">${r.name}</span>
    </button>`;}).join('');
}

/* ============================================================
   BRANCHING TRACKER  (git-graph, OPDA styling)
   ============================================================ */
const T = { W:1000, RAIL:128, BR:54, DIV:92, H:212 };
const nodeX = (i,n)=> (i+0.5)/n*T.W;
function branchGeo(role,b){
  const r=roleObj(role), n=r.nodes.length;
  const fromX=nodeX(r.nodes.findIndex(x=>x.id===b.from),n);
  const toX=nodeX(r.nodes.findIndex(x=>x.id===b.to),n);
  return {fromX,toX,midX:(fromX+toX)/2};
}
function renderTracker(role){
  const r=roleObj(role), n=r.nodes.length, el=document.getElementById('tracker');

  // rail segments
  let segs='';
  for(let i=0;i<n-1;i++){
    const cls=nodeDone(role,r.nodes[i+1].id)?'hseg done':'hseg';
    segs+=`<path class="${cls}" d="M ${nodeX(i,n)} ${T.RAIL} L ${nodeX(i+1,n)} ${T.RAIL}" vector-effect="non-scaling-stroke"/>`;
  }

  // dependency branches (above the line)
  let legs='', deps='';
  (r.branches||[]).forEach(b=>{
    const bs=branchState(role,b), g=branchGeo(role,b);
    const up = bs==='upcoming'?'hleg future':(bs==='done'?'hleg merged':'hleg open');
    const dn = bs==='upcoming'?'hleg future':(bs==='done'?'hleg merged':'hleg pend');
    const off=Math.min(64,(g.midX-g.fromX)*0.7);
    legs+=`<path class="${up}" d="M ${g.fromX} ${T.RAIL} L ${g.fromX+off} ${T.BR} L ${g.midX} ${T.BR}" vector-effect="non-scaling-stroke"/>`;
    legs+=`<path class="${dn}" d="M ${g.midX} ${T.BR} L ${g.toX-off} ${T.BR} L ${g.toX} ${T.RAIL}" vector-effect="non-scaling-stroke"/>`;
    const dl=g.midX/T.W*100;
    const inner = bs==='done'?sealSvg:(bs==='pending'?'?':'');
    const click = bs==='pending'?` data-jump="${b.tab}" title="Resolved in the ${b.party} tab"`:'';
    deps+=`<div class="hdep ${bs}" style="left:${dl}%;top:${T.BR}px;"${click}>${inner}</div>`;
    deps+=`<div class="hdeplab ${bs}" style="left:${dl}%;">${b.label}${bs==='pending'?`<span class="hd-sub">↳ ${b.party} tab</span>`:''}</div>`;
  });

  const divider=`<line class="hdiv" x1="14" y1="${T.DIV}" x2="${T.W-14}" y2="${T.DIV}" vector-effect="non-scaling-stroke"/>`;
  const svgEl=`<svg viewBox="0 0 ${T.W} ${T.H}" preserveAspectRatio="none">${divider}${segs}${legs}</svg>`;

  // rail nodes + labels
  let nodes='';
  r.nodes.forEach((nd,i)=>{
    const st=nodeState(role,nd.id), left=(i+0.5)/n*100;
    let inner;
    if(nd.kind==='origin') inner='';
    else if(st==='done') inner=sealSvg;
    else if(st==='waiting') inner='?';
    else inner=svg(nd.icon||'bolt',2);
    const kc=(nd.kind==='auto'||nd.kind==='merge')?'kauto':'';
    const click=st==='current'?` data-node="${nd.id}"`:'';
    nodes+=`<div class="hnode ${nd.kind==='origin'?'origin':''} ${kc} ${st}" style="left:${left}%;top:${T.RAIL}px;"${click}>${inner}</div>`;
    if(nd.kind==='origin'){
      nodes+=`<div class="hlab" style="left:${left}%;"><div class="ln muted">${nd.ln}</div></div>`;
    } else {
      const tag = nd.kind==='auto'?'<span class="ktag auto">auto</span>'
                : nd.kind==='merge'?'<span class="ktag auto">auto · merge</span>'
                : '<span class="ktag input">input</span>';
      nodes+=`<div class="hlab ${st==='current'?'cur':''}" style="left:${left}%;"><div class="ln">${nd.ln}${nd.sub?`<span class="sub">${nd.sub}</span>`:''}</div>${tag}</div>`;
    }
  });
  const hints = (r.branches&&r.branches.length)
    ? `<div class="hhint up" style="top:${T.DIV-22}px;">↑ another party</div><div class="hhint dn" style="top:${T.DIV+7}px;">↓ ${SHORT[role]}'s own steps</div>`
    : '';
  el.innerHTML = svgEl + nodes + deps + hints;
}

/* ============================================================
   STACKED NODE PANELS (inputs below the tracker)
   ============================================================ */
function lockCard(txt){ return `<div class="card s12 lockpanel"><span class="lk-ico">${svg('lock',2)}</span><span>${txt}</span></div>`; }
function waitCard(b){ return `<div class="card s12 waitpanel"><span class="wp-ico">${svg('clock',2)}</span><span>Waiting on the <b>${b.party}</b> — ${b.label.toLowerCase()} must land first. ${jumpBtn(b.tab,'Go to '+b.party)}</span></div>`; }

function nodePanel(role,nd){
  const st=nodeState(role,nd.id);
  // status badge for the header
  let badge='', bcls='';
  if(st==='done'){ badge='done'; bcls='done'; }
  else if(st==='current'){ badge='your move'; bcls='go'; }
  else if(st==='firing'){ badge='firing…'; bcls='fire'; }
  else if(st==='waiting'){ const b=gatePending(role,nd.id); badge='waiting on '+(b?b.party:'…'); bcls='wait'; }
  else { badge='upcoming'; bcls='up'; }
  const api = nd.api?`<span class="np-api mono">${nd.api}</span>`:'';

  // body
  let body='';
  if(nd.kind==='auto'||nd.kind==='merge'){
    if(st==='done') body=`<div class="autoline ok">${sealSvg}<div>${val(nd.fired)}</div></div>`;
    else if(st==='firing') body=`<div class="autoline fire">${svg('bolt',2)}<span>Prerequisites landed — calling the APIs…</span></div>`;
    else if(st==='waiting'){ const b=gatePending(role,nd.id); body=`<div class="autoline wait">${svg('clock',2)}<span>${val(nd.pend)} ${b?jumpBtn(b.tab,'Go to '+b.party):''}</span></div>`; }
    else body=`<div class="autoline up">${svg('clock',2)}<span>${val(nd.pend)}</span></div>`;
  } else { // input
    if(st==='current'||st==='done') body=val(nd.body);
    else if(st==='waiting'){ if(nd.openOnReach) body=val(nd.body); else body=waitCard(gatePending(role,nd.id)); }
    else body=lockCard(val(nd.lock)||'complete the earlier steps first');
  }
  const num = st==='done'?'✓':(nd.kind==='auto'||nd.kind==='merge'?'∗':'');
  return `<div class="npanel ${bcls}" id="np-${nd.id}">
    <div class="np-head">
      <span class="np-num ${bcls}">${num}</span>
      <span class="np-t">${nd.ln}${nd.sub?` <span class="np-sub">${nd.sub}</span>`:''}</span>
      ${api}
      <span class="np-stat ${bcls}">${badge}</span>
    </div>
    <div class="np-body">${body}</div>
  </div>`;
}

function renderNodes(role){
  const r=roleObj(role);
  document.getElementById('nodes').innerHTML =
    r.nodes.filter(n=>n.kind!=='origin').map(nd=>nodePanel(role,nd)).join('');
  // shared stream for the conveyancers
  const sm=document.getElementById('streamMount');
  if(role==='sconv'||role==='bconv'){
    sm.innerHTML = `<div class="grid" style="margin-top:18px;">${renderBlocks([{type:'stream',title:'Live transaction stream — every signed event',span:12,seal:'ok',provLabel:'JWT verified'}])}</div>`;
  } else sm.innerHTML='';
}

/* ============================================================
   FLOW = persona + tracker + nodes + stream + chain
   ============================================================ */
function renderFlow(){
  const r=roleObj(state.role), cue=roleCue(state.role);
  let chip='';
  if(cue&&cue.kind==='move') chip=`<span class="waitchip act">${svg('bolt',2)} Your move · ${cue.text}</span>`;
  else if(cue&&cue.kind==='wait') chip=`<span class="waitchip">${svg('clock',2)} Waiting on ${cue.party} · ${cue.text}</span>`;
  else if(cue&&cue.kind==='done') chip=`<span class="waitchip ok">${sealSvg} This role's steps are all complete</span>`;
  document.getElementById('persona').innerHTML = `
    <div class="avatar">${svg(r.avatar,1.7)}</div>
    <div class="ptxt">
      <span class="rolenum">${r.n}</span>
      <h1>${r.name}</h1>
      <p>${r.desc}</p>
      ${chip}
    </div>
    <div class="pstats">
      ${r.stats.map(s=>`<div class="s"><div class="v ${s.ok?'ok':''}">${s.v}</div><div class="l">${s.l}</div></div>`).join('')}
    </div>`;
  renderTracker(state.role);
  renderNodes(state.role);
  renderChain();
  updateTopSearch();
}
function render(){ renderRail(); renderFlow(); }

/* ============================================================
   PROPERTY CHAIN (shared, all roles)
   ============================================================ */
function chainStatus(){
  if(eventFired('tid_received')) return ['Completed','ok'];
  if(eventFired('completion_actioned')) return ['Completing',''];
  if(eventFired('completion_set')) return ['Completion set',''];
  if(gateReleased('seller_consent')) return ['In conveyancing',''];
  if(reqDone()) return ['Pack requested',''];
  if(state.published) return ['Listed',''];
  if(state.addr) return ['Onboarding',''];
  return ['Preparing',''];
}
const VMC_MILESTONE_STATUS = {
  'Completion':          ['Completed','ok'],
  'Completion Date Set': ['Completion set',''],
  'Exchange':            ['Exchanged',''],
  'Mortgage Offered':    ['Mortgage offered',''],
  'Mortgage Applied':    ['Mortgage applied',''],
  'Searches Ordered':    ['Searches ordered',''],
  'Cash Buyer':          ['Cash buyer',''],
  'SSTC':                ['SSTC',''],
  'Fall Through':        ['Fallen through','warn'],
};
function vmcStatus(){
  const chain = typeof realData!=='undefined'&&realData.chain?.data?.data?.[0];
  if(!chain) return null;
  const milestones=chain.milestones||[];
  if(!milestones.length) return null;
  const latest=[...milestones].sort((a,b)=>b.date>a.date?1:-1)[0];
  return VMC_MILESTONE_STATUS[latest.label]??[latest.label,''];
}
function chainLinks(){
  const [st,ok]=chainStatus();
  const vmcSt=vmcStatus();
  const chain=typeof realData!=='undefined'&&realData.chain?.data?.data?.[0];
  if(chain&&chain.properties&&chain.properties.length){
    return chain.properties.map(p=>{
      const isOurs=p.uprn===resolvedUprn();
      return {name:p.address||p.displayAddress||'Property',sub:isOurs?'this sale':'',
              stat:isOurs?(vmcSt?vmcSt[0]:st):'',tone:isOurs?(vmcSt?vmcSt[1]:ok):'',ours:isOurs};
    });
  }
  return [
    {name:'First-time buyer',sub:'no chain below',stat:'ready',tone:'ok'},
    {name:'14 Elm Grove',sub:'this sale',stat:vmcSt?vmcSt[0]:st,tone:vmcSt?vmcSt[1]:ok,ours:true},
    {name:'Onward purchase',sub:'seller buying on',stat:'offer accepted',tone:''},
    {name:'Top of chain',sub:'vacant possession',stat:'no onward',tone:'ok'},
  ];
}
function clinkHtml(l,pos){
  if(!l) return '';
  const statCls=l.ours?(l.tone||''):('muted '+(l.tone||'')).trim();
  const stat=l.stat?`<span class="cstat ${statCls}">${l.stat}</span>`:'';
  return `<div class="clink ${l.ours?'ours':''}"><span class="cpos">${pos}</span><b>${l.name}</b><span class="csub">${l.sub}</span>${stat}</div>`;
}
function chainMore(n){ return `<div class="cmore"><span class="cm1">⋯</span><span class="cm2">+${n} more</span></div>`; }
function renderChain(){
  const m=document.getElementById('chainMount'); if(!m) return;
  const arrow=`<div class="carrow">${svg('handoff',1.8)}</div>`;
  const links=chainLinks();
  const live=typeof realData!=='undefined'&&!!realData.chain;
  let inner;
  if(links.length>4){
    const oi=links.findIndex(l=>l.ours);
    const leftHidden=oi-1,rightHidden=links.length-(oi+2);
    const parts=[];
    if(leftHidden>0) parts.push(chainMore(leftHidden));
    if(links[oi-1]) parts.push(clinkHtml(links[oi-1],oi));
    parts.push(clinkHtml(links[oi],oi+1));
    if(links[oi+1]) parts.push(clinkHtml(links[oi+1],oi+2));
    if(rightHidden>0) parts.push(chainMore(rightHidden));
    inner=parts.join(arrow);
  } else {
    inner=links.map((l,i)=>clinkHtml(l,i+1)).join(arrow);
  }
  const tag=live
    ?`<span class="partnertag live">${svg('check',2)} ViewMyChain · live</span>`
    :`<span class="partnertag">${svg('info',2)} ViewMyChain · integration partner</span>`;
  m.innerHTML=`
  <div class="card chaincard s12">
    <div class="chead"><span class="ct">Property chain — visible to every role</span>${tag}</div>
    <div class="chain">${inner}</div>
    <div class="note" style="margin-top:14px;"><span class="ni">${svg('info')}</span><div>No OPDA API — chain position &amp; status come from the <b>ViewMyChain integration partner</b>, populated alongside the agent's material information. Every role sees the same chain; <b>our sale</b> is matched by UPRN and its status tracks the live transaction.</div></div>
  </div>`;
}
function updateTopSearch(){
  const s=document.getElementById('topSearch'); if(!s) return;
  if(state.addr){
    s.classList.remove('empty');
    const addrLine = state.addr.address || '';
    const addrParts = addrLine ? addrLine.split(',') : [];
    document.getElementById('topAddr').textContent = addrParts.length ? addrParts[0].trim() : 'No property resolved';
    document.getElementById('topSub').textContent = addrParts.length > 1 ? '· ' + addrParts.slice(1).join(',').trim() : '';
    const uprnNum = document.getElementById('topUprnNum');
    if(uprnNum) uprnNum.textContent = state.addr.uprn || '—';
    document.getElementById('topUprn').style.display = state.addr.uprn ? '' : 'none';
  } else {
    s.classList.add('empty');
    document.getElementById('topAddr').textContent='No property resolved';
    document.getElementById('topSub').textContent='· search in the Estate Agent flow';
    document.getElementById('topUprn').style.display='none';
  }
}

/* ============================================================
   SHARED TRANSACTION STREAM
   ============================================================ */
function buildStream(){
  const L=[];
  if(state.addr){ L.push([state.addr.time,'places.address.resolved','Agent']); }
  if(flagDone('agent.uprn')) L.push([state.flags['agent.uprn'].time,'uprn.validated','Agent']);
  if(state.invited) L.push([state.invited.time,'identity.invite.sent','Agent']);
  if(state.id.seller) L.push([state.id.seller.time,'seller.identity.verified','Seller']);
  if(flagDone('agent.pack')) L.push([state.flags['agent.pack'].time,'listing.info.sourced','Agent']);
  if(flagDone('seller.packSourced')) L.push([state.flags['seller.packSourced'].time,'property.pack.sourced','Seller']);
  if(state.advid) L.push([state.advid.time,'seller.identity.advanced','Seller']);
  if(flagDone('agent.ready')) L.push([state.flags['agent.ready'].time,'listing.details.ready','Agent']);
  if(state.published) L.push([state.published.time,'listing.published','Agent']);
  if(state.id.buyer) L.push([state.id.buyer.time,'buyer.identity.verified','Buyer']);
  const cs=state.gates&&state.gates.seller_consent;
  if(cs){
    if(cs.reqTime) L.push([cs.reqTime,'consent.requested','Buyer']);
    if(cs.status==='granted') L.push([cs.decTime,'seller.consent.granted','Seller']);
    if(cs.status==='denied') L.push([cs.decTime,'seller.consent.denied','Seller']);
  }
  if(state.surv.sconv) L.push([state.surv.sconv.time,'documents.surveys.retrieved','Seller Conv.']);
  if(state.sof) L.push([state.sof.time,'funds.traced','Buyer Conv.']);
  if(flagDone('bconv.aml')) L.push([state.flags['bconv.aml'].time,'aml.cleared','Buyer Conv.']);
  if(state.surv.bconv) L.push([state.surv.bconv.time,'documents.surveys.retrieved','Buyer Conv.']);
  if(eventFired('completion_set')) L.push([state.fired.completion_set.time,'completion.date.set','Seller Conv.']);
  if(eventFired('completion_actioned')) L.push([state.fired.completion_actioned.time,'completion.actioned','Buyer Conv.']);
  if(eventFired('tid_received')) L.push([state.fired.tid_received.time,'tid.received','Buyer Conv.']);
  // merge live Smoove webhook events received by the BFF
  for(const ev of bffEvents) L.push(ev);
  return L.sort((a,b)=> a[0]<b[0]?-1:a[0]>b[0]?1:0);
}

/* ============================================================
   PASSPORT VIEW (shared layer)
   ============================================================ */
/* wrap a passport card with a "{ } JSON" inspect chip that deep-links to its payload */
function pgrid(specs){
  return specs.map(s=>{
    const inner=renderBlocks([s]);
    if(!s.payloadId) return inner;
    return `<div class="plwrap" style="grid-column:span ${s.span||6};">${inner}<button class="plchip" data-inspect="${s.payloadId}" title="View the signed JSON behind this fact">${svg('braces',2)} JSON</button></div>`;
  }).join('');
}
function renderPassport(){
  const cards = [
    {type:'kpis',title:'Council tax',span:3,payloadId:'council_tax',items:[{label:'Band',value:'D',seal:'warn'}]},
    {type:'epc',title:'Energy — EPC',span:3,payloadId:'epc',band:'C',value:'C · 72',potential:'B',seal:'ok',provLabel:'signed'},
    {type:'kpis',title:'Mining / coalfield',span:3,payloadId:'coalfield',items:[{label:'Status',value:'OFF',seal:'ok',sub:'low risk'}]},
    {type:'kpis',title:'Source of funds',span:3,payloadId:'source_of_funds',items:[{label:'Status',value:'Verified',small:true,seal:'ok',sub:'deposit traced'}]}
  ];
  const wide = [
    {type:'kpis',title:'Title register &amp; ownership',span:8,cols:3,seal:'ok',provLabel:'signed HMLR',payloadId:'title_register',
      items:[{label:'Tenure',value:'Freehold',small:true},{label:'Title number',value:'ABC12345',small:true},{label:'Price paid',value:'£xxx,xxx',small:true}]},
    {type:'map',title:'Location',span:4,payloadId:'address'}
  ];
  const docs = [{type:'docs',title:'Survey documents',span:6,seal:'ok',provLabel:'signed S3',payloadId:'surveys',
    items:[{name:'RICS Level 2 survey.pdf',meta:'2.4 MB · pre-signed S3',seal:'ok'},{name:'Floor plan.pdf',meta:'480 KB · pre-signed S3',seal:'ok'}]},
    {type:'status',tone:'ok',title:'Identity &amp; AML',span:6,seal:'ok',provLabel:'signed',
      lines:['Buyer identity verified','Source of funds traced','AML screening clear']}];

  document.getElementById('passportView').innerHTML = `
    <div class="persona" style="margin-bottom:22px;">
      <div class="avatar">${svg('home',1.7)}</div>
      <div class="ptxt">
        <span class="rolenum">Shared layer</span>
        <h1>Property Passport</h1>
        <p>The single property truth every role reads from. Each source wears a provenance seal driven by its signature block — open <b>{ } JSON</b> on any card to inspect the signed payload behind it.</p>
      </div>
      <div class="pstats"><div class="s"><div class="v ok">6 / 7</div><div class="l">sources signed</div></div><div class="s"><div class="v">8</div><div class="l">APIs</div></div></div>
    </div>
    <div class="sectlabel">Property facts</div>
    <div class="grid" style="margin-bottom:18px;">${pgrid(cards)}</div>
    <div class="grid" style="margin-bottom:18px;">${pgrid(wide)}</div>
    <div class="grid">${pgrid(docs)}</div>`;
}

/* ============================================================
   SIGNED PAYLOADS VIEW (Inspector lens, shared layer)
   ============================================================ */
function payloadRetrieved(gate){
  switch(gate){
    case 'addr':       return !!state.addr;
    case 'pack':       return flagDone('agent.pack');
    case 'sof':        return !!state.sof;
    case 'surv':       return !!(state.surv && (state.surv.sconv||state.surv.bconv));
    case 'sellerPack': return flagDone('seller.packSourced');
    case 'chain':      return !!(typeof realData!=='undefined'&&realData.chain);
    default:           return !!state.addr;
  }
}
function gateHint(gate){
  if(gate==='sof')        return 'bconv';
  if(gate==='surv')       return 'sconv';
  if(gate==='sellerPack') return 'seller';
  if(gate==='chain')      return 'agent';
  return 'agent';
}
// Return real sig data from BFF realData when available, else fall back to static model.
function resolvedSig(s){
  if(s.id==='chain'){
    const p=realData.chain?.provenance;
    if(p) return { alg:p.alg, kid:p.kid, iss:'ViewMyChain', signedAt:p.signedAt, value:p.signature };
  }
  if(s.id==='property_pack'){
    const jws=realData.sellerPack?.jwsSignature;
    if(jws) return { alg:'ES256', kid:'(x-jws-signature)', iss:'Sprift / PDI', signedAt:'(see header)', value:jws };
  }
  // For our OPDA API sources, pull provenance from realData.pack when available.
  const packMap={epc:'epc',council_tax:'councilTax',coalfield:'coalfield',title_register:'titleRegister'};
  const key=packMap[s.id];
  if(key){
    const p=realData.pack?.[key]?.provenance;
    if(p) return { alg:p.alg, kid:p.kid, iss:'(OPDA)', signedAt:p.signedAt, value:p.signature };
  }
  return s.sig;
}
function jsonHighlight(obj){
  const json = JSON.stringify(obj, null, 2)
    .replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
  return json.replace(/("(\\u[a-zA-Z0-9]{4}|\\[^u]|[^\\"])*"(\s*:)?|\b(true|false|null)\b|-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)/g, m=>{
    let cls='jn';
    if(/^"/.test(m)) cls = /:$/.test(m.trim()) ? 'jk' : 'js';
    else if(/true|false/.test(m)) cls='jb';
    else if(/null/.test(m)) cls='jnull';
    return '<span class="'+cls+'">'+m+'</span>';
  });
}
function payloadCard(s){
  const got = payloadRetrieved(s.gate);
  const sealEl = !got ? `<span class="plseal pend">${svg('clock',2)} not retrieved</span>`
    : s.signed ? `<span class="plseal ok">${svg('check',2.4)} signed</span>`
    : `<span class="plseal warn">unsigned</span>`;
  let body;
  if(!got){
    const tab=gateHint(s.gate);
    body=`<div class="plpend">Not yet retrieved — pulled in via the ${roleObj(tab).name} flow. <button class="jumplink" data-jump="${tab}">Go there →</button></div>`;
  } else {
    const sig = resolvedSig(s);
    const isReal = sig !== s.sig;
    const sigBlock = s.signed
      ? `${isReal?'<div style="font-family:var(--mono);font-size:9px;color:var(--ok-2);margin-bottom:6px;text-transform:uppercase;letter-spacing:.5px;">✓ live signature from BFF</div>':''}
         <div class="plsigrow"><span>alg</span><b>${sig.alg}</b></div>
         <div class="plsigrow"><span>kid</span><b>${sig.kid}</b></div>
         <div class="plsigrow"><span>iss</span><b>${sig.iss}</b></div>
         <div class="plsigrow"><span>signed</span><b>${sig.signedAt}</b></div>
         <div class="plsigval mono">${sig.value||'(pending BFF response)'}</div>`
      : `<div class="plnosig">${s.sig.note}</div>`;
    body=`<pre class="pljson"><code>${jsonHighlight(s.claims)}</code></pre>
      <button class="plsigtoggle ${s.signed?'':'warn'}" data-sigtoggle="${s.id}">${svg('shield',2)} ${s.signed?'JWS signature':'signature status'}<span class="plcaret">▸</span></button>
      <div class="plsig" id="sig-${s.id}" hidden>${sigBlock}</div>`;
  }
  return `<div class="card plcard s6 ${got?'':'pl-pend'}" id="pl-${s.id}">
    <div class="plhead"><div class="plt"><span class="plname">${s.name}</span><span class="plmeta mono">${s.service} · ${s.endpoint}</span></div>${sealEl}</div>
    ${body}</div>`;
}
function renderPayloads(){
  const host=document.getElementById('payloadsView');
  if(!state.addr){
    host.innerHTML=`<div class="plempty">${svg('braces',1.6)}<div><h2>No payloads yet</h2><p>Resolve a property in the Estate Agent flow — its signed source payloads appear here as each one is pulled back.</p><button class="btn amber" data-jump="agent">${svg('arrow')} Go to the Estate Agent</button></div></div>`;
    return;
  }
  const got = PAYLOADS.sources.filter(s=>payloadRetrieved(s.gate));
  const signedCount = got.filter(s=>s.signed).length;
  const env = Object.assign({}, PAYLOADS.envelope, { sourcesRetrieved: got.length, sourcesSigned: signedCount });
  host.innerHTML = `
    <div class="persona" style="margin-bottom:22px;">
      <div class="avatar">${svg('braces',1.7)}</div>
      <div class="ptxt">
        <span class="rolenum">Inspector lens</span>
        <h1>Signed payloads</h1>
        <p>The raw, signed source responses behind every Passport fact. Each is a self-contained JWS — verify the signature, read the claims. Open <b>{ } JSON</b> on a Passport card to land on its source.</p>
      </div>
      <div class="pstats">
        <div class="s"><div class="v">${got.length} / ${PAYLOADS.sources.length}</div><div class="l">retrieved</div></div>
        <div class="s"><div class="v ok">${signedCount}</div><div class="l">signed</div></div>
        <div class="s"><div class="v">ES256</div><div class="l">algorithm</div></div>
      </div>
    </div>
    <div class="sectlabel">Property-pack envelope</div>
    <div class="grid" style="margin-bottom:18px;">
      <div class="card plcard plenvelope s12" id="pl-envelope">
        <div class="plhead"><div class="plt"><span class="plname">Property pack</span><span class="plmeta mono">${env.pack}</span></div><span class="plseal ok">${svg('check',2.4)} pack verified</span></div>
        <pre class="pljson"><code>${jsonHighlight(env)}</code></pre>
      </div>
    </div>
    <div class="sectlabel">Source payloads</div>
    <div class="grid">${PAYLOADS.sources.map(payloadCard).join('')}</div>`;
}
function inspectPayload(id){
  setView('payloads');
  const sc=document.querySelector('main.main'), el=document.getElementById('pl-'+id);
  if(el&&sc){
    const r=el.getBoundingClientRect(), sr=sc.getBoundingClientRect();
    sc.scrollTop += (r.top - sr.top) - 90;
    el.classList.add('plflash'); setTimeout(()=>el.classList.remove('plflash'),1500);
  }
}
function bngToLatLng(E,N){
  const a=6377563.396,b=6356256.909,F0=0.9996012717;
  const lat0=0.85521133347,lng0=-0.034906585,N0=-100000,E0=400000;
  const e2=1-(b*b)/(a*a),n=(a-b)/(a+b),n2=n*n,n3=n*n*n;
  let lat=lat0,M=0;
  do{lat=(N-N0-M)/(a*F0)+lat;M=b*F0*((1+n+5/4*n2+5/4*n3)*(lat-lat0)-(3*n+3*n2+21/8*n3)*Math.sin(lat-lat0)*Math.cos(lat+lat0)+(15/8*n2+15/8*n3)*Math.sin(2*(lat-lat0))*Math.cos(2*(lat+lat0))-35/24*n3*Math.sin(3*(lat-lat0))*Math.cos(3*(lat+lat0)));}while(Math.abs(N-N0-M)>=0.00001);
  const sl=Math.sin(lat),cl=Math.cos(lat),tl=Math.tan(lat);
  const nu=a*F0/Math.sqrt(1-e2*sl*sl),rho=a*F0*(1-e2)/Math.pow(1-e2*sl*sl,1.5),eta2=nu/rho-1;
  const dE=E-E0,dE3=dE*dE*dE,dE5=dE3*dE*dE,dE7=dE5*dE*dE;
  const VII=tl/(2*rho*nu),VIII=tl/(24*rho*nu*nu*nu)*(5+3*tl*tl+eta2-9*tl*tl*eta2);
  const IX=tl/(720*rho*Math.pow(nu,5))*(61+90*tl*tl+45*Math.pow(tl,4));
  const X=1/(cl*nu),XI=1/(cl*6*nu*nu*nu)*(nu/rho+2*tl*tl);
  const XII=1/(cl*120*Math.pow(nu,5))*(5+28*tl*tl+24*Math.pow(tl,4));
  const XIIA=1/(cl*5040*Math.pow(nu,7))*(61+662*tl*tl+1320*Math.pow(tl,4)+720*Math.pow(tl,6));
  return[(lat-VII*dE*dE+VIII*Math.pow(dE,4)-IX*Math.pow(dE,6))*180/Math.PI,(lng0+X*dE-XI*dE3+XII*dE5-XIIA*dE7)*180/Math.PI];
}
let _map = null;
function initMap(){
  const el = document.getElementById('leaflet-map');
  if(!el || !window.L) return;
  let lat=51.4712, lng=-2.6003;
  const addr=typeof realData!=='undefined'&&realData.address?.data?.[0];
  if(addr?.xCoordinate&&addr?.yCoordinate){
    try{[lat,lng]=bngToLatLng(addr.xCoordinate,addr.yCoordinate);}catch(e){}
  }
  if(_map){ _map.remove(); _map = null; }
  _map = L.map(el, { zoomControl:true, attributionControl:true });
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution:'© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
    maxZoom:19
  }).addTo(_map);
  delete L.Icon.Default.prototype._getIconUrl;
  L.Icon.Default.mergeOptions({
    iconRetinaUrl:'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
    iconUrl:'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
    shadowUrl:'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
  });
  L.marker([lat, lng]).addTo(_map);
  _map.setView([lat, lng], 16);
}

function setView(v){
  state.view=v;
  document.querySelectorAll('#viewseg button').forEach(b=>b.classList.toggle('on',b.dataset.view===v));
  document.getElementById('flowsView')?.classList.toggle('active',v==='flows');
  document.getElementById('passportView')?.classList.toggle('active',v==='passport');
  document.getElementById('payloadsView')?.classList.toggle('active',v==='payloads');
  if(v==='passport'){ renderPassport(); initMap(); }
  if(v==='payloads') renderPayloads();
  persist();
}

/* ============================================================
   ACTIONS  (each ends with sync → render + persist + cascade)
   ============================================================ */
function sync(){ render(); persist(); cascade(); }
function setRole(id){ state.role=id; render(); persist(); }
function mark(key){ state.lastKey=key; }
function resetAll(){
  const role=state.role||'agent', view=state.view||'flows';
  state={ role, view, flags:{}, fired:{}, gates:{}, id:{}, surv:{},
          addr:null, invited:null, published:null, advid:null, sof:null, lastKey:null };
  firing=null; realData={}; render(); setView(view); persist(); cascade();
}

function searchAddress(){
  if(state.addr) return;
  const q = document.getElementById('addrInput')?.value?.trim() || '14 Elm Grove, Bristol BS6 5DB';
  state.addr={time:nowHM()}; mark(state.addr.time+'|places.address.resolved'); sync();
  bffFetch('/demo-api/address?q='+encodeURIComponent(q))
    .then(r=>{
      if(r && r.data && r.data.length){
        if(r.data.length === 1){
          selectAddress(r.data[0]);
        } else {
          realData.addressResults = r.data;
          renderFlow();
        }
      }
    });
}
function selectAddress(item){
  realData.address = { data: [item] };
  realData.addressResults = null;
  state.addr = Object.assign(state.addr || {time:nowHM()}, {
    uprn: item.uprn,
    address: item.address
  });
  sync();
  bffFetch(`/demo-api/chain/${item.uprn || '100091225620'}`)
    .then(cr=>{ if(cr){ realData.chain=cr; renderChain(); } });
}
function resetSearch(){ state.addr=null; realData.address=null; realData.addressResults=null; sync(); }
function inviteSeller(){ if(state.invited) return; state.invited={time:nowHM()}; mark(state.invited.time+'|identity.invite.sent'); sync(); }
function resetInvite(){ state.invited=null; sync(); }
function publishListing(){ if(state.published) return; state.published={time:nowHM()}; mark(state.published.time+'|listing.published'); sync(); }
function resetPublish(){ state.published=null; sync(); }
function submitId(role){ state.id[role]={time:nowHM()}; mark(state.id[role].time+'|'+role+'.identity.verified'); sync(); }
function editId(role){ state.id[role]=null; sync(); }
function submitAdvId(){ state.advid={time:nowHM()}; mark(state.advid.time+'|seller.identity.advanced'); sync(); }
function resetAdvId(){ state.advid=null; sync(); }
function traceFunds(){
  state.sof={time:nowHM()}; mark(state.sof.time+'|funds.traced'); sync();
  bffFetch('/demo-api/source-of-funds', {method:'POST'})
    .then(r=>{ if(r){ realData.sof=r; renderFlow(); } });
}
function resetFunds(){ state.sof=null; sync(); }
function retrieveSurveys(role){
  state.surv[role]={time:nowHM()}; mark(state.surv[role].time+'|documents.surveys.retrieved'); sync();
  bffFetch(`/demo-api/surveys/${resolvedUprn()}`)
    .then(r=>{ if(r){ realData.surveys=r; renderFlow(); } });
}
function requestPack(gate){ const c=state.gates[gate]||{}; if(c.status==='granted') return; state.gates[gate]={status:'requested',reqTime:nowHM(),decTime:null}; mark(state.gates[gate].reqTime+'|consent.requested'); sync(); }
function decideConsent(gate,ok){ const c=state.gates[gate]||{}; state.gates[gate]={status:ok?'granted':'denied',reqTime:c.reqTime,decTime:nowHM(),by:state.role}; mark(state.gates[gate].decTime+'|seller.consent.'+(ok?'granted':'denied')); sync(); }
function revokeConsent(gate){ const c=state.gates&&state.gates[gate]; if(!c) return; state.gates[gate]={status:'requested',reqTime:c.reqTime||nowHM(),decTime:null}; mark(null); sync(); }
function withdrawReq(gate){ if(state.gates) delete state.gates[gate]; mark(null); sync(); }
function fireEvent(id){ if(eventFired(id)) return; state.fired[id]={time:nowHM()}; mark(state.fired[id].time+'|'+TRIGGER_EVENTS.find(e=>e.id===id).event); sync(); }
function resetEvent(id){ if(state.fired) delete state.fired[id];
  // tid is auto-derived from the bconv.tid flag; clear it too so it re-fires correctly
  if(id==='completion_actioned'){ delete state.fired.tid_received; if(state.flags) delete state.flags['bconv.tid']; }
  sync(); }

function persist(){ try{ const c=Object.assign({},state); localStorage.setItem('opda-state',JSON.stringify(c)); }catch(e){} }

/* ============================================================
   EVENTS
   ============================================================ */
document.getElementById('roleBar').addEventListener('click',e=>{ const b=e.target.closest('.rtab'); if(b&&b.dataset.role) setRole(b.dataset.role); });
document.getElementById('viewseg').addEventListener('click',e=>{ const b=e.target.closest('button'); if(b&&b.dataset.view) setView(b.dataset.view); });
document.body.addEventListener('click',e=>{
  if(e.target.closest('[data-resetall]')){ resetAll(); return; }
  const insp=e.target.closest('[data-inspect]'); if(insp){ inspectPayload(insp.dataset.inspect); return; }
  const sgt=e.target.closest('[data-sigtoggle]'); if(sgt){ const el=document.getElementById('sig-'+sgt.dataset.sigtoggle); if(el){ el.hidden=!el.hidden; sgt.classList.toggle('open',!el.hidden); } return; }
  const jl=e.target.closest('.jumplink[data-jump]'); if(jl){ setRole(jl.dataset.jump); window.scrollTo({top:0,behavior:'smooth'}); return; }
  const jump=e.target.closest('[data-jump]'); if(jump){ setRole(jump.dataset.jump); window.scrollTo({top:0,behavior:'smooth'}); return; }
  const node=e.target.closest('[data-node]'); if(node){ const p=document.getElementById('np-'+node.dataset.node); if(p) p.scrollIntoView({behavior:'smooth',block:'center'}); return; }
  if(e.target.closest('[data-search]')){
    const sugg=e.target.closest('.sugg');
    if(sugg){ const inp=document.getElementById('addrInput'); if(inp) inp.value=sugg.textContent.trim(); }
    searchAddress(); return;
  }
  const pick=e.target.closest('[data-addrpick]');
  if(pick){ const item=(typeof realData!=='undefined'&&realData.addressResults)?.[parseInt(pick.dataset.addrpick)]; if(item) selectAddress(item); return; }
  if(e.target.closest('[data-searchreset]')){ resetSearch(); return; }
  if(e.target.closest('[data-invite]')){ inviteSeller(); return; }
  if(e.target.closest('[data-invitereset]')){ resetInvite(); return; }
  if(e.target.closest('[data-publish]')){ publishListing(); return; }
  if(e.target.closest('[data-publishreset]')){ resetPublish(); return; }
  if(e.target.closest('[data-advid]')){ submitAdvId(); return; }
  if(e.target.closest('[data-advidreset]')){ resetAdvId(); return; }
  if(e.target.closest('[data-funds]')){ traceFunds(); return; }
  if(e.target.closest('[data-fundsreset]')){ resetFunds(); return; }
  const fb=e.target.closest('[data-fire]'); if(fb){ fireEvent(fb.dataset.fire); return; }
  const er=e.target.closest('[data-eventreset]'); if(er){ resetEvent(er.dataset.eventreset); return; }
  const req=e.target.closest('[data-request]'); if(req){ requestPack(req.dataset.request); return; }
  const dec=e.target.closest('[data-decide]'); if(dec){ decideConsent(dec.dataset.gate, dec.dataset.decide==='yes'); return; }
  const rv=e.target.closest('[data-revoke]'); if(rv){ revokeConsent(rv.dataset.revoke); return; }
  const wd=e.target.closest('[data-withdraw]'); if(wd){ withdrawReq(wd.dataset.withdraw); return; }
  const ids=e.target.closest('[data-idsubmit]'); if(ids){ submitId(ids.dataset.idsubmit); return; }
  const ide=e.target.closest('[data-idedit]'); if(ide){ editId(ide.dataset.idedit); return; }
  const sgv=e.target.closest('[data-survget]'); if(sgv){ retrieveSurveys(sgv.dataset.survget); return; }
});

/* ============================================================
   INIT
   ============================================================ */
try{ const s=JSON.parse(localStorage.getItem('opda-state')); if(s&&s.role&&roleObj(s.role)){ state=Object.assign(state,s); } }catch(e){}
state.flags=state.flags||{}; state.id=state.id||{}; state.surv=state.surv||{}; state.gates=state.gates||{}; state.fired=state.fired||{};
renderRail();
renderFlow();
setView(state.view||'flows');
cascade();
pollBffEvents();
setInterval(pollBffEvents, 15000);
