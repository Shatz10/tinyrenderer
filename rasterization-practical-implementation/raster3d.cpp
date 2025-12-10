// Copyright (C) 2012  www.scratchapixel.com
// Distributed under the terms of the CC BY-NC-ND 4.0 License.
// https://creativecommons.org/licenses/by-nc-nd/4.0/
// clang++ -o raster3d.exe raster3d.cpp -O3

#define _USE_MATH_DEFINES // 启用数学常量定义（如M_PI）

#include "geometry.h"       // 几何数学库（向量、矩阵等）
#include <fstream>          // 文件流操作
#include <chrono>           // 时间测量
#include <iostream>         // 输入输出流
#include "../tgaimage.h"    // TGA图像处理库
#include "../raster3d.h"   // 光栅化相关声明
#include "cow.h"           // 牛模型数据

static const float inchToMm = 25.4; // 英寸到毫米的转换常量
enum FitResolutionGate { kFill = 0, kOverscan }; // 屏幕适配模式枚举：填充或过扫描

/**
 * 计算屏幕坐标系的边界值
 * @param filmApertureWidth 胶片孔径宽度（英寸）
 * @param filmApertureHeight 胶片孔径高度（英寸）
 * @param imageWidth 图像宽度（像素）
 * @param imageHeight 图像高度（像素）
 * @param fitFilm 适配模式
 * @param nearClippingPlane 近裁剪面距离
 * @param focalLength 焦距（毫米）
 * @param top 输出：顶部边界
 * @param bottom 输出：底部边界
 * @param left 输出：左侧边界
 * @param right 输出：右侧边界
 */
void computeScreenCoordinates(
    const float &filmApertureWidth,
    const float &filmApertureHeight,
    const uint32_t &imageWidth,
    const uint32_t &imageHeight,
    const FitResolutionGate &fitFilm,
    const float &nearClippingPlane,
    const float &focalLength,
    float &top, float &bottom, float &left, float &right
)
{
    float filmAspectRatio = filmApertureWidth / filmApertureHeight; // 胶片宽高比
    float deviceAspectRatio = imageWidth / (float)imageHeight;       // 设备宽高比
    
    // 根据胶片尺寸和焦距计算顶部和右侧边界
    top = ((filmApertureHeight * inchToMm / 2) / focalLength) * nearClippingPlane;
    right = ((filmApertureWidth * inchToMm / 2) / focalLength) * nearClippingPlane;

    // 计算水平视场角并输出
    float fov = 2 * 180 / M_PI * atan((filmApertureWidth * inchToMm / 2) / focalLength);
    std::cerr << "Field of view " << fov << std::endl;
    
    float xscale = 1;
    float yscale = 1;
    
    // 根据适配模式调整缩放比例
    switch (fitFilm) {
        default:
        case kFill: // 填充模式：确保图像填满屏幕
            if (filmAspectRatio > deviceAspectRatio) {
                xscale = deviceAspectRatio / filmAspectRatio;
            }
            else {
                yscale = filmAspectRatio / deviceAspectRatio;
            }
            break;
        case kOverscan: // 过扫描模式：确保图像完全显示
            if (filmAspectRatio > deviceAspectRatio) {
                yscale = filmAspectRatio / deviceAspectRatio;
            }
            else {
                xscale = deviceAspectRatio / filmAspectRatio;
            }
            break;
    }
    
    // 应用缩放比例
    right *= xscale;
    top *= yscale;
    
    // 计算对称的底部和左侧边界
    bottom = -top;
    left = -right;
}

/**
 * 将世界坐标转换为光栅坐标
 * @param vertexWorld 世界坐标顶点
 * @param worldToCamera 世界到相机的变换矩阵
 * @param l 左侧边界
 * @param r 右侧边界
 * @param t 顶部边界
 * @param b 底部边界
 * @param near 近裁剪面距离
 * @param imageWidth 图像宽度
 * @param imageHeight 图像高度
 * @param vertexRaster 输出：光栅坐标
 */
void convertToRaster(
    const Vec3f &vertexWorld,
    const Matrix44f &worldToCamera,
    const float &l,
    const float &r,
    const float &t,
    const float &b,
    const float &near,
    const uint32_t &imageWidth,
    const uint32_t &imageHeight,
    Vec3f &vertexRaster
)
{
    Vec3f vertexCamera;

    // 世界坐标转相机坐标
    worldToCamera.multVecMatrix(vertexWorld, vertexCamera);
    
    Vec2f vertexScreen;
    // 透视投影到屏幕坐标
    vertexScreen.x = near * vertexCamera.x / -vertexCamera.z;
    vertexScreen.y = near * vertexCamera.y / -vertexCamera.z;
    
    Vec2f vertexNDC;
    // 屏幕坐标转归一化设备坐标(NDC)
    vertexNDC.x = 2 * vertexScreen.x / (r - l) - (r + l) / (r - l);
    vertexNDC.y = 2 * vertexScreen.y / (t - b) - (t + b) / (t - b);

    // NDC坐标转光栅坐标
    vertexRaster.x = (vertexNDC.x + 1) / 2 * imageWidth;
    vertexRaster.y = (1 - vertexNDC.y) / 2 * imageHeight;
    vertexRaster.z = -vertexCamera.z; // 存储深度值
}

