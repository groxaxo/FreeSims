# FreeSims Architecture Documentation

**Project FreeSims** is an open-source engine recreation of The Sims 1. This document provides a complete architecture map of the game engine, identifying every component, explaining their purposes, and showing how they connect and interact.

---

## Table of Contents

1. [High-Level Overview](#high-level-overview)
2. [Major Components](#major-components)
3. [Component Details](#component-details)
4. [Subsystems in SimsVille](#subsystems-in-simsville)
5. [Data Flow & Interaction](#data-flow--interaction)
6. [File Formats](#file-formats)
7. [Build & Dependencies](#build--dependencies)

---

## High-Level Overview

FreeSims is built on **MonoGame** (cross-platform game framework) and recreates The Sims 1 engine with enhanced features and multiplayer capability. The architecture is modular, consisting of 6 major C# projects that work together to provide game client, server, file handling, and debugging tools.

### Core Technologies
- **MonoGame 3.6+**: Cross-platform rendering (DirectX/OpenGL)
- **OpenTK 1.2**: OpenGL bindings
- **C# / .NET Framework 4.5**
- **Target Platforms**: Windows, Linux

---

## Major Components

The FreeSims engine consists of **6 C# projects**, organized as follows:

| Component | Type | Purpose | Namespace | Dependencies |
|-----------|------|---------|-----------|--------------|
| **SimsVille** | Executable | Main game client - Core gameplay engine, UI, rendering, world management | `FSO.Client` | sims.common, sims.files, SimsNet |
| **SimsNet** | Executable | Virtual Machine server - Handles game logic, AI, object interactions, scripting | `SimsNet` | sims.common, sims.files |
| **sims.common** | Library | Shared rendering framework, 3D graphics, Vitaboy animation system, camera/input | `TSO.Common`, `FSO.Common` | sims.files |
| **sims.files** | Library | File format handlers - FAR archives, IFF chunks, DBPF database, audio, HIT | `Simslib`, `FSO.Files` | (standalone) |
| **sims.debug** | WinForms Tool | Debug utility - Catalog, roofing, and info table visualization | `sims.debug` | sims.files |
| **sims.parser** | WinForms Tool | PS2 disc/neighborhood/house data extraction tool | `sims.parser` | None (uses DiscUtils) |

---

## Component Details

### 1. SimsVille (Game Client)

**Purpose**: The main player-facing application that runs the game client.

**Key Responsibilities**:
- Initialize MonoGame engine and graphics pipeline
- Load game assets via ContentManager
- Manage UI screens and user input
- Render 3D world and avatars
- Execute game logic via SimsAntics VM
- Handle multiplayer networking (client-side)
- Coordinate audio playback via HIT system

**Entry Point**: `FSO.Client.Program.Main()` → creates `TSOGame` instance (inherits from MonoGame `Game`)

**Main Class**: `TSOGame : FSO.Common.Rendering.Framework.Game`
- Initializes `UILayer` (UI management)
- Initializes `_3DLayer` (3D scene management)
- Game loop: `Update()` → `Draw()`

**Directory Structure**:
```
SimsVille/
├── ContentManager/        # Asset loading system (providers & codecs)
├── Debug/                 # Debug UI (ActionQueue, VMExaminer, etc.)
├── GameContent/          # Game-specific content handling
├── HIT/                  # Audio system (FSCPlayer, AmbiencePlayer, HITVM)
├── Network/              # Client-side networking (UIPacketHandlers, NetworkController)
├── SimsAntics/           # Virtual Machine for behavior execution
├── UI/                   # User interface screens and panels
├── Utils/                # Utility classes
├── Vitaboy/              # Avatar rendering and animation
├── World/                # 3D world rendering and spatial management
├── Game.cs               # Main game class (TSOGame)
└── Program.cs            # Entry point
```

---

### 2. SimsNet (VM Server)

**Purpose**: Standalone server that runs the Virtual Machine for object behaviors and game logic.

**Key Responsibilities**:
- Host VM instance on network (port 37564)
- Execute BHAV (Behavior) scripts
- Manage object interactions and AI
- Handle server-side networking (packet broadcasting)
- Synchronize game state across clients

**Entry Point**: `SimsNet.Program.Main()` → creates `VMInstance`

**Key Classes**:
- `VMInstance`: Main server controller
  - Initializes VM with Blueprint, VMContext
  - Listens for client connections
  - Manages VMServerDriver for command distribution

**Uses GonzoNet**: Encrypted packet-based networking library

---

### 3. sims.common (Rendering Framework)

**Purpose**: Shared library providing rendering abstractions, 3D graphics utilities, and the Vitaboy avatar system.

**Key Responsibilities**:
- 3D scene management (cameras, layers, world coordinates)
- Vitaboy avatar rendering (skeleton, mesh, binding, animation, outfit system)
- Game loop framework (`GameScreen` base class)
- Input and UI state management
- Common rendering utilities (sprite batching, shader management)

**Directory Structure**:
```
sims.common/
├── content/              # Content loading framework
├── Model/                # 3D model classes
├── rendering/            # Rendering framework (Camera, Scene, Layers, GraphicsDevice)
├── utils/                # Utility classes
├── FSOEnvironment.cs     # Environment settings (paths, DPI scaling, OS detection)
├── IniConfig.cs          # INI file configuration
└── Log.cs                # Logging system
```

**Key Subsystems**:
- **Vitaboy**: Avatar mesh/animation system
  - `Skeleton.cs`: Bone hierarchy
  - `Mesh.cs`: 3D geometry
  - `Binding.cs`: Mesh-to-skeleton binding
  - `Animation.cs`: Keyframe animation data
  - `Outfit.cs`: Clothing/texture mapping
  - `Avatar.cs`: Complete avatar instance
  - `Animator.cs`: Animation controller

- **Rendering Framework**:
  - `Game.cs`: Base game class with Update/Draw loop
  - `GameScreen.cs`: Base for UI screens
  - `_3DLayer.cs`: 3D scene layer
  - `WorldCamera.cs`: Camera controller

---

### 4. sims.files (File Format Library)

**Purpose**: Low-level library for reading and parsing The Sims 1 file formats.

**Key Responsibilities**:
- Decompress FAR1/FAR3 archives
- Parse IFF (Interchange File Format) chunks
- Handle DBPF (Database Packed File) format
- Load textures (TGA, BMP, SPR, SPR2)
- Load audio files (XA, UTK, HIT formats)
- Parse font files (OTF)
- Resource hashing and identification

**Directory Structure**:
```
sims.files/
├── FAR1/                 # FAR1 archive decompression
├── FAR3/                 # FAR3 archive decompression (RefPack algorithm)
├── formats/              # File format parsers (IFF chunks)
│   ├── dbpf/             # DBPF database format
│   ├── iff/              # IFF chunk types
│   │   ├── chunks/       # All chunk types (BHAV, OBJD, GLOB, STR, SPR2, etc.)
│   │   ├── IffFile.cs
│   │   └── IffChunk.cs
│   ├── otf/              # Font format
│   └── tga/              # TGA image format
├── HIT/                  # HIT audio system formats (HSM, FSC, Hitlist, EVT, Track)
├── UTK/                  # UTK audio format
├── XA/                   # XA audio format (PS1/PS2 ADPCM)
├── utils/                # Utilities (IoBuffer, hash functions)
├── Endian.cs             # Endian conversion utilities
├── Hash.cs               # Resource ID hashing
├── ImageLoader.cs        # Image loading (BMP, SPR)
└── Tuning.cs             # Game tuning constants
```

**Key File Formats**:
- **FAR Archives**: Compressed file containers (FAR1 = simple, FAR3 = RefPack compression)
- **IFF (Interchange File Format)**: Chunk-based format containing:
  - `BHAV`: Behavior scripts (bytecode)
  - `OBJD`: Object definitions
  - `GLOB`: Global data
  - `STR`: String tables
  - `SPR`, `SPR2`: Sprite graphics
  - `BMP`: Bitmap images
  - `TTAB`, `TTAT`, `TTAs`: Interaction pie menu trees
  - `DGRP`: Draw groups (rendering)
  - `SLOT`: Object slots
  - `PALT`: Color palettes
  - `And 50+ other chunk types`
- **DBPF**: Database Packed File (The Sims 2/3 format, partial support)
- **HIT**: Audio sequencing format
- **XA**: PlayStation ADPCM audio
- **UTK**: Maxis proprietary audio format

---

### 5. sims.debug (Debug Tool)

**Purpose**: WinForms debugging application for inspecting game assets.

**Key Features**:
- Catalog table viewer
- Roof style viewer
- Info table inspector
- File format debugging

**Files**:
- `Form1.cs`: Main debug UI
- `CatalogTable.cs`: Catalog data viewer
- `RoofTable.cs`: Roof style viewer
- `InfoTable.cs`: Info table viewer

---

### 6. sims.parser (PS2 Parser Tool)

**Purpose**: Tool for extracting neighborhood, house, and family data from The Sims PS2 disc images.

**Key Features**:
- PS2 disc image parsing (uses DiscUtils library)
- NGH (neighborhood) file extraction
- House data extraction
- Family/Sim data extraction

**Files**:
- `Form1.cs`: Main parser UI
- `Data/`: Parsed data structures

---

## Subsystems in SimsVille

SimsVille is the most complex component, containing multiple subsystems that work together:

### 1. SimsAntics (Virtual Machine)

**Purpose**: Execute behavior scripts (BHAV bytecode) for object interactions and AI.

**Key Classes**:

- **VM** (`VM.cs`): Main virtual machine controller
  - Manages VMContext, VMEntity instances
  - Network support (VMServerDriver / VMClientDriver)
  - Clock management and thread scheduling
  - Handles persistence (save/load)

- **VMContext** (`VMContext.cs`): Execution environment
  - Holds Blueprint (world architecture)
  - Architecture (walls, floors, objects)
  - ObjectQueries (spatial queries)
  - Clock and random number generator

- **VMEntity** (abstract base): Represents game entities
  - Object or Avatar instance in the VM
  - Has RTTI (Runtime Type Info)
  - Container management (inventory)
  - Property system (Attributes, SemiAttributes, ObjectData)

- **VMThread** (`VMThread.cs`): Script execution thread
  - Stack frame management
  - Action queue (queued interactions)
  - Registers: `TempRegisters[20]`, `TempXL[2]`
  - Max instruction loop safety (~500,000 instructions)
  - Execution state (Active, Paused, Finished, Killed)

- **VMRoutine** (`VMRoutine.cs`): Wraps BHAV chunks
  - Instructions array (compiled from BHAV bytecode)
  - Local variables and arguments
  - Runtime info (name, chunk ID)

- **VMArchitecture** (`VMArchitecture.cs`): Lot layout manager
  - Walls, Floors, ObjectSupport
  - BuildableArea, RoomMap
  - Roof style and pitch
  - Terrain heights

- **VMInstruction**: Executable instruction (translated from BHAV)

- **Primitives** (50+ classes): Instruction implementations
  - `VMGrab.cs`: Grab/release object
  - `VMPlaySound.cs`: Play sound effects
  - `VMPushInteraction.cs`: Queue interaction
  - `VMSubRoutine.cs`: Call sub-behavior
  - `VMExpression.cs`: Math/logic operations
  - `VMGotoRelativePosition.cs`: Movement
  - `VMSetMotiveChange.cs`: Modify Sim needs
  - And many more...

**Execution Model**:
1. VMThread executes VMInstructions sequentially
2. Each instruction is a Primitive with `Execute()` method
3. Primitives return exit codes: `GOTO_TRUE`, `GOTO_FALSE`, `RETURN_TRUE`, `RETURN_FALSE`, etc.
4. Control flow: branches, subroutines, interrupts
5. Thread can be suspended/resumed for animations

**Data Flow**:
```
BHAV Chunk (IFF file)
    ↓
VMRoutine (compiled instructions)
    ↓
VMThread (execution)
    ↓
Primitive.Execute() (game logic)
    ↓
VMEntity state changes
```

---

### 2. World (3D Rendering & Spatial Management)

**Purpose**: Manage 3D world state, spatial queries, and rendering.

**Key Classes**:

- **Blueprint** (`Blueprint.cs`): Central world data structure
  - `WallTile[][]`: Wall layout (2D grid)
  - `FloorTile[][]`: Floor layout (2D grid)
  - `WallComponent`: Wall renderer
  - `FloorComponent`: Floor renderer
  - `RoofComponent`: Roof renderer
  - `ObjectComponent[]`: All objects in world
  - `AvatarComponent[]`: All avatars in world
  - `TerrainComponent`: Terrain renderer
  - Damage tracking (for change detection)
  - Cutaway system (hide walls for visibility)
  - SubWorlds (basement/2nd floor)

- **WorldComponent** (abstract): Base for renderable components
  - Lifecycle: `Initialize()`, `Draw()`, `Update()`
  - Events: `OnRotationChanged()`, `OnZoomChanged()`, `OnScrollChanged()`
  - Layer-based rendering

- **WorldState** (`WorldState.cs`): Current view state
  - `GraphicsDevice`: MonoGame graphics
  - `WorldCamera`: Camera controller
  - `ViewDimensions`: Screen size
  - `Zoom`: Current zoom level (1x, 2x, 3x)
  - `Rotation`: Current rotation (0°, 90°, 180°, 270°)
  - `CenterTile`: Current view center
  - `Level`: Current floor level

- **WorldCamera** (`WorldCamera.cs`): Camera management
  - View/projection matrix calculation
  - Scrolling and zooming
  - World-to-screen coordinate conversion

- **Component Types**:
  - `EntityComponent`: Base for objects/avatars
  - `ObjectComponent`: Represents objects (furniture, etc.)
  - `AvatarComponent`: Represents Sims
  - `TerrainComponent`: Ground terrain
  - `RoofComponent`: Roof rendering
  - `WallComponent`: Wall rendering
  - `FloorComponent`: Floor rendering

**Rendering Pipeline**:
1. WorldState determines view parameters
2. Blueprint provides data (walls, floors, objects, avatars)
3. Components render themselves based on WorldState
4. Batch renderers optimize draw calls:
   - `_2DWorldBatch`: 2D sprite rendering
   - `_3DWorldBatch`: 3D object rendering
5. Sprite sorting by depth
6. DGRP (Draw Group) renderer handles multi-sprite objects

**Coordinate Systems**:
- **World Coordinates**: Float (X, Y, Z) in world space
- **Tile Coordinates**: Integer (X, Y) grid positions
- **Screen Coordinates**: Pixel (X, Y) on screen

---

### 3. UI (User Interface)

**Purpose**: Screen management, UI panels, and user input handling.

**Framework Classes**:
- `UIElement`: Base UI class
- `UIContainer`: Container for child elements
- `UIScreen`: Full-screen UI
- `GameScreen`: Base for game screens (extends UIScreen)
- `UIButton`, `UILabel`, `UISlider`, `UIListBox`, `UIDialog`, `UIAlert`, `UITextBox`, `UIGridViewer`, etc.

**Screens** (GameScreen subclasses):
- `CoreGameScreen`: Main gameplay screen
  - Manages UILiveMode, UIBuildMode, UIBuyMode
  - Handles lot loading and VM initialization
  - Coordinates UI ↔ World ↔ VM interaction

- `PersonSelection`: Character creation/selection
- `PersonSelectionEdit`: Character customization
- `LoadingScreen`: Asset loading screen
- `EALogo`, `MaxisLogo`, `Credits`: Startup screens
- `DebugTypeFaceScreen`: Font debugging

**Key Panels**:
- **UILiveMode**: Main gameplay HUD
  - Avatar thumbnail
  - Motive bars (Hunger, Energy, Fun, etc.)
  - EOD (Enhanced Object Display) controls
  - Clock/date display

- **UIBuildMode**: Construction tools
  - Terrain editing
  - Wall placement
  - Door/window placement
  - Roof editing
  - Delete tool

- **UIBuyMode**: Shopping catalog
  - Object categories
  - Object preview
  - Purchase UI

- **UIPieMenu**: Context-sensitive interaction menu
  - Shows available interactions on object/avatar
  - Hierarchical menu (TTAB-driven)

- **UILotControl**: Lot management
  - Lot info display
  - Visitor controls

- **UIInteractionQueue**: Action queue display
  - Shows queued interactions for selected Sim
  - Cancel/reorder actions

- **UIPersonGrid** / **UIPersonPage**: Character info
  - Skills, relationships, biography

- **UIOptions**: Game settings
- **UIModMenu**: Mod management
- **UIUCP**: User-created content pool

- **EOD Plugins**: Enhanced Object Displays
  - `UIDanceFloor`: Dance floor controls
  - `UIPizzaMaker`: Pizza making UI
  - `UISign`: Editable sign UI
  - `EODStub`: Plugin framework

- **LotControls**:
  - `UIFloorPainter`: Floor texture painting
  - `UIWallPainter`: Wall texture/style painting
  - `UIRoofPainter`: Roof editing
  - `UICheatHandler`: Cheat code processing

**UI Event Flow**:
```
User Input (Mouse/Keyboard)
    ↓
GameScreen.Update() (processes input)
    ↓
UIElement.Update() (propagates to children)
    ↓
Event Handlers (OnMouseEvent, OnKeyDown, etc.)
    ↓
Game Logic (VM commands, UI state changes)
```

---

### 4. HIT (Audio System)

**Purpose**: Sequence-based audio playback and ambient sound management.

**Key Components**:

- **FSCPlayer** (`FSCPlayer.cs`): Flexible Sequence Container player
  - Plays FSC audio sequences
  - Manages playback position and loops
  - Tempo control (`BeatLength = 60 / Tempo`)
  - Sound effect caching
  - `Tick()`: Synchronizes playback with game time
  - `SetManualTempo()`, `SetVolume()`, `Play()`, `Stop()`

- **AmbiencePlayer** (`AmbiencePlayer.cs`): Ambient audio wrapper
  - Supports both FSC sequences and XA looped streams
  - Routes audio through HITVM
  - Handles streaming and FSC playback modes
  - Volume control and fading

- **HITVM** (`HITVM.cs`): HIT Virtual Machine / Audio Interpreter
  - Manages audio threads and events
  - Coordinates multiple audio sources
  - Event system integration

- **HITThread** (`HITThread.cs`): Audio playback thread
  - Thread management for concurrent audio
  - Priority handling

**File Formats** (in `sims.files/HIT/`):
- **FSC** (`FSC.cs`): Flexible Sequence Container
  - Sequence of audio samples with timing
  - Supports loops and tempo

- **HSM** (`HSM.cs`): HIT Sound Manager
  - Sound metadata and grouping

- **Hitlist** (`Hitlist.cs`): Audio track list
  - Maps track IDs to audio files

- **EVT** (`EVT.cs`): Event system
  - Triggers audio on game events

- **Track** (`Track.cs`): Individual audio track
- **Patch** (`Patch.cs`): Audio patch data
- **Hot** (`Hot.cs`): Hot region audio
- **TLO** (`TLO.cs`): Track list object

**Audio Flow**:
```
Game Event (object interaction, ambience, etc.)
    ↓
HITVM.PlayTrack(trackID)
    ↓
HITThread created/resumed
    ↓
FSCPlayer or AmbiencePlayer
    ↓
SoundEffect.Play() (MonoGame)
    ↓
Audio output
```

---

### 5. Vitaboy (Avatar System)

**Purpose**: Avatar mesh rendering, skeletal animation, and clothing system.

**Key Classes**:

- **Avatar** (`Avatar.cs`): Complete avatar instance
  - Combines Skeleton, Mesh, Binding, Animation, Outfit
  - Appearance management
  - Head tracking (look-at)
  - Shadow rendering

- **Skeleton** (`Skeleton.cs`): Bone hierarchy
  - Named bones (ROOT, PELVIS, SPINE0, SPINE1, etc.)
  - Bone transformations (position, rotation, scale)
  - Inverse kinematics support

- **Mesh** (`Mesh.cs`): 3D geometry
  - Vertex positions, normals, UVs
  - Bone weights (skinning)
  - LOD (Level of Detail) support

- **Binding** (`Binding.cs`): Mesh-to-skeleton binding
  - Maps mesh vertices to skeleton bones
  - Defines how geometry deforms with animation

- **Animation** (`Animation.cs`): Keyframe animation data
  - Bone transforms over time
  - Locomotion (root motion)
  - Events (footstep sounds, etc.)

- **Outfit** (`Outfit.cs`): Clothing/texture mapping
  - Body textures
  - Clothing layers (skin, underwear, shirt, pants, etc.)
  - Accessories (hats, glasses, etc.)

- **Animator** (`Animator.cs`): Animation controller
  - Blending between animations
  - Playback speed control
  - Animation events

- **SimAvatar** (`SimAvatar.cs`): Sim-specific avatar
  - Extends Avatar for gameplay integration
  - Motive-based animations (tired walk, etc.)

**Avatar Rendering Pipeline**:
```
1. Load Skeleton (.skel file)
2. Load Mesh (.mesh file)
3. Load Binding (.bnd file)
4. Load Animation (.anim file)
5. Load Outfit (.oft file, textures)
    ↓
6. Animator updates bone transforms (current animation frame)
    ↓
7. Binding deforms mesh vertices based on bone transforms
    ↓
8. Mesh rendered with outfit textures
    ↓
9. Shadow rendered (projected mesh)
```

**File Formats**:
- `.skel`: Skeleton definition
- `.mesh`: 3D geometry
- `.bnd`: Binding data
- `.anim`: Animation keyframes
- `.oft`: Outfit definition
- Textures: TGA, BMP

---

### 6. ContentManager (Asset Loading)

**Purpose**: Load and cache game assets from FAR archives.

**Architecture**:

**Providers** (resource loaders):
- Load specific resource types
- Cache loaded resources
- Handle resource references (by hash ID or filename)
- Support hot-reloading

**Avatar Providers**:
- `AvatarAnimationProvider`: Loads .anim files
- `AvatarAppearanceProvider`: Loads appearance data
- `AvatarBindingProvider`: Loads .bnd files
- `AvatarCollectionsProvider`: Loads avatar collections
- `AvatarMeshProvider`: Loads .mesh files
- `AvatarOutfitProvider`: Loads .oft files
- `AvatarSkeletonProvider`: Loads .skel files
- `AvatarTextureProvider`: Loads avatar textures
- `AvatarThumbnailProvider`: Loads avatar thumbnails

**World Providers**:
- `WorldObjectProvider`: Loads IFF objects
- `WorldFloorProvider`: Loads floor textures/data
- `WorldWallProvider`: Loads wall styles/textures
- `WorldRoofProvider`: Loads roof styles/textures
- `WorldGlobalProvider`: Loads global data (GLOB chunks)
- `WorldObjectGlobals`: Global object data
- `WorldObjectCatalog`: Object catalog

**UI Providers**:
- `UIGraphicsProvider`: Loads UI graphics

**TS1 Legacy Providers** (The Sims 1 compatibility):
- `TS1Provider`: General TS1 asset loading
- `TS1AvatarTextureProvider`: TS1 avatar textures
- `TS1JobProvider`: TS1 career data
- `TS1NeighbourProvider`: TS1 neighbor data
- `TS1BMFProvider`: TS1 font files
- `TS1BCFProvider`: TS1 font files

**Other Providers**:
- `HandgroupProvider`: Hand animation data

**Codecs** (file format decoders):
- `IContentCodec<T>`: Interface with `Decode(Stream) → T`
- `AnimationCodec`: Decodes .anim files
- `MeshCodec`: Decodes .mesh files
- `SkeletonCodec`: Decodes .skel files
- `BindingCodec`: Decodes .bnd files
- `TextureCodec`: Decodes textures
- `AppearanceCodec`: Decodes appearance data
- `OutfitCodec`: Decodes .oft files
- `CollectionCodec`: Decodes collections
- `PurchasableOutfitCodec`: Decodes purchasable outfits
- `HandgroupCodec`: Decodes hand animations
- `IffCodec`: Decodes IFF files
- `OTFCodec`: Decodes OTF fonts

**Framework**:
- `FAR3Provider`: Loads from FAR3 archives
- `FAR1Provider`: Loads from FAR1 archives
- `PackingslipProvider`: Manages packingslip files (asset manifests)
- `FileProvider`: Direct file system access

**Caching**:
- Resource cache with 100MB memory limit
- Least-recently-used (LRU) eviction
- Reference counting for shared resources

**Content Loading Flow**:
```
Request Resource (by ID or name)
    ↓
Provider checks cache
    ↓
If not cached:
    ↓
    FAR3Provider locates in archive
    ↓
    Decompress (RefPack algorithm)
    ↓
    Codec decodes binary → Object
    ↓
    Cache result
    ↓
Return resource
```

---

### 7. Network (Multiplayer)

**Purpose**: Client-server communication and multiplayer state synchronization.

**Key Components**:

- **NetworkController** (`NetworkController.cs`): State machine for network flow
  - States: Disconnected → Login → City → Lot
  - Events:
    - `OnLoginProgress`: Login progress updates
    - `OnLoginStatus`: Login success/failure
    - `OnNewCityServer`: Connected to city server
    - `OnCityServerOffline`: City server disconnected
    - `OnLoginNotifyCity`: Notify city of login
    - `OnCharacterCreation`: Character creation flow
    - `OnCityToken`: City token received
    - `OnLotCost`: Lot purchase cost
    - `OnPlayerAlreadyOnline`: Duplicate login detection
    - `OnTransitionCity`: City transition
  - Delegates for callbacks

- **NetworkFacade** (`NetworkFacade.cs`): High-level network interface
  - Abstracts network operations
  - Event registration

- **VMNetDriver** (abstract): Base for server/client network drivers
  - **VMServerDriver** (`VMServerDriver.cs`): Server-side
    - Sends commands to clients
    - Ban/kick users
    - Manages GlobalLink (server state)
    - Broadcasts state changes
  
  - **VMClientDriver** (`VMClientDriver.cs`): Client-side
    - Receives commands from server
    - Sends user actions to server
    - Handles latency compensation

- **VMNetCommand** (abstract): Network command base
  - All commands extend `VMNetCommandBodyAbstract`
  - Serializable for network transmission

- **Command Types**:
  - `VMNetMoveObjectCmd`: Move object
  - `VMNetGotoCmd`: Move Sim
  - `VMNetInteractionCmd`: Queue interaction
  - `VMNetChatCmd`: Chat message
  - `VMNetEODEventCmd`: EOD plugin event
  - `VMNetPlaceInventoryCmd`: Place from inventory
  - `VMNetBuyObjectCmd`: Purchase object
  - `VMNetDeleteObjectCmd`: Delete object
  - `VMNetArchitectureCmd`: Build mode action (walls, floors, etc.)
  - `VMNetSimLeaveCmd`: Sim leaves lot
  - `VMNetSimJoinCmd`: Sim joins lot
  - `VMNetRoommateCmd`: Roommate management
  - `VMNetAsyncResponseCmd`: Asynchronous response
  - `VMNetTuningCmd`: Tuning value change
  - Many more...

- **Event System** (`Events/`):
  - `LoginEvent`: Login-related events
  - `ProgressEvent`: Progress updates
  - `NetworkEvent`: Generic network events
  - `CityTransitionEvent`: City transitions
  - `CityViewEvent`: City view updates
  - `PacketError`: Packet error handling
  - `EventSink`: Event aggregation

- **Packet Handlers**:
  - `UIPacketHandlers`: UI ↔ Network layer
  - `UIPacketSenders`: Send packets from UI

- **Data Models**:
  - `PlayerAccount`: Player account data
  - `LotTileEntry`: Lot information
  - `CityDataRetriever`: City data fetching
  - `Cache`: Network data caching

**Protocol**:
- Uses **GonzoNet** encryption library
- Symmetric and asymmetric encryption support
- TCP-based with custom packet framing
- Command-based architecture (similar to RPC)

**Network Flow (Client)**:
```
User Action (UI)
    ↓
UIPacketSender creates VMNetCommand
    ↓
VMClientDriver.SendCommand()
    ↓
Serialize command
    ↓
GonzoNet encrypts and sends
    ↓
[NETWORK]
    ↓
Server receives and validates
    ↓
Server broadcasts to all clients
    ↓
[NETWORK]
    ↓
VMClientDriver.ReceiveCommand()
    ↓
Deserialize command
    ↓
VM.HandleNetworkCommand()
    ↓
VM state updated
    ↓
UI reflects changes
```

**Network Flow (Server - SimsNet)**:
```
Server starts VMInstance on port 37564
    ↓
Listen for client connections
    ↓
Client connects
    ↓
VMServerDriver created for client
    ↓
Client sends VMNetCommand
    ↓
Server validates and executes
    ↓
Server broadcasts to all connected clients
    ↓
Clients update their local VM state
```

---

## Data Flow & Interaction

### Complete System Architecture

```
┌────────────────────────────────────────────────────────────────────┐
│                        SimsVille (Game Client)                     │
│                      FSO.Client.TSOGame (Main)                     │
└───────────────────┬────────────────────────┬───────────────────────┘
                    │                        │
        ┌───────────▼─────────┐    ┌─────────▼──────────┐
        │   UILayer            │    │  _3DLayer          │
        │   (Screens/Panels)   │    │  (3D Scene Mgmt)   │
        └───────────┬──────────┘    └─────────┬──────────┘
                    │                         │
        ┌───────────▼──────────────────────────▼───────────┐
        │                CoreGameScreen                    │
        │  (Coordinates UI ↔ World ↔ VM ↔ Network)        │
        └────┬─────────┬────────────┬──────────┬──────────┘
             │         │            │          │
    ┌────────▼───┐ ┌──▼──────┐ ┌───▼────┐ ┌──▼─────────┐
    │ UILiveMode │ │UIBuild  │ │UIBuy   │ │UIPieMenu   │
    │ (HUD)      │ │Mode     │ │Mode    │ │(Interact)  │
    └────────────┘ └─────────┘ └────────┘ └────────────┘
                                    │
                    ┌───────────────┼───────────────┐
                    │               │               │
            ┌───────▼────┐  ┌───────▼────┐  ┌──────▼──────┐
            │   World    │  │ SimsAntics │  │   Vitaboy   │
            │ (Blueprint,│  │    (VM)    │  │  (Avatars)  │
            │ Components)│  │            │  │             │
            └────────────┘  └─────┬──────┘  └─────────────┘
                                  │
                    ┌─────────────┼─────────────┐
                    │             │             │
            ┌───────▼───┐  ┌──────▼──────┐  ┌──▼─────┐
            │    HIT    │  │  Network    │  │Content │
            │  (Audio)  │  │(Client/Srv) │  │Manager │
            └───────────┘  └──────┬──────┘  └────┬───┘
                                  │              │
                           ┌──────▼──────┐       │
                           │  SimsNet    │       │
                           │ (VM Server) │       │
                           └─────────────┘       │
                                                 │
                    ┌─────────────┬──────────────┘
                    │             │
            ┌───────▼───┐  ┌──────▼──────┐
            │sims.common│  │ sims.files  │
            │(Rendering,│  │  (FAR, IFF, │
            │ Vitaboy,  │  │  DBPF, HIT, │
            │ Framework)│  │  Formats)   │
            └─────┬─────┘  └──────┬──────┘
                  │                │
                  └────────┬───────┘
                           │
                  ┌────────▼────────┐
                  │  Game Assets    │
                  │  (FAR archives, │
                  │   IFF files)    │
                  └─────────────────┘
```

### Component Interaction Summary

1. **Game Startup**:
   - `Program.Main()` creates `TSOGame`
   - `TSOGame.Initialize()` sets up graphics, content loading
   - ContentManager loads initial assets from FAR archives
   - UILayer creates login/menu screens

2. **Loading a Lot**:
   - User selects lot from UI
   - `CoreGameScreen.LoadLot()` called
   - ContentManager loads lot IFF file (via `WorldObjectProvider`)
   - Blueprint created from lot data (walls, floors, objects)
   - VM initialized with Blueprint and VMContext
   - World components initialized (WallComponent, FloorComponent, etc.)
   - Avatars spawned via Vitaboy system

3. **Gameplay Loop** (60 FPS):
   - `TSOGame.Update()`:
     - UILayer.Update() (process input, update UI)
     - VM.Update() (execute BHAV scripts, update entity state)
     - World.Update() (update components, animations)
     - HIT.Update() (audio playback)
     - Network.Update() (send/receive packets)
   - `TSOGame.Draw()`:
     - World.Draw() (render 3D scene: terrain, floors, walls, objects, avatars)
     - UILayer.Draw() (render UI panels)

4. **User Interaction**:
   - User clicks on object
   - UILayer detects click, determines clicked object
   - UIPieMenu displays available interactions (from TTAB)
   - User selects interaction
   - VMNetInteractionCmd created and sent (if multiplayer)
   - VM.PushInteraction() queues BHAV script
   - VMThread executes BHAV instructions
   - Primitives modify entity state, play animations, play sounds
   - World and UI update to reflect changes

5. **Multiplayer Flow**:
   - Client connects to SimsNet server
   - Server creates VMServerDriver for client
   - Client sends VMNetCommands (interactions, build actions, chat, etc.)
   - Server validates and executes commands
   - Server broadcasts state changes to all clients
   - Clients update local VM state
   - UI and World reflect synchronized state

6. **Audio Playback**:
   - Game event triggers audio (object interaction, ambience, etc.)
   - HITVM.PlayTrack(trackID) called
   - Hitlist maps trackID to FSC file
   - FSCPlayer loads FSC sequence
   - HITThread manages playback
   - SoundEffect.Play() outputs audio

7. **Avatar Rendering**:
   - AvatarComponent holds Avatar instance
   - Avatar has Skeleton, Mesh, Binding, Animation, Outfit
   - Animator updates bone transforms (current animation frame)
   - Binding deforms mesh vertices based on bones
   - Mesh rendered with outfit textures
   - Shadow rendered (projected mesh)

---

## File Formats

### Archive Formats

| Format | Description | Compression | Used For |
|--------|-------------|-------------|----------|
| **FAR1** | File Archive v1 | None | Simple file containers |
| **FAR3** | File Archive v3 | RefPack | Main game assets (optimized) |
| **DBPF** | Database Packed File | Varies | The Sims 2/3 format (partial support) |

### IFF Chunk Types (50+ types)

| Chunk | Description | Purpose |
|-------|-------------|---------|
| **BHAV** | Behavior | Bytecode scripts for object behaviors |
| **OBJD** | Object Definition | Object metadata (GUID, name, price, etc.) |
| **GLOB** | Global Data | Global variables and settings |
| **STR** | String Table | Localized text strings |
| **SPR** | Sprite | 2D sprite graphics |
| **SPR2** | Sprite v2 | 2D sprite graphics (newer format) |
| **BMP** | Bitmap | Bitmap images |
| **DGRP** | Draw Group | Multi-sprite rendering groups |
| **TTAB** | Interaction Table | Pie menu interaction definitions |
| **TTAT** | Interaction Attributes | Interaction metadata |
| **TTAs** | Interaction Strings | Interaction text |
| **SLOT** | Object Slots | Object attachment points |
| **PALT** | Palette | Color palettes |
| **CTSS** | Catalog String Set | Catalog descriptions |
| **OBJF** | Object Functions | Object function metadata |
| **TMPR** | Template | Object templates |
| **BCON** | Behavior Constants | Numeric constants for BHAV |
| **TPRP** | Template Properties | Template property data |
| **FWAV** | Sound | WAV audio data |
| **TREE** | Tree | Tree table (interaction hierarchy) |
| **And 30+ more** | | |

### Audio Formats

| Format | Description | Platform | Codec |
|--------|-------------|----------|-------|
| **XA** | PlayStation ADPCM | PS1/PS2 | ADPCM |
| **UTK** | Maxis Proprietary | PC | Custom |
| **FSC** | Flexible Sequence Container | PC | Sequenced samples |
| **HIT** | HIT System (HSM, EVT, etc.) | PC | Various |

### Graphics Formats

| Format | Description | Usage |
|--------|-------------|-------|
| **TGA** | Truevision TGA | Textures |
| **BMP** | Bitmap | UI graphics, sprites |
| **SPR** | Sprite | Game sprites |
| **SPR2** | Sprite v2 | Game sprites (newer) |

### Avatar Formats

| Format | Description |
|--------|-------------|
| **.skel** | Skeleton definition (bone hierarchy) |
| **.mesh** | 3D geometry (vertices, normals, UVs, weights) |
| **.bnd** | Binding (mesh-to-skeleton mapping) |
| **.anim** | Animation (keyframes) |
| **.oft** | Outfit (clothing/texture mapping) |

### Other Formats

| Format | Description |
|--------|-------------|
| **OTF** | Font (OpenType-like) |
| **BCF** | Font (TS1) |
| **BMF** | Font (TS1) |
| **NGH** | Neighborhood (PS2) |

---

## Build & Dependencies

### Build System

**Solution**: `SimsVille.sln` (Visual Studio solution)

**Build Requirements**:
- **Windows**: Visual Studio 2017 or higher
- **Linux**: Mono
- **.NET Framework**: 4.5
- **MonoGame**: 3.6 or higher
- **OpenTK**: 1.2

**Build Command**:
```bash
# Windows
msbuild SimsVille.sln /p:Configuration=Release

# Linux
msbuild SimsVille.sln /p:Configuration=Release /p:Platform=x86
```

### Project Dependencies

```
SimsVille (Executable)
├── sims.common (Library)
│   └── sims.files (Library)
├── sims.files (Library)
└── SimsNet (Executable)
    ├── sims.common (Library)
    └── sims.files (Library)

sims.debug (Executable)
└── sims.files (Library)

sims.parser (Executable)
└── (None - uses DiscUtils NuGet package)
```

### External Dependencies

**NuGet Packages**:
- **MonoGame.Framework**: 3.6+ (core game framework)
- **OpenTK**: 1.2 (OpenGL bindings)
- **LogThis**: Logging framework
- **GonzoNet**: Network encryption library
- **DiscUtils**: Disc image parsing (sims.parser only)

**Runtime Requirements**:
- **Windows**: DirectX 11
- **Linux**: OpenGL 3.0, SDL2

### Content Requirements

**Game Files**: Original The Sims 1 installation required
- Complete Collection or Legacy Collection
- Default installation path expected
- FAR archives, IFF files, audio files loaded at runtime

---

## Development Guide

### Adding a New Object Interaction

1. Create BHAV chunk in IFF file (use sims.debug or external tool)
2. Add interaction to TTAB (pie menu table)
3. Add strings to STR chunk
4. Implement primitives if custom logic needed (SimsAntics/primitives/)
5. Test in VM

### Adding a New UI Screen

1. Create class extending `GameScreen`
2. Implement `Initialize()`, `Update()`, `Draw()`
3. Add UI elements (UIButton, UILabel, etc.)
4. Register screen with UILayer
5. Handle navigation (ShowScreen, RemoveScreen)

### Adding a New File Format

1. Create codec in `sims.files/formats/`
2. Implement parser (read binary data)
3. Create provider in `SimsVille/ContentManager/`
4. Register provider with ContentManager
5. Test loading/parsing

### Adding a New Audio Feature

1. Add to HIT system (`SimsVille/HIT/`)
2. Create HITThread or modify HITVM
3. Implement FSCPlayer extension or new player
4. Register with audio system
5. Test playback

### Adding Network Commands

1. Create `VMNetCommand` subclass (`SimsVille/Network/`)
2. Implement serialization (properties)
3. Add handler in VMServerDriver/VMClientDriver
4. Add UI packet sender if needed
5. Test client-server sync

---

## Key Design Patterns

1. **Provider Pattern**: ContentManager uses providers for resource loading
2. **Codec Pattern**: File formats decoded by specialized codecs
3. **Component Pattern**: World uses component-based architecture
4. **Command Pattern**: Network uses command objects for actions
5. **Observer Pattern**: Events throughout (NetworkController, UI, etc.)
6. **Singleton Pattern**: GameFacade provides global access
7. **State Machine**: NetworkController manages connection states
8. **Virtual Machine**: SimsAntics executes bytecode scripts
9. **Layer System**: Rendering uses layered approach (UI, 3D, 2D)
10. **Factory Pattern**: Object creation via factories (ContentManager)

---

## Performance Considerations

1. **Asset Caching**: 100MB memory limit, LRU eviction
2. **Batch Rendering**: Minimize draw calls with sprite batching
3. **Spatial Partitioning**: Object queries optimized with grid
4. **Thread Safety**: VM execution thread-safe with locks
5. **Network Optimization**: Command batching, delta updates
6. **Audio Streaming**: XA format streamed, FSC cached
7. **LOD System**: Avatars have level-of-detail support
8. **Culling**: Off-screen objects not rendered

---

## Security Considerations

1. **Network Encryption**: GonzoNet provides encryption
2. **Command Validation**: Server validates all client commands
3. **Sandbox**: VM execution sandboxed (max instruction limit)
4. **Input Sanitization**: UI input validated
5. **File Validation**: Asset files checksummed
6. **Access Control**: Server can ban/kick users

---

## Future Architecture

The modular design allows for future enhancements:

1. **Modern Rendering**: Swap MonoGame for Unity/Unreal
2. **Cloud Saves**: Add cloud save support via Network
3. **Mod Support**: ContentManager already supports mods
4. **Mobile Support**: MonoGame enables mobile ports
5. **VR Support**: 3D world architecture VR-ready
6. **Scripting API**: Expose VM to Lua/Python
7. **Database Backend**: Replace file-based with DB
8. **Microservices**: Split SimsNet into multiple services

---

## Glossary

| Term | Definition |
|------|------------|
| **BHAV** | Behavior - Bytecode scripts for object behaviors |
| **FAR** | File Archive - Compressed asset container |
| **IFF** | Interchange File Format - Chunk-based file format |
| **DBPF** | Database Packed File - The Sims 2/3 format |
| **VM** | Virtual Machine - Executes BHAV scripts |
| **DGRP** | Draw Group - Multi-sprite rendering definition |
| **TTAB** | Interaction Table - Pie menu definitions |
| **HIT** | Audio system - Sequence-based audio playback |
| **FSC** | Flexible Sequence Container - Audio sequence format |
| **XA** | PlayStation ADPCM audio format |
| **Vitaboy** | Avatar system - Mesh, skeleton, animation |
| **EOD** | Enhanced Object Display - Plugin UI system |
| **RTTI** | Runtime Type Info - Object metadata |
| **LotView** | 3D world view - Rendering system |
| **Blueprint** | World data - Walls, floors, objects |

---

## Conclusion

FreeSims is a complex, multi-layered game engine with:
- **6 major components** (client, server, libraries, tools)
- **7 key subsystems** in SimsVille (UI, World, VM, Audio, Avatar, Content, Network)
- **50+ file formats** (archives, chunks, audio, graphics, models)
- **Modular architecture** enabling extensibility and maintenance

This architecture map provides developers with a complete understanding of the codebase structure, component interactions, and data flows. Each subsystem is designed to be relatively independent, allowing focused development and testing.

For questions or contributions, refer to the LICENSE.md (Mozilla Public License v2.0) and project README.md.

---

**Document Version**: 1.0  
**Last Updated**: 2026-01-27  
**Maintainer**: FreeSims Project
