const Api = {
  async state() {
    const r = await fetch('/api/state', { cache: 'no-store' });
    if (!r.ok) throw new Error('state');
    return r.json();
  },
  async layout() {
    const r = await fetch('/api/layout', { cache: 'no-store' });
    if (!r.ok) throw new Error('layout');
    return r.json();
  },
  async saveLayout(layout) {
    await fetch('/api/layout', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(layout)
    });
  },
  async control(id, body) {
    const r = await fetch(`/api/control/${encodeURIComponent(id)}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
    });
    return r.json();
  },
  async action(id, body = {}) {
    const r = await fetch(`/api/action/${encodeURIComponent(id)}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
    });
    return r.json();
  }
};

const fallbackLayout = {
  pages: [
    {
      id: 'home',
      name: 'Home',
      tiles: [
        { id: 'vol', component: 'master.volume', view: 'slider', size: '2x1', title: 'Master Volume' },
        { id: 'mute', component: 'master.muted', view: 'toggle', size: '1x1', title: 'Mute' },
        { id: 'mic', component: 'mic.muted', view: 'toggle', size: '1x1', title: 'Microphone' },
        { id: 'bright', component: 'display.brightness', view: 'plusminus', size: '2x1', title: 'Brightness' }
      ]
    },
    {
      id: 'system',
      name: 'System',
      tiles: [
        { id: 'lock', component: 'system.lock', view: 'action', size: '1x1', title: 'Lock' },
        { id: 'sleep', component: 'system.sleep', view: 'action', size: '1x1', title: 'Sleep' },
        { id: 'restart', component: 'system.restart', view: 'action', size: '1x1', title: 'Restart' },
        { id: 'shutdown', component: 'system.shutdown', view: 'action', size: '1x1', title: 'Shutdown' }
      ]
    }
  ]
};

const catalog = [
  ['Master Volume', 'master.volume', 'slider', '2x1', 'Range • Slider'],
  ['Master ±', 'master.volume', 'plusminus', '2x1', 'Range • Buttons'],
  ['Master Mute', 'master.muted', 'toggle', '1x1', 'Boolean'],
  ['Brightness', 'display.brightness', 'slider', '2x1', 'Range • Slider'],
  ['Brightness ±', 'display.brightness', 'plusminus', '2x1', 'Range • Buttons'],
  ['Microphone', 'mic.muted', 'toggle', '1x1', 'Boolean'],
  ['Mic Volume', 'mic.volume', 'slider', '2x1', 'Range • Slider'],
  ['CPU', 'cpu', 'metric', '1x1', 'Telemetry'],
  ['GPU', 'gpu', 'metric', '1x1', 'Telemetry'],
  ['Lock', 'system.lock', 'action', '1x1', 'System Action'],
  ['Sleep', 'system.sleep', 'action', '1x1', 'System Action'],
  ['Restart', 'system.restart', 'action', '1x1', 'System Action'],
  ['Shutdown', 'system.shutdown', 'action', '1x1', 'System Action']
];

const S = {
  mode: 'dashboard',
  edit: false,
  page: 'home',
  online: false,
  controls: {
    'master.volume': 50,
    'master.muted': false,
    'display.brightness': 50,
    'mic.volume': 70,
    'mic.muted': false,
    'stream.live': false,
    'media.playing': false
  },
  telemetry: {
    cpu: { usage: '--', temperature: '--' },
    gpu: { usage: '--', temperature: '--', fps: '--' },
    ram: { usage: '--' },
    network: { downMbps: '--', pingMs: '--' },
    system: { status: 'HOST OFFLINE', host: 'AKENO-PC' }
  },
  layout: structuredClone(fallbackLayout)
};

function clamp(v) {
  const n = Number(v);
  if (Number.isNaN(n)) return 0;
  return Math.max(0, Math.min(100, n));
}

function page() {
  return S.layout.pages.find((x) => x.id === S.page) || S.layout.pages[0];
}

function toast(message) {
  const el = document.querySelector('#toast');
  el.textContent = message;
  el.classList.add('show');
  clearTimeout(toast.t);
  toast.t = setTimeout(() => el.classList.remove('show'), 1200);
}

function toDisplayValue(v) {
  return v === null || v === undefined ? '--' : v;
}

