# Contributing

1. Fork this repository (For further details, see https://docs.github.com/en/github/getting-started-with-github/fork-a-repo)
2. Develop changes to a new branch to your forked repository
3. Create a Pull Request from your forked repository against this repository
   - Pull request description should answer these questions: "What has been changed" and "What is this for"

## Developing

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
