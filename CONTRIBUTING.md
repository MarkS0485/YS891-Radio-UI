# Contributing to YS891-Radio-UI

**Anyone is welcome to contribute** — bug fixes, new CAT commands, simulator
improvements, tests, documentation, or just a well-described bug report. There's
no gatekeeping here. If it makes the library better, it's welcome.

Please be civil; this project follows the [Contributor Covenant](CODE_OF_CONDUCT.md).

---

## Send patches, not file replacements

This is the one thing we ask you to take seriously.

**Every change should be the smallest diff that does the job.** Prefer focused,
incremental **patches** over wholesale file replacements.

Concretely, a good contribution:

- **Touches only the lines it needs to.** Don't rewrite or replace a whole file
  to change a few lines.
- **Doesn't reformat untouched code.** No drive-by whitespace, brace, or
  import reshuffles in code you aren't actually changing — they bury the real
  change and make review painful.
- **Does one thing.** Keep unrelated changes in separate commits or separate
  pull requests. A reviewer should be able to read the diff and see *exactly*
  what changed and *why*.
- **Reads as a sequence of small commits** rather than one giant one, where it
  makes sense.

This isn't bureaucracy — it's how the whole codebase is built. Adding a CAT
command is *one* `CatSpec` table entry plus *one* one-line method, not surgery.
Patches that respect that stay easy to review and quick to merge.

> Prefer the classic patch workflow? `git format-patch` / `git diff` patches are
> perfectly welcome too. However you send it, the same rule applies: minimal,
> focused, reviewable.

---

## Getting set up

The projects are SDK-style and pull in
`Microsoft.NETFramework.ReferenceAssemblies`, so you don't need a full Visual
Studio install — the .NET SDK is enough.

```bash
dotnet build FT891.sln          # builds Core, Simulator, and Tests
dotnet test  FT891.sln          # runs the full xUnit suite (132 tests at time of writing)
```

> **Target framework is `net48`.** Building is cross-platform via the reference
> assemblies, but the test suite runs on .NET Framework, so running tests is
> happiest on **Windows**. CI runs them on `windows-latest`.

---

## Making a change

1. **Fork** the repo (or branch, if you have access) off `main`.
2. Make your focused change, **with tests** for any behaviour change.
3. Run `dotnet test FT891.sln` and make sure it's green locally.
4. Open a **pull request against `main`**. CI (build + the xUnit suite on net48)
   runs automatically on your PR — keep it green.
5. Small, single-purpose PRs get reviewed and merged fastest.

---

## What a good change looks like here

A little architecture context, so your patch fits the grain of the code:

- **`CatSpec` is the single source of truth.** Every command maps to its exact
  reply length. A new command is a table entry plus a one-line method that
  builds a frame and slices the value out of a fixed-width reply. Keep that
  pattern — don't reintroduce runtime branching that the table exists to avoid.
- **Everything is testable over loopback.** The client depends only on
  `ICatTransport`, so the same code drives a real radio (serial) or the
  in-process simulator (TCP). New behaviour should be verifiable against the
  simulator — add a round-trip test rather than relying on hardware.
- **net48 without compromise.** Modern C# (records, target-typed `new`) is fine;
  it just has to compile on .NET Framework 4.8 via the existing `IsExternalInit`
  shim. Don't pull in dependencies that drop net48 support.
- **Cover your change with tests** — spec consistency, protocol format, or a
  simulator round-trip, whichever fits.

---

## Bugs, features, and security

- **Bugs and features:** please use the
  [issue templates](.github/ISSUE_TEMPLATE). A bug report with the frame you
  sent and the reply you got is worth a hundred words.
- **Security:** do **not** open a public issue for a vulnerability — follow
  [SECURITY.md](SECURITY.md) instead.

---

## Licensing of contributions

This project is licensed under the **GNU GPL v3.0 (or later)** — see
[LICENSE](LICENSE). By submitting a contribution, you agree that it is licensed
under the same terms.

Thanks for helping out. 73!
