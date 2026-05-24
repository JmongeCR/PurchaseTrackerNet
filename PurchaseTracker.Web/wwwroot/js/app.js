/**
 * PurchaseTracker App Shell — v2
 *
 * Handles:
 *  1. Service Worker registration + update lifecycle
 *  2. Offline detection + banner
 *  3. Bottom sheet component
 *  4. PWA install prompt
 *  5. Bottom nav active state
 *  6. Numpad for amount entry
 *  7. Toast notifications
 *  8. Transition animations
 */

'use strict';

/* ══════════════════════════════════════════════════════════ */
/* 1. SERVICE WORKER                                          */
/* ══════════════════════════════════════════════════════════ */

if ('serviceWorker' in navigator) {
  window.addEventListener('load', async () => {
    try {
      const reg = await navigator.serviceWorker.register('/sw.js', { scope: '/' });

      // Prompt user to refresh when a new SW is waiting
      reg.addEventListener('updatefound', () => {
        const newWorker = reg.installing;
        newWorker.addEventListener('statechange', () => {
          if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
            showUpdateToast();
          }
        });
      });
    } catch (err) {
      console.warn('[SW] Registration failed:', err);
    }
  });

  // Pre-cache the current page after it loads
  navigator.serviceWorker.ready.then(reg => {
    reg.active?.postMessage({ type: 'CACHE_PAGE', url: location.href });
  });
}

function showUpdateToast() {
  PT.toast('Nueva versión disponible', {
    type: 'neutral',
    action: { label: 'Actualizar', fn: () => { location.reload(); } },
    duration: 0,
  });
}

/* ══════════════════════════════════════════════════════════ */
/* 2. OFFLINE DETECTION                                       */
/* ══════════════════════════════════════════════════════════ */

let offlineBanner = null;

function showOfflineBanner() {
  if (offlineBanner) return;
  offlineBanner = document.createElement('div');
  offlineBanner.className = 'offline-banner animate-fade-in';
  offlineBanner.innerHTML = `
    <span class="offline-banner__pulse"></span>
    <span class="offline-banner__text">Sin conexión — mostrando datos guardados</span>
    <button class="btn btn--ghost btn--sm" onclick="location.reload()">Reintentar</button>
  `;
  const content = document.querySelector('.page-content') || document.body;
  content.insertAdjacentElement('beforebegin', offlineBanner);
}

function hideOfflineBanner() {
  if (!offlineBanner) return;
  offlineBanner.remove();
  offlineBanner = null;
}

window.addEventListener('offline', showOfflineBanner);
window.addEventListener('online',  () => { hideOfflineBanner(); PT.toast('Conexión restaurada', { type: 'success' }); });

// Show banner immediately if already offline on page load
if (!navigator.onLine) {
  document.addEventListener('DOMContentLoaded', showOfflineBanner);
}

/* ══════════════════════════════════════════════════════════ */
/* 3. BOTTOM SHEET                                            */
/* ══════════════════════════════════════════════════════════ */

class BottomSheet {
  constructor(id) {
    this.sheet    = document.getElementById(id);
    this.backdrop = document.getElementById(id + '-backdrop');
    this.isOpen   = false;
    this._startY  = 0;
    this._currentY = 0;
    if (!this.sheet) return;
    this._bindEvents();
  }

  open() {
    if (!this.sheet) return;
    document.body.style.overflow = 'hidden';
    this.backdrop?.classList.add('is-open');
    this.sheet.classList.add('is-open');
    this.isOpen = true;
    this.sheet.dispatchEvent(new CustomEvent('sheet:open'));
  }

  close() {
    if (!this.sheet) return;
    document.body.style.overflow = '';
    this.backdrop?.classList.remove('is-open');
    this.sheet.classList.remove('is-open');
    this.isOpen = false;
    this.sheet.dispatchEvent(new CustomEvent('sheet:close'));
  }

  toggle() { this.isOpen ? this.close() : this.open(); }

  _bindEvents() {
    // Close button
    this.sheet.querySelectorAll('[data-sheet-close]')
      .forEach(el => el.addEventListener('click', () => this.close()));

    // Backdrop click
    this.backdrop?.addEventListener('click', () => this.close());

    // ESC key
    document.addEventListener('keydown', e => {
      if (e.key === 'Escape' && this.isOpen) this.close();
    });

    // Drag-to-close (touch)
    const handle = this.sheet.querySelector('.sheet__handle');
    if (handle) {
      handle.addEventListener('touchstart', e => {
        this._startY = e.touches[0].clientY;
      }, { passive: true });

      handle.addEventListener('touchmove', e => {
        this._currentY = e.touches[0].clientY - this._startY;
        if (this._currentY > 0) {
          this.sheet.style.transform = `translateY(${this._currentY}px)`;
          this.sheet.style.transition = 'none';
        }
      }, { passive: true });

      handle.addEventListener('touchend', () => {
        this.sheet.style.transition = '';
        if (this._currentY > 100) {
          this.close();
        } else {
          this.sheet.style.transform = '';
        }
        this._currentY = 0;
      });
    }
  }
}

