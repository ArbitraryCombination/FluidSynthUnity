# FluidSynth Unity

[FluidSynth](https://www.fluidsynth.org/) ported to C# through
[Midi Player Tool Kit (Unity Asset)](https://assetstore.unity.com/packages/tools/audio/maestro-midi-player-tool-kit-free-107994) 
(although none of that code should remain) with Unity bindings.
Comes with [NVorbis](https://github.com/NVorbis/NVorbis) OGG Vorbis decoder.

All parts are heavily modified: stripped down and optimized.
Stripping down relates to playing MIDI files, stereo sound and other features that were not needed for Omnibullet.
The goal was to optimize performance and the binary size.

- [How to use](#how-to-use)
- [Install](#install)
- [Configuration](#configuration)

<!-- toc -->

## How to use

*Work In Progress*

## Install

Open `Packages/manifest.json` with your favorite text editor. Add following line to the dependencies block:
```json
{
  "dependencies": {
    "cz.omnibullet.fluidsynth-unity": "https://github.com/ArbitraryCombination/FluidSynthUnity.git"
  }
}
```

## Configuration

*Work In Progress*

## License

The project builds upon multiple separately licensed components:

- [FluidSynth](https://github.com/FluidSynth/fluidsynth)
  - [LGPL 2.1 License](fluidsynth.LICENSE.txt)
  - Copyright © 2022 FluidSynth contributors
- [NVorbis](https://github.com/NVorbis/NVorbis)
  - [MIT License](nvorbis.LICENSE.txt)
  - Copyright © 2020 Andrew Ward
- [GeneralUser GS sound font](https://www.schristiancollins.com/generaluser.php)
  - [GeneralUser GS v1.44 License v2.0](generalusergs.LICENSE.txt)
- [Maestro - Midi Player Tool Kit - Free](https://assetstore.unity.com/packages/tools/audio/maestro-midi-player-tool-kit-free-107994)
  - [Asset Store Terms of Service](https://unity.com/legal/as-terms) - since this adds a "substantial amount of original creative work developed or licensed outside of the Asset Store" ([FAQ](https://assetstore.unity.com/browse/eula-faq)), I guess this is ok?
- The modifications and additions performed as part of this package
  - [Zero-Clause BSD License](https://opensource.org/license/0bsd/)
  - Copyright © 2022-2023 Arbitrary Combination

Note that while the modifications are under 0BSD,
you still need to follow the conditions of other licenses.
