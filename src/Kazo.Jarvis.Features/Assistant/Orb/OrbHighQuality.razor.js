// Drawing of the high-quality orb (UI catalog, section 2). Only draws: the speed comes from C# (OrbMotion),
// the color and the light from the style sheet of the enclosing `.orb`, and no rule about the assistant
// state is written here (ADR 0005, section 5).
//
// Cost (ADR 0011, sections 5 and 8):
// - The five rings and the turning gyroscope are Web Animations on `transform`: the browser runs them on
//   prepared layers, with no script per frame. At speed 0 they are paused.
// - The canvas (plasma sphere and binary clouds) is the only thing drawn by script, at 30 images per second
//   at most (ADR 0005, section 5), and only while something changes: a rotation, a speed change, or the
//   0.7 s fade of a change of look. Otherwise the loop stops: the orb is a still image, with no timer and no
//   requestAnimationFrame (ADR 0005, section 6). `data-loop` on the root says which, for the tests.
// - The 0 and 1 of the clouds are small images prepared once per color and size, not text drawn each time.

const FRAME_MS = 1000 / 30;
const FADE_MS = 700;

// The whole gyroscope turns on its vertical axis at 0.18 rad/s at speed 1, and rocks on its horizontal axis
// between -0.5 and -0.1 rad (prototype: rotateX(-0.3 + 0.2 sin(0.7 θ))).
const WORLD_TURN_MS = (2 * Math.PI / 0.18) * 1000;
const WOBBLE_MS = 25000;

const reducedQuery = typeof matchMedia === "function" ? matchMedia("(prefers-reduced-motion: reduce)") : null;
const orbs = new WeakMap();

// Seeded random numbers, so the sphere is the same at every start (prototype: Park–Miller, seed 7).
function generator(seed) {
    let value = seed;
    return () => (value = (value * 16807) % 2147483647) / 2147483647;
}

// 18 filaments: great circles of the sphere, deformed by a sinusoid (UI catalog, section 2).
const FILAMENTS = (() => {
    const random = generator(7);
    return [...Array(18)].map(() => {
        const a = random() * Math.PI * 2;
        const b = Math.acos(2 * random() - 1);
        const n = [Math.sin(b) * Math.cos(a), Math.sin(b) * Math.sin(a), Math.cos(b)];
        let u = Math.abs(n[0]) < 0.9 ? [1, 0, 0] : [0, 1, 0];
        const d = u[0] * n[0] + u[1] * n[1] + u[2] * n[2];
        u = [u[0] - d * n[0], u[1] - d * n[1], u[2] - d * n[2]];
        const l = Math.hypot(...u);
        u = u.map(x => x / l);
        const v = [n[1] * u[2] - n[2] * u[1], n[2] * u[0] - n[0] * u[2], n[0] * u[1] - n[1] * u[0]];
        return { u, v, phase: random() * 6.28, lobes: 2 + Math.floor(random() * 3), speed: 0.4 + random() * 0.8, alpha: 0.25 + random() * 0.35 };
    });
})();

// Value noise for the clouds (prototype, mode 5).
const NOISE = (() => {
    const random = generator(3);
    return [...Array(512)].map(() => random());
})();

function hash(x, y) {
    return NOISE[((x & 63) * 7 + (y & 63) * 131) & 511];
}

function valueNoise(x, y) {
    const xi = Math.floor(x), yi = Math.floor(y);
    const fx = x - xi, fy = y - yi;
    const sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
    const a = hash(xi, yi), b = hash(xi + 1, yi), c = hash(xi, yi + 1), d = hash(xi + 1, yi + 1);
    return a + (b - a) * sx + (c - a) * sy + (a - b - c + d) * sx * sy;
}

function fractal(x, y) {
    return valueNoise(x, y) * 0.55 + valueNoise(x * 2.1 + 5, y * 2.1) * 0.3 + valueNoise(x * 4.3, y * 4.3 + 9) * 0.15;
}

// Clouds: 42 latitudes × 96 longitudes, axis tilted by 0.38 rad (UI catalog, section 2).
const LATITUDES = 42;
const LONGITUDES = 96;
const TILT = 0.38;
const GLYPH_SIZES = 8;

