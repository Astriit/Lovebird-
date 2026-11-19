Lovebird 1.0
Single-player launcher for OptiScaler / frame generation

The interface uses a dark launcher-style theme with a blue #0078F2 accent,
Segoe UI, translucent panels and a blurred wallpaper behind the library. The
window has its own minimize, maximize and close buttons. Every start rolls a
random The Neighbourhood track in the island at the top.

RUNNING IT

Lovebird.exe is a normal Windows app. No setup is required — unzip the folder
anywhere and run the EXE. "Lovebird Setup.exe" is the full installer: it asks
for a drive and folder, unpacks everything, adds shortcuts and a Programs and
Features entry. Games stay untouched until you press Launch in the app.

HOW IT WORKS

1. On the first start it scans Steam, Epic, GOG Galaxy and ordinary folders on
   local drives. Use RESCAN in the left panel to run it again.
2. Pick a game from the list. Double-click works too.
3. Pick one of two modes:
     01 — upscale only, no frame generation
     02 — upscale plus FSR3 frame generation
4. Press LAUNCH. Lovebird copies the DLLs it needs, applies the matching preset
   to OptiScaler.ini, then starts the game.
5. In game: Insert opens the OptiScaler menu, End toggles frame generation.
   For 30 -> 60 through RTSS, set the base to 30 and the OptiScaler limit to
   at least 60.

RESTORE

RESTORE in the header only removes what Lovebird put there. A file is deleted
only when its hash still matches the one recorded in .lovebird_install.json.
OptiScaler.ini is put back from the backup. Files you changed, or files that
were already there, are left alone.

ONLINE GAMES

Titles with known online names and common anti-cheat (EasyAntiCheat,
BattlEye, Vanguard, RICOCHET and others) are skipped by the scanner. You can
add more names in:

    %APPDATA%\Lovebird\online_blocklist.txt

One game per line. The SETTINGS button opens that file. Never run OptiScaler
in a game with online anti-cheat.

COVERS

Covers come from the Steam CDN — by appid for Steam games, by search for the
rest — and are cached in %APPDATA%\Lovebird\covers. With no internet you get a
generated placeholder instead of an error.

SAFETY NOTES

- If dxgi.dll is already taken, Lovebird tries winmm.dll, version.dll and the
  other usual names. It never overwrites a DLL it cannot account for.
- Every install writes .lovebird_install.json with the file list and hashes,
  so restore stays precise.
- FSR4 on Windows 10: if it crashes, keep the D3D12_Optiscaler folder and set
  FsrAgilitySDKUpgrade=true. That folder is already in the payload.
- SmartScreen or an antivirus may warn about the unsigned EXE. That is normal
  for an independent build.

UNINSTALL

Use Programs and Features -> Lovebird 1.0, or run Lovebird --uninstall.
Games and their files are not touched.

CREDITS AND LICENSES

The payload is my OptiScaler v2.8 build with MFG Unlocker from mfg3.7z; those
files were not modified. Licenses live in payload\Licenses\. The bird icon and
background were drawn and tuned for this desktop. SharpCompress is MIT and is
used by the installer and the update check.

Lovebird 1.0 — personal use.

Made Using Opencode AI
