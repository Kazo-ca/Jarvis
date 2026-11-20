// Drawing of the light orb (UI catalog, section 3). Only draws: the speed and the pulses come from C#
// (OrbMotion), and no rule about the assistant state is written here (ADR 0005, section 5).
//
// Cost (ADR 0011, section 5): the rings rotate with the Web Animations API on `transform`, which the browser
// runs on prepared layers, with no script per frame. At speed 0 the animations are paused: the orb is a
// still image, with no timer and no requestAnimationFrame (ADR 0005, section 6). A speed change eases over
// about 0.6 s with requestAnimationFrame, then the loop stops.

// [selector, milliseconds per turn at speed 1, direction] (UI catalog, section 3).
const RINGS = [
    [".orb-ring-inner", 24000, 1],
    [".orb-ring-middle", 36000, -1],
    [".orb-ring-outer", 60000, 1],
];

const reducedQuery = typeof matchMedia === "function" ? matchMedia("(prefers-reduced-motion: reduce)") : null;
const orbs = new WeakMap();

export function attach(root, rate, pulses, reduced) {
    if (!root || orbs.has(root)) {
        return;
    }

    const state = {
        root,
        rings: RINGS.map(([selector, duration, direction]) => {
            const element = root.querySelector(selector);
            const animation = element.animate(
                [{ transform: "rotate(0deg)" }, { transform: `rotate(${360 * direction}deg)` }],
                { duration, iterations: Infinity });
            animation.pause();
            return { element, animation };
        }),
        rate: 0,
        target: 0,
        pulses: false,
        reducedOption: false,
        frame: 0,
        timers: [],
        onMediaChange: () => apply(state),
    };
    orbs.set(root, state);
    reducedQuery?.addEventListener("change", state.onMediaChange);
    update(root, rate, pulses, reduced);
}

export function update(root, rate, pulses, reduced) {
    const state = orbs.get(root);
    if (!state) {
        return;
    }

    state.target = Math.max(0, Number(rate) || 0);
    state.pulses = Boolean(pulses);
    state.reducedOption = Boolean(reduced);
    apply(state);
}

export function setLevel(root, level) {
    root?.style.setProperty("--orb-level", String(Math.min(1, Math.max(0, Number(level) || 0))));
}

export function detach(root) {
    const state = orbs.get(root);
    if (!state) {
        return;
    }

    orbs.delete(root);
    reducedQuery?.removeEventListener("change", state.onMediaChange);
    cancelAnimationFrame(state.frame);
    stopPulses(state);
    state.rings.forEach(ring => ring.animation.cancel());
}

function isReduced(state) {
    return state.reducedOption || Boolean(reducedQuery?.matches);
}

function apply(state) {
    const reduced = isReduced(state);
    state.root.dataset.motion = reduced ? "reduced" : "full";
    const target = reduced ? 0 : state.target;

    stopPulses(state);
    if (state.pulses && !reduced) {
        state.rings.forEach((_, index) => schedulePulse(state, index, Math.random() * 300));
    }

    if (reduced) {
        // No movement at all, not even easing to a stop.
        state.rate = 0;
        cancelAnimationFrame(state.frame);
        state.frame = 0;
        setRate(state, 0);
        return;
    }

    if (!state.frame && state.rate !== target) {
        state.frame = requestAnimationFrame(() => ramp(state));
    }
}

function ramp(state) {
    const target = isReduced(state) ? 0 : state.target;
    state.rate += (target - state.rate) * 0.12;
    if (Math.abs(target - state.rate) < 0.01) {
        state.rate = target;
    }

    setRate(state, state.rate);
    state.frame = state.rate === target ? 0 : requestAnimationFrame(() => ramp(state));
}

function setRate(state, rate) {
    state.rings.forEach(({ animation }) => {
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

// Speaking, light version: each ring pulses on its own, at random (scale and opacity, compositor only).
function schedulePulse(state, index, delay) {
    state.timers[index] = setTimeout(() => {
        const svg = state.rings[index].element.querySelector("svg");
        const grow = 0.025 + Math.random() * 0.045;
        svg?.animate(
            [
                { transform: "scale(1)", opacity: 0.8 },
                { transform: `scale(${1 + grow})`, opacity: 1, offset: 0.3 },
                { transform: "scale(1)", opacity: 0.8 },
            ],
            { duration: 220 + Math.random() * 260, easing: "ease-out" });
        schedulePulse(state, index, 140 + Math.random() * 520);
    }, delay);
}

function stopPulses(state) {
    state.timers.forEach(clearTimeout);
    state.timers.length = 0;
}