// 三个数的最小值
float min3(const float &a, const float &b, const float &c)
{ return std::min(a, std::min(b, c)); }

// 三个数的最大值
float max3(const float &a, const float &b, const float &c)
{ return std::max(a, std::max(b, c)); }

// 计算三角形边函数（用于重心坐标计算）
float edgeFunction(const Vec3f &a, const Vec3f &b, const Vec3f &c)
{ return (c[0] - a[0]) * (b[1] - a[1]) - (c[1] - a[1]) * (b[0] - a[0]); }

// 全局常量定义
const uint32_t imageWidth = 640;  // 图像宽度（像素）
const uint32_t imageHeight = 480; // 图像高度（像素）
// 世界到相机的变换矩阵（相机位置和朝向）
const Matrix44f worldToCamera = {0.707107, -0.331295, 0.624695, 0, 0, 0.883452, 0.468521, 0, -0.707107, -0.331295, 0.624695, 0, -1.63871, -5.747777, -40.400412, 1};

const uint32_t ntris = 3156;          // 三角形数量（牛模型）
const float nearClippingPlane = 1;    // 近裁剪面距离
const float farClippingPLane = 1000;  // 远裁剪面距离
float focalLength = 20;               // 焦距（毫米）

// 35mm全孔径胶片尺寸（英寸）
float filmApertureWidth = 0.980;
float filmApertureHeight = 0.735;

/**
 * 3D场景渲染主函数
 * 实现完整的3D光栅化渲染管线
 */
