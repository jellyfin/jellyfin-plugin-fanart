<h1 align="center">Jellyfin Fanart Plugin</h1>
<h3 align="center">Part of the <a href="https://jellyfin.org/">Jellyfin Project</a></h3>

## About

The Jellyfin Fanart plugin allows you to get images for your Movies and T.V Shows from <a href="https://fanart.tv/">Fanart.</a>


## Build & Installation Process

1. Clone this repository
2. Ensure you have .NET Core SDK set up and installed
3. Build the plugin with your favorite IDE or the `dotnet` command:

```
dotnet publish --configuration Release --output bin
```

4. Place the resulting `Jellyfin.Plugin.Fanart.dll` file in a folder called `plugins/` inside your Jellyfin data directory

### Screenshot

<img src=screenshot.png>