export function attach(root, rate, pulses, reduced) {
    if (!root || orbs.has(root)) {
        return;
    }

    const canvas = root.querySelector(".hq-sphere");
    const spins = [...root.querySelectorAll(".hq-spin")].map((element, index) => {
        const turn = Number(element.dataset.turn) || 30000;
        const direction = Number(element.dataset.direction) || 1;
        const animation = element.animate(
            [{ transform: "rotate(0deg)" }, { transform: `rotate(${360 * direction}deg)` }],
            { duration: turn, iterations: Infinity });

        // Each ring starts at its own angle, like the prototype; fixed, so every start looks the same.
        animation.currentTime = (turn * ((index * 0.37) % 1));
        animation.pause();
        return animation;
    });

    const world = root.querySelector(".hq-world")?.animate(
        [{ transform: "rotateY(0rad)" }, { transform: `rotateY(${2 * Math.PI}rad)` }],
        { duration: WORLD_TURN_MS, iterations: Infinity });
    const wobble = root.querySelector(".hq-wobble")?.animate(
        [{ transform: "rotateX(-0.5rad)" }, { transform: "rotateX(-0.1rad)" }],
        { duration: WOBBLE_MS, iterations: Infinity, direction: "alternate", easing: "ease-in-out" });
    if (wobble) {
        wobble.currentTime = WOBBLE_MS / 2;
    }

    const animations = [...spins, world, wobble].filter(Boolean);
    animations.forEach(animation => animation.pause());

    const state = {
        root,
        orb: root.closest(".orb"),
        canvas,
        context: canvas?.getContext("2d") ?? null,
        animations,
        appliedRate: 0,
        rate: 0,
        target: 0,
        reducedOption: false,
        time: 3,
        spin: 0,
        level: 0,
        levelTarget: 0,
        color: null,
        light: 0.85,
        glyphs: null,
        glyphKey: "",
        frame: 0,
        lastFrame: 0,
        lastDraw: 0,
        fadeUntil: 0,
        onMediaChange: () => apply(state),
        resize: null,
    };

    if (typeof ResizeObserver === "function" && canvas) {
        state.resize = new ResizeObserver(() => {
            fitCanvas(state);
            draw(state, 0);
        });
        state.resize.observe(canvas);
    }

    orbs.set(root, state);
    reducedQuery?.addEventListener("change", state.onMediaChange);
    fitCanvas(state);
    update(root, rate, pulses, reduced);
}

export function update(root, rate, pulses, reduced) {
    const state = orbs.get(root);
    if (!state) {
        return;
    }

    state.target = Math.max(0, Number(rate) || 0);
    state.reducedOption = Boolean(reduced);

    // Called on a change of look as well: let the color and the light fade, then stop.
    state.fadeUntil = performance.now() + FADE_MS;
    apply(state);
}

export function setLevel(root, level) {
    const value = Math.min(1, Math.max(0, Number(level) || 0));
    const state = orbs.get(root);
    state?.orb?.style.setProperty("--orb-level", String(value));
    if (state) {
        state.levelTarget = value;
    }
}

export function detach(root) {
    const state = orbs.get(root);
    if (!state) {
        return;
    }

    orbs.delete(root);
    reducedQuery?.removeEventListener("change", state.onMediaChange);
    state.resize?.disconnect();
    cancelAnimationFrame(state.frame);
    state.animations.forEach(animation => animation.cancel());
}

function isReduced(state) {
    return state.reducedOption || Boolean(reducedQuery?.matches);
}

function apply(state) {
    const reduced = isReduced(state);
    if (state.orb) {
        state.orb.dataset.motion = reduced ? "reduced" : "full";
    }

    if (reduced) {
        // No movement at all, not even easing to a stop. The color still fades: it is light, not motion.
        state.rate = 0;
        setRate(state, 0);
    }

    start(state);
}

function start(state) {
    if (!state.frame) {
        state.lastFrame = 0;
        state.root.dataset.loop = "running";
        state.frame = requestAnimationFrame(now => loop(state, now));
    }
}

function loop(state, now) {
    state.frame = 0;

    // 30 images per second at most (ADR 0005, section 5).
    if (state.lastDraw && now - state.lastDraw < FRAME_MS - 1) {
        state.frame = requestAnimationFrame(next => loop(state, next));
        return;
    }

    const dt = Math.min(0.1, state.lastFrame ? (now - state.lastFrame) / 1000 : 0);
    state.lastFrame = now;
    state.lastDraw = now;

    const target = isReduced(state) ? 0 : state.target;
    state.rate += (target - state.rate) * Math.min(1, dt * 8);
    if (Math.abs(target - state.rate) < 0.002) {
        state.rate = target;
    }

    setRate(state, state.rate);
    state.time += dt * Math.max(state.rate, 0.0001);
    state.spin += dt * state.rate * 0.35;
    state.level += (state.levelTarget - state.level) * Math.min(1, dt * 1.5);
    draw(state, state.level);

    const moving = state.rate > 0 || state.rate !== target;
    if (moving || now < state.fadeUntil) {
        state.frame = requestAnimationFrame(next => loop(state, next));
        return;
    }

    // Nothing moves any more: one last image, then no loop at all.
    draw(state, 0);
    state.root.dataset.loop = "stopped";
}

