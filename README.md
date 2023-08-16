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
  - [LGPL 2.1 License](LICENSE~/fluidsynth.txt)
  - Copyright © 2022 FluidSynth contributors
- [NVorbis](https://github.com/NVorbis/NVorbis)
  - [MIT License](LICENSE~/nvorbis.txt)
  - Copyright © 2020 Andrew Ward
- [GeneralUser GS sound font](https://www.schristiancollins.com/generaluser.php)
  - [GeneralUser GS v1.44 License v2.0](LICENSE~/generalusergs.txt)
- [Maestro - Midi Player Tool Kit - Free](https://assetstore.unity.com/packages/tools/audio/maestro-midi-player-tool-kit-free-107994)
  - [Asset Store Terms of Service](https://unity.com/legal/as-terms) - since this adds a "substantial amount of original creative work developed or licensed outside of the Asset Store" ([FAQ](https://assetstore.unity.com/browse/eula-faq)), I guess this is ok?
- The modifications and additions performed as part of this package
  - [Zero-Clause BSD License](https://opensource.org/license/0bsd/)
  - Copyright © 2022-2023 Arbitrary Combination

Note that while the modifications are under 0BSD,
you still must follow the terms and conditions of other licenses.

## Contributing

1. Fork this repository (For further details, see https://docs.github.com/en/github/getting-started-with-github/fork-a-repo)
2. Develop changes to a new branch to your forked repository
3. Create a Pull Request from your forked repository against this repository
  - Pull request description should answer these questions: "What has been changed" and "What is this for"

### Developing

One way to develop this Unity package is to create a new Unity Project and copy this package to its Assets folder.

This way .meta files (required by Unity) are generated automatically. Assets available in the package can now be tested and developed inside the project.

After making changes you can test your package by eg. installing it via Git URL:

Open `Packages/manifest.json` with your favorite text editor. Add following line to the dependencies block:
```json
    {
        "dependencies": {
            "com.arbitrary-combination.FluidSynthUnity": "https://github.com/ArbitraryCombination/FluidSynthUnity.git"
        }
    }
```

For further details, see [Unity docs about custom packages](https://docs.unity3d.com/Manual/CustomPackages.html).
