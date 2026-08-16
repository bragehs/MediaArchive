// Constellation — force-directed genre graph for the Library. Ported from the
// design draft; fed by .NET (LibraryQueries.GetLibraryAsync), item taps route to
// Blazor navigation and search calls the server. Runs inside the page, not the
// whole window, so the app bar and tab bar stay put.
window.constellation = (function () {
  const PAL = { Book: ['#a9812f', '#7a5320'], Game: ['#4a6d63', '#2b4746'], Movie: ['#3b4a6b', '#26304a'], Show: ['#8c3f33', '#5e2620'] };
  const TL = { Book: 'Book', Game: 'Game', Movie: 'Film', Show: 'Show' };
  const HUES = [[200, 84, 150], [92, 196, 224], [214, 167, 74], [139, 190, 90], [236, 140, 88],
    [232, 140, 180], [150, 180, 210], [120, 200, 140], [176, 150, 110], [220, 96, 96], [236, 206, 92],
    [169, 198, 142], [180, 140, 220], [110, 200, 200], [210, 120, 140], [160, 200, 110]];

  const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
  const rgb = a => `rgb(${a[0] | 0},${a[1] | 0},${a[2] | 0})`;
  const rgba = (a, o) => `rgba(${a[0] | 0},${a[1] | 0},${a[2] | 0},${o})`;

  let items = [], dotNet = null;
  let nodes = [], links = [], adj = new Map();
  let genreNode = {}, hueOf = {}, genreItems = {};
  let el = {}, VW = 0, VH = 0, DPR = 1, cam = { x: 0, y: 0, z: 0.66 };
  let alpha = 1, dragging = null, selected = null, hiSet = null, searchHits = null;
  let raf = 0, wiredWindow = false, searchTimer = 0;

  const genresOf = it => [...new Set(it.g || [])];

  function makeRng(seed) {
    return function () {
      seed |= 0; seed = seed + 0x6D2B79F5 | 0;
      let t = Math.imul(seed ^ seed >>> 15, 1 | seed);
      t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
      return ((t ^ t >>> 14) >>> 0) / 4294967296;
    };
  }

  function build() {
    nodes = []; links = []; adj = new Map(); genreNode = {}; hueOf = {}; genreItems = {};
    const rnd = makeRng(1337);
    items.forEach((it, idx) => genresOf(it).forEach(g => (genreItems[g] = genreItems[g] || []).push(idx)));
    const mk = (id, o) => { const n = { id, vx: 0, vy: 0, dx: 0, dy: 0, phase: nodes.length * 1.7, ...o }; nodes.push(n); adj.set(id, new Set()); return n; };
    const link = (a, b, rel) => { links.push({ s: a, t: b, rel }); adj.get(a.id).add(b.id); adj.get(b.id).add(a.id); };

    const gkeys = Object.keys(genreItems).sort((a, b) => genreItems[b].length - genreItems[a].length);
    gkeys.forEach((g, i) => {
      hueOf[g] = HUES[i % HUES.length];
      const ang = i / gkeys.length * 6.2832;
      genreNode[g] = mk('g:' + g, {
        kind: 'genre', name: g, col: hueOf[g], count: genreItems[g].length,
        r: 18 + Math.sqrt(genreItems[g].length) * 11, x: Math.cos(ang) * 380, y: Math.sin(ang) * 380
      });
    });

    // One node per (item, genre). Copies of a shared work are NOT linked to each
    // other — that kept the graph readable rather than a hairball.
    const copies = {};
    items.forEach((it, idx) => {
      const gs = genresOf(it); if (!gs.length) return;
      gs.forEach(g => {
        const gn = genreNode[g], list = genreItems[g], pos = list.indexOf(idx);
        const ang = pos / list.length * 6.2832 + rnd() * 0.4;
        const rad = gn.r + 82;
        const n = mk('i:' + idx + '@' + g, {
          kind: 'item', name: it.t, item: it, idx, genre: g, col: hueOf[g], r: 30,
          x: gn.x + Math.cos(ang) * rad, y: gn.y + Math.sin(ang) * rad
        });
        link(n, gn, 'orbit');
        (copies[idx] = copies[idx] || []).push(n);
      });
      if (it.img) { const im = new Image(); im.onload = () => { (copies[idx] || []).forEach(n => n.im = im); }; im.src = it.img; }
    });
  }

  function descItems(node) { return node.kind === 'item' ? [node.idx] : genreItems[node.name].slice(); }
  function connectedIds(node) {
    const set = new Set([node.id]);
    if (node.kind === 'item') {
      nodes.forEach(n => { if (n.kind === 'item' && n.idx === node.idx) { set.add(n.id); adj.get(n.id).forEach(x => set.add(x)); } });
      return set;
    }
    adj.get(node.id).forEach(instId => {
      set.add(instId);
      adj.get(instId).forEach(x => { set.add(x); if (x[0] === 'i') adj.get(x).forEach(y => set.add(y)); });
    });
    return set;
  }

  function step() {
    if (alpha < 0.004) return;
    alpha *= 0.987;
    const n = nodes.length;
    for (let i = 0; i < n; i++) {
      const a = nodes[i];
      for (let j = i + 1; j < n; j++) {
        const b = nodes[j];
        let dx = a.x - b.x, dy = a.y - b.y, d2 = dx * dx + dy * dy || 0.01;
        const bothG = (a.kind === 'genre' && b.kind === 'genre');
        const rep = bothG ? 42000 : (a.kind === 'item' && b.kind === 'item' ? 7000 : 5600);
        const minD = a.r + b.r + 22;
        let f = rep / d2;
        if (d2 < minD * minD) f += (minD * minD - d2) / d2 * 1.1;
        const d = Math.sqrt(d2); dx /= d; dy /= d;
        a.vx += dx * f * alpha; a.vy += dy * f * alpha;
        b.vx -= dx * f * alpha; b.vy -= dy * f * alpha;
      }
    }
    for (const l of links) {
      let dx = l.t.x - l.s.x, dy = l.t.y - l.s.y, d = Math.hypot(dx, dy) || .01;
      if (l.rel === 'orbit') {
        const rest = l.t.r + 82, f = (d - rest) * 0.05 * alpha; dx /= d; dy /= d;
        l.s.vx += dx * f; l.s.vy += dy * f;
        l.t.vx -= dx * f * 0.02; l.t.vy -= dy * f * 0.02;
      } else {
        const f = (d - 165) * 0.0022 * alpha; dx /= d; dy /= d;
        l.s.vx += dx * f; l.s.vy += dy * f;
        l.t.vx -= dx * f; l.t.vy -= dy * f;
      }
    }
    for (const a of nodes) {
      a.vx -= a.x * 0.0011 * alpha; a.vy -= a.y * 0.0011 * alpha;
      a.vx *= 0.85; a.vy *= 0.85;
      if (a !== dragging) { a.x += a.vx; a.y += a.vy; }
    }
  }

  function resize() {
    const r = el.stage.getBoundingClientRect();
    DPR = Math.min(devicePixelRatio || 1, 2.5); VW = r.width; VH = r.height;
    el.canvas.width = VW * DPR; el.canvas.height = VH * DPR;
    el.canvas.style.width = VW + 'px'; el.canvas.style.height = VH + 'px';
    ctx.setTransform(DPR, 0, 0, DPR, 0, 0);
  }

  function draw() {
    step();
    ctx.setTransform(DPR, 0, 0, DPR, 0, 0); ctx.clearRect(0, 0, VW, VH);
    const g = ctx.createRadialGradient(VW / 2, VH * 0.44, 60, VW / 2, VH * 0.44, Math.max(VW, VH) * 0.85);
    g.addColorStop(0, '#161616'); g.addColorStop(1, '#0a0a0a'); ctx.fillStyle = g; ctx.fillRect(0, 0, VW, VH);

    ctx.save(); ctx.translate(VW / 2 + cam.x, VH * 0.46 + cam.y); ctx.scale(cam.z, cam.z);
    const active = id => hiSet ? hiSet.has(id) : (searchHits ? searchHits.has(id) : true);

    const T = performance.now() / 1000;
    for (const a of nodes) {
      if (a === dragging) { a.dx = a.x; a.dy = a.y; continue; }
      const amp = a.kind === 'item' ? 3.4 : 1.6;
      a.dx = a.x + Math.sin(T * 0.5 + a.phase) * amp;
      a.dy = a.y + Math.cos(T * 0.42 + a.phase * 1.3) * amp;
    }

    for (const l of links) {
      const on = active(l.s.id) && active(l.t.id);
      const dim = (hiSet || searchHits) && !on;
      const twin = l.rel === 'twin';
      const ax = l.s.dx, ay = l.s.dy, bx = l.t.dx, by = l.t.dy;
      const mx = (ax + bx) / 2, my = (ay + by) / 2, vx = bx - ax, vy = by - ay, len = Math.hypot(vx, vy) || 1;
      const bow = len * (twin ? 0.2 : 0.05) * ((l.s.idx % 2) ? 1 : -1);
      ctx.lineWidth = 1;
      ctx.strokeStyle = dim ? 'rgba(150,160,140,.045)' : (twin ? 'rgba(242,239,230,.11)' : rgba(l.t.col, 0.3));
      ctx.beginPath(); ctx.moveTo(ax, ay);
      ctx.quadraticCurveTo(mx - vy / len * bow, my + vx / len * bow, bx, by); ctx.stroke();
    }
    for (const a of nodes) {
      if (a.kind !== 'item') continue;
      const on = active(a.id), dim = (hiSet || searchHits) && !on;
      ctx.globalAlpha = dim ? 0.12 : 1;
      ctx.save();
      ctx.beginPath(); ctx.arc(a.dx, a.dy, a.r, 0, 6.2832);
      if (!dim) { ctx.shadowColor = 'rgba(0,0,0,.5)'; ctx.shadowBlur = 8; ctx.shadowOffsetY = 2; }
      ctx.fillStyle = '#0c0c0c'; ctx.fill();
      ctx.shadowColor = 'transparent'; ctx.shadowBlur = 0; ctx.shadowOffsetY = 0;
      ctx.clip();
      if (a.im) {
        const iw = a.im.naturalWidth, ih = a.im.naturalHeight, s = Math.max(2 * a.r / iw, 2 * a.r / ih);
        ctx.drawImage(a.im, a.dx - iw * s / 2, a.dy - ih * s / 2, iw * s, ih * s);
      } else {
        const p = PAL[a.item.ty] || ['#3a4a34', '#26301f'];
        const lg = ctx.createLinearGradient(a.dx - a.r, a.dy - a.r, a.dx + a.r, a.dy + a.r);
        lg.addColorStop(0, p[0]); lg.addColorStop(1, p[1]); ctx.fillStyle = lg;
        ctx.fillRect(a.dx - a.r, a.dy - a.r, a.r * 2, a.r * 2);
      }
      ctx.restore();
      if (a === selected) { ctx.beginPath(); ctx.arc(a.dx, a.dy, a.r, 0, 6.2832); ctx.lineWidth = 2.5; ctx.strokeStyle = '#fafcf8'; ctx.stroke(); }
    }
    ctx.globalAlpha = 1;
    for (const a of nodes) {
      if (a.kind !== 'genre') continue;
      const on = active(a.id), dim = (hiSet || searchHits) && !on;
      ctx.globalAlpha = dim ? 0.16 : 1; ctx.beginPath(); ctx.arc(a.dx, a.dy, a.r, 0, 6.2832);
      const rgd = ctx.createRadialGradient(a.dx - a.r * .3, a.dy - a.r * .3, a.r * .2, a.dx, a.dy, a.r);
      rgd.addColorStop(0, rgba(a.col, .97)); rgd.addColorStop(1, rgba(a.col, .74)); ctx.fillStyle = rgd;
      ctx.shadowColor = rgba(a.col, .7); ctx.shadowBlur = dim ? 0 : 22; ctx.fill(); ctx.shadowBlur = 0;
      if (a === selected) { ctx.lineWidth = 2.5; ctx.strokeStyle = '#fafcf8'; ctx.stroke(); }
      ctx.globalAlpha = dim ? 0.3 : 1; ctx.fillStyle = '#0c120a';
      ctx.font = `${clamp(a.r * 0.42, 9, 17) | 0}px "Cinzel Decorative",serif`; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
      if (a.r > 16 || !(hiSet || searchHits) || on) ctx.fillText(a.name, a.dx, a.dy);
    }
    ctx.globalAlpha = 1; ctx.restore();
    raf = requestAnimationFrame(draw);
  }

  function toWorld(px, py) {
    const r = el.stage.getBoundingClientRect();
    return { x: (px - r.left - (VW / 2 + cam.x)) / cam.z, y: (py - r.top - (VH * 0.46 + cam.y)) / cam.z };
  }
  function nodeAt(px, py) {
    const w = toWorld(px, py); let best = null, bd = 1e9;
    for (const a of nodes) { const d = Math.hypot(a.x - w.x, a.y - w.y); const hr = a.r + (a.kind === 'item' ? 11 : 4); if (d < hr && d < bd) { bd = d; best = a; } }
    return best;
  }

  function openItem(item) { if (dotNet && item) dotNet.invokeMethodAsync('OpenItem', item.id); }

  function select(node) {
    selected = node;
    if (!node) { hiSet = null; el.panel.classList.remove('on'); el.scrim.classList.remove('on'); el.reset.classList.remove('on'); return; }
    hiSet = connectedIds(node); alpha = Math.max(alpha, 0.4); el.reset.classList.add('on');
    renderPanel(node); el.panel.classList.add('on'); el.scrim.classList.add('on');
  }
  function clearSel() { select(null); }

  function posterHTML(it) {
    const badge = `<div class="cbadge">${TL[it.ty] || it.ty}</div>`;
    const rt = it.r ? `<div class="crt">★ ${it.r}</div>` : '';
    return it.img
      ? `<div class="cposter" style="background-image:url('${it.img}')">${badge}${rt}</div>`
      : `<div class="cposter cfb" style="background:linear-gradient(150deg,${(PAL[it.ty] || ['#3a4a34', '#26301f'])[0]},${(PAL[it.ty] || ['#3a4a34', '#26301f'])[1]})">${badge}${rt}<div class="cfbt">${it.t}</div></div>`;
  }
  function cellHTML(idx, delay) {
    const it = items[idx];
    return `<div class="ccell" data-i="${idx}" style="animation-delay:${delay}ms">${posterHTML(it)}
      <div class="ccap"><div class="cct">${it.t}</div><div class="ccc">${it.c || ''}</div></div></div>`;
  }

  function renderPanel(node) {
    const idxs = descItems(node).sort((a, b) => (items[b].fav ? 1 : 0) - (items[a].fav ? 1 : 0) || (items[b].r || 0) - (items[a].r || 0));
    const co = {}; idxs.forEach(i => [...new Set(items[i].g)].forEach(g => { if (g !== node.name) co[g] = (co[g] || 0) + 1; }));
    const related = Object.entries(co).sort((a, b) => b[1] - a[1]);
    el.phead.innerHTML = `<div class="cpdot" style="background:${rgb(node.col)};color:${rgb(node.col)}"></div>
      <div class="cpmeta"><div class="cpkick">Genre · ${idxs.length} works</div>
      <div class="cptitle">${node.name}</div>
      <div class="cpsub">${node.count >= 6 ? 'A hub of the archive — many works pass through it.' : 'Tap a cover to open it.'}</div></div>`;
    let d = 0;
    const relHTML = related.length ? `<div class="cpgroup">Threads into</div>
      <div class="ctags" style="margin-bottom:4px">${related.map(([g, c]) => `<span class="ctag" data-g="${g}">
        <i style="background:${rgb(hueOf[g])}"></i>${g} · ${c}</span>`).join('')}</div>
      <div class="cpgroup">${idxs.length} works</div>` : '';
    el.pscroll.innerHTML = relHTML + `<div class="cgrid">${idxs.map(i => cellHTML(i, d += 45)).join('')}</div>`;
    el.pscroll.scrollTop = 0; wirePanel();
  }
  function wirePanel() {
    el.pscroll.querySelectorAll('.ccell').forEach(cell => cell.onclick = () => openItem(items[+cell.dataset.i]));
    el.pscroll.querySelectorAll('[data-g]').forEach(chip => chip.onclick = () => { const nd = genreNode[chip.dataset.g]; if (nd) { select(nd); centerOn(nd); } });
  }
  function centerOn(node) { cam.x = -node.x * cam.z; cam.y = -node.y * cam.z; alpha = Math.max(alpha, 0.2); }
  function zoomBy(f) { cam.z = clamp(cam.z * f, 0.32, 3); alpha = Math.max(alpha, 0.05); }
  // Open zoomed in on the centre so covers are legible; pan / zoom out to explore.
  function frameInitial() {
    let sx = 0, sy = 0; for (const n of nodes) { sx += n.x; sy += n.y; }
    const cx = nodes.length ? sx / nodes.length : 0, cy = nodes.length ? sy / nodes.length : 0;
    cam.z = 0.85; cam.x = -cx * cam.z; cam.y = -cy * cam.z;
  }

  function fitView(pad) {
    pad = pad || 60;
    let minx = 1e9, maxx = -1e9, miny = 1e9, maxy = -1e9;
    for (const n of nodes) { minx = Math.min(minx, n.x - n.r); maxx = Math.max(maxx, n.x + n.r); miny = Math.min(miny, n.y - n.r); maxy = Math.max(maxy, n.y + n.r); }
    const gw = maxx - minx || 1, gh = maxy - miny || 1;
    cam.z = clamp(Math.min((VW - pad * 2) / gw, (VH - pad * 2) / gh), 0.3, 1.4);
    cam.x = -((minx + maxx) / 2) * cam.z; cam.y = -((miny + maxy) / 2) * cam.z;
  }

  // ---- pointer: tap / drag-node / pan / pinch ----
  let panning = false, moved = false, last = { x: 0, y: 0 }, pinch = null;
  const pts = new Map();
  function wirePointer() {
    const stage = el.stage;
    stage.onpointerdown = e => {
      stage.setPointerCapture(e.pointerId); pts.set(e.pointerId, { x: e.clientX, y: e.clientY });
      if (pts.size === 2) {
        const p = [...pts.values()];
        const mx = (p[0].x + p[1].x) / 2, my = (p[0].y + p[1].y) / 2;
        pinch = { d: Math.hypot(p[0].x - p[1].x, p[0].y - p[1].y), z: cam.z, w: toWorld(mx, my) };
        dragging = null; panning = false; return;
      }
      moved = false; last = { x: e.clientX, y: e.clientY };
      const n = nodeAt(e.clientX, e.clientY); if (n) dragging = n; else panning = true;
    };
    stage.onpointermove = e => {
      if (!pts.has(e.pointerId)) return; pts.set(e.pointerId, { x: e.clientX, y: e.clientY });
      if (pinch && pts.size >= 2) {
        const p = [...pts.values()];
        const d = Math.hypot(p[0].x - p[1].x, p[0].y - p[1].y);
        const mx = (p[0].x + p[1].x) / 2, my = (p[0].y + p[1].y) / 2;
        cam.z = clamp(pinch.z * d / pinch.d, 0.32, 3);
        const r = el.stage.getBoundingClientRect();
        cam.x = (mx - r.left - VW / 2) - pinch.w.x * cam.z;
        cam.y = (my - r.top - VH * 0.46) - pinch.w.y * cam.z;
        return;
      }
      const dx = e.clientX - last.x, dy = e.clientY - last.y;
      if (Math.abs(dx) + Math.abs(dy) > 3) moved = true;
      if (dragging) { const w = toWorld(e.clientX, e.clientY); dragging.x = w.x; dragging.y = w.y; dragging.vx = 0; dragging.vy = 0; alpha = Math.max(alpha, 0.3); }
      else if (panning) { cam.x += dx; cam.y += dy; }
      last = { x: e.clientX, y: e.clientY };
    };
    const up = e => {
      pts.delete(e.pointerId); if (pts.size < 2) pinch = null;
      if (!moved && (dragging || panning)) {
        const n = nodeAt(e.clientX, e.clientY);
        if (n && n.kind === 'item') openItem(n.item);
        else if (n) select(n);
        else clearSel();
      }
      dragging = null; if (pts.size === 0) panning = false;
    };
    stage.onpointerup = up; stage.onpointercancel = up;
    stage.onwheel = e => { e.preventDefault(); cam.z = clamp(cam.z * (e.deltaY < 0 ? 1.1 : 0.9), 0.32, 3); };
  }

  // ---- search: genres client-side, items from the server ----
  function rowForGenre(n) {
    return `<div class="crow" data-g="${n.name}"><span class="cdot" style="background:${rgb(n.col)};color:${rgb(n.col)}"></span>
      <span class="crttl">${n.name}</span><span class="crmeta">${n.count} works</span></div>`;
  }
  function rowForItem(it) {
    return `<div class="crow" data-i="${it.id}"><span class="cdot" style="background:${rgb(hueOf[(it.g || [])[0]] || [140, 160, 130])}"></span>
      <span class="crttl">${it.t}</span><span class="crmeta">${TL[it.ty] || it.ty}</span></div>`;
  }
  function runSearch() {
    const v = el.q.value.trim(), lv = v.toLowerCase();
    el.clr.classList.toggle('on', !!v);
    if (!v) { searchHits = null; el.res.classList.remove('on'); el.res.innerHTML = ''; if (!selected) hiSet = null; return; }
    clearTimeout(searchTimer);
    searchTimer = setTimeout(() => doSearch(v, lv), 170);
  }
  async function doSearch(v, lv) {
    const gMatch = nodes.filter(n => n.kind === 'genre' && n.name.toLowerCase().includes(lv));
    let iMatch = [];
    try { iMatch = (await dotNet.invokeMethodAsync('Search', v)) || []; } catch (e) { iMatch = []; }
    searchHits = new Set(gMatch.map(n => n.id));
    const hitIds = new Set(iMatch.map(it => it.id));
    nodes.forEach(n => { if (n.kind === 'item' && hitIds.has(n.item.id)) searchHits.add(n.id); });
    if (!selected) hiSet = null;
    let html = '';
    if (gMatch.length) html += `<div class="crhead">Genres</div>` + gMatch.map(rowForGenre).join('');
    if (iMatch.length) html += `<div class="crhead">Titles</div>` + iMatch.slice(0, 40).map(rowForItem).join('');
    if (!gMatch.length && !iMatch.length) html = `<div class="crow"><span class="crttl" style="color:var(--dim)">No matches in the archive.</span></div>`;
    el.res.innerHTML = html; el.res.classList.add('on');
    el.res.querySelectorAll('[data-g]').forEach(r => r.onclick = () => { const nd = genreNode[r.dataset.g]; el.q.blur(); el.res.classList.remove('on'); if (nd) { select(nd); centerOn(nd); } });
    el.res.querySelectorAll('[data-i]').forEach(r => r.onclick = () => { el.q.blur(); el.res.classList.remove('on'); openItem({ id: +r.dataset.i }); });
  }

  function init(data, ref) {
    if (raf) cancelAnimationFrame(raf);
    items = data || []; dotNet = ref;
    el = {
      stage: document.getElementById('cstage'), canvas: document.getElementById('ccanvas'),
      panel: document.getElementById('cpanel'), scrim: document.getElementById('cscrim'),
      phead: document.getElementById('cphead'), pscroll: document.getElementById('cpscroll'),
      reset: document.getElementById('creset'), cnt: document.getElementById('ccnt'),
      q: document.getElementById('cq'), res: document.getElementById('cres'), clr: document.getElementById('cclr'),
      zin: document.getElementById('czin'), zout: document.getElementById('czout')
    };
    if (!el.canvas) return;
    ctx = el.canvas.getContext('2d');
    cam = { x: 0, y: 0, z: 0.66 }; selected = null; hiSet = null; searchHits = null; dragging = null;

    build();
    resize();
    alpha = 1; let guard = 0; while (alpha > 0.02 && guard++ < 4000) step();
    frameInitial();

    if (!wiredWindow) { addEventListener('resize', () => { if (el.canvas) resize(); }); wiredWindow = true; }
    wirePointer();
    el.reset.onclick = clearSel;
    el.scrim.onclick = clearSel;
    if (el.zin) el.zin.onclick = () => zoomBy(1.35);
    if (el.zout) el.zout.onclick = () => zoomBy(1 / 1.35);
    el.q.oninput = runSearch;
    el.q.onfocus = () => { if (el.q.value) el.res.classList.add('on'); };
    el.clr.onclick = () => { el.q.value = ''; runSearch(); el.q.focus(); };
    el.cnt.textContent = `${items.length} works · ${Object.keys(genreNode).length} genres`;

    alpha = 1;
    raf = requestAnimationFrame(draw);
  }

  function dispose() { if (raf) cancelAnimationFrame(raf); raf = 0; }

  return { init, dispose };
})();
