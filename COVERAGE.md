# YS891-Radio-UI — FT891.Core Coverage Map

Every public `ICatInterface` member, and where the app exercises it. The TESTS tab additionally calls **every read command** in one heatmap run.

## Connection / engine
| Member | UI home |
|---|---|
| `Connect` / `Disconnect` / `IsConnected` | CONNECT / DISCONNECT buttons (Serial / TCP / built-in simulator) |
| `InterCommandDelayMs` | CONSOLE → Engine tuning slider (also set by INITIALIZE) |
| `TimeoutRetryCount` | CONSOLE → Engine tuning slider |
| `LastCommand` / `LastResponse` / `LastResponseBytes` | CONSOLE → raw result + "show last response bytes (hex)" |
| `FrameSent` / `FrameReceived` | CONSOLE → live wire trace |
| `InitializeLibraryAsync` | TESTS → INITIALIZE (calibrate timing) |
| `RunDiagnosticAsync` | TESTS → "Lib Diagnostic" tile |
| `SendRawCommandAsync` | CONSOLE → raw CAT command (+ "Raw ID;" tile in TESTS) |

## VFO / frequency / band
| Member | UI home |
|---|---|
| `Get/SetVfoAFrequencyAsync` | PANEL main dial (coalesced) + frequency display; sweeps |
| `Get/SetVfoBFrequencyAsync` | METERS → VFO B read/set |
| `CopyVfoAToVfoBAsync` / `CopyVfoBToVfoAAsync` / `SwapVfosAsync` | PANEL soft keys A▶B / B▶A / A/B |
| `BandUpAsync` / `BandDownAsync` | PANEL BAND ▲/▼ |
| `SelectBandAsync` | PANEL band combo (left cluster) |
| `FrequencyUpAsync` / `FrequencyDownAsync` | PANEL ◂ STEP / STEP ▸ under the dial |
| `ZeroInAsync` | CW & MSG → ZERO-IN |

## Mode / RX settings
| Member | UI home |
|---|---|
| `Get/SetModeAsync` | PANEL MODE key (cycles) |
| `Get/SetAgcModeAsync` | PANEL AGC key (cycles) |
| `Get/SetAfGain/RfGain/MicGain/SqlLevelAsync` | PANEL MULTI knob (AF / RF / MIC / SQL) |
| `Get/SetMonitorLevelAsync` | FUNCTIONS → Receiver → Monitor level |
| `Get/SetRfAttenuatorAsync` | FUNCTIONS → Attenuator |
| `Get/SetPreampAsync` | FUNCTIONS → Preamp |
| `Get/SetNoiseReductionAsync` (+level) | PANEL NR key; FUNCTIONS NR level |
| `Get/SetNoiseBlankerAsync` (+level) | PANEL NB key; FUNCTIONS NB level |
| `Get/SetAutoNotchAsync` | FUNCTIONS → DSP → Auto notch |
| `Get/SetManualNotchAsync` | FUNCTIONS → DSP → Manual notch + frequency |
| `Get/SetContourAsync` | FUNCTIONS → DSP → Contour on/freq/width |
| `Get/SetBandwidthAsync` | FUNCTIONS → DSP → IF bandwidth |
| `Get/SetIfShiftAsync` | FUNCTIONS → Receiver → IF shift |
| `Get/SetClarifierAsync` + `ClarifierUp/Down/ClearAsync` | FUNCTIONS → Receiver → Clarifier + nudge buttons |
| `Get/SetLockAsync` | PANEL LOCK key (also blocks the dial) |
| `Get/SetFastStepAsync` | FUNCTIONS → System → Fast step |

## TX
| Member | UI home |
|---|---|
| `IsTransmittingAsync` / `SetMoxAsync` | PANEL MOX (armed two-press gate) + TX lamp |
| `Get/SetSplitAsync` | PANEL SPL key + SPLIT indicator |
| `Get/SetTxPowerAsync` | PANEL MULTI (PWR) + FUNCTIONS TX power |
| `Get/SetSpeechProcessorAsync` (+levels in/out) | FUNCTIONS → Transmitter |
| `Get/SetVoxAsync` (+gain, delay) | FUNCTIONS → Transmitter |
| `Get/SetTunerStateAsync` | FUNCTIONS → Antenna tuner (Off/On/Start) |
| `Get/SetPowerAsync` | FUNCTIONS → POWER OFF RADIO (confirmed); Power tile in TESTS reads it |

