(() => {
  'use strict';
  const root = document.querySelector('[data-larp-demo]');
  if (!root) return;
  const reduced = window.matchMedia('(prefers-reduced-motion: reduce)');
  const pitches = [60, 62, 64, 65, 67, 69, 71, 72];
  // Original short demonstration phrase; times are seconds from the start.
  const phrase = [
    [0.7, 60, 0.40], [1.2, 64, 0.40], [1.7, 67, 0.65],
    [2.5, 69, 0.35], [3.0, 67, 0.58], [3.8, 64, 0.42],
    [4.4, 65, 0.42], [5.0, 69, 0.42], [5.0, 72, 0.42],
    [5.7, 67, 0.50], [6.4, 60, 0.75], [6.4, 64, 0.75]
  ].map(([time, pitch, duration]) => ({time, pitch, duration}));
  const duration = 7.4;
  const lead = 1.7;
  root.classList.add('larp-demo');
  root.innerHTML = `
    <div class="larp-demo-head">
      <div><h2>A few notes, in motion.</h2><p>A short original example shows how notes become piano keys.</p></div>
      <div class="larp-demo-controls"><button type="button" data-demo-toggle>Pause</button><button type="button" class="larp-demo-sound" data-demo-sound>Play with sound</button></div>
    </div>
    <p class="larp-demo-status" data-demo-status role="status" aria-live="polite"></p>
    <div class="larp-demo-flow" aria-hidden="true"><span>Notes in a MIDI</span><span>Keys on a piano</span></div>
    <div class="larp-demo-stage" aria-label="Illustration of notes moving toward piano keys" role="img"><div class="larp-demo-hitline"></div></div>
    <div class="larp-demo-keys" aria-hidden="true"></div>
    <p class="larp-demo-caption">An illustration only. The website does not control Roblox.</p>`;
  const toggle = root.querySelector('[data-demo-toggle]');
  const sound = root.querySelector('[data-demo-sound]');
  const status = root.querySelector('[data-demo-status]');
  const stage = root.querySelector('.larp-demo-stage');
  const keyboard = root.querySelector('.larp-demo-keys');
  const notes = phrase.map(item => {
    const node = document.createElement('span');
    node.className = 'larp-demo-note';
    node.style.left = `${pitches.indexOf(item.pitch) * 12.5 + 1}%`;
    node.style.width = '10.5%';
    stage.append(node);
    return node;
  });
  const keys = pitches.map((pitch, index) => {
    const node = document.createElement('div');
    node.className = 'larp-demo-key';
    node.innerHTML = `<span>${['C','D','E','F','G','A','B','C'][index]}</span>`;
    keyboard.append(node);
    return node;
  });
  let playing = !reduced.matches;
  let soundOn = false;
  let elapsed = 0;
  let lastFrame = null;
  let frame = 0;
  let sounding = new Set();
  let stopped = false;
  const player = () => window.larpianoPiano;
  function updateControls(message) {
    toggle.textContent = playing ? 'Pause' : elapsed >= duration ? 'Replay' : 'Play';
    sound.textContent = soundOn ? 'Mute' : 'Play with sound';
    if (message) status.textContent = message;
  }
  function silence() {
    if (player()) player().allOff('demo');
    sounding.clear();
  }
  function draw() {
    const visible = new Set();
    const stageHeight = stage.clientHeight;
    phrase.forEach((item, index) => {
      const active = elapsed >= item.time && elapsed < item.time + item.duration;
      if (active) visible.add(item.pitch);
      const distance = (elapsed - (item.time - lead)) / lead;
      const show = distance >= 0 && distance < 1;
      const y = reduced.matches ? stageHeight * .48 : Math.max(0, distance * (stageHeight - 14));
      notes[index].style.opacity = show ? '1' : '0';
      notes[index].style.transform = `translateY(${y}px)`;
    });
    keys.forEach((key, index) => key.classList.toggle('is-active', visible.has(pitches[index])));
    if (playing && soundOn && player()) {
      for (const pitch of sounding) if (!visible.has(pitch)) player().release(pitch, 'demo');
      for (const pitch of visible) if (!sounding.has(pitch)) player().press(pitch, 'demo');
      sounding = visible;
    }
  }
  function tick(now) {
    if (!playing || stopped) { frame = 0; return; }
    if (lastFrame !== null) elapsed = Math.min(duration, elapsed + Math.min((now - lastFrame) / 1000, .1));
    lastFrame = now;
    draw();
    if (elapsed >= duration) {
      playing = false; silence(); lastFrame = null;
      updateControls(soundOn ? 'Example finished. Select Replay to hear it again.' : 'Silent example finished. Select Play with sound to hear it.');
      frame = 0; return;
    }
    frame = requestAnimationFrame(tick);
  }
  function start() {
    if (elapsed >= duration) elapsed = 0;
    playing = true; lastFrame = null; draw();
    updateControls(soundOn ? 'Playing the example with sound.' : 'Playing a silent example.');
    if (!frame) frame = requestAnimationFrame(tick);
  }
  function pause(message) {
    playing = false; lastFrame = null; silence();
    if (frame) cancelAnimationFrame(frame);
    frame = 0; draw(); updateControls(message || 'Example paused.');
  }
  async function enableSound() {
    const instrument = player();
    if (!instrument || typeof instrument.enableAudio !== 'function') {
      updateControls('Sound is unavailable in this browser. The visual demo can still play.');
      return false;
    }
    try {
      const result = await instrument.enableAudio();
      if (result === false) throw new Error('Audio unavailable');
      soundOn = true; return true;
    } catch {
      soundOn = false;
      updateControls('Sound could not start. Check your browser audio settings.');
      return false;
    }
  }
  toggle.addEventListener('click', async () => {
    if (playing) { pause(); return; }
    if (soundOn && !await enableSound()) return;
    start();
  });
  sound.addEventListener('click', async () => {
    if (soundOn) {soundOn = false; silence(); updateControls('Sound muted.'); return;}
    if (!await enableSound()) return;
    elapsed = 0; silence();
    start();
  });
  document.addEventListener('visibilitychange', () => {if (document.hidden && playing) pause('Example paused while this tab was in the background.');});
  window.addEventListener('pagehide', () => {stopped = true; silence(); if (frame) cancelAnimationFrame(frame);});
  window.addEventListener('pageshow', event => {
    if (!event.persisted) return;
    stopped = false;
    frame = 0;
    pause('Example paused. Select Play to continue.');
  });
  updateControls(playing ? 'A silent example plays once. Choose Play with sound to hear it.' : 'Select Play with sound to hear the example.');
  draw();
  if (playing) frame = requestAnimationFrame(tick);
})();
