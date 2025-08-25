using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace TinyRendererCS
{
    /// <summary>
    /// PNG图像类，用于处理PNG格式的图像文件
    /// 使用ImageSharp库进行PNG图像的读取、写入和操作
    /// 主要用作渲染器的帧缓冲区，存储最终的渲染结果
    /// </summary>
    public class PngImage
    {
        /// <summary>内部图像对象，使用RGB24像素格式</summary>
        private readonly Image<Rgb24> _image;

        /// <summary>获取图像宽度</summary>
        public int Width => _image.Width;
        /// <summary>获取图像高度</summary>
        public int Height => _image.Height;

        /// <summary>
        /// 构造函数，创建指定尺寸的PNG图像
        /// </summary>
        /// <param name="width">图像宽度</param>
        /// <param name="height">图像高度</param>
        public PngImage(int width, int height)
        {
            _image = new Image<Rgb24>(width, height);
        }

        /// <summary>
        /// 设置指定位置的像素颜色
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="color">要设置的颜色（TGA格式）</param>
        public void Set(int x, int y, TgaColor color)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                // 将TGA颜色转换为RGB24格式
                _image[x, y] = new Rgb24(color.R, color.G, color.B);
            }
        }

        /// <summary>
        /// 获取指定位置的像素颜色
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <returns>像素颜色（TGA格式），如果坐标超出范围则返回默认颜色</returns>
        public TgaColor Get(int x, int y)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                var pixel = _image[x, y];
                // 将RGB24格式转换为TGA颜色，设置透明度为255（不透明）
                return new TgaColor(pixel.R, pixel.G, pixel.B, 255);
            }
            return new TgaColor();
        }

        /// <summary>
        /// 将图像保存为PNG文件
        /// </summary>
        /// <param name="filename">输出文件路径</param>
        /// <returns>保存成功返回true，失败返回false</returns>
        public bool SavePng(string filename)
        {
            try
            {
                _image.SaveAsPng(filename);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 释放图像资源
        /// </summary>
        public void Dispose()
        {
            _image?.Dispose();
        }
    }
}
