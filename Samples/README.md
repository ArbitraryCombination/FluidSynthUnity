# Samples

This folder contains a project which showcases how to use this package.

# Running package tests

You can store sample project here to run your packages tests on,
since Unity does not provide standalone package testing solution.

1. Create new Unity project
2. Import your package in manifest.json and mark it as testable:
```json
{
  "dependencies": {
    "cz.omnibullet.fluidsynth-unity": "https://github.com/ArbitraryCombination/FluidSynthUnity.git"
  },
  "testables": [ "cz.omnibullet.fluidsynth-unity" ]
}
```
