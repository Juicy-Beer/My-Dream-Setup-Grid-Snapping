# *My Dream Setup Grid Snap*

My Dream Setup has no snapping so I went and made a mod that adds it. Hold an item and it snaps to a grid instead of moving around freely.

Press F8 in game to open the menu. Pick a grid size (1 cm up to 1 m, or type your own), and choose which axes snap. F7 turns snapping on and off. Both keys can be changed in the menu, and your settings are saved.

## Install

Download `GridSnap.dll` from the Releases page on the right.

1. Put BepInEx 5 (x64) in the game folder, next to `MDS.exe`.
2. Drop `GridSnap.dll` into `BepInEx/plugins`.
3. On Linux or Steam Deck, add `WINEDLLOVERRIDES="winhttp=n,b" %command%` to the game's Steam launch options, which are in **Properties**.

## Building it yourself

You need the **.NET SDK.** The game's DLLs aren't in this repo, so the build uses the ones from your own install. It looks in the usual Steam folder, and if yours are somewhere else, point it at the folder with `MDS.exe`:

    dotnet build -c Release -p:GameDir="D:\Games\Steam\steamapps\common\My dream setup\MDS"

The DLL ends up in `bin/Release/net472/`.

I've only tested this on Fedora Linux, so be wary.
