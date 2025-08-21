using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace TinyRendererCS
{
    public class Model
    {
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Vector2> _texCoords = new();
        private readonly List<int> _facetVertices = new();
        private readonly List<int> _facetNormals = new();
        private readonly List<int> _facetTexCoords = new();
        
        private TgaImage _diffuseMap = new();
        private TgaImage _normalMap = new();
        private TgaImage _specularMap = new();

        public int VertexCount => _vertices.Count;
        public int FaceCount => _facetVertices.Count / 3;

        public Model(string filename)
        {
            LoadObj(filename);
            LoadTextures(filename);
        }

        private void LoadObj(string filename)
        {
            try
            {
                using var reader = new StreamReader(filename);
                string? line;
                
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                        continue;

                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0) continue;

                    switch (parts[0])
                    {
                        case "v" when parts.Length >= 4:
                            if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) &&
                                double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double z))
                            {
                                _vertices.Add(new Vector3(x, y, z));
                            }
                            break;

                        case "vn" when parts.Length >= 4:
                            if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double nx) &&
                                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double ny) &&
                                double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double nz))
                            {
                                _normals.Add(new Vector3(nx, ny, nz).Normalized());
                            }
                            break;

                        case "vt" when parts.Length >= 3:
                            if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double u) &&
                                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                            {
                                _texCoords.Add(new Vector2(u, 1.0 - v)); // Flip V coordinate
                            }
                            break;

                        case "f" when parts.Length >= 4:
                            ParseFace(parts);
                            break;
                    }
                }

                Console.WriteLine($"Loaded model: {VertexCount} vertices, {FaceCount} faces, {_texCoords.Count} tex coords, {_normals.Count} normals");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading OBJ file: {ex.Message}");
            }
        }

        private void ParseFace(string[] parts)
        {
            var faceVertices = new List<(int v, int t, int n)>();

            for (int i = 1; i < parts.Length; i++)
            {
                var indices = parts[i].Split('/');
                if (indices.Length >= 1 && int.TryParse(indices[0], out int vIndex))
                {
                    int tIndex = 0, nIndex = 0;
                    
                    if (indices.Length >= 2 && !string.IsNullOrEmpty(indices[1]))
                        int.TryParse(indices[1], out tIndex);
                    
                    if (indices.Length >= 3 && !string.IsNullOrEmpty(indices[2]))
                        int.TryParse(indices[2], out nIndex);

                    // OBJ indices are 1-based, convert to 0-based
                    faceVertices.Add((vIndex - 1, tIndex - 1, nIndex - 1));
                }
            }

            // Triangulate face (assuming it's already triangulated)
            if (faceVertices.Count == 3)
            {
                foreach (var (v, t, n) in faceVertices)
                {
                    _facetVertices.Add(v);
                    _facetTexCoords.Add(t);
                    _facetNormals.Add(n);
                }
            }
            else if (faceVertices.Count > 3)
            {
                Console.WriteLine("Warning: Non-triangulated face detected. Only triangulated meshes are supported.");
            }
        }

        private void LoadTextures(string filename)
        {
            string baseName = Path.GetFileNameWithoutExtension(filename);
            string directory = Path.GetDirectoryName(filename) ?? "";

            LoadTexture(Path.Combine(directory, baseName + "_diffuse.tga"), ref _diffuseMap, "diffuse");
            LoadTexture(Path.Combine(directory, baseName + "_nm_tangent.tga"), ref _normalMap, "normal");
            LoadTexture(Path.Combine(directory, baseName + "_spec.tga"), ref _specularMap, "specular");
        }

        private void LoadTexture(string filename, ref TgaImage texture, string type)
        {
            if (File.Exists(filename))
            {
                bool success = texture.ReadTgaFile(filename);
                Console.WriteLine($"Texture {type}: {filename} - {(success ? "OK" : "FAILED")}");
            }
        }

        public Vector3 GetVertex(int index)
        {
            if (index < 0 || index >= _vertices.Count)
                return new Vector3();
            return _vertices[index];
        }

        public Vector3 GetVertex(int face, int vertex)
        {
            int index = _facetVertices[face * 3 + vertex];
            return GetVertex(index);
        }

        public Vector3 GetNormal(int face, int vertex)
        {
            int index = _facetNormals[face * 3 + vertex];
            if (index < 0 || index >= _normals.Count)
                return new Vector3(0, 0, 1);
            return _normals[index];
        }

        public Vector3 GetNormal(Vector2 uv)
        {
            if (_normalMap.Width == 0 || _normalMap.Height == 0)
                return new Vector3(0, 0, 1);

            var color = _normalMap.Get(
                (int)(uv.X * _normalMap.Width), 
                (int)(uv.Y * _normalMap.Height)
            );

            return new Vector3(
                color.R / 255.0 * 2.0 - 1.0,
                color.G / 255.0 * 2.0 - 1.0,
                color.B / 255.0 * 2.0 - 1.0
            );
        }

        public Vector2 GetTexCoord(int face, int vertex)
        {
            int index = _facetTexCoords[face * 3 + vertex];
            if (index < 0 || index >= _texCoords.Count)
                return new Vector2();
            return _texCoords[index];
        }

        public TgaColor SampleDiffuse(Vector2 uv)
        {
            if (_diffuseMap.Width == 0 || _diffuseMap.Height == 0)
                return TgaColor.White;

            return _diffuseMap.Get(
                (int)(uv.X * _diffuseMap.Width), 
                (int)(uv.Y * _diffuseMap.Height)
            );
        }

        public TgaColor SampleSpecular(Vector2 uv)
        {
            if (_specularMap.Width == 0 || _specularMap.Height == 0)
                return new TgaColor(0, 0, 0, 255);

            return _specularMap.Get(
                (int)(uv.X * _specularMap.Width), 
                (int)(uv.Y * _specularMap.Height)
            );
        }

        public TgaImage DiffuseMap => _diffuseMap;
        public TgaImage SpecularMap => _specularMap;
        public TgaImage NormalMap => _normalMap;
    }
}
