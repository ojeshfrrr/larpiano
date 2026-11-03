# Manual Roblox integration checklist

Target: Meet People Across The World — https://www.roblox.com/games/78699621133325/Meet-People-Across-The-World

Do not mark a profile verified until the expected notes have been checked in the specific piano game. Record the game name/version, date, Windows version, profile and selected process ID alongside results.

1. In Settings, select the intended Roblox window and record its title and PID. Check diagnostics show `Foreground: True` only while that window is focused. Switch elsewhere and confirm playback pauses and the held-key list clears. Return to Roblox and resume with the shortcut.
2. Enter a small mapping based on the game's own documented or observed controls. Use individual tests and the sequence in pitch order. For each test record **four separate outcomes**:
   - Windows accepted the synthetic input (a test completion or diagnostic dispatch count; this is not proof of receipt).
   - The selected Roblox window was foreground at dispatch (the check is subject to a race).
   - Roblox actually reacted to that key (observe the game).
   - The target piano produced the expected note (listen or use the game's note display).
3. Resolve any missing or duplicate pitches. Only after manual comparison, mark the profile user-verified and save it. Reload it and confirm the assignments. The example profile is not a game layout.
4. Use Settings > Controlled playback test for a single note, five-note melody, three-note chord, tempo, pause/resume and emergency stop. Separately test a MIDI file with successive repeated notes and overlapping same-pitch notes. The current policy holds an overlapping key continuously; determine whether the game expects a second onset and record the result. Do not infer a correct policy without the game test.
5. Test Ctrl+Alt+F8 to start, pause and resume while Roblox stays foreground; confirm no KEYNOTE activation. Change a shortcut, check conflicts are reported and restart to check persistence. Test Ctrl+Alt+F9 (or the configured stop shortcut) during a note and while a future note is pending. Confirm no further notes and no held keys remain.
6. Cause focus loss during a chord; confirm pause and cleanup, then restore focus and resume. Repeat stopping and closing while a key is held. Record failures in cleanup diagnostics and verify physical key state separately. Never assume OS rejection can be repaired automatically.
7. Observe diagnostics for scheduled, dispatched, failed, late, average, p95 and maximum lateness. Compare the audible output independently; the timing counters are estimates, not hard real-time guarantees.

No Roblox test is performed by the automated suite. `SendInput` success does not prove Roblox or its piano received or interpreted an input.