void render3DScene()
{
    // 计算相机到世界的变换矩阵（用于某些计算）
    Matrix44f cameraToWorld = worldToCamera.inverse();

    float t, b, l, r; // 屏幕边界变量
    
    // 计算屏幕坐标边界
    computeScreenCoordinates(
        filmApertureWidth, filmApertureHeight,
        imageWidth, imageHeight,
        kOverscan,
        nearClippingPlane,
        focalLength,
        t, b, l, r);
    
    // 分配帧缓冲区（存储像素颜色，RGB格式）
    Vec3<unsigned char> *frameBuffer = new Vec3<unsigned char>[imageWidth * imageHeight];

    // 初始化帧缓冲区为白色
    for (uint32_t i = 0; i < imageWidth * imageHeight; ++i) {
		frameBuffer[i] = Vec3<unsigned char>(255);
	}

    // 分配深度缓冲区（存储每个像素的深度值）
    float *depthBuffer = new float[imageWidth * imageHeight];

    // 初始化深度缓冲区为远裁剪面值
    for (uint32_t i = 0; i < imageWidth * imageHeight; ++i) {
		depthBuffer[i] = farClippingPLane;
	}

    // 开始渲染计时
    auto t_start = std::chrono::high_resolution_clock::now();
    
    // 遍历所有三角形进行光栅化
    for (uint32_t i = 0; i < ntris; ++i) {
        // 获取三角形三个顶点的世界坐标
        const Vec3f &v0 = vertices[nvertices[i * 3]];
        const Vec3f &v1 = vertices[nvertices[i * 3 + 1]];
        const Vec3f &v2 = vertices[nvertices[i * 3 + 2]];
        
        // 转换为光栅坐标
        Vec3f v0Raster, v1Raster, v2Raster;
        convertToRaster(v0, worldToCamera, l, r, t, b, nearClippingPlane, imageWidth, imageHeight, v0Raster);
        convertToRaster(v1, worldToCamera, l, r, t, b, nearClippingPlane, imageWidth, imageHeight, v1Raster);
        convertToRaster(v2, worldToCamera, l, r, t, b, nearClippingPlane, imageWidth, imageHeight, v2Raster);
        
        // 透视校正：存储1/z值用于插值
        v0Raster.z = 1 / v0Raster.z,
        v1Raster.z = 1 / v1Raster.z,
        v2Raster.z = 1 / v2Raster.z;
        
        // 获取纹理坐标
        Vec2f st0 = st[stindices[i * 3]];
        Vec2f st1 = st[stindices[i * 3 + 1]];
        Vec2f st2 = st[stindices[i * 3 + 2]];

        // 透视校正纹理坐标
        st0 *= v0Raster.z, st1 *= v1Raster.z, st2 *= v2Raster.z;
    
        // 计算三角形包围盒（最小/最大x,y坐标）
        float xmin = min3(v0Raster.x, v1Raster.x, v2Raster.x);
        float ymin = min3(v0Raster.y, v1Raster.y, v2Raster.y);
        float xmax = max3(v0Raster.x, v1Raster.x, v2Raster.x);
        float ymax = max3(v0Raster.y, v1Raster.y, v2Raster.y);
        
        // 跳过完全在屏幕外的三角形（优化）
        if (xmin > imageWidth - 1 || xmax < 0 || ymin > imageHeight - 1 || ymax < 0) continue;

        // 计算实际需要渲染的像素范围（与屏幕边界相交的部分）
        uint32_t x0 = std::max(int32_t(0), (int32_t)(std::floor(xmin)));
        uint32_t x1 = std::min(int32_t(imageWidth) - 1, (int32_t)(std::floor(xmax)));
        uint32_t y0 = std::max(int32_t(0), (int32_t)(std::floor(ymin)));
        uint32_t y1 = std::min(int32_t(imageHeight) - 1, (int32_t)(std::floor(ymax)));

        // 计算三角形面积（用于重心坐标归一化）
        float area = edgeFunction(v0Raster, v1Raster, v2Raster);
        
        // 遍历包围盒内的所有像素
        for (uint32_t y = y0; y <= y1; ++y) {
            for (uint32_t x = x0; x <= x1; ++x) {
                // 像素中心采样点
                Vec3f pixelSample(x + 0.5, y + 0.5, 0);
                
                // 计算重心坐标权重（判断点是否在三角形内）
                float w0 = edgeFunction(v1Raster, v2Raster, pixelSample);
                float w1 = edgeFunction(v2Raster, v0Raster, pixelSample);
                float w2 = edgeFunction(v0Raster, v1Raster, pixelSample);
                
                // 如果点在三角形内（所有权重非负）
                if (w0 >= 0 && w1 >= 0 && w2 >= 0) {
                    // 归一化重心坐标权重
                    w0 /= area;
                    w1 /= area;
                    w2 /= area;
                    
                    // 透视校正插值：计算1/z的加权平均值
                    float oneOverZ = v0Raster.z * w0 + v1Raster.z * w1 + v2Raster.z * w2;
                    float z = 1 / oneOverZ; // 实际深度值
 
					// 深度测试：如果当前像素更近
					if (z < depthBuffer[y * imageWidth + x]) {
                        // 更新深度缓冲区
                        depthBuffer[y * imageWidth + x] = z;
                        
                        // 纹理坐标插值
                        Vec2f st = st0 * w0 + st1 * w1 + st2 * w2;
                        st *= z; // 透视校正
                        
                        // 计算相机空间坐标用于光照计算
                        Vec3f v0Cam, v1Cam, v2Cam;
                        worldToCamera.multVecMatrix(v0, v0Cam);
                        worldToCamera.multVecMatrix(v1, v1Cam);
                        worldToCamera.multVecMatrix(v2, v2Cam);
                        
                        // 屏幕空间插值（用于光照计算）
                        float px = (v0Cam.x/-v0Cam.z) * w0 + (v1Cam.x/-v1Cam.z) * w1 + (v2Cam.x/-v2Cam.z) * w2;
                        float py = (v0Cam.y/-v0Cam.z) * w0 + (v1Cam.y/-v1Cam.z) * w1 + (v2Cam.y/-v2Cam.z) * w2;
                        
                        // 相机空间点坐标
                        Vec3f pt(px * z, py * z, -z);
                        
                        // 计算三角形法线
                        Vec3f n = (v1Cam - v0Cam).crossProduct(v2Cam - v0Cam);
                        n.normalize();
                        
                        // 计算视线方向
                        Vec3f viewDirection = -pt;
                        viewDirection.normalize();
                        
                        // 计算法线与视线方向的点积（漫反射光照）
                        float nDotView =  std::max(0.f, n.dotProduct(viewDirection));
                        
                        // 棋盘格纹理效果
                        const int M = 10; // 棋盘格密度
                        float checker = (fmod(st.x * M, 1.0) > 0.5) ^ (fmod(st.y * M, 1.0) < 0.5);
                        float c = 0.3 * (1 - checker) + 0.7 * checker; // 混合颜色
                        
                        // 应用纹理到光照
                        nDotView *= c;
                        
                        // 设置像素颜色（灰度值，基于光照强度）
                        frameBuffer[y * imageWidth + x].x = nDotView * 255;
                        frameBuffer[y * imageWidth + x].y = nDotView * 255;
                        frameBuffer[y * imageWidth + x].z = nDotView * 255;
                    }
                }
            }
        }
    }
    
	// 结束计时并输出渲染时间
	auto t_end = std::chrono::high_resolution_clock::now();
	auto passedTime = std::chrono::duration<double, std::milli>(t_end - t_start).count();
	std::cerr << "Wall passed time: " << passedTime << "ms" << std::endl;
    
	// 创建TGA图像对象（RGB格式）
	TGAImage image(imageWidth, imageHeight, TGAImage::RGB);
	
	// 将帧缓冲区数据复制到TGA图像对象中
	for (int y = 0; y < imageHeight; y++) {
		for (int x = 0; x < imageWidth; x++) {
			Vec3<unsigned char> color = frameBuffer[y * imageWidth + x];
			image.set(x, y, TGAColor(color.x, color.y, color.z, 255));
		}
	}
	
	// 输出TGA格式图片文件
	image.write_tga_file("./output.tga");
    
	// 释放动态分配的内存
	delete [] frameBuffer;
	delete [] depthBuffer;
    
    return;
}