using System;
using System.Collections.Generic;

namespace TinyRendererCS
{
    /// <summary>
    /// 着色器接口，定义了顶点着色器和片元着色器的基本结构
    /// </summary>
    public interface IShader
    {
        /// <summary>
        /// 顶点着色器，处理单个顶点的变换
        /// </summary>
        /// <param name="face">当前面的索引</param>
        /// <param name="vertex">当前顶点的索引(0-2)</param>
        /// <param name="glPosition">输出裁剪空间中的顶点位置</param>
        void Vertex(int face, int vertex, out Vector4 glPosition);
        
        /// <summary>
        /// 片元着色器，计算像素颜色
        /// </summary>
        /// <param name="barycentric">当前像素在三角形内的重心坐标</param>
        /// <param name="color">输出的颜色值</param>
        /// <returns>是否丢弃该片元</returns>
        bool Fragment(Vector3 barycentric, out TgaColor color);
    }

    /// <summary>
    /// 渲染器静态类，提供基本的渲染管线功能
    /// </summary>
    public static class Renderer
    {
        // 模型视图矩阵：将顶点从模型空间变换到相机空间
        public static Matrix4x4 ModelView = Matrix4x4.Identity();
        // 投影矩阵：将顶点从相机空间变换到裁剪空间
        public static Matrix4x4 Projection = Matrix4x4.Identity();
        // 视口矩阵：将标准化设备坐标(NDC)映射到屏幕空间
        public static Matrix4x4 Viewport = Matrix4x4.Identity();

        /// <summary>
        /// 设置视口变换矩阵
        /// </summary>
        /// <param name="x">视口左下角X坐标</param>
        /// <param name="y">视口左下角Y坐标</param>
        /// <param name="width">视口宽度</param>
        /// <param name="height">视口高度</param>
        public static void SetViewport(int x, int y, int width, int height)
        {
            Viewport = MatrixHelper.Viewport(x, y, width, height);
        }

        /// <summary>
        /// 设置透视投影矩阵
        /// </summary>
        /// <param name="coeff">透视系数，控制透视程度</param>
        public static void SetProjection(double coeff)
        {
            Projection = MatrixHelper.Perspective(coeff);
        }

        /// <summary>
        /// 设置观察矩阵(相机位置和朝向)
        /// </summary>
        /// <param name="eye">相机位置</param>
        /// <param name="center">观察目标点</param>
        /// <param name="up">上向量，定义相机的上方向</param>
        public static void SetLookAt(Vector3 eye, Vector3 center, Vector3 up)
        {
            ModelView = MatrixHelper.LookAt(eye, center, up);
        }

        /// <summary>
        /// 光栅化三角形
        /// </summary>
        /// <param name="clipVertices">裁剪空间中的三个顶点</param>
        /// <param name="shader">使用的着色器</param>
        /// <param name="image">输出图像</param>
        /// <param name="zBuffer">深度缓存，用于可见性测试</param>
        public static void Rasterize(Vector4[] clipVertices, IShader shader, PngImage image, double[] zBuffer)
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

        /// <summary>
        /// 计算三角形在屏幕空间中的包围盒
        /// </summary>
        /// <param name="vertices">三角形三个顶点的屏幕坐标</param>
        /// <param name="width">图像宽度</param>
        /// <param name="height">图像高度</param>
        /// <returns>包围盒的边界(minX, minY, maxX, maxY)</returns>
        private static (int minX, int minY, int maxX, int maxY) GetBoundingBox(Vector3[] vertices, int width, int height)
        {
            int minX = Math.Max(0, (int)Math.Min(Math.Min(vertices[0].X, vertices[1].X), vertices[2].X));
            int minY = Math.Max(0, (int)Math.Min(Math.Min(vertices[0].Y, vertices[1].Y), vertices[2].Y));
            int maxX = Math.Min(width - 1, (int)Math.Max(Math.Max(vertices[0].X, vertices[1].X), vertices[2].X));
            int maxY = Math.Min(height - 1, (int)Math.Max(Math.Max(vertices[0].Y, vertices[1].Y), vertices[2].Y));
            
            return (minX, minY, maxX, maxY);
        }

