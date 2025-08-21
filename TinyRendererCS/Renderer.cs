using System;
using System.Collections.Generic;

namespace TinyRendererCS
{
    public interface IShader
    {
        void Vertex(int face, int vertex, out Vector4 glPosition);
        bool Fragment(Vector3 barycentric, out TgaColor color);
    }

    public static class Renderer
    {
        public static Matrix4x4 ModelView = Matrix4x4.Identity();
        public static Matrix4x4 Projection = Matrix4x4.Identity();
        public static Matrix4x4 Viewport = Matrix4x4.Identity();

        public static void SetViewport(int x, int y, int width, int height)
        {
            Viewport = MatrixHelper.Viewport(x, y, width, height);
        }

        public static void SetProjection(double coeff)
        {
            Projection = MatrixHelper.Perspective(coeff);
        }

        public static void SetLookAt(Vector3 eye, Vector3 center, Vector3 up)
        {
            ModelView = MatrixHelper.LookAt(eye, center, up);
        }

        public static void Rasterize(Vector4[] clipVertices, IShader shader, TgaImage image, double[] zBuffer)
        {
            // Convert clip coordinates to screen coordinates
            var screenVertices = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                var projected = Viewport * clipVertices[i];
                screenVertices[i] = new Vector3(
                    projected.X / projected.W,
                    projected.Y / projected.W,
                    projected.Z / projected.W
                );
            }

            // Find bounding box
            var bbox = GetBoundingBox(screenVertices, image.Width, image.Height);

            // Rasterize triangle
            for (int x = bbox.minX; x <= bbox.maxX; x++)
            {
                for (int y = bbox.minY; y <= bbox.maxY; y++)
                {
                    var barycentric = GetBarycentric(screenVertices, new Vector2(x, y));
                    
                    if (barycentric.X < 0 || barycentric.Y < 0 || barycentric.Z < 0)
                        continue; // Point is outside triangle

                    // Interpolate Z coordinate
                    double z = screenVertices[0].Z * barycentric.X + 
                              screenVertices[1].Z * barycentric.Y + 
                              screenVertices[2].Z * barycentric.Z;

                    int bufferIndex = y * image.Width + x;
                    if (bufferIndex >= 0 && bufferIndex < zBuffer.Length && z > zBuffer[bufferIndex])
                    {
                        zBuffer[bufferIndex] = z;
                        
                        if (shader.Fragment(barycentric, out TgaColor color))
                            continue; // Fragment was discarded
                        
                        image.Set(x, y, color);
                    }
                }
            }
        }

        private static (int minX, int minY, int maxX, int maxY) GetBoundingBox(Vector3[] vertices, int width, int height)
        {
            int minX = Math.Max(0, (int)Math.Min(Math.Min(vertices[0].X, vertices[1].X), vertices[2].X));
            int minY = Math.Max(0, (int)Math.Min(Math.Min(vertices[0].Y, vertices[1].Y), vertices[2].Y));
            int maxX = Math.Min(width - 1, (int)Math.Max(Math.Max(vertices[0].X, vertices[1].X), vertices[2].X));
            int maxY = Math.Min(height - 1, (int)Math.Max(Math.Max(vertices[0].Y, vertices[1].Y), vertices[2].Y));
            
            return (minX, minY, maxX, maxY);
        }

        private static Vector3 GetBarycentric(Vector3[] vertices, Vector2 point)
        {
            var v0 = vertices[2] - vertices[0];
            var v1 = vertices[1] - vertices[0];
            var v2 = new Vector3(point.X, point.Y, 0) - vertices[0];

            double dot00 = v0 * v0;
            double dot01 = v0 * v1;
            double dot02 = v0 * v2;
            double dot11 = v1 * v1;
            double dot12 = v1 * v2;

            double invDenom = 1.0 / (dot00 * dot11 - dot01 * dot01);
            double u = (dot11 * dot02 - dot01 * dot12) * invDenom;
            double v = (dot00 * dot12 - dot01 * dot02) * invDenom;

            return new Vector3(1.0 - u - v, v, u);
        }
    }

    public class PhongShader : IShader
    {
        private readonly Model _model;
        private readonly Vector3 _lightDir;
        private Vector2[] _varyingUv = new Vector2[3];
        private Vector3[] _varyingNormal = new Vector3[3];
        private Vector3[] _viewTriangle = new Vector3[3];

        public PhongShader(Model model, Vector3 lightDirection)
        {
            _model = model;
            // Transform light direction to view coordinates
            var lightVec4 = new Vector4(lightDirection.X, lightDirection.Y, lightDirection.Z, 0);
            var transformedLight = Renderer.ModelView * lightVec4;
            _lightDir = transformedLight.XYZ().Normalized();
        }

        public void Vertex(int face, int vertex, out Vector4 glPosition)
        {
            var normal = _model.GetNormal(face, vertex);
            var position = _model.GetVertex(face, vertex);
            
            glPosition = Renderer.ModelView * new Vector4(position.X, position.Y, position.Z, 1.0);
            _varyingUv[vertex] = _model.GetTexCoord(face, vertex);
            
            // Transform normal to view coordinates
            var normalVec4 = new Vector4(normal.X, normal.Y, normal.Z, 0);
            var transformedNormal = Renderer.ModelView.Invert().Transpose() * normalVec4;
            _varyingNormal[vertex] = transformedNormal.XYZ();
            
            _viewTriangle[vertex] = glPosition.XYZ();
            glPosition = Renderer.Projection * glPosition;
        }

        public bool Fragment(Vector3 barycentric, out TgaColor color)
        {
            // Interpolate normal
            var normal = (_varyingNormal[0] * barycentric.X + 
                         _varyingNormal[1] * barycentric.Y + 
                         _varyingNormal[2] * barycentric.Z).Normalized();

            // Interpolate UV coordinates
            var uv = _varyingUv[0] * barycentric.X + 
                     _varyingUv[1] * barycentric.Y + 
                     _varyingUv[2] * barycentric.Z;

            // Sample normal map if available
            var normalFromMap = _model.GetNormal(uv);
            if (normalFromMap.Norm() > 0.1) // If normal map exists
            {
                // Transform normal from tangent space (simplified)
                normal = normalFromMap.Normalized();
            }

            // Calculate lighting
            double diffuse = Math.Max(0.0, normal * _lightDir);
            
            // Calculate specular (simplified Phong)
            var viewDir = new Vector3(0, 0, 1); // Camera is at origin in view space
            var reflectDir = normal * (normal * _lightDir) * 2.0 - _lightDir;
            double specular = Math.Pow(Math.Max(0.0, reflectDir * viewDir), 32);
            
            // Sample textures
            var diffuseColor = _model.SampleDiffuse(uv);
            var specularColor = _model.SampleSpecular(uv);
            
            // Combine lighting
            double ambient = 0.1;
            double specularIntensity = specularColor.R / 255.0;
            
            color = new TgaColor(
                (byte)Math.Min(255, (ambient + diffuse) * diffuseColor.R + specular * specularIntensity * 255),
                (byte)Math.Min(255, (ambient + diffuse) * diffuseColor.G + specular * specularIntensity * 255),
                (byte)Math.Min(255, (ambient + diffuse) * diffuseColor.B + specular * specularIntensity * 255),
                255
            );

            return false; // Don't discard fragment
        }
    }
}