/* ══════════════════════════════════════════════════════════ */
/* 4. PWA INSTALL PROMPT                                      */
/* ══════════════════════════════════════════════════════════ */

let deferredInstallPrompt = null;
const INSTALL_DISMISSED_KEY = 'pt-install-dismissed';
const INSTALL_VISIT_KEY     = 'pt-visit-count';

window.addEventListener('beforeinstallprompt', e => {
  e.preventDefault();
  deferredInstallPrompt = e;
  maybeShowInstallPrompt();
});

function maybeShowInstallPrompt() {
  if (sessionStorage.getItem(INSTALL_DISMISSED_KEY)) return;
  if (window.matchMedia('(display-mode: standalone)').matches) return;

  const visits = parseInt(localStorage.getItem(INSTALL_VISIT_KEY) || '0') + 1;
  localStorage.setItem(INSTALL_VISIT_KEY, visits);

  // Show after 2nd visit
  if (visits >= 2) {
    setTimeout(showInstallPrompt, 4000);
  }
}

function showInstallPrompt() {
  if (!deferredInstallPrompt) return;
  const prompt = document.getElementById('install-prompt');
  if (!prompt) return;
  prompt.classList.add('is-open');
}

function dismissInstallPrompt() {
  const prompt = document.getElementById('install-prompt');
  prompt?.classList.remove('is-open');
  sessionStorage.setItem(INSTALL_DISMISSED_KEY, '1');
}

async function triggerInstall() {
  if (!deferredInstallPrompt) return;
  deferredInstallPrompt.prompt();
  const { outcome } = await deferredInstallPrompt.userChoice;
  deferredInstallPrompt = null;
  dismissInstallPrompt();
  if (outcome === 'accepted') {
    PT.toast('App instalada correctamente', { type: 'success' });
  }
}

window.addEventListener('appinstalled', () => {
  deferredInstallPrompt = null;
  dismissInstallPrompt();
});

/* ══════════════════════════════════════════════════════════ */
/* 5. BOTTOM NAV — ACTIVE STATE                               */
/* ══════════════════════════════════════════════════════════ */

function syncBottomNavActive() {
  const path  = location.pathname;
  const items = document.querySelectorAll('.bottom-nav__item[data-href]');
  items.forEach(item => {
    const href = item.dataset.href;
    const active = href === '/'
      ? path === '/'
      : path.startsWith(href);
    item.classList.toggle('active', active);
    item.setAttribute('aria-current', active ? 'page' : 'false');
  });
}

document.addEventListener('DOMContentLoaded', syncBottomNavActive);

/* ══════════════════════════════════════════════════════════ */
/* 6. NUMPAD — AMOUNT ENTRY                                   */
/* ══════════════════════════════════════════════════════════ */

class AmountNumpad {
  constructor({ displayEl, inputEl, onConfirm } = {}) {
    this.display   = typeof displayEl === 'string' ? document.querySelector(displayEl) : displayEl;
    this.input     = typeof inputEl   === 'string' ? document.querySelector(inputEl)   : inputEl;
    this.onConfirm = onConfirm || (() => {});
    this._raw      = '';
    if (this.display) this._render();
  }

  press(key) {
    if (key === 'backspace') {
      this._raw = this._raw.slice(0, -1);
    } else if (key === 'confirm') {
      this.onConfirm(this._getValue());
      return;
    } else if (key === '.') {
      if (this._raw.includes('.')) return;
      this._raw += this._raw === '' ? '0.' : '.';
    } else {
      if (this._raw.length >= 12) return;
      this._raw += key;
    }
    this._render();
    if (this.input) this.input.value = this._getValue();
  }

  reset() { this._raw = ''; this._render(); }

  _getValue() {
    return this._raw === '' ? '0' : this._raw;
  }

  _render() {
    if (!this.display) return;
    const val = this._raw || '0';
    const parts = val.split('.');
    const intPart   = parseInt(parts[0] || '0', 10).toLocaleString('es-CR');
    const decPart   = parts.length > 1 ? '.' + parts[1] : '';
    this.display.textContent = intPart + decPart;
    this.display.classList.toggle('is-empty', this._raw === '');
  }
}

/* ══════════════════════════════════════════════════════════ */
/* 7. TOAST SYSTEM                                            */
/* ══════════════════════════════════════════════════════════ */

const PT = window.PT || {};
window.PT = PT;

