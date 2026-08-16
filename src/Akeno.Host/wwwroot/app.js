const STORAGE_LAYOUT = 'akeno.layout';
const STORAGE_TOKEN = 'akeno.pair.token';

const base = {
  pages: [
    {
      id: 'home',
      name: 'Home',
      tiles: [
        ['mic', 'mic.muted', 'toggle', '1x1', 'Microphone'],
        ['live', 'stream.live', 'toggle', '1x1', 'OBS'],
        ['vol', 'master.volume', 'slider', '2x1', 'Master Volume'],
        ['cpu', 'cpu', 'metric', '1x1', 'CPU'],
        ['gpu', 'gpu', 'metric', '1x1', 'GPU'],
        ['bright', 'display.brightness', 'plusminus', '2x1', 'Brightness'],
        ['lock', 'system.lock', 'action', '1x1', 'Lock PC'],
        ['shutdown', 'system.shutdown', 'action', '1x1', 'Shutdown']
      ]
    },
    {
      id: 'audio',
      name: 'Audio',
      tiles: [
        ['av', 'master.volume', 'slider', '4x1', 'Master Volume'],
        ['apm', 'master.volume', 'plusminus', '2x1', 'Master ±'],
        ['am', 'mic.muted', 'toggle', '1x1', 'Mute Mic']
      ]
    }
  ]
};
base.pages.forEach((p) => {
  p.tiles = p.tiles.map((x) => ({ id: x[0], component: x[1], view: x[2], size: x[3], title: x[4] }));
});

const S = {
  mode: 'dashboard',
  edit: false,
  page: 'home',
  online: false,
  pairingRequired: false,
  token: localStorage.getItem(STORAGE_TOKEN) || '',
  controls: {
    'master.volume': 72,
    'display.brightness': 68,
    'mic.muted': false,
    'stream.live': false,
    'media.playing': true
  },
  components: {},
  componentDefs: [],
  telemetry: {
    cpu: { usage: 34, temperature: 52 },
    gpu: { usage: 67, temperature: 62, fps: 144 },
    ram: { usage: 42 },
    network: { downMbps: 0, pingMs: 12 },
    system: { status: 'Static Demo', host: 'AKENO-PC' }
  },
  layout: loadLayout()
};

function authHeaders() {
  const headers = { 'Content-Type': 'application/json' };
  if (S.token) headers.Authorization = 'Bearer ' + S.token;
  return headers;
}

const A = {
  async config() {
    try {
      const r = await fetch('api/config', { cache: 'no-store' });
      if (!r.ok) return null;
      return await r.json();
    } catch {
      return null;
    }
  },
  async state() {
    try {
      const r = await fetch('api/state', { cache: 'no-store' });
      if (!r.ok) throw new Error('state');
      return await r.json();
    } catch {
      return null;
    }
  },
  async components() {
    try {
      const r = await fetch('api/components', { cache: 'no-store' });
      if (!r.ok) return null;
      return await r.json();
    } catch {
      return null;
    }
  },
  async pair(code) {
    try {
      const r = await fetch('api/pair', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ code })
      });
      if (!r.ok) return null;
      return await r.json();
    } catch {
      return null;
    }
  },
  async control(key, body) {
    try {
      const r = await fetch(`api/control/${encodeURIComponent(key)}`, {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify(body)
      });
      if (r.status === 401) {
        toast('Pairing required');
        return null;
      }
      return await r.json();
    } catch {
      return null;
    }
  },
  async action(key, body = {}) {
    try {
      const r = await fetch(`api/action/${encodeURIComponent(key)}`, {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify(body)
      });
      if (r.status === 401) {
        toast('Pairing required');
        return null;
      }
      return await r.json();
    } catch {
      return null;
    }
  }
};

function loadLayout() {
  try {
    return JSON.parse(localStorage.getItem(STORAGE_LAYOUT)) || structuredClone(base);
  } catch {
    return structuredClone(base);
  }
}

function saveLayout() {
  localStorage.setItem(STORAGE_LAYOUT, JSON.stringify(S.layout));
}

function page() {
  return S.layout.pages.find((x) => x.id === S.page) || S.layout.pages[0];
}

