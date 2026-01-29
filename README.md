# Project FreeSims

Project FreeSims is intended to be an open source engine recreation of The Sims 1, it still requires the game files for the basic usage, the main purpose is to allow users have an expanded engine with better gameplay experience and new features.

## The Basics.

 Each user create a Sim, and add him to neighborhood, also the user can have their own house and can go work, do his life like in normal game, but you can also visit other players houses, and community lots. Is not intended to be online game.

## Prerequisites

- Windows
  - Visual Studio 2017+ (or Build Tools) with .NET Framework 4.5
  - Bundled dependencies in `SimsVille/Dependencies` (MonoGame.Framework, OpenTK, Tao.Sdl, GonzoNet, GOLDEngine, TargaImagePCL, DiscUtils)
- Linux
  - Mono with `msbuild`
  - OpenGL 3.0 and SDL2 runtime libraries
	
## Build

Open `SimsVille/SimsVille.sln` in Visual Studio 2017+ and build (Release/x86). On Linux, use Mono:

```bash
msbuild SimsVille/SimsVille.sln /p:Configuration=Release /p:Platform=x86
```
	
## Installation

- Install The Sims 1 (Complete Collection or Legacy Collection).
  - Windows: the game installer sets the registry path that FreeSims uses to locate assets.
  - Linux: copy the game files into `game/The Sims/` (relative to the FreeSims folder).
- Build the solution (see above).
- Run `SimsVille/bin/Release/SimsVille.exe` (or run from Visual Studio).

## AI Agents (optional sidecar)

An optional AI sidecar (Ollama + FastAPI) can run alongside SimsNet. Files live under `ai/`.
Set `FREESIMS_AI_URL` to point SimsNet at a non-default AI server URL.

```bash
cd ai/docker
cp .env.example .env
docker compose -f compose.ai.yml up -d --build
curl http://127.0.0.1:8066/health
```
	
# Screenshots

![Screenshot](preview.png)

## License

This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

## Donations
[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/P5P4GF1E0)
