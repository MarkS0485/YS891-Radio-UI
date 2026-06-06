# YS891-Radio-UI — Test Checklist

## ✅ Final pass results (built-in simulator, 2026-06-06)

Everything below was exercised end-to-end via UI automation against `--sim`; screenshots in `screenshots\`, gallery in `README-plus.md`:

- **Panel**: live S-meter (S1↔S9+40 pulsing with the band model), tune, mode, LOCK/SPL, MULTI — ✅
- **Scanner**: SWEEP complete + VFO restore ✅ · FIND CHATTER found RAG-CHEW @ 14.250 S9+30, click-to-tune ✅ · HEATMAP at ±12.5k/±50k/±250k, stripes + max-hold, cancel + restore ✅
- **Memory**: write-with-tag ch 5, read, GO TO, QMB store/recall, READ ALL ✅
- **CW**: TUNE TO BEACON + DECODE → *"vvv vvv this is ys891 = calling any station…"* (shorthand expanded), stop/restore ✅
- **Meters**: live polling all 7 types, radio status read ✅
- **Tests tab**: **60/60 function tiles green**, INITIALIZE works ✅
- **Console**: raw `ID;` send, wire trace ✅
- **Audio Lab**: WASAPI loopback captured playing audio → waveform/spectrum/waterfall all live ✅
- **Disco**: lights/floor/marquee, beat-reactive with loopback ✅
- Fixed during the pass: ToggleButtons now fire on `Checked`/`Unchecked` (UIA/accessibility-safe), SPAN label showed "±12k" for 12.5 kHz, heatmap grid upped 60→100 points/pass (coarser grids stepped over narrow stations)

Remaining items are the 🔶 real-hardware ones (COM port, BAND/MX on the rig, TX into a dummy load) — see the bench session at the bottom.

> **Layout note (v3):** the app is now tabbed — PANEL / AUDIO LAB / SCANNER / MEMORY / CW & MSG / METERS / FUNCTIONS / TESTS / CONSOLE. See `COVERAGE.md` for where every library command lives. New untested areas: **MEMORY** (channel ops, read-all, QMB), **CW & MSG** (send CW, keyer + voice memories, zero-in), **METERS** (live bars for all 7 meter types, radio status, VFO B), **TESTS** (heatmap walks every read command: blue→yellow→green/red at 100 ms cadence), **CONSOLE** (raw CAT, wire trace, delay/retry tuning), and **audio loopback** ("System audio (what you hear)" — the default device — analyses whatever Windows is playing; mic devices remain available).

Status legend: ✅ verified against the built-in simulator during development (2026-06-06) · ⬜ still to test · 🔶 needs real hardware or external setup.

## Build & launch

- ✅ `dotnet build YS891-Radio-UI.slnx` — zero warnings, zero errors (TreatWarningsAsErrors on)
- ✅ NuGet-only consumption: FT891.Core 2.0.0 + FT891.Simulator 2.0.0 (no project references)
- ✅ App launches; `--sim` flag auto-connects to the built-in simulator
- ⬜ Launch on a machine with only .NET Framework 4.8 (no SDK) — copy `bin\Debug\net48\` output

## Connection

- ✅ Built-in simulator option: spins up `SimulatorServer` on an ephemeral port in-process, connects, populates display within one poll (~250 ms)
- ✅ Disconnect: monitor stopped, port/simulator released, status shows "Disconnected."
- ✅ Settings persistence: last connection choice saved to `user.config`, pre-selected on next launch
- ⬜ External TCP option against a separately running `FT891.Simulator.exe` (default 127.0.0.1:4000)
- 🔶 Serial option against the real FT-891 (USB CAT, 38400 baud — must match the radio's CAT RATE menu)
- ⬜ Connect dialog validation: no COM port selected, bad host/port values
- ⬜ Reconnect after disconnect (and connect-while-connected swaps cleanly)
- ⬜ Resilience: kill the external simulator mid-session → status-bar error (MonitorError), no crash, reconnect recovers
- ⬜ Close the window while connected → clean shutdown, no lingering process

## Display (LCD)

- ✅ Frequency renders via `FrequencyFormat.ToFormattedString` (e.g. `14.250.000`), live from first poll
- ✅ Mode, AGC, STEP, memory channel (`ch 1`) indicators
- ✅ LOCK / SPLIT indicators appear and clear with state
- ✅ S-meter bar + `FormatSMeter` label (S0 against quiet simulator)
- 🔶 S-meter ramp (green→amber→red) with real signals / simulator SignalSource
- 🔶 TX indicator + red lamp: driven by `TransmitChanged` (radio truth). **Simulator does not implement the MX command, so TX never lights against the sim — test on the real rig.**

## Tuning

- ✅ Mouse wheel over the main dial: one detent per notch, frequency steps by the selected STEP size (verified +5/−3 × 100 Hz, exact final value — coalescing works, no wire flooding)
- ✅ Drag the dial round: angle-tracked detents (verified by hand)
- ✅ Optimistic display: digits follow the hand instantly; monitor echoes don't fight back (no flicker)
- ✅ STEP key cycles 10 Hz / 100 Hz / 1 kHz / 10 kHz
- ✅ LOCK blocks dial input (wheel did not change frequency while locked)
- ⬜ Fast continuous spin (stress: dozens of detents/sec, final value must match the dial)
- ⬜ Frequency clamps at receiver limits (30 kHz / 56 MHz)

## Buttons

- ✅ MODE cycles LSB→USB→CW→AM→FM→DATA-USB (verified USB→CW→AM)
- ✅ SPL toggles split, indicator follows
- ✅ AGC cycle command accepted (label cycles AUTO/FAST/MID/SLOW)
- ✅ NB / NR toggles accepted by simulator
- ✅ Buttons disabled until connected, re-enable on connect
- 🔶 BAND ▲/▼: **simulator does not implement BU/BD — frequency won't move against the sim.** Test on the real rig.
- ⬜ A/B (swap VFOs) and A▶B (copy) — verify VFO B actually changes (read back via simulator state or rig)

## MULTI knob

- ✅ Wheel/drag adjusts the selected parameter (AF 80 → 96 with +4 detents, step 4)
- ✅ Click cycles AF → RF → MIC → SQL → PWR and re-reads the current value from the radio
- ⬜ Values clamp at `FT891Ranges` limits (e.g. AF at 0/255, PWR at 5/100)

## MOX / TX safety

- ✅ Single press only ARMS (status: "press again within 3 s"), does **not** key the radio
- ✅ Auto-disarm after 3 s if not confirmed
- ✅ Second press keys (SetMox true), third press unkeys
- 🔶 TX lamp/indicator tracks the radio's real TX state (needs real rig — see above)
- ⬜ MOX disabled while disconnected and during a sweep

## Scope / Sweep

- ✅ LIVE mode: scrolling S-meter history strip while connected
- ✅ SWEEP: banner ("SWEEPING — RECEIVE INTERRUPTED"), monitor paused, spectrum drawn across VFO ±50 kHz at 1 kHz steps
- ✅ Sweep completes → "Sweep complete.", VFO restored to the exact pre-sweep frequency
- ✅ Esc (and CANCEL button) aborts mid-sweep → "Sweep cancelled.", VFO still restored
- ✅ Scope holds the sweep result ~8 s, then falls back to LIVE
- 🔶 Sweep over real band activity (or simulator `SignalSource`) draws actual peaks
- ⬜ Dial/MODE/MOX controls disabled during sweep

## Functions panel (F key)

- ✅ Opens with a live CAT link, reads current values from the radio (verified: TX power, proc levels, VOX gain/delay, key speed populated from the simulator)
- ⬜ Each toggle applies immediately: preamp, attenuator, IF shift, clarifier, speech processor, VOX, keyer, CW spot
- ⬜ Each slider applies (coalesced) and clamps to `FT891Ranges`: NR/NB level, TX power, VOX gain/delay, key speed, pitch, semi break-in delay, proc input/output
- ⬜ Break-in and tuner combos apply. **Tuner START keys the radio — dummy load or clear frequency on the real rig.**
- ⬜ INITIALIZE (calibrate timing): reports the settled inter-command delay; sweep/tuning feel snappier afterwards on fast links
- ⬜ RUN FUNCTION TESTS: grid fills with one (Function, Result) row per read command; summary counts problems
- ⬜ Window closes cleanly; reopening after disconnect/reconnect gets the new CAT link

## Audio views (VIEW key cycles LIVE → WAVE → SPEC → FALL)

> Audio comes from a Windows input device (the rig's USB audio codec, line-in, or any microphone for testing) — CAT cannot carry audio.

- ⬜ Device combo lists input devices; AUDIO toggle starts/stops capture; status bar confirms
- ⬜ WAVE: oscilloscope trace follows the channel audio (talk into a mic to test)
- ⬜ SPEC: bar analyzer responds 0–5 kHz, red peak-hold caps fall slowly
- ⬜ FALL: waterfall scrolls, blue→red colormap tracks loudness; whistle = a bright vertical line that moves with pitch
- ⬜ AUDIO off → WAVE shows the idle hint; views freeze harmlessly
- ⬜ Audio keeps running across radio disconnect/reconnect (independent subsystems)

## Span / Sweep / Find Chatter

- ⬜ SPAN cycles ±12.5k / ±50k / ±250k / ±1M; sweep status message reflects the span; step scales (~100 points per scan)
- ⬜ CHTR scans the span, then lists up to 12 active frequencies strongest-first (needs signals: real band, or simulator `SignalSource`)
- ⬜ Clicking a chatter hit tunes the VFO to it and closes the list
- ⬜ "No chatter above the noise floor" message on a quiet span
- ⬜ Esc / CANCEL aborts a chatter scan; VFO restored

## Disco mode

- ✅ PLAY HIT SINGLE opens the correct video (field-verified, apparently)
- ⬜ DISCO toggle: lights spawn/flash at ~7 Hz, strobe wash alternates, marquee scrolls
- ⬜ With AUDIO running: lights pump with the channel audio level
- ⬜ Esc exits disco mode; panel still fully operable underneath (it's an overlay — buttons are covered while it's on, that's authentic)

## Scanners against the built-in simulator (fixed — band activity now wired)

> The built-in simulator now has an ether: six 20 m stations (FT8 14.070 / NCDXF BCN 14.100 / DX SSB 14.195 / SSTV 14.230 / RAG-CHEW 14.250 / MARITIME 14.313, several duty-cycled) plus a 16 wpm CW beacon at **14.058**. `BusyThreshold` = 55.

- ⬜ Connect (built-in sim) at 14.250.000 → S-meter pulses to ~S9 most of the time (RAG-CHEW, 80% duty); LIVE scope dances
- ⬜ SWEEP ±50k at 14.250 → peaks at 14.230 / 14.250 / 14.313
- ⬜ Tune 14.150, SPAN ±250k, SWEEP → all six stations visible
- ⬜ FIND CHATTER → strong stations listed (uses S-meter peaks **and** the radio's busy flag); click a hit → tunes there, S-meter jumps
- ⬜ HEATMAP → rows accumulate; duty-cycled stations show as dashed vertical stripes; max-hold trace fills in on top; Esc stops; VFO restored
- ⬜ CW & MSG → TUNE TO BEACON → DECODE → readable text appears (beacon keys "VVV VVV DE YS891 … CQ CQ CQ …"), key lamp blinks, RAW shows dits/dahs; STOP restores the monitor
- ⬜ All of the above on real hardware: sweep/chatter/heatmap are pure CAT (no simulator knowledge); Morse decoder works on any strong CW signal

## Disco mode 2.0

- ⬜ Lights/floor tiles slam on audio beats (energy-spike detector) when capture is running
- ⬜ ♫ LOAD TRACK plays an MP3/WAV through the speakers, auto-starts loopback capture, lights dance to it; ■ stops; track pauses when disco closes and resumes on reopen
- ⬜ Dance floor tiles shuffle continuously, full recolor on each beat

## Known simulator limitations (not app bugs)

| Behaviour | Cause | Test on |
|---|---|---|
| BAND ▲/▼ doesn't move frequency | Sim has no `BU`/`BD` handler | Real FT-891 |
| TX lamp never lights | Sim has no `MX` handler; `IF` frame TX flag never set | Real FT-891 |

## Real-hardware session (when the rig is on the bench)

1. Radio menu: CAT RATE 38400, CAT TOT sensible; USB cable in, note the COM port.
2. Connect via Serial — display should populate in ~250 ms.
3. Spin the physical VFO knob on the radio → app display follows (radio-side changes accepted by echo guards).
4. Tune from the app → radio display follows; no lag/fighting.
5. BAND ▲/▼, MODE from the app → radio follows.
6. MOX arm+confirm into a dummy load → TX lamp + radio keys; unkey works. **Dummy load. Check power (MULTI → PWR) first.**
7. Sweep across a live band → spectrum shows real signals; VFO restored; audio resumes.
