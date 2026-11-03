/* A small, self-contained instrument. All sound is synthesized locally. */
(() => {
  'use strict';
  const root = document.getElementById('playable-piano');
  if (!root) return;

  const notes = [
    { midi: 60, name: 'C4', key: 'A', code: 'KeyA', white: true },
    { midi: 61, name: 'C sharp 4', key: 'W', code: 'KeyW', slot: 1 },
    { midi: 62, name: 'D4', key: 'S', code: 'KeyS', white: true },
    { midi: 63, name: 'D sharp 4', key: 'E', code: 'KeyE', slot: 2 },
    { midi: 64, name: 'E4', key: 'D', code: 'KeyD', white: true },
    { midi: 65, name: 'F4', key: 'F', code: 'KeyF', white: true },
    { midi: 66, name: 'F sharp 4', key: 'T', code: 'KeyT', slot: 4 },
    { midi: 67, name: 'G4', key: 'G', code: 'KeyG', white: true },
    { midi: 68, name: 'G sharp 4', key: 'Y', code: 'KeyY', slot: 5 },
    { midi: 69, name: 'A4', key: 'H', code: 'KeyH', white: true },
    { midi: 70, name: 'A sharp 4', key: 'U', code: 'KeyU', slot: 6 },
    { midi: 71, name: 'B4', key: 'J', code: 'KeyJ', white: true },
    { midi: 72, name: 'C5', key: 'K', code: 'KeyK', white: true }
  ];
  const byMidi = new Map(notes.map(note => [note.midi, note]));
  const byCode = new Map(notes.map(note => [note.code, note]));
  const buttons = new Map();
  const active = new Map();
  const pointers = new Map();
  let context = null;
  let audioReady = false;
  let message = root.querySelector('[data-piano-status]');
  const board = document.createElement('div');
  board.className = 'lp-piano-board';
  board.setAttribute('role', 'group');
  board.setAttribute('aria-label', 'Playable piano keyboard');
  const whiteKeys = document.createElement('div');
  whiteKeys.className = 'lp-piano-whites';
  board.appendChild(whiteKeys);
  if (!message) {
    message = document.createElement('p');
    message.dataset.pianoStatus = '';
    message.className = 'lp-piano-status';
    message.setAttribute('role', 'status');
    message.textContent = 'Tap a key or use your keyboard to play.';
  }

  function say(text) { if (message) message.textContent = text; }
  function startVoice(midi) {
    const voice = active.get(midi);
    if (!voice || voice.gain || !audioReady || !context || context.state !== 'running') return;
    const now = context.currentTime;
    const gain = context.createGain();
    const oscillator = context.createOscillator();
    const overtone = context.createOscillator();
    const overtoneGain = context.createGain();
    const frequency = 440 * Math.pow(2, (midi - 69) / 12);
    oscillator.type = 'triangle';
    oscillator.frequency.setValueAtTime(frequency, now);
    overtone.type = 'sine';
    overtone.frequency.setValueAtTime(frequency * 2, now);
    overtoneGain.gain.setValueAtTime(.12, now);
    gain.gain.setValueAtTime(.0001, now);
    gain.gain.exponentialRampToValueAtTime(.11, now + .015);
    gain.gain.exponentialRampToValueAtTime(.035, now + .6);
    oscillator.connect(gain);
    overtone.connect(overtoneGain);
    overtoneGain.connect(gain);
    gain.connect(context.destination);
    oscillator.start(now);
    overtone.start(now);
    voice.gain = gain;
    voice.oscillators = [oscillator, overtone];
  }
  function stopVoice(voice) {
    if (!voice.gain || !context) return;
    const now = context.currentTime;
    const parameter = voice.gain.gain;
    if (typeof parameter.cancelAndHoldAtTime === 'function') parameter.cancelAndHoldAtTime(now);
    else { parameter.cancelScheduledValues(now); parameter.setValueAtTime(Math.max(.0001, parameter.value), now); }
    parameter.exponentialRampToValueAtTime(.0001, now + .12);
    voice.oscillators.forEach(oscillator => oscillator.stop(now + .14));
    setTimeout(() => { voice.gain.disconnect(); voice.oscillators.forEach(oscillator => oscillator.disconnect()); }, 250);
  }
  async function enableAudio() {
    const Audio = window.AudioContext || window.webkitAudioContext;
    if (!Audio) { say('Audio is unavailable in this browser.'); return false; }
    try {
      if (!context) context = new Audio();
      if (context.state !== 'running') await context.resume();
      audioReady = context.state === 'running';
      if (!audioReady) { say('Tap a piano key to enable sound.'); return false; }
      say('Sound is on. Play with the keys or tap the piano.');
      active.forEach((_, midi) => startVoice(midi));
      return true;
    } catch (_) { audioReady = false; say('Sound could not start. Try tapping a key again.'); return false; }
  }
  function press(midi, source = 'demo') {
    if (!byMidi.has(midi)) return;
    let voice = active.get(midi);
    if (!voice) { voice = { sources: new Set(), gain: null, oscillators: [] }; active.set(midi, voice); }
    if (voice.sources.has(source)) return;
    voice.sources.add(source);
    buttons.get(midi).classList.add('is-active');
    buttons.get(midi).setAttribute('aria-pressed', 'true');
    startVoice(midi);
    window.dispatchEvent(new CustomEvent('larpiano:note', { detail: { note: midi, active: true, source } }));
  }
  function release(midi, source = 'demo') {
    const voice = active.get(midi);
    if (!voice || !voice.sources.delete(source)) return;
    if (voice.sources.size === 0) {
      stopVoice(voice);
      active.delete(midi);
      buttons.get(midi).classList.remove('is-active');
      buttons.get(midi).setAttribute('aria-pressed', 'false');
    }
    window.dispatchEvent(new CustomEvent('larpiano:note', { detail: { note: midi, active: voice.sources.size > 0, source } }));
  }
  function allOff(source) {
    [...active.entries()].forEach(([midi, voice]) => {
      [...voice.sources].forEach(owner => { if (!source || source === owner) release(midi, owner); });
    });
  }
  function userPress(midi, source) {
    press(midi, source);
    void enableAudio();
  }

  notes.forEach(note => {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = `lp-piano-key ${note.white ? 'lp-piano-white' : 'lp-piano-black'}`;
    button.dataset.note = String(note.midi);
    button.setAttribute('aria-label', `${note.name}, keyboard ${note.key}`);
    button.setAttribute('aria-pressed', 'false');
    if (note.slot) button.style.setProperty('--slot', String(note.slot * 12.5) + '%');
    const label = document.createElement('span');
    label.className = 'lp-piano-label';
    label.textContent = note.key;
    label.setAttribute('aria-hidden', 'true');
    button.appendChild(label);
    (note.white ? whiteKeys : board).appendChild(button);
    buttons.set(note.midi, button);
    button.addEventListener('pointerdown', event => {
      if (event.button !== 0 && event.pointerType === 'mouse') return;
      event.preventDefault();
      const owner = `pointer:${event.pointerId}`;
      pointers.set(event.pointerId, note.midi);
      button.setPointerCapture(event.pointerId);
      userPress(note.midi, owner);
    });
    const endPointer = event => {
      const midi = pointers.get(event.pointerId);
      if (midi === undefined) return;
      pointers.delete(event.pointerId);
      release(midi, `pointer:${event.pointerId}`);
    };
    button.addEventListener('pointerup', endPointer);
    button.addEventListener('pointercancel', endPointer);
    button.addEventListener('lostpointercapture', endPointer);
    button.addEventListener('keydown', event => {
      if (event.key !== ' ' && event.key !== 'Enter') return;
      if (event.repeat) return;
      event.preventDefault();
      userPress(note.midi, `button:${note.midi}`);
    });
    button.addEventListener('keyup', event => {
      if (event.key !== ' ' && event.key !== 'Enter') return;
      event.preventDefault();
      release(note.midi, `button:${note.midi}`);
    });
    button.addEventListener('blur', () => release(note.midi, `button:${note.midi}`));
  });
  root.prepend(board);
  if (!message.isConnected) root.appendChild(message);

  function isEditing(target) {
    return target instanceof Element && (target.isContentEditable || !!target.closest('input, textarea, select, [contenteditable="true"]'));
  }
  document.addEventListener('keydown', event => {
    const note = byCode.get(event.code);
    if (!note || event.repeat || event.ctrlKey || event.metaKey || event.altKey || isEditing(event.target)) return;
    if (event.target instanceof Element && event.target.closest('a, button, summary') && !root.contains(event.target)) return;
    event.preventDefault();
    userPress(note.midi, `keyboard:${event.code}`);
  });
  document.addEventListener('keyup', event => {
    const note = byCode.get(event.code);
    if (note) release(note.midi, `keyboard:${event.code}`);
  });
  window.addEventListener('blur', () => { pointers.clear(); allOff(); });
  document.addEventListener('visibilitychange', () => { if (document.hidden) { pointers.clear(); allOff(); } });
  window.larpianoPiano = { enableAudio, press, release, allOff };
  window.dispatchEvent(new CustomEvent('larpiano:piano-ready'));
})();
