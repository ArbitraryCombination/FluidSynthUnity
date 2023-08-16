# FluidSynth Unity

FluidSynth core ported to C# with Unity bindings

- [How to use](#how-to-use)
- [Install](#install)
  - [via OpenUPM](#via-openupm)
  - [via Git URL](#via-git-url)
  - [Tests](#tests)
- [Configuration](#configuration)

<!-- toc -->

## How to use

*Work In Progress*

## Install

### via OpenUPM

The package is also available on the [openupm registry](https://openupm.com/packages/com.arbitrary-combination.FluidSynthUnity). You can install it eg. via [openupm-cli](https://github.com/openupm/openupm-cli).

```
openupm add com.arbitrary-combination.FluidSynthUnity
```

### via Git URL

Open `Packages/manifest.json` with your favorite text editor. Add following line to the dependencies block:
```json
{
  "dependencies": {
    "com.arbitrary-combination.FluidSynthUnity": "https://github.com/arbitrary-combination/FluidSynthUnity.git"
  }
}
```

### Tests

The package can optionally be set as *testable*.
In practice this means that tests in the package will be visible in the [Unity Test Runner](https://docs.unity3d.com/2017.4/Documentation/Manual/testing-editortestsrunner.html).

Open `Packages/manifest.json` with your favorite text editor. Add following line **after** the dependencies block:
```json
{
  "dependencies": {
  },
  "testables": [ "com.arbitrary-combination.FluidSynthUnity" ]
}
```

## Configuration

*Work In Progress*

## License

LGPL 2.1 License

[FluidSynth](https://github.com/FluidSynth/fluidsynth)
Copyright © 2022 FluidSynth contributors

This C# port
Copyright © 2022-2023 Arbitrary Combination
