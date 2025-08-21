using System;
using System.IO;

namespace TinyRendererCS
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: TinyRendererCS <model.obj>");
                Console.WriteLine("Example: TinyRendererCS ../obj/african_head/african_head.obj");
                return;
            }

            const int width = 800;
            const int height = 800;
            var lightDir = new Vector3(1, 1, 1).Normalized();
            var eye = new Vector3(1, 1, 3);
            var center = new Vector3(0, 0, 0);
            var up = new Vector3(0, 1, 0);

            // Setup rendering pipeline
            Renderer.SetLookAt(eye, center, up);
            Renderer.SetViewport(width / 8, height / 8, width * 3 / 4, height * 3 / 4);
            Renderer.SetProjection(-1.0 / (eye - center).Norm());

            // Create framebuffer and z-buffer
            var framebuffer = new TgaImage(width, height, TgaImage.Format.RGB);
            var zBuffer = new double[width * height];
            for (int i = 0; i < zBuffer.Length; i++)
                zBuffer[i] = double.MinValue;

            // Process each model
            for (int m = 0; m < args.Length; m++)
            {
                string modelPath = args[m];
                
                if (!File.Exists(modelPath))
                {
                    Console.WriteLine($"Error: Model file '{modelPath}' not found.");
                    continue;
                }

                Console.WriteLine($"Loading model: {modelPath}");
                var model = new Model(modelPath);
                var shader = new PhongShader(model, lightDir);

                // Render each triangle
                for (int face = 0; face < model.FaceCount; face++)
                {
                    var clipVertices = new Vector4[3];
                    
                    // Run vertex shader for each vertex of the triangle
                    for (int vertex = 0; vertex < 3; vertex++)
                    {
                        shader.Vertex(face, vertex, out clipVertices[vertex]);
                    }
                    
                    // Rasterize the triangle
                    Renderer.Rasterize(clipVertices, shader, framebuffer, zBuffer);
                }
            }

            // Save the rendered image
            string outputPath = "framebuffer.tga";
            bool success = framebuffer.WriteTgaFile(outputPath);
            
            if (success)
            {
                Console.WriteLine($"Rendered image saved to: {outputPath}");
            }
            else
            {
                Console.WriteLine("Error: Failed to save rendered image.");
            }
        }
    }
}