function toast(message) {
  const el = document.querySelector('#toast');
  el.textContent = message;
  el.classList.add('show');
  clearTimeout(toast.timer);
  toast.timer = setTimeout(() => el.classList.remove('show'), 1200);
}

function clamp(v) {
  return Math.max(0, Math.min(100, +v || 0));
}

function compState(id) {
  return S.components[id] || { available: true };
}

function isAvailable(id) {
  return compState(id).available !== false;
}

function disabledClass(id) {
  return isAvailable(id) ? '' : ' disabled';
}

async function range(key, value) {
  if (!isAvailable(key)) return;
  S.controls[key] = clamp(value);
  render();
  await A.control(key, { value: S.controls[key] });
}

async function adj(key, delta) {
  if (!isAvailable(key)) return;
  S.controls[key] = clamp((S.controls[key] || 0) + delta);
  render();
  await A.control(key, { operation: delta > 0 ? 'increment' : 'decrement', step: Math.abs(delta) });
}

async function tog(key) {
  if (!isAvailable(key)) return;
  S.controls[key] = !S.controls[key];
  render();
  await A.control(key, { bool: S.controls[key] });
}

async function act(key) {
  const dangerous = key === 'system.restart' || key === 'system.shutdown';
  if (dangerous) {
    const ok = window.confirm(`${key.replace('system.', '').toUpperCase()} requires confirmation. Continue?`);
    if (!ok) return;
    const response = await A.action(key, { confirm: true });
    toast(response?.success ? 'Action executed' : 'Action blocked');
    return;
  }

  const response = await A.action(key);
  toast(response?.success === false ? 'Action failed' : `${key} triggered`);
}

function metric(title, val, sub, size = '1x1') {
  return `<div class="tile s${size}"><div class="eyebrow">${title}</div><div class="metric">${val}</div><div class="sub">${sub || ''}</div></div>`;
}

function dashboard() {
  const t = S.telemetry;
  const c = S.controls;
  return `<div class="panel"><div class="head"><div><div class="title">SYSTEM DASHBOARD</div><div class="sub">Monitor everything. Control anything.</div></div><div class="sub">${S.online ? '● LAN HOST' : '○ STATIC DEMO'}</div></div><div class="grid dashboard"><div class="tile hero"><div class="row"><div><div class="eyebrow">AKENO SYSTEM</div><div class="metric">${t.system.status}</div><div class="sub">${t.system.host}</div></div><div class="moon"></div></div><div class="progress"><i style="width:${t.gpu.usage}%"></i></div></div>${metric('CPU', `${t.cpu.usage}%`, `${t.cpu.temperature}°C`)}${metric('GPU', `${t.gpu.usage}%`, `${t.gpu.temperature}°C`)}${metric('FPS', t.gpu.fps || '--', 'Live')}${metric('RAM', `${t.ram.usage}%`, 'Memory')}<div class="tile s2x1${disabledClass('master.volume')}"><div class="row"><div><div class="eyebrow">Master Volume</div><div class="metric">${Math.round(c['master.volume'] || 0)}%</div></div><span>◉</span></div><input class="range" data-range="master.volume" type="range" min="0" max="100" value="${c['master.volume'] || 0}" ${isAvailable('master.volume') ? '' : 'disabled'}><div class="sub">${compState('master.volume').error || ''}</div></div><div class="tile s2x1 ${!c['mic.muted'] ? 'active' : ''}${disabledClass('mic.muted')}"><div class="eyebrow">Microphone</div><div class="metric">${c['mic.muted'] ? 'MUTED' : 'ACTIVE'}</div><div class="sub">${compState('mic.muted').error || 'Tap to toggle'}</div><button class="full" data-toggle="mic.muted"></button></div>${metric('Network', t.network.downMbps || '--', 'Mbps', '2x1')}${metric('Ping', t.network.pingMs || '--', 'ms')}<div class="tile s1x1 ${c['stream.live'] ? 'active' : ''}"><div class="eyebrow">Stream</div><div class="metric">${c['stream.live'] ? 'LIVE' : 'OFF'}</div><button class="full" data-toggle="stream.live"></button></div></div></div>`;
}