function setRate(state, rate) {
    if (Math.abs(rate - state.appliedRate) < 0.001 && (rate === 0) === (state.appliedRate === 0)) {
        return;
    }

    state.appliedRate = rate;
    state.animations.forEach(animation => {
        if (rate === 0) {
            animation.pause();
        } else {
            animation.updatePlaybackRate(rate);
            if (animation.playState !== "running") {
                animation.play();
            }
        }
    });
}

function fitCanvas(state) {
    const canvas = state.canvas;
    if (!canvas) {
        return;
    }

    const ratio = Math.min(2, devicePixelRatio || 1);
    const side = Math.max(1, Math.round(canvas.getBoundingClientRect().width * ratio));
    if (canvas.width !== side) {
        canvas.width = side;
        canvas.height = side;
        state.glyphKey = "";
    }
}

// Reads the color and the light of the look from the style sheet (registered, so computed and fading).
function readStyle(state) {
    const style = state.orb ? getComputedStyle(state.orb) : null;
    const color = style?.getPropertyValue("--orb-color") ?? "";
    const match = color.match(/[\d.]+/g);
    if (match && match.length >= 3) {
        state.color = match.slice(0, 3).map(Number);
    } else if (/^#[0-9a-f]{6}$/i.test(color.trim())) {
        const n = parseInt(color.trim().slice(1), 16);
        state.color = [n >> 16, (n >> 8) & 255, n & 255];
    } else {
        state.color ??= [95, 230, 255];
    }

    const light = parseFloat(style?.getPropertyValue("--orb-light") ?? "");
    if (Number.isFinite(light)) {
        state.light = light;
    }
}

// The 0 and 1 in GLYPH_SIZES sizes, in the cloud color, prepared once per color and canvas size.
function glyphsFor(state, radius, rgb) {
    const key = `${radius}|${rgb.join(",")}`;
    if (state.glyphKey === key && state.glyphs) {
        return state.glyphs;
    }

    const family = getComputedStyle(state.root).getPropertyValue("--jarvis-font-mono") || "monospace";
    state.glyphs = [...Array(GLYPH_SIZES)].map((_, index) => {
        const depth = (index + 0.5) / GLYPH_SIZES;
        const size = Math.max(1, radius * 0.055 * (0.35 + 0.65 * depth));
        return ["0", "1"].map(character => {
            const sprite = document.createElement("canvas");
            sprite.width = Math.ceil(size * 0.8) + 2;
            sprite.height = Math.ceil(size * 1.2) + 2;
            const context = sprite.getContext("2d");
            context.font = `500 ${size}px ${family}`;
            context.textAlign = "center";
            context.textBaseline = "middle";
            context.fillStyle = `rgb(${rgb[0]},${rgb[1]},${rgb[2]})`;
            context.fillText(character, sprite.width / 2, sprite.height / 2);
            return sprite;
        });
    });
    state.glyphKey = key;
    return state.glyphs;
}