function normalizeLayout(raw) {
  if (!raw?.pages?.length) return structuredClone(fallbackLayout);
  return {
    pages: raw.pages.map((p, pageIndex) => ({
      id: p.id || `p${pageIndex + 1}`,
      name: p.name || `Page ${pageIndex + 1}`,
      tiles: (p.widgets || p.tiles || []).map((w, tileIndex) => ({
        id: w.id || Math.random().toString(36).slice(2, 9),
        component: w.componentId || w.component,
        view: w.view || 'button',
        size: w.size || '1x1',
        title: w.titleOverride || w.title || 'Widget'
      }))
    }))
  };
}

function toBackendLayout(layout) {
  return {
    pages: layout.pages.map((p, pageIndex) => ({
      id: p.id,
      name: p.name,
      order: pageIndex,
      widgets: p.tiles.map((t, tileIndex) => ({
        id: t.id,
        componentId: t.component,
        view: t.view,
        size: t.size,
        position: tileIndex,
        titleOverride: t.title,
        configuration: {}
      }))
    }))
  };
}

async function saveLayout() {
  try {
    await Api.saveLayout(toBackendLayout(S.layout));
  } catch {
    toast('Layout save failed');
  }
}

async function range(key, value) {
  S.controls[key] = clamp(value);
  render();
  await Api.control(key, { value: S.controls[key] });
}

async function adjust(key, delta) {
  S.controls[key] = clamp((S.controls[key] || 0) + delta);
  render();
  await Api.control(key, { operation: delta > 0 ? 'increment' : 'decrement', step: Math.abs(delta) });
}

async function toggle(key) {
  S.controls[key] = !S.controls[key];
  render();
  await Api.control(key, { bool: S.controls[key] });
}

async function action(key) {
  const dangerous = key === 'system.restart' || key === 'system.shutdown';
  if (dangerous && !confirm(`Confirm ${key.replace('system.', '')}?`)) return;
  const result = await Api.action(key, dangerous ? { confirmed: true } : {});
  toast(result.success ? `${key} triggered` : (result.error || result.message || 'Action failed'));
}

function metric(title, val, sub, size = '1x1') {
  return `<div class="tile s${size}"><div class="eyebrow">${title}</div><div class="metric">${toDisplayValue(val)}</div><div class="sub">${sub || ''}</div></div>`;
}

function dashboard() {
  const t = S.telemetry;
  const c = S.controls;
  return `<div class="panel"><div class="head"><div><div class="title">SYSTEM DASHBOARD</div><div class="sub">Monitor everything. Control anything.</div></div><div class="sub">${S.online ? '● PC CONNECTED' : '○ HOST OFFLINE'}</div></div><div class="grid dashboard"><div class="tile hero"><div class="row"><div><div class="eyebrow">AKENO SYSTEM</div><div class="metric">${t.system.status}</div><div class="sub">${t.system.host}</div></div><div class="moon"></div></div><div class="progress"><i style="width:${Number(t.gpu.usage) || 0}%"></i></div></div>${metric('CPU', `${toDisplayValue(t.cpu.usage)}%`, `${toDisplayValue(t.cpu.temperature)}°C`)}${metric('GPU', `${toDisplayValue(t.gpu.usage)}%`, `${toDisplayValue(t.gpu.temperature)}°C`)}${metric('RAM', `${toDisplayValue(t.ram.usage)}%`, 'Memory')}<div class="tile s2x1"><div class="row"><div><div class="eyebrow">Master Volume</div><div class="metric">${Math.round(c['master.volume'] || 0)}%</div></div><span>${c['master.muted'] ? '🔇' : '🔊'}</span></div><input class="range" data-range="master.volume" type="range" min="0" max="100" value="${c['master.volume'] || 0}"></div><div class="tile s2x1 ${!c['mic.muted'] ? 'active' : ''}"><div class="eyebrow">Microphone</div><div class="metric">${c['mic.muted'] ? 'MUTED' : 'ACTIVE'}</div><div class="sub">Tap to toggle</div><button class="full" data-toggle="mic.muted"></button></div><div class="tile s2x1"><div class="row"><div><div class="eyebrow">Brightness</div><div class="metric">${toDisplayValue(c['display.brightness'])}%</div></div><span>☀</span></div><div class="pmrow"><button class="pm" data-adj="display.brightness" data-d="-5">−</button><button class="pm" data-adj="display.brightness" data-d="5">＋</button></div></div>${metric('Network ↓', toDisplayValue(t.network.downMbps), 'Mbps', '1x1')}${metric('Ping', toDisplayValue(t.network.pingMs), 'ms')}</div></div>`;
}