function widget(tile) {
  const c = S.controls;
  const t = S.telemetry;
  let body = '';

  if (tile.component === 'cpu') {
    body = `<div class="eyebrow">${tile.title}</div><div class="metric">${t.cpu.usage}%</div><div class="sub">${t.cpu.temperature}°C</div>`;
  } else if (tile.component === 'gpu') {
    body = `<div class="eyebrow">${tile.title}</div><div class="metric">${t.gpu.usage}%</div><div class="sub">${t.gpu.temperature}°C • ${t.gpu.fps || '--'} FPS</div>`;
  } else if (tile.view === 'slider') {
    const v = Math.round(c[tile.component] || 0);
    body = `<div class="row"><div><div class="eyebrow">${tile.title}</div><div class="metric">${v}%</div></div><span>◉</span></div><input class="range" data-range="${tile.component}" type="range" min="0" max="100" value="${v}" ${isAvailable(tile.component) ? '' : 'disabled'}><div class="sub">${compState(tile.component).error || ''}</div>`;
  } else if (tile.view === 'plusminus') {
    const v = Math.round(c[tile.component] || 0);
    body = `<div class="row"><div><div class="eyebrow">${tile.title}</div><div class="metric">${v}%</div></div><span>◐</span></div><div class="pmrow"><button class="pm" data-adj="${tile.component}" data-d="-5" ${isAvailable(tile.component) ? '' : 'disabled'}>−</button><button class="pm" data-adj="${tile.component}" data-d="5" ${isAvailable(tile.component) ? '' : 'disabled'}>＋</button></div><div class="sub">${compState(tile.component).error || ''}</div>`;
  } else if (tile.view === 'toggle') {
    const v = !!c[tile.component];
    const txt = tile.component === 'mic.muted' ? (v ? 'OFF' : 'ON') : (v ? 'ON' : 'OFF');
    body = `<div class="eyebrow">${tile.title}</div><div class="metric">${txt}</div><div class="sub">${compState(tile.component).error || 'Tap to toggle'}</div><button class="full" data-toggle="${tile.component}"></button>`;
  } else {
    body = `<div class="eyebrow">Action</div><div class="metric">${tile.title}</div><div class="sub">Tap to run</div><button class="full" data-action="${tile.component}"></button>`;
  }

  const active = (tile.component === 'stream.live' && c['stream.live']) || (tile.component === 'mic.muted' && !c['mic.muted']);
  return `<div class="tile s${tile.size} ${active ? 'active' : ''}${disabledClass(tile.component)}" draggable="${S.edit}" data-tile="${tile.id}">${body}<div class="menu"><button data-resize="${tile.id}">↔</button><button data-delete="${tile.id}">×</button></div></div>`;
}

function deck() {
  const p = page();
  return `<div class="panel ${S.edit ? 'editing' : ''}"><div class="head"><div><div class="title">DECK MODE</div><div class="sub">${S.edit ? 'Drag, resize and customize' : 'Your screen. Your controls.'}</div></div><button class="add" style="padding:8px 12px" data-gallery>＋ Add</button></div><div class="pages">${S.layout.pages.map((x) => `<button class="pill ${x.id === S.page ? 'on' : ''}" data-page="${x.id}">${x.name}</button>`).join('')}<button class="pill" data-new>＋</button></div><div class="grid">${p.tiles.map(widget).join('')}</div></div>`;
}

function fallbackCatalog() {
  return [
    { title: 'Master Volume', component: 'master.volume', view: 'slider', size: '2x1', info: 'Range • Slider' },
    { title: 'Master ±', component: 'master.volume', view: 'plusminus', size: '2x1', info: 'Range • Buttons' },
    { title: 'Brightness', component: 'display.brightness', view: 'slider', size: '2x1', info: 'Range • Slider' },
    { title: 'Brightness ±', component: 'display.brightness', view: 'plusminus', size: '2x1', info: 'Range • Buttons' },
    { title: 'Microphone', component: 'mic.muted', view: 'toggle', size: '1x1', info: 'Toggle' },
    { title: 'Lock PC', component: 'system.lock', view: 'action', size: '1x1', info: 'Action' },
    { title: 'Restart PC', component: 'system.restart', view: 'action', size: '1x1', info: 'Dangerous Action' },
    { title: 'Shutdown PC', component: 'system.shutdown', view: 'action', size: '1x1', info: 'Dangerous Action' },
    { title: 'CPU', component: 'cpu', view: 'metric', size: '1x1', info: 'Telemetry' },
    { title: 'GPU', component: 'gpu', view: 'metric', size: '1x1', info: 'Telemetry' }
  ];
}