function draw(state, level) {
    const context = state.context;
    if (!context) {
        return;
    }

    // Reading the style costs a style pass: only while the look fades (or the voice moves the light).
    if (!state.color || performance.now() < state.fadeUntil || state.levelTarget > 0 || level > 0.001) {
        readStyle(state);
    }
    const side = context.canvas.width;
    const cx = side / 2, cy = side / 2;
    const R = side / 2.2;
    const energy = state.light;
    const [r, g, b] = state.color.map(Math.round);
    const C = alpha => `rgba(${r},${g},${b},${alpha})`;
    const ratio = Math.min(2, devicePixelRatio || 1);

    context.setTransform(1, 0, 0, 1, 0, 0);
    context.globalCompositeOperation = "source-over";
    context.clearRect(0, 0, side, side);

    // Body: darker and more transparent so the clouds show (UI catalog, section 2).
    const k = 0.45;
    let gradient = context.createRadialGradient(cx - R * 0.25, cy - R * 0.3, R * 0.05, cx, cy, R);
    gradient.addColorStop(0, `rgba(255,255,255,${0.95 * k})`);
    gradient.addColorStop(0.18, C(0.9 * k));
    gradient.addColorStop(0.6, C(0.35 * k));
    gradient.addColorStop(0.92, C(0.12 * k));
    gradient.addColorStop(1, C(0.55));
    context.beginPath();
    context.arc(cx, cy, R, 0, Math.PI * 2);
    context.fillStyle = gradient;
    context.fill();

    // Filaments, additive and faint.
    context.save();
    context.beginPath();
    context.arc(cx, cy, R * 0.98, 0, Math.PI * 2);
    context.clip();
    context.globalCompositeOperation = "lighter";
    const cosY = Math.cos(state.spin), sinY = Math.sin(state.spin), cosX = Math.cos(0.45), sinX = Math.sin(0.45);
    const t = state.time;
    context.lineWidth = ratio;
    for (const filament of FILAMENTS) {
        context.beginPath();
        for (let i = 0; i <= 90; i++) {
            const theta = i / 90 * Math.PI * 2;
            const radius = 0.5 + 0.38 * Math.sin(filament.lobes * theta + filament.phase + t * filament.speed * (0.5 + energy)) * energy;
            const cos = Math.cos(theta), sin = Math.sin(theta);
            const x = (cos * filament.u[0] + sin * filament.v[0]) * radius;
            const y = (cos * filament.u[1] + sin * filament.v[1]) * radius;
            const z = (cos * filament.u[2] + sin * filament.v[2]) * radius;
            const x2 = x * cosY + z * sinY, z2 = -x * sinY + z * cosY;
            const y2 = y * cosX - z2 * sinX;
            const px = cx + x2 * R, py = cy + y2 * R;
            if (i) {
                context.lineTo(px, py);
            } else {
                context.moveTo(px, py);
            }
        }

        context.strokeStyle = `rgba(${Math.min(255, r + 90)},${Math.min(255, g + 60)},${Math.min(255, b + 40)},${filament.alpha * energy * 0.7 * 0.4})`;
        context.stroke();
    }

    context.restore();
    drawClouds(state, context, cx, cy, R, t + 3, [Math.min(255, r + 120), Math.min(255, g + 60), Math.min(255, b + 30)]);

    // Bright rim and a small white core; the voice raises the core's light, never the size (ADR 0011).
    context.globalCompositeOperation = "lighter";
    gradient = context.createRadialGradient(cx, cy, R * 0.82, cx, cy, R * 1.04);
    gradient.addColorStop(0, C(0));
    gradient.addColorStop(0.8, C(0.55));
    gradient.addColorStop(1, C(0));
    context.fillStyle = gradient;
    context.beginPath();
    context.arc(cx, cy, R * 1.05, 0, Math.PI * 2);
    context.fill();

    gradient = context.createRadialGradient(cx, cy, 0, cx, cy, R * 0.45);
    gradient.addColorStop(0, `rgba(255,255,255,${(0.55 + level * 0.35) * 0.25})`);
    gradient.addColorStop(1, C(0));
    context.fillStyle = gradient;
    context.beginPath();
    context.arc(cx, cy, R * 0.45, 0, Math.PI * 2);
    context.fill();
    context.globalCompositeOperation = "source-over";
}

// Binary planet clouds: latitude bands drifting at different speeds, the front face only, smaller and
// fainter towards the edge (UI catalog, section 2; prototype, mode 5).
function drawClouds(state, context, cx, cy, R, t, rgb) {
    const glyphs = glyphsFor(state, R, rgb);
    const cosT = Math.cos(TILT), sinT = Math.sin(TILT);

    context.save();
    context.beginPath();
    context.arc(cx, cy, R * 0.97, 0, Math.PI * 2);
    context.clip();
    context.globalCompositeOperation = "lighter";

    for (let i = 0; i < LATITUDES; i++) {
        const latitude = -1.45 + i * (2.9 / (LATITUDES - 1));
        const cosL = Math.cos(latitude), sinL = Math.sin(latitude);
        const band = Math.sin(latitude * 5) * 0.5;
        const drift = t * (0.18 + band * 0.12);
        const ny = (latitude + 1.6) * 5.5 + band * 2;
        for (let j = 0; j < LONGITUDES; j++) {
            const longitude = j / LONGITUDES * Math.PI * 2;
            const turned = longitude + t * 0.12;
            const x = cosL * Math.sin(turned);
            const z = cosL * Math.cos(turned);
            const z2 = sinL * sinT + z * cosT;
            if (z2 < 0.02) {
                continue;
            }

            const nx = ((longitude - drift) / (Math.PI * 2)) * 16;
            let density = fractal(((nx % 16) + 16) % 16, ny);
            density = (density - 0.4) * 2.6;
            if (density <= 0) {
                continue;
            }

            const alpha = Math.min(1, Math.min(1, density) * Math.pow(z2, 0.8) * 0.75 * 1.7);
            const y2 = sinL * cosT - z * sinT;
            const bit = (Math.imul(i * 977 + j * 131 + Math.floor(t * 1.5 + (i + j) * 0.37), 2654435761 | 0) >>> 11) & 1;
            const sprite = glyphs[Math.min(GLYPH_SIZES - 1, Math.floor(z2 * GLYPH_SIZES))][bit];
            context.globalAlpha = alpha;
            context.drawImage(sprite, cx + x * R * 0.94 - sprite.width / 2, cy - y2 * R * 0.94 - sprite.height / 2);
        }
    }

    context.globalAlpha = 1;
    context.restore();
    context.globalCompositeOperation = "source-over";
}
