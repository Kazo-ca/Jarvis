// Orb prototype for spike E6: Canvas 2D rings, frame rate capped by ?fps=, state chosen by ?state=.
// Idle draws slow breathing rings; speaking fakes a voice level so the cost of a busy state is visible too.
const params = new URLSearchParams(location.search);
const fps = Number(params.get('fps') || 30);
const state = params.get('state') || 'idle';
const canvas = document.getElementById('orb');
const ctx = canvas.getContext('2d');
const c = canvas.width / 2;
let last = 0;

function level(t) { return state === 'speaking' ? 0.5 + 0.5 * Math.abs(Math.sin(t / 90) * Math.sin(t / 210)) : 0; }

function draw(t) {
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  const breath = 0.5 + 0.5 * Math.sin(t / 1600);
  const lv = level(t);
  const glow = ctx.createRadialGradient(c, c, 4, c, c, 70 + 25 * lv);
  glow.addColorStop(0, `rgba(120, 220, 255, ${0.55 + 0.3 * breath})`);
  glow.addColorStop(1, 'rgba(0, 120, 255, 0)');
  ctx.fillStyle = glow;
  ctx.beginPath(); ctx.arc(c, c, 70 + 25 * lv, 0, Math.PI * 2); ctx.fill();
  for (let i = 0; i < 3; i++) {
    const r = 48 + i * 18 + 4 * breath + 12 * lv;
    const a0 = t / (2400 + i * 900) * (i % 2 ? -1 : 1);
    ctx.strokeStyle = `rgba(140, 230, 255, ${0.35 + 0.2 * breath - i * 0.08})`;
    ctx.lineWidth = 2;
    ctx.beginPath(); ctx.arc(c, c, r, a0, a0 + Math.PI * 1.4); ctx.stroke();
  }
}

function loop(t) {
  // Low rates use a timer: an idle requestAnimationFrame loop still wakes the renderer 60 times a second.
if (fps > 10) requestAnimationFrame(loop); else if (fps > 0) setInterval(() => draw(performance.now()), 1000 / fps); else draw(0);
  if (t - last < 1000 / fps - 1) return; // cap the frame rate: the browser still wakes at 60 Hz, we only draw at fps
  last = t;
  draw(t);
}
// Low rates use a timer: an idle requestAnimationFrame loop still wakes the renderer 60 times a second.
if (fps > 10) requestAnimationFrame(loop); else if (fps > 0) setInterval(() => draw(performance.now()), 1000 / fps); else draw(0); // fps=0: one still frame, measures the bare cost of the host