function gallery() {
  const source = S.componentDefs.length
    ? S.componentDefs.flatMap((c) => c.views.map((v) => ({
      title: c.name,
      component: c.id,
      view: v === 'status' ? 'toggle' : v === 'value' ? 'slider' : v,
      size: c.defaultSize || '1x1',
      info: `${c.category} • ${v}`
    })))
    : fallbackCatalog();

  return `<div class="gallery" id="gallery"><div class="sheet"><div class="head"><div><b>Widget Gallery</b><div class="sub">One function, multiple views.</div></div><button class="icon" data-close>×</button></div><div class="items">${source.map((x) => `<div class="item" data-add='${JSON.stringify({ title: x.title, component: x.component, view: x.view, size: x.size })}'><b>${x.title}</b><small>${x.info} • ${x.size}</small></div>`).join('')}</div></div></div>`;
}

function topActions() {
  if (!S.pairingRequired) return '';
  if (S.token) return `<button class="icon on" title="Paired">✓</button>`;
  return `<button class="icon" data-pair title="Pair device">🔐</button>`;
}

function render() {
  document.querySelector('#app').innerHTML = `<div class="shell"><div class="top"><div class="brand"><div class="logo">A</div><div><div class="name">AKENO CONTROL CENTER</div><div class="tag">BEYOND THE DAWN • NEO-SAMURAI NOIR</div></div></div><div style="display:flex;gap:8px">${topActions()}${S.mode === 'deck' ? `<button class="icon ${S.edit ? 'on' : ''}" data-edit>✦</button>` : ''}</div></div><div class="tabs"><button class="tab ${S.mode === 'dashboard' ? 'on' : ''}" data-mode="dashboard">Dashboard</button><button class="tab ${S.mode === 'deck' ? 'on' : ''}" data-mode="deck">Deck</button></div>${S.mode === 'dashboard' ? dashboard() : deck()}</div>${gallery()}`;
  bind();
}

function bind() {
  document.querySelectorAll('[data-mode]').forEach((e) => {
    e.onclick = () => {
      S.mode = e.dataset.mode;
      render();
    };
  });
  document.querySelector('[data-edit]')?.addEventListener('click', () => {
    S.edit = !S.edit;
    render();
  });
  document.querySelector('[data-pair]')?.addEventListener('click', pairDevice);
  document.querySelectorAll('[data-page]').forEach((e) => {
    e.onclick = () => {
      S.page = e.dataset.page;
      render();
    };
  });
  document.querySelector('[data-new]')?.addEventListener('click', () => {
    const id = `p${Date.now()}`;
    S.layout.pages.push({ id, name: `Page ${S.layout.pages.length + 1}`, tiles: [] });
    S.page = id;
    saveLayout();
    render();
  });
  document.querySelectorAll('[data-range]').forEach((e) => (e.oninput = () => range(e.dataset.range, e.value)));
  document.querySelectorAll('[data-adj]').forEach((e) => (e.onclick = () => adj(e.dataset.adj, +e.dataset.d)));
  document.querySelectorAll('[data-toggle]').forEach((e) => (e.onclick = (x) => {
    x.stopPropagation();
    tog(e.dataset.toggle);
  }));
  document.querySelectorAll('[data-action]').forEach((e) => (e.onclick = (x) => {
    x.stopPropagation();
    act(e.dataset.action);
  }));
  document.querySelector('[data-gallery]')?.addEventListener('click', () => document.querySelector('#gallery').classList.add('open'));
  document.querySelector('[data-close]')?.addEventListener('click', () => document.querySelector('#gallery').classList.remove('open'));
  document.querySelectorAll('[data-add]').forEach((e) => (e.onclick = () => {
    const x = JSON.parse(e.dataset.add);
    page().tiles.push({ id: Math.random().toString(36).slice(2, 9), ...x });
    saveLayout();
    document.querySelector('#gallery').classList.remove('open');
    render();
  }));
  document.querySelectorAll('[data-delete]').forEach((e) => (e.onclick = (x) => {
    x.stopPropagation();
    page().tiles = page().tiles.filter((t) => t.id !== e.dataset.delete);
    saveLayout();
    render();
  }));
  document.querySelectorAll('[data-resize]').forEach((e) => (e.onclick = (x) => {
    x.stopPropagation();
    const tile = page().tiles.find((t) => t.id === e.dataset.resize);
    const sizes = ['1x1', '2x1', '2x2', '4x1', '4x2'];
    tile.size = sizes[(sizes.indexOf(tile.size) + 1) % sizes.length];
    saveLayout();
    render();
  }));
  drag();
}