function widget(tile) {
  const c = S.controls;
  const t = S.telemetry;
  let body = '';

  if (tile.component === 'cpu') {
    body = `<div class="eyebrow">${tile.title}</div><div class="metric">${toDisplayValue(t.cpu.usage)}%</div><div class="sub">${toDisplayValue(t.cpu.temperature)}°C</div>`;
  } else if (tile.component === 'gpu') {
    body = `<div class="eyebrow">${tile.title}</div><div class="metric">${toDisplayValue(t.gpu.usage)}%</div><div class="sub">${toDisplayValue(t.gpu.temperature)}°C</div>`;
  } else if (tile.view === 'slider') {
    const v = Math.round(c[tile.component] || 0);
    body = `<div class="row"><div><div class="eyebrow">${tile.title}</div><div class="metric">${v}%</div></div><span>◉</span></div><input class="range" data-range="${tile.component}" type="range" min="0" max="100" value="${v}">`;
  } else if (tile.view === 'plusminus') {
    const v = Math.round(c[tile.component] || 0);
    body = `<div class="row"><div><div class="eyebrow">${tile.title}</div><div class="metric">${v}%</div></div><span>◐</span></div><div class="pmrow"><button class="pm" data-adj="${tile.component}" data-d="-5">−</button><button class="pm" data-adj="${tile.component}" data-d="5">＋</button></div>`;
  } else if (tile.view === 'toggle') {
    const on = !!c[tile.component];
    body = `<div class="eyebrow">${tile.title}</div><div class="metric">${on ? 'ON' : 'OFF'}</div><div class="sub">Tap to toggle</div><button class="full" data-toggle="${tile.component}"></button>`;
  } else {
    body = `<div class="eyebrow">Action</div><div class="metric">${tile.title}</div><div class="sub">Tap to run</div><button class="full" data-action="${tile.component}"></button>`;
  }

  return `<div class="tile s${tile.size}" draggable="${S.edit}" data-tile="${tile.id}">${body}<div class="menu"><button data-resize="${tile.id}">↔</button><button data-delete="${tile.id}">×</button></div></div>`;
}

function deck() {
  const p = page();
  return `<div class="panel ${S.edit ? 'editing' : ''}"><div class="head"><div><div class="title">DECK MODE</div><div class="sub">${S.edit ? 'Drag, resize and customize' : 'Your screen. Your controls.'}</div></div><button class="add" style="padding:8px 12px" data-gallery>＋ Add</button></div><div class="pages">${S.layout.pages.map((x) => `<button class="pill ${x.id === S.page ? 'on' : ''}" data-page="${x.id}">${x.name}</button>`).join('')}<button class="pill" data-new>＋</button></div><div class="grid">${p.tiles.map(widget).join('')}</div></div>`;
}

function gallery() {
  return `<div class="gallery" id="gallery"><div class="sheet"><div class="head"><div><b>Widget Gallery</b><div class="sub">One function, multiple views.</div></div><button class="icon" data-close>×</button></div><div class="items">${catalog.map((x) => `<div class="item" data-add='${JSON.stringify({ title: x[0], component: x[1], view: x[2], size: x[3] })}'><b>${x[0]}</b><small>${x[4]} • ${x[3]}</small></div>`).join('')}</div></div></div>`;
}

function render() {
  document.querySelector('#app').innerHTML = `<div class="shell"><div class="top"><div class="brand"><div class="logo">A</div><div><div class="name">AKENO CONTROL CENTER</div><div class="tag">BEYOND THE DAWN • NEO-SAMURAI NOIR</div></div></div>${S.mode === 'deck' ? `<button class="icon ${S.edit ? 'on' : ''}" data-edit>✦</button>` : ''}</div><div class="tabs"><button class="tab ${S.mode === 'dashboard' ? 'on' : ''}" data-mode="dashboard">Dashboard</button><button class="tab ${S.mode === 'deck' ? 'on' : ''}" data-mode="deck">Deck</button></div>${S.mode === 'dashboard' ? dashboard() : deck()}</div>${gallery()}`;
  bind();
}

