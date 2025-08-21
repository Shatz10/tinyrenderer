# TinyRenderer C# Version

A C# port of the TinyRenderer software renderer, originally created by ssloy. This implementation demonstrates how 3D graphics rendering works without using any graphics libraries.

## Features

- **Software Rendering**: Pure C# implementation with no external graphics dependencies
- **OBJ Model Loading**: Supports Wavefront OBJ files with vertices, normals, and texture coordinates
- **TGA Image Support**: Read and write TGA image files for textures and output
- **Phong Shading**: Diffuse and specular lighting with normal mapping support
- **Z-Buffer**: Proper depth testing for hidden surface removal
- **Matrix Math**: Complete 3D math library with vectors and matrices

## Usage

```bash
dotnet run <model.obj>
```

Example:
```bash
dotnet run ../obj/african_head/african_head.obj
```

The rendered image will be saved as `framebuffer.tga`.

## Project Structure

- `Geometry.cs` - Vector and matrix math library
- `TgaImage.cs` - TGA image format handling
- `Model.cs` - OBJ file loading and texture management
- `Renderer.cs` - Software rendering pipeline and shaders
- `Program.cs` - Main application entry point

## Rendering Pipeline

1. **Model Loading**: Parse OBJ files and load associated textures
2. **Vertex Transformation**: Transform vertices through model-view and projection matrices
3. **Rasterization**: Convert triangles to pixels using barycentric coordinates
4. **Fragment Shading**: Calculate pixel colors with Phong lighting model
5. **Z-Buffer Testing**: Handle depth testing for proper occlusion

## Supported Textures

The renderer automatically looks for these texture files:
- `*_diffuse.tga` - Base color/albedo map
- `*_nm_tangent.tga` - Normal map for surface detail
- `*_spec.tga` - Specular map for reflectivity

## Requirements

- .NET 8.0 or later
- No external dependencies

## Building

```bash
dotnet build
```

## Comparison with Original

This C# version maintains the same core algorithms and structure as the original C++ implementation while leveraging C#'s memory safety and modern language features.
