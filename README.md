# INFOGR Ray Tracer

A real-time ray-tracing application written in C# with OpenTK. The project renders a progressive image into a CPU-side framebuffer and displays it through an OpenGL textured fullscreen quad.

## Features

- Recursive ray tracing with reflections and emissive materials
- Sphere, plane, triangle, and triangle-mesh primitives
- BVH acceleration for triangle meshes
- Diffuse, specular, metallic, roughness, normal, and ambient-occlusion materials
- Directional, point, spot, and area lights
- Environment lighting from a cubemap
- Progressive accumulation and configurable anti-aliasing
- Perspective and orthographic projection modes
- Interactive camera controls and runtime scene switching
- Optional debug ray visualization
- SIMD-optimized triangle intersection when supported by the runtime

## Requirements

- .NET 10 SDK
- A desktop environment with OpenGL support
- A graphics driver capable of creating an OpenGL core profile context

The project uses the following NuGet packages:

- [OpenTK](https://www.nuget.org/packages/OpenTK)
- [AssimpNet](https://www.nuget.org/packages/AssimpNet)
- [SixLabors.ImageSharp](https://www.nuget.org/packages/SixLabors.ImageSharp)

## Build and run

From the repository root:

```powershell
dotnet restore
dotnet build
dotnet run
```

The application opens a `1024 x 512` window by default. Run it from the repository root so the relative paths to `assets/` and `shaders/` resolve correctly.

## Controls

| Key | Action |
| --- | --- |
| `W` `A` `S` `D` | Move the camera |
| `Q` / `E` | Yaw left / right |
| `F` / `R` | Pitch down / up |
| `Space` | Move the camera up |
| `Left Shift` | Move the camera down |
| `T` / `G` | Increase / decrease field of view |
| `Right Shift` | Toggle animation |
| `O` / `I` | Decrease / increase focus distance |
| `K` / `L` | Decrease / increase aperture |
| `Tab` | Toggle debug view |
| `Z` | Toggle anti-aliasing |
| `P` | Toggle projection mode |
| `1` | Mesh-heavy scene |
| `2` | Sphere grid scene |
| `3` | Mixed primitives scene |
| `4` | Area-light scene |
| `` ` `` | Show or hide the controls overlay |
| `Escape` | Exit the application |

Clicking the render window can be used for the application's pixel-inspection/debug functionality.

## Project layout

| Path | Description |
| --- | --- |
| `RayTracer.csproj` | .NET project and package references |
| `template.cs` | OpenTK window and OpenGL presentation pipeline |
| `MyApplication.cs` | Application setup, scenes, input, and rendering loop |
| `Raytracer.cs` | Ray traversal, shading, reflections, and lighting |
| `Scene.cs` | Scene graph, lights, environment, and intersections |
| `Primitive.cs`, `Triangle.cs`, `TriangleMesh.cs` | Renderable primitives and mesh data |
| `BVH.cs` | Bounding-volume hierarchy acceleration |
| `Camera.cs` | Camera movement and projection |
| `assets/` | Models, textures, cubemaps, and other scene resources |
| `shaders/` | GLSL shaders used to display the framebuffer |

## Asset paths

The application currently loads several resources using paths relative to the project output location, including:

```text
../../../assets/
../../../shaders/
```

For this reason, launching with `dotnet run` from the repository root is the recommended workflow. If you launch the compiled executable from another directory, make sure the expected relative directories are available.

## License and attribution

The project includes third-party scene resources and shader/template material. Refer to the attribution and license files included alongside those resources, including [`assets/cubemaps/yokohama3/readme.txt`](assets/cubemaps/yokohama3/readme.txt).
