# Samples

`ExampleUnityProject` contains a project which showcases how to use this package.

`DevelopmentProject` symlinks (on Windows you may need special permissions for symlinks to work)
the package files into it, so that it can be used for development without the copying workaround
outlined below.

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
