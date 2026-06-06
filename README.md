[![CI](https://github.com/MarkS0485/YS891-Radio-UI/actions/workflows/ci.yml/badge.svg)](https://github.com/MarkS0485/YS891-Radio-UI/actions/workflows/ci.yml)

# YS891-Radio-UI

A **skeuomorphic Windows front panel** for the **Yaesu FT-891** HF/50 MHz
transceiver — a WPF desktop app driven entirely by the
[FT891.Core](https://github.com/MarkS0485/FT891-Interface) CAT library.

> Target framework: **net48** (WPF) · Status: **early scaffold** — the panel
> chrome (bezel, LCD, knobs, buttons) is being built out.

---

## What it is

A virtual front panel: tune, change mode, set power and watch the S-meter from
your desktop, against either a **real FT-891** over USB or the
[virtual radio simulator](https://github.com/MarkS0485/FT891-Interface/tree/main/FT891.Simulator)
— the same code path drives both.

Built on the v2.0.0 library features designed for exactly this kind of app:

| Library feature | What the panel uses it for |
|---|---|
| `RadioMonitor` | Live readouts — frequency, mode, TX lamp, S-meter — via change events marshalled to the UI thread |
| `FT891Exception` | One catch for every radio failure; the message goes straight to the status bar |
| `CancellationToken` support | Clean shutdown — no hung waits when the window closes |
| `FrequencyFormat` / `MeterScale` | The dial readout ("14.250.000") and the S-meter scale ("S9+20") |
| `FT891Ranges` | Knob and slider limits that match what the radio actually accepts |
| `KeyingPort` | PTT via the radio's second USB COM port (RTS), hardware only |

## Build & run

Requires the **.NET SDK** on Windows (net48 builds via the
`Microsoft.NETFramework.ReferenceAssemblies` package — no full Visual Studio
install needed).

```bash
dotnet build YS891.RadioUI/YS891.RadioUI.csproj
dotnet run --project YS891.RadioUI
```

No radio? Run the simulator from the
[FT891-Interface](https://github.com/MarkS0485/FT891-Interface) repo and point
the panel at `127.0.0.1:4000`.

## Layout

```
YS891-Radio-UI.slnx
└── YS891.RadioUI/              the WPF app (net48)
    ├── App.xaml(.cs)           entry point; dispatcher-level error net
    ├── MainWindow.xaml(.cs)    the panel
    └── Themes/                 Colors, Buttons, Knobs resource dictionaries
```

## License

Released under the **GNU General Public License v3.0 (or later)** — see
[LICENSE](LICENSE), same as the FT891-Interface library it builds on.