        /// <summary>
        /// 计算点P在三角形内的重心坐标
        /// </summary>
        /// <param name="vertices">三角形三个顶点</param>
        /// <param name="point">要测试的点</param>
        /// <returns>重心坐标(α, β, γ)</returns>
        /// <summary>
        /// 计算点P在三角形ABC内的重心坐标(α,β,γ)
        /// 重心坐标满足: P = αA + βB + γC, 其中 α + β + γ = 1
        /// </summary>
        /// <param name="vertices">三角形三个顶点[A,B,C]</param>
        /// <param name="point">要测试的点P</param>
        /// <returns>重心坐标(α,β,γ)</returns>
        /// <remarks>
        /// 数学原理：
        /// 1. 将点P表示为P = A + u*(B-A) + v*(C-A)
        /// 2. 可以重写为 P = (1-u-v)*A + u*B + v*C
        /// 3. 其中(1-u-v, u, v)就是重心坐标(α,β,γ)
        /// 4. 通过解以下方程组求u,v：
        ///    (P-A) = u*(B-A) + v*(C-A)
        /// 5. 使用Cramer法则解这个线性方程组
        /// </remarks>
        private static Vector3 GetBarycentric(Vector3[] vertices, Vector2 point)
        {
            // 计算边向量 v0 = C - A, v1 = B - A
            var v0 = vertices[2] - vertices[0];
            var v1 = vertices[1] - vertices[0];
            
            // 计算向量 v2 = P - A
            var v2 = new Vector3(point.X, point.Y, 0) - vertices[0];

            // 计算点积
            double dot00 = v0 * v0;  // v0·v0
            double dot01 = v0 * v1;  // v0·v1
            double dot02 = v0 * v2;  // v0·v2
            double dot11 = v1 * v1;  // v1·v1
            double dot12 = v1 * v2;  // v1·v2

            // 计算行列式 det = |v0·v0 v0·v1|
            //                  |v1·v0 v1·v1|
            // 根据Cramer法则计算u和v
            double invDenom = 1.0 / (dot00 * dot11 - dot01 * dot01);
            
            // u = (v1·v1)(v0·v2) - (v0·v1)(v1·v2) / det
            double u = (dot11 * dot02 - dot01 * dot12) * invDenom;
            
            // v = (v0·v0)(v1·v2) - (v0·v1)(v0·v2) / det
            double v = (dot00 * dot12 - dot01 * dot02) * invDenom;

            // 返回重心坐标(α,β,γ) = (1-u-v, v, u)
            return new Vector3(1.0 - u - v, v, u);
        }
    }

    /// <summary>
    /// Phong着色器实现，支持漫反射、高光和法线贴图
    /// </summary>
    public class PhongShader : IShader
    {
        private readonly Model _model;           // 3D模型数据
        private readonly Vector3 _lightDir;       // 光源方向(在视图空间中)
        private Vector2[] _varyingUv = new Vector2[3];     // 顶点纹理坐标插值变量
        private Vector3[] _varyingNormal = new Vector3[3]; // 顶点法线插值变量
        private Vector3[] _viewTriangle = new Vector3[3];  // 视图空间中的三角形顶点

        /// <summary>
        /// 创建Phong着色器实例
        /// </summary>
        /// <param name="model">要渲染的3D模型</param>
        /// <param name="lightDirection">世界空间中的光照方向</param>
        public PhongShader(Model model, Vector3 lightDirection)
        {
            _model = model;
            // Transform light direction to view coordinates
            var lightVec4 = new Vector4(lightDirection.X, lightDirection.Y, lightDirection.Z, 0);
            var transformedLight = Renderer.ModelView * lightVec4;
            _lightDir = transformedLight.XYZ().Normalized();
        }

        /// <summary>
        /// <summary>
        /// 顶点着色器：处理顶点变换和准备插值变量
        /// 1. 将顶点位置从模型空间变换到裁剪空间
        /// 2. 准备用于插值的变量(UV坐标、法线等)
        /// 3. 法线变换使用逆转置矩阵(逆转置矩阵)来保持法线与表面的垂直关系
        /// </summary>
        /// <remarks>
        /// 法线变换说明：
        /// 法线不能直接使用模型视图矩阵变换，需要使用模型视图矩阵的逆转置矩阵
        /// 这是因为法线是方向向量，需要保持与表面的垂直关系
        /// 数学推导：如果切向量T经过矩阵M变换后为T'=MT
        /// 则法线N需要满足N'^T * T' = 0 => N^T * T = 0
        /// 所以 N'^T * MT = 0 => N'^T = N^T * M^-1 => N' = (M^-1)^T * N
        /// </remarks>
        public void Vertex(int face, int vertex, out Vector4 glPosition)
        {
            // 获取顶点法线(模型空间)
            var normal = _model.GetNormal(face, vertex);
            // 获取顶点位置(模型空间)
            var position = _model.GetVertex(face, vertex);
            
            // 将顶点位置变换到视图空间
            // 公式: v_view = ModelView * v_model
            glPosition = Renderer.ModelView * new Vector4(position.X, position.Y, position.Z, 1.0);
            
            // 保存顶点的纹理坐标用于插值
            _varyingUv[vertex] = _model.GetTexCoord(face, vertex);
            
            // 法线变换：使用模型视图矩阵的逆转置矩阵
            // 这样可以保持法线与表面的垂直关系
            var normalVec4 = new Vector4(normal.X, normal.Y, normal.Z, 0);
            var transformedNormal = Renderer.ModelView.Invert().Transpose() * normalVec4;
            _varyingNormal[vertex] = transformedNormal.XYZ();
            
            // 保存视图空间中的顶点位置(用于后续计算)
            _viewTriangle[vertex] = glPosition.XYZ();
            
            // 将顶点位置从视图空间变换到裁剪空间
            // 公式: v_clip = Projection * v_view
            glPosition = Renderer.Projection * glPosition;
        }

