# KEYNOTE

Windows MIDI-to-keyboard piano player. Requires Windows x64 and a **verified mapping for your specific Roblox piano**. The included `sample-unverified.json` is a development example only; it is not a game layout.

For the short workflow, select the Roblox window and a library song on Player, focus the selected Roblox piano window, and press Ctrl+Alt+F8. Selecting an entry loads its complete MIDI without starting playback. The app remembers the mapping and octave and selects Roblox automatically when exactly one Roblox window is available. Songs with unmapped notes or conflicting key holds still require adjustment before playback. Ctrl+Alt+F9 stops playback; select a song again to start a new session after an emergency stop.

## Download

Download the Windows x64 ZIP from [GitHub Releases](https://github.com/ojeshfrrr/larpiano/releases/latest), extract the entire ZIP, and run `Keynote.App.exe`. Songs and mappings are not bundled. The application requires a verified mapping for the specific piano.

Website: https://larpiano.portways.app/

## Build and run

Install the .NET 10 SDK, then run:

```powershell
dotnet restore Keynote.sln
dotnet build Keynote.sln -c Release
dotnet test Keynote.sln -c Release
dotnet run --project Keynote.App/Keynote.App.csproj -c Release
```

To publish a self-contained x64 executable:

```powershell
dotnet publish Keynote.App/Keynote.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

Run `publish\Keynote.App.exe`; no separate .NET runtime is required. The files can be copied to a folder and deleted to uninstall.

## Use

1. Start Roblox and open the target piano. On Player, refresh and select its window.
2. Import a MIDI file from Player. In Mapping, assign every pitch used by the song to a letter or digit, optionally with Shift; load and save JSON profiles as needed. Resolve duplicate assignments. Octave shift changes which mapping pitch is looked up.
3. Use Ctrl+Alt+F8 while the selected Roblox window is foreground to start a newly imported song, pause, or resume. Ctrl+Alt+F9 cancels playback and calibration. The Play button can also arm playback until Roblox is focused. Losing focus pauses; return to Roblox and use the shortcut to resume. Shortcuts can be changed in Settings and are saved locally. A registration conflict is reported there.

## MIDI and playback behavior

Standard MIDI format 0 and synchronous format 1 are imported through DryWetMIDI. Tempo changes are applied to both note starts and ends. Same-channel overlapping notes are paired with note-offs in first-on, first-off order. Malformed unmatched note events and format 2 independent sequences are rejected. MIDI control changes (including sustain pedal), pitch bends, velocity dynamics and instruments are not reproduced by keyboard input.

Mappings are user-configured; the included sample is unverified and is never loaded automatically. Use A-Z, 0-9, or explicit `SHIFT+key` assignments per pitch. Letters without `SHIFT+` remain unshifted, including existing uppercase profiles. Overlapping natural and shifted notes sharing one physical key block playback with an error. Shift is pressed around the note onset and released immediately; the note key remains held until its scheduled release. Missing notes, duplicate assignments and unsupported key names block playback. An octave shift changes the lookup pitch. Editing a profile clears its verified flag.

The scheduler uses Stopwatch and cancellable short waits. A speed change preserves the elapsed position; overlapping notes mapped to the same key share a held key, while consecutive notes release and re-press. Pausing releases held keys, freezes position and re-presses active notes on resume. Playback pauses on focus loss, stops on cancellation or an input error, and attempts to release every key it owns. Resume requires the selected window to regain focus. Input cleanup failures are reported and kept retryable.

## Current limitations

- A three-note calibration and part of one song were confirmed audible in a specific Roblox piano. Other arrangements and full musical accuracy remain unverified. Native input uses physical scan codes for both key-down and key-up. Full musical accuracy and every safety scenario still require the game checklist. The example mapping is not a target layout. Shift-modified letters and digits are supported; other special keys are unsupported. Synthetic input may be blocked; no bypass is attempted.
- Global hotkeys, calibration and the native Windows input path have automated simulated coverage; limited manual testing was performed in one Roblox piano, not across all safety scenarios. Hotkey registration can fail when a shortcut is already in use. The Stop shortcut cancels an armed calibration or session; a stopped session must be started anew using Play.
- Windows SendInput targets the system input stream. Focus can change between the foreground check and dispatch, and a key-up needed for cleanup may reach the new foreground window. Windows can also reject a key-up; failures are reported and remain tracked for a retry, but release cannot be guaranteed when the OS refuses input. Avoid holding mapped keys physically during playback.
- The scheduler is not hard real-time; Windows scheduling can delay events. Overlapping same-pitch note-ons share a held key, so the second onset is not retriggered. No sustain pedal interpretation is performed.
- Note-on and note-off events must be paired within each track. Files relying on cross-track note pairs or controller-only note termination are rejected. Format 2 independent sequences are not supported.
- Automated tests include simulated hotkey registration, focus and input behavior, independently authored Standard MIDI files from jazz-soft/test-midi-files (see `Keynote.Tests/Fixtures` for sources and MIT license), targeted malformed/tempo/overlap cases, fake-input playback tests and a simulated native-input boundary. They do not establish behavior for all MIDI files, measure precision under system load, or validate real Roblox input.
- Mapping and shortcut preferences are persisted locally. The piano roll has pitch labels but no zoom. There is no bundled custom icon, structured logging, or installer.

## Calibration and diagnostics

In Mapping, edit a key cell and press A–Z or 0–9, optionally holding Shift. Individual tests and the test sequence wait for the selected window to be foreground; the sequence uses assigned keys in pitch order. A successful dispatch does not establish the note the piano played. After checking the target game, explicitly mark the profile user-verified; editing assignments clears that mark. See `ROBLOX_TEST_CHECKLIST.md` for the separate dispatch, foreground, receipt and audible-note checks.

Settings displays the selected window and PID, foreground status, playback state, held application keys and scheduler counters. Scheduled counts include logical events; dispatched counts cover actual scheduled key presses/releases and may be lower for overlapping notes that share a hold. Calibration, pause/resume and cleanup sends are not included in dispatched counts. Lateness is measured at dispatch using song position, with late defined as over 10 ms; p95 uses 1 ms buckets with a 2000 ms upper bucket. Counters are diagnostic estimates, not a hard real-time guarantee.

## Discover, extract, and library

Discover searches MIDIsss and Mutopia concurrently by title or composer, isolates provider failures, caches recent results, and displays each provider's last successful search. Search suggestions come only from local search history, stored in `%LOCALAPPDATA%\KEYNOTE\search-history.json`. Results with identical known SHA-256 hashes are merged with their provider attributions; without a hash, merging is conservative and uses a shared direct URL with matching metadata or the same provider ID. No genre or popularity is inferred from titles or ratings. Piano-instrument and multi-track filters only include files inspected during the current session, and piano-instrument status does not establish a solo arrangement.

MIDIsss user-uploaded results are for personal and educational use under [MIDIsss terms](https://midisss.com/terms.php). Its API does not expose artist, pagination, vote-count, or per-file license details. Mutopia results come from its public search page, which explicitly allows crawling in robots.txt. The adapter reads that public HTML format, includes the displayed piece-specific license, supports its Next 10 pagination and downloads direct `.mid` links from the official FTP path; it can break if the page format changes. Where there is no direct MIDI link, open the official piece page. BitMidi and MIDI Collection are website-only. MuseScore downloads are not automated.

Opennan's documented free Song to MIDI endpoint accepts a song name, an HTTPS YouTube link, or a solo-piano audio/video upload (up to 10 minutes and 200 MB). The provider advertises three conversions per IP per day. Its API documentation says no key is needed, while its [general terms](https://opennan.com/legal/terms) say an account is required; confirm current requirements with the provider. Submit only content you have rights to process. Starting a conversion sends your input or selected file to Opennan. KEYNOTE displays the source when supplied, otherwise says it is unknown; name searches can select a piano cover. Cancellation stops local polling and download but does not cancel the provider's job. Opennan conversion has not been live-verified.

Downloads are limited to 10 MB, approved HTTPS provider hosts and non-redirecting responses. MIDI headers and the existing importer validate content before it enters `%LOCALAPPDATA%\KEYNOTE\Library`. Library files use SHA-256 names; duplicate files are detected, source attribution and licenses are retained, damaged metadata can be recovered, and checksums are verified on opening. Inspection reports tracks, pitches, tempo, programs and detected unsupported events. Preflight updates for selected tracks with estimated playable notes, missing/conflicting mappings, overlap and concurrency counts; unselected tracks are explicitly excluded, and no transposition is applied. The importer still rejects format 2 and unmatched note events, and playback still ignores controllers, pitch bends, instruments and velocity.
## Phase 7.2 validation and safety

Target experience: https://www.roblox.com/games/78699621133325/Meet-People-Across-The-World. No keyboard layout is assumed. Before real playback, manually test and mark the mapping verified for the piano; the sample profile remains unverified and is never loaded automatically. The Settings controlled tests use the configured mapping and selected Roblox window. Single note, five-note melody, chord and tempo patterns dispatch automatically only after the window is foreground. The pause/resume and emergency-stop patterns hold a note for 15 seconds so the corresponding hotkeys can be exercised. Windows dispatch does not establish that Roblox received input or produced the expected note. Focus-loss and cleanup-failure counts are visible in diagnostics. Real-game validation is partial; only a short calibration and part of one song were confirmed audible.

Online imports preserve the source, license and SHA-256 in Player. Cancelling an online operation prevents its prepared song from being committed; a library save that already completed is retained. MIDI parsing uses a synchronous third-party parser on a worker thread: cancellation is checked before and after parsing, not inside the parser. Online downloads are limited to 10 MB; local MIDI files have no equivalent size limit. Inspection may parse a file multiple times and preflight sorts note events, so very large files can still consume substantial memory and time. The piano roll caches its note list and refreshes the cursor less frequently, but drawing remains proportional to note count. Closing cancels online work and attempts key cleanup; a remote Opennan conversion may continue.

The published executable has a hidden `--workflow-test` mode for isolated background validation with mocked HTTP. It requires an existing absolute test directory and an explicit MIDI fixture path and disables hotkeys. It does not test live provider HTTP or Roblox. Use `--workflow-test <test-directory> <fixture.mid>` only with disposable test data.

## Updates

The Updater tab checks the latest published stable GitHub release over HTTPS without requiring GitHub CLI or an account. It compares three-part version tags such as `v1.0.1` with the installed assembly version. The tab opens the GitHub releases page; installation is manual and no executable is automatically downloaded or replaced.

## Remapped music

The Remapped music tab lists public-domain arrangements prepared for the Visual Pianos C2–C7 layout. Search, select an arrangement, then choose **Download and select**. The app checks the download's SHA-256 and imports it through the normal MIDI library; it applies the arrangement's recommended octave, but never automatically marks a mapping verified. Use **Load piano layout** to start with the included unverified Visual Pianos reference, then test and verify your own piano before playback. The catalog details identify the original source, license, attribution and note changes. The individual arrangements have not all been tested in Roblox.

The catalog and its small MIDI files are served by the existing Portways website, with no additional database required. Only reviewed public-domain files are redistributed. See `remapped_catalog/README.md` for operation and the optional future PostgreSQL backend.

### Modern picks

The Modern picks tab keeps the requested artist and song shortlist separate from the remapped catalogue. Select a title to search the existing discovery providers or filter your local library. The shortlist does not include, host, or guarantee MIDI files for these songs, and they are not pre-mapped. Check the source terms and inspect any MIDI before playback.