function drag() {
  if (!S.edit) return;
  let from;
  document.querySelectorAll('[data-tile]').forEach((e) => {
    e.ondragstart = () => {
      from = e.dataset.tile;
      e.classList.add('drag');
    };
    e.ondragend = () => {
      e.classList.remove('drag');
      document.querySelectorAll('.over').forEach((x) => x.classList.remove('over'));
    };
    e.ondragover = (x) => {
      x.preventDefault();
      if (e.dataset.tile !== from) e.classList.add('over');
    };
    e.ondragleave = () => e.classList.remove('over');
    e.ondrop = (x) => {
      x.preventDefault();
      const to = e.dataset.tile;
      const tiles = page().tiles;
      const i = tiles.findIndex((tile) => tile.id === from);
      const j = tiles.findIndex((tile) => tile.id === to);
      if (i < 0 || j < 0 || i === j) return;
      const [moved] = tiles.splice(i, 1);
      tiles.splice(j, 0, moved);
      saveLayout();
      render();
    };
  });
}

function applyIncomingState(d) {
  if (!d) return;
  S.online = true;
  Object.assign(S.controls, d.controls || {});
  S.telemetry = d.telemetry || S.telemetry;
  S.components = d.components || {};
  render();
}

function simulateTelemetry() {
  S.online = false;
  S.telemetry.cpu.usage = Math.max(5, Math.min(95, S.telemetry.cpu.usage + Math.round((Math.random() - 0.5) * 7)));
  S.telemetry.gpu.usage = Math.max(5, Math.min(99, S.telemetry.gpu.usage + Math.round((Math.random() - 0.5) * 9)));
  render();
}

async function bootstrapConfig() {
  const config = await A.config();
  S.pairingRequired = !!config?.pairingRequired;
  const defs = await A.components();
  if (defs) {
    S.componentDefs = defs;
    S.components = Object.fromEntries(defs.map((d) => [d.id, d.state]));
  }
}

async function pairDevice() {
  const code = window.prompt('Enter the 6-digit pairing code from the host PC:');
  if (!code) return;
  const result = await A.pair(code);
  if (!result?.token) {
    toast('Pairing failed');
    return;
  }
  S.token = result.token;
  localStorage.setItem(STORAGE_TOKEN, S.token);
  toast('Paired');
  render();
}

function startSse() {
  if (!window.EventSource) return false;
  try {
    const source = new EventSource('api/events');
    source.onmessage = (event) => {
      try {
        applyIncomingState(JSON.parse(event.data));
      } catch {
        // ignored
      }
    };
    source.onerror = () => {
      source.close();
      startPolling();
    };
    return true;
  } catch {
    return false;
  }
}

async function pollOnce() {
  const d = await A.state();
  if (d) applyIncomingState(d);
  else simulateTelemetry();
}

function startPolling() {
  pollOnce();
  setInterval(pollOnce, 2200);
}

render();
bootstrapConfig().then(async () => {
  await pollOnce();
  if (!startSse()) startPolling();
});

if ('serviceWorker' in navigator && location.protocol.startsWith('http')) {
  navigator.serviceWorker.register('sw.js').catch(() => {});
}