        /// <summary>
        /// 片元着色器：计算每个像素的最终颜色
        /// 实现了Phong光照模型，包含环境光、漫反射、高光分量
        /// 支持法线贴图增强表面细节
        /// </summary>
        /// <remarks>
        /// Phong光照模型公式：
        /// I = I_ambient + I_diffuse + I_specular
        /// I_ambient = k_a * I_light
        /// I_diffuse = k_d * (L·N) * I_light
        /// I_specular = k_s * (R·V)^n * I_light
        /// 其中：
        /// - L: 光线方向(指向光源)
        /// - N: 表面法线
        /// - R: 反射向量 R = 2*(N·L)*N - L
        /// - V: 视线方向(指向相机)
        /// - n: 高光指数，控制高光范围
        /// </remarks>
        public bool Fragment(Vector3 barycentric, out TgaColor color)
        {
            // 使用重心坐标插值计算当前像素的法线
            // 公式: N = α*N0 + β*N1 + γ*N2
            // 其中(α,β,γ)是当前像素的重心坐标，N0,N1,N2是顶点法线
            var normal = (_varyingNormal[0] * barycentric.X + 
                         _varyingNormal[1] * barycentric.Y + 
                         _varyingNormal[2] * barycentric.Z).Normalized();

            // 插值计算当前像素的UV坐标
            // 公式: uv = α*uv0 + β*uv1 + γ*uv2
            var uv = _varyingUv[0] * barycentric.X + 
                     _varyingUv[1] * barycentric.Y + 
                     _varyingUv[2] * barycentric.Z;

            // 采样法线贴图(如果存在)
            // 法线贴图存储了切线空间中的法线扰动
            var normalFromMap = _model.GetNormal(uv);
            if (normalFromMap.Norm() > 0.1) // 如果法线贴图存在
            {
                // 简化处理：直接使用法线贴图中的法线
                // 注意：完整的实现应该将法线从切线空间变换到视图空间
                normal = normalFromMap.Normalized();
            }

            // 计算漫反射光照
            // 公式: diffuse = max(0, N·L)
            // N是表面法线，L是指向光源的方向
            double diffuse = Math.Max(0.0, normal * _lightDir);
            
            // 计算高光反射(Phong模型)
            // 视图方向(在视图空间中，相机位于原点，方向为(0,0,1))
            var viewDir = new Vector3(0, 0, 1);
            
            // 计算反射向量 R = 2*(N·L)*N - L
            var reflectDir = normal * (normal * _lightDir) * 2.0 - _lightDir;
            
            // 计算高光强度 (R·V)^n
            // n是高光指数，控制高光的集中程度
            double specular = Math.Pow(Math.Max(0.0, reflectDir * viewDir), 32);
            
            // 采样漫反射贴图和高光贴图
            var diffuseColor = _model.SampleDiffuse(uv);
            var specularColor = _model.SampleSpecular(uv);
            
            // 组合光照效果
            double ambient = 0.1;  // 环境光强度
            double specularIntensity = specularColor.R / 255.0;  // 从高光贴图中获取高光强度
            
            // 最终颜色计算: 环境光 + 漫反射 + 高光
            // 每个颜色通道单独计算
            color = new TgaColor(
                (byte)Math.Min(255, (ambient + diffuse) * diffuseColor.R + specular * specularIntensity * 255),
                (byte)Math.Min(255, (ambient + diffuse) * diffuseColor.G + specular * specularIntensity * 255),
                (byte)Math.Min(255, (ambient + diffuse) * diffuseColor.B + specular * specularIntensity * 255),
                255  // 完全不透明
            );

            return false; // 不丢弃该片元
        }
    }
}