## Memory
| Member | UI home |
|---|---|
| `Get/SetMemoryChannelAsync` | MEMORY → channel box + GO TO CHANNEL (display shows `ch N`) |
| `ChannelUpDownAsync` | MEMORY → CH ▲/▼ |
| `ReadMemoryAsync` | MEMORY → READ CHANNEL + READ ALL list |
| `WriteMemoryAsync` | MEMORY → WRITE VFO▶MEM (with tag) |
| `CopyVfoAToMemoryAsync` / `CopyMemoryToVfoAAsync` | MEMORY → quick copy buttons (and double-click recall) |
| `StoreToQuickMemoryBankAsync` / `RecallQuickMemoryBankAsync` | MEMORY → QMB STORE / RECALL |

## CW / messages
| Member | UI home |
|---|---|
| `SendCwAsync` | CW & MSG → SEND |
| `Get/SetKeySpeedAsync`, `Get/SetKeyPitchAsync` | FUNCTIONS → CW *(moved with the keyer group)* |
| `Get/SetBreakInModeAsync`, `Get/SetSemiBreakInDelayAsync` | FUNCTIONS → CW |
| `Get/SetCwSpotAsync`, `Get/SetKeyerAsync` | FUNCTIONS → CW |
| `Get/SetKeyerMemoryAsync` (slots 1–5) | CW & MSG → keyer memory rows |
| `LoadMessageAsync` / `PlaybackAsync` (slots 1–5) | CW & MSG → voice message LOAD / PLAYBACK |

## Repeater / scan
| Member | UI home |
|---|---|
| `Get/SetCtcssAsync` + `Get/SetCtcssDcsNumberAsync` | FUNCTIONS → Transmitter → CTCSS + tone # |
| `Get/SetOffsetAsync` | FUNCTIONS → Transmitter → Repeater offset |
| `Get/SetScanModeAsync` | FUNCTIONS → System → Scan combo |

## Meters / status
| Member | UI home |
|---|---|
| `GetSMeterAsync` | PANEL S-meter (via RadioMonitor) + sweeps/chatter |
| `ReadMeterAsync` (all 7 `MeterType`s) | METERS → live meter bars |
| `GetBusyAsync` | METERS → BUSY indicator |
| `GetRadioInfoAsync` / `GetRadioIdAsync` / `GetOppositeVfoInfoAsync` | METERS → READ RADIO STATUS |
| `Get/SetDimmerAsync` | FUNCTIONS → System → Dimmer |
| `Get/SetAutoInformationAsync` | FUNCTIONS → System → Auto information |

## Support types exercised
`RadioMonitor` (live panel), `FrequencyFormat` (display/parsing), `MeterScale` (S-meter/SWR/watts), `FT891Ranges` (every slider's min/max/clamp), `IntRange`, all enums, `TcpCatTransport` + `SerialPortTransport` (connect picker), `FT891Exception` (every error path → status bar), `CatSpec` (indirectly via `SendRawCommandAsync` auto response length).

From **FT891.Simulator** (built-in simulator mode): `SimulatorServer` (+ `SignalSource`, `BusyThreshold`), `RadioState` (seeded VFO/mode), `Morse.CwBeacon` (14.058 MHz, 16 wpm, decodable), `Morse.MorseDecoder` + `Morse.MorseCode.UnitMs` + `Morse.MorseAbbreviations.Expand` (the CW tab's live decoder — pure CAT, works on real signals too). `BandModel` is ported from FT891.Demo into `Services\BandModel.cs` (it isn't packaged) to give the simulated band its six stations.

Note: `GetBusyAsync` is additionally exercised per-step by FIND CHATTER (`SweepService` `includeBusy`).