function bind() {
  document.querySelectorAll('[data-mode]').forEach((el) => {
    el.onclick = () => {
      S.mode = el.dataset.mode;
      render();
    };
  });

  document.querySelector('[data-edit]')?.addEventListener('click', () => {
    S.edit = !S.edit;
    render();
  });

  document.querySelectorAll('[data-page]').forEach((el) => {
    el.onclick = () => {
      S.page = el.dataset.page;
      render();
    };
  });

  document.querySelector('[data-new]')?.addEventListener('click', async () => {
    const id = `p${Date.now()}`;
    S.layout.pages.push({ id, name: `Page ${S.layout.pages.length + 1}`, tiles: [] });
    S.page = id;
    await saveLayout();
    render();
  });

  document.querySelectorAll('[data-range]').forEach((el) => {
    el.oninput = () => range(el.dataset.range, el.value);
  });

  document.querySelectorAll('[data-adj]').forEach((el) => {
    el.onclick = () => adjust(el.dataset.adj, Number(el.dataset.d));
  });

  document.querySelectorAll('[data-toggle]').forEach((el) => {
    el.onclick = (ev) => {
      ev.stopPropagation();
      toggle(el.dataset.toggle);
    };
  });

  document.querySelectorAll('[data-action]').forEach((el) => {
    el.onclick = (ev) => {
      ev.stopPropagation();
      action(el.dataset.action);
    };
  });

  document.querySelector('[data-gallery]')?.addEventListener('click', () => document.querySelector('#gallery').classList.add('open'));
  document.querySelector('[data-close]')?.addEventListener('click', () => document.querySelector('#gallery').classList.remove('open'));

  document.querySelectorAll('[data-add]').forEach((el) => {
    el.onclick = async () => {
      const data = JSON.parse(el.dataset.add);
      page().tiles.push({ id: Math.random().toString(36).slice(2, 9), ...data });
      await saveLayout();
      document.querySelector('#gallery').classList.remove('open');
      render();
    };
  });

  document.querySelectorAll('[data-delete]').forEach((el) => {
    el.onclick = async (ev) => {
      ev.stopPropagation();
      page().tiles = page().tiles.filter((x) => x.id !== el.dataset.delete);
      await saveLayout();
      render();
    };
  });

  document.querySelectorAll('[data-resize]').forEach((el) => {
    el.onclick = async (ev) => {
      ev.stopPropagation();
      const tile = page().tiles.find((x) => x.id === el.dataset.resize);
      const sizes = ['1x1', '2x1', '2x2', '4x1', '4x2'];
      tile.size = sizes[(sizes.indexOf(tile.size) + 1) % sizes.length];
      await saveLayout();
      render();
    };
  });

  drag();
}

function drag() {
  if (!S.edit) return;
  let from;
  document.querySelectorAll('[data-tile]').forEach((el) => {
    el.ondragstart = () => {
      from = el.dataset.tile;
      el.classList.add('drag');
    };
    el.ondragend = () => {
      el.classList.remove('drag');
      document.querySelectorAll('.over').forEach((x) => x.classList.remove('over'));
    };
    el.ondragover = (ev) => {
      ev.preventDefault();
      if (el.dataset.tile !== from) el.classList.add('over');
    };
    el.ondragleave = () => el.classList.remove('over');
    el.ondrop = async (ev) => {
      ev.preventDefault();
      const to = el.dataset.tile;
      const tiles = page().tiles;
      const i = tiles.findIndex((x) => x.id === from);
      const j = tiles.findIndex((x) => x.id === to);
      if (i < 0 || j < 0 || i === j) return;
      const [moved] = tiles.splice(i, 1);
      tiles.splice(j, 0, moved);
      await saveLayout();
      render();
    };
  });
}

function applyState(data) {
  S.online = true;
  Object.assign(S.controls, data.controls || {});
  S.telemetry = data.telemetry || S.telemetry;
  render();
}

function connectEvents() {
  try {
    const stream = new EventSource('/api/events');
    stream.addEventListener('streamChanged', (event) => {
      const data = JSON.parse(event.data);
      applyState(data);
    });
    stream.onerror = () => {
      S.online = false;
      render();
    };
  } catch {
    setInterval(syncState, 2000);
  }
}

async function syncState() {
  try {
    const data = await Api.state();
    applyState(data);
  } catch {
    S.online = false;
    render();
  }
}

async function boot() {
  try {
    const layout = await Api.layout();
    S.layout = normalizeLayout(layout);
    S.page = S.layout.pages[0]?.id || 'home';
  } catch {
    S.layout = structuredClone(fallbackLayout);
  }

  render();
  await syncState();
  connectEvents();
  if ('serviceWorker' in navigator && location.protocol.startsWith('http')) {
    navigator.serviceWorker.register('/sw.js').catch(() => {});
  }
}

boot();