PT.toast = function(message, { type = 'neutral', duration = 4000, action } = {}) {
  let region = document.querySelector('.toast-region');
  if (!region) {
    region = document.createElement('div');
    region.className = 'toast-region';
    document.body.appendChild(region);
  }

  const toast = document.createElement('div');
  toast.className = `toast toast--${type}`;

  const icons = { success: '✓', danger: '✕', neutral: 'ℹ', warning: '⚠' };
  toast.innerHTML = `
    <span class="toast__icon">${icons[type] ?? icons.neutral}</span>
    <span class="toast__text">${message}</span>
    ${action ? `<button class="btn btn--ghost btn--sm toast__action">${action.label}</button>` : ''}
    <button class="toast__close" aria-label="Cerrar">×</button>
  `;

  if (action) {
    toast.querySelector('.toast__action').addEventListener('click', () => {
      action.fn();
      remove();
    });
  }

  const remove = () => {
    toast.style.opacity = '0';
    toast.style.transform = 'translateY(8px)';
    toast.style.transition = 'opacity .2s, transform .2s';
    setTimeout(() => toast.remove(), 220);
  };

  toast.querySelector('.toast__close').addEventListener('click', remove);
  region.appendChild(toast);

  if (duration > 0) setTimeout(remove, duration);

  return { remove };
};

/* ══════════════════════════════════════════════════════════ */
/* 8. PAGE TRANSITIONS                                        */
/* ══════════════════════════════════════════════════════════ */

document.addEventListener('DOMContentLoaded', () => {
  // Animate page content in
  const content = document.querySelector('.page-content');
  if (content) {
    content.style.opacity = '0';
    content.style.transform = 'translateY(8px)';
    requestAnimationFrame(() => {
      content.style.transition = 'opacity .25s ease-out, transform .25s ease-out';
      content.style.opacity    = '';
      content.style.transform  = '';
    });
  }

  // Activate bottom nav
  syncBottomNavActive();

  // Wire numpad keys via data attributes
  document.querySelectorAll('[data-numpad-target]').forEach(pad => {
    const targetId  = pad.dataset.numpadTarget;
    const displayId = pad.dataset.numpadDisplay;
    const display   = document.getElementById(displayId);
    const input     = document.getElementById(targetId);
    if (!display && !input) return;

    const numpad = new AmountNumpad({ displayEl: display, inputEl: input });
    pad.querySelectorAll('.numpad__key').forEach(key => {
      key.addEventListener('click', () => numpad.press(key.dataset.key));
    });
  });

  // Wire bottom sheet triggers
  document.querySelectorAll('[data-sheet]').forEach(trigger => {
    const id    = trigger.dataset.sheet;
    const sheet = new BottomSheet(id);
    trigger.addEventListener('click', () => sheet.open());
    // Expose instance on element for external control
    trigger._sheet = sheet;
  });

  // Wire install prompt buttons
  document.querySelectorAll('[data-install-trigger]')
    .forEach(el => el.addEventListener('click', triggerInstall));
  document.querySelectorAll('[data-install-dismiss]')
    .forEach(el => el.addEventListener('click', dismissInstallPrompt));

  // Auto-dismiss flash messages after 5s
  document.querySelectorAll('.flash').forEach(flash => {
    setTimeout(() => {
      flash.style.transition = 'opacity .3s, transform .3s';
      flash.style.opacity    = '0';
      flash.style.transform  = 'translateX(16px)';
      setTimeout(() => flash.remove(), 320);
    }, 5000);
  });
});

/* ══════════════════════════════════════════════════════════ */
/* 9. OFFLINE MUTATION QUEUE (exposed for Razor Pages)        */
/* ══════════════════════════════════════════════════════════ */

PT.queueMutation = async function(url, method, data) {
  if (!('indexedDB' in window)) return false;
  try {
    const db = await openQueueDB();
    const tx = db.transaction('queue', 'readwrite');
    tx.objectStore('queue').add({
      url,
      method,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
      queuedAt: Date.now(),
    });
    await txComplete(tx);

    if ('serviceWorker' in navigator && 'sync' in ServiceWorkerRegistration.prototype) {
      const reg = await navigator.serviceWorker.ready;
      await reg.sync.register('pt-sync-transactions');
    }
    return true;
  } catch (err) {
    console.warn('[PT] Queue failed:', err);
    return false;
  }
};

function openQueueDB() {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open('pt-sync', 1);
    req.onupgradeneeded = e => {
      const db = e.target.result;
      if (!db.objectStoreNames.contains('queue'))
        db.createObjectStore('queue', { keyPath: 'id', autoIncrement: true });
    };
    req.onsuccess = e => resolve(e.target.result);
    req.onerror   = e => reject(e.target.error);
  });
}
function txComplete(tx) {
  return new Promise((resolve, reject) => {
    tx.oncomplete = resolve;
    tx.onerror    = () => reject(tx.error);
  });
}
