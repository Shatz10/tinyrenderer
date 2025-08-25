using System;
using System.IO;

namespace TinyRendererCS
{
    /// <summary>
    /// TGA颜色结构体，用于表示BGRA格式的像素颜色
    /// TGA格式使用BGRA顺序存储颜色分量
    /// </summary>
    public struct TgaColor
    {
        /// <summary>蓝色分量 (0-255)</summary>
        public byte B;
        /// <summary>绿色分量 (0-255)</summary>
        public byte G;
        /// <summary>红色分量 (0-255)</summary>
        public byte R;
        /// <summary>透明度分量 (0-255，255为完全不透明)</summary>
        public byte A;

        /// <summary>
        /// 构造函数，创建一个新的TGA颜色
        /// </summary>
        /// <param name="r">红色分量 (0-255)</param>
        /// <param name="g">绿色分量 (0-255)</param>
        /// <param name="b">蓝色分量 (0-255)</param>
        /// <param name="a">透明度分量 (0-255，默认255为不透明)</param>
        public TgaColor(byte r = 0, byte g = 0, byte b = 0, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        /// <summary>
        /// 索引器，允许通过索引访问颜色分量
        /// 0=蓝色, 1=绿色, 2=红色, 3=透明度
        /// </summary>
        /// <param name="i">颜色分量索引 (0-3)</param>
        /// <returns>对应的颜色分量值</returns>
        public byte this[int i]
        {
            get => i switch { 0 => B, 1 => G, 2 => R, 3 => A, _ => throw new IndexOutOfRangeException() };
            set
            {
                switch (i)
                {
                    case 0: B = value; break;
                    case 1: G = value; break;
                    case 2: R = value; break;
                    case 3: A = value; break;
                    default: throw new IndexOutOfRangeException();
                }
            }
        }

        /// <summary>预定义的白色</summary>
        public static TgaColor White => new(255, 255, 255, 255);
        /// <summary>预定义的红色</summary>
        public static TgaColor Red => new(255, 0, 0, 255);
        /// <summary>预定义的绿色</summary>
        public static TgaColor Green => new(0, 255, 0, 255);
        /// <summary>预定义的蓝色</summary>
        public static TgaColor Blue => new(0, 0, 255, 255);
        /// <summary>预定义的黑色</summary>
        public static TgaColor Black => new(0, 0, 0, 255);
    }

    /// <summary>
    /// TGA图像类，用于读取、写入和操作TGA格式的图像文件
    /// TGA (Truevision Graphics Adapter) 是一种常用的图像格式，支持多种颜色深度
    /// </summary>
    public class TgaImage
    {
        /// <summary>
        /// 图像格式枚举，定义每个像素的字节数
        /// </summary>
        public enum Format
        {
            /// <summary>灰度图像，每像素1字节</summary>
            Grayscale = 1,
            /// <summary>RGB彩色图像，每像素3字节</summary>
            RGB = 3,
            /// <summary>RGBA彩色图像（含透明通道），每像素4字节</summary>
            RGBA = 4
        }

        /// <summary>图像宽度（像素）</summary>
        private int _width;
        /// <summary>图像高度（像素）</summary>
        private int _height;
        /// <summary>图像格式</summary>
        private Format _format;
        /// <summary>图像像素数据，按行存储</summary>
        private byte[] _data;

        /// <summary>获取图像宽度</summary>
        public int Width => _width;
        /// <summary>获取图像高度</summary>
        public int Height => _height;
        /// <summary>获取图像格式</summary>
        public Format ImageFormat => _format;

        /// <summary>
        /// 默认构造函数，创建一个空的TGA图像
        /// </summary>
        public TgaImage()
        {
            _width = 0;
            _height = 0;
            _format = Format.RGB;
            _data = Array.Empty<byte>();
        }

        /// <summary>
        /// 构造函数，创建指定尺寸和格式的TGA图像
        /// </summary>
        /// <param name="width">图像宽度</param>
        /// <param name="height">图像高度</param>
        /// <param name="format">图像格式</param>
        public TgaImage(int width, int height, Format format)
        {
            _width = width;
            _height = height;
            _format = format;
            _data = new byte[width * height * (int)format];
        }

        /// <summary>
        /// 从文件读取TGA图像数据
        /// </summary>
        /// <param name="filename">TGA文件路径</param>
        /// <returns>读取成功返回true，失败返回false</returns>
        public bool ReadTgaFile(string filename)
        {
            try
            {
                using var file = new FileStream(filename, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(file);

                // 读取TGA文件头（18字节）
                byte idLength = reader.ReadByte();           // 图像ID字段长度
                byte colorMapType = reader.ReadByte();       // 颜色映射表类型
                byte dataTypeCode = reader.ReadByte();       // 图像数据类型
                ushort colorMapOrigin = reader.ReadUInt16(); // 颜色映射表起始索引
                ushort colorMapLength = reader.ReadUInt16(); // 颜色映射表长度
                byte colorMapDepth = reader.ReadByte();      // 颜色映射表位深度
                ushort xOrigin = reader.ReadUInt16();        // 图像X坐标起点
                ushort yOrigin = reader.ReadUInt16();        // 图像Y坐标起点
                ushort width = reader.ReadUInt16();          // 图像宽度
                ushort height = reader.ReadUInt16();         // 图像高度
                byte bitsPerPixel = reader.ReadByte();       // 每像素位数
                byte imageDescriptor = reader.ReadByte();    // 图像描述符

                _width = width;
                _height = height;
                _format = (Format)(bitsPerPixel / 8);  // 根据位深度确定格式

                // 跳过图像ID字段
                if (idLength > 0)
                    reader.ReadBytes(idLength);

                // 跳过颜色映射表
                if (colorMapType != 0)
                    reader.ReadBytes(colorMapLength * (colorMapDepth / 8));

                _data = new byte[_width * _height * (int)_format];

                // 根据数据类型读取图像数据
                if (dataTypeCode == 2 || dataTypeCode == 3) // 未压缩数据
                {
                    _data = reader.ReadBytes(_data.Length);
                }
                else if (dataTypeCode == 10 || dataTypeCode == 11) // RLE压缩数据
                {
                    LoadRleData(reader);
                }

                // 如果需要，垂直翻转图像（TGA原点在左下角）
                if ((imageDescriptor & 0x20) == 0)
                    FlipVertically();

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 将TGA图像数据写入文件
        /// </summary>
        /// <param name="filename">输出文件路径</param>
        /// <param name="vflip">是否垂直翻转图像</param>
        /// <param name="rle">是否使用RLE压缩</param>
        /// <returns>写入成功返回true，失败返回false</returns>
        public bool WriteTgaFile(string filename, bool vflip = true, bool rle = true)
        {
            try
            {
                using var file = new FileStream(filename, FileMode.Create, FileAccess.Write);
                using var writer = new BinaryWriter(file);

                // 根据格式和压缩选项确定数据类型代码
                byte dataTypeCode = (byte)(rle ? ((int)_format == 1 ? 11 : 10) : ((int)_format == 1 ? 3 : 2));

                // 写入TGA文件头
                writer.Write((byte)0);                      // 图像ID长度
                writer.Write((byte)0);                      // 颜色映射表类型
                writer.Write(dataTypeCode);                 // 数据类型代码
                writer.Write((ushort)0);                    // 颜色映射表起始索引
                writer.Write((ushort)0);                    // 颜色映射表长度
                writer.Write((byte)0);                      // 颜色映射表位深度
                writer.Write((ushort)0);                    // X坐标起点
                writer.Write((ushort)0);                    // Y坐标起点
                writer.Write((ushort)_width);               // 图像宽度
                writer.Write((ushort)_height);              // 图像高度
                writer.Write((byte)((int)_format * 8));     // 每像素位数
                writer.Write((byte)(vflip ? 0 : 0x20));     // 图像描述符

                // 根据压缩选项写入图像数据
                if (rle)
                {
                    UnloadRleData(writer);  // 使用RLE压缩写入
                }
                else
                {
                    writer.Write(_data);    // 直接写入未压缩数据
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取指定位置的像素颜色
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <returns>像素颜色，如果坐标超出范围则返回默认颜色</returns>
        public TgaColor Get(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _width || y >= _height)
                return new TgaColor();

            int offset = (y * _width + x) * (int)_format;
            var color = new TgaColor();

            switch (_format)
            {
                case Format.Grayscale:
                    // 灰度图像：将灰度值复制到RGB三个通道
                    color = new TgaColor(_data[offset], _data[offset], _data[offset], 255);
                    break;
                case Format.RGB:
                    // RGB图像：注意TGA格式存储顺序为BGR
                    color = new TgaColor(_data[offset + 2], _data[offset + 1], _data[offset], 255);
                    break;
                case Format.RGBA:
                    // RGBA图像：包含透明通道
                    color = new TgaColor(_data[offset + 2], _data[offset + 1], _data[offset], _data[offset + 3]);
                    break;
            }

            return color;
        }

        /// <summary>
        /// 设置指定位置的像素颜色
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="color">要设置的颜色</param>
        public void Set(int x, int y, TgaColor color)
        {
            if (x < 0 || y < 0 || x >= _width || y >= _height)
                return;

            int offset = (y * _width + x) * (int)_format;

            switch (_format)
            {
                case Format.Grayscale:
                    // 灰度图像：将RGB转换为灰度值（简单平均）
                    _data[offset] = (byte)((color.R + color.G + color.B) / 3);
                    break;
                case Format.RGB:
                    // RGB图像：按BGR顺序存储
                    _data[offset] = color.B;
                    _data[offset + 1] = color.G;
                    _data[offset + 2] = color.R;
                    break;
                case Format.RGBA:
                    // RGBA图像：按BGRA顺序存储
                    _data[offset] = color.B;
                    _data[offset + 1] = color.G;
                    _data[offset + 2] = color.R;
                    _data[offset + 3] = color.A;
                    break;
            }
        }

        /// <summary>
        /// 水平翻转图像（左右镜像）
        /// </summary>
        public void FlipHorizontally()
        {
            int bytesPerPixel = (int)_format;
            int halfWidth = _width / 2;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < halfWidth; x++)
                {
                    int leftOffset = (y * _width + x) * bytesPerPixel;
                    int rightOffset = (y * _width + (_width - 1 - x)) * bytesPerPixel;

                    // 交换左右对应像素的所有字节
                    for (int b = 0; b < bytesPerPixel; b++)
                    {
                        (_data[leftOffset + b], _data[rightOffset + b]) = (_data[rightOffset + b], _data[leftOffset + b]);
                    }
                }
            }
        }

        /// <summary>
        /// 垂直翻转图像（上下颠倒）
        /// </summary>
        public void FlipVertically()
        {
            int bytesPerPixel = (int)_format;
            int lineSize = _width * bytesPerPixel;  // 每行的字节数
            var line = new byte[lineSize];          // 临时缓冲区

            // 交换上下对应的行
            for (int y = 0; y < _height / 2; y++)
            {
                int topOffset = y * lineSize;
                int bottomOffset = (_height - 1 - y) * lineSize;

                // 三步交换：上行->临时，下行->上行，临时->下行
                Array.Copy(_data, topOffset, line, 0, lineSize);
                Array.Copy(_data, bottomOffset, _data, topOffset, lineSize);
                Array.Copy(line, 0, _data, bottomOffset, lineSize);
            }
        }

        /// <summary>
        /// 从二进制流中加载RLE（行程长度编码）压缩的图像数据
        /// RLE是一种简单的无损压缩算法，将连续相同的数据压缩为计数+数据的形式
        /// </summary>
        /// <param name="reader">二进制读取器</param>
        private void LoadRleData(BinaryReader reader)
        {
            int pixelCount = _width * _height;
            int currentPixel = 0;
            int bytesPerPixel = (int)_format;

            while (currentPixel < pixelCount)
            {
                byte chunkHeader = reader.ReadByte();
                
                if (chunkHeader < 128) // 原始数据块（未压缩）
                {
                    chunkHeader++;  // 实际像素数量 = header + 1
                    for (int i = 0; i < chunkHeader; i++)
                    {
                        // 逐个读取像素数据
                        for (int b = 0; b < bytesPerPixel; b++)
                        {
                            _data[currentPixel * bytesPerPixel + b] = reader.ReadByte();
                        }
                        currentPixel++;
                    }
                }
                else // RLE压缩数据块（重复像素）
                {
                    chunkHeader -= 127;  // 重复次数 = header - 127
                    var pixel = new byte[bytesPerPixel];
                    
                    // 读取要重复的像素数据
                    for (int b = 0; b < bytesPerPixel; b++)
                        pixel[b] = reader.ReadByte();

                    // 重复写入该像素
                    for (int i = 0; i < chunkHeader; i++)
                    {
                        for (int b = 0; b < bytesPerPixel; b++)
                        {
                            _data[currentPixel * bytesPerPixel + b] = pixel[b];
                        }
                        currentPixel++;
                    }
                }
            }
        }

        /// <summary>
        /// 将图像数据以RLE（行程长度编码）压缩格式写入二进制流
        /// 该方法会分析像素数据，决定使用原始编码还是RLE压缩编码
        /// </summary>
        /// <param name="writer">二进制写入器</param>
        private void UnloadRleData(BinaryWriter writer)
        {
            const int maxChunkLength = 128;  // TGA格式限制的最大块长度
            int pixelCount = _width * _height;
            int currentPixel = 0;
            int bytesPerPixel = (int)_format;

            while (currentPixel < pixelCount)
            {
                int chunkStart = currentPixel;
                bool isRaw = true;      // 是否使用原始编码
                byte runLength = 1;     // 当前块的长度

                // 前瞻分析，决定使用RLE还是原始编码
                while (currentPixel + runLength < pixelCount && runLength < maxChunkLength)
                {
                    bool same = true;
                    // 比较当前像素与下一个像素是否相同
                    for (int b = 0; b < bytesPerPixel; b++)
                    {
                        if (_data[currentPixel * bytesPerPixel + b] != 
                            _data[(currentPixel + runLength) * bytesPerPixel + b])
                        {
                            same = false;
                            break;
                        }
                    }

                    if (same)
                    {
                        runLength++;
                        // 如果连续相同像素达到4个或以上，使用RLE编码
                        if (runLength >= 4) isRaw = false;
                    }
                    else
                    {
                        // 如果已经决定使用RLE但遇到不同像素，停止
                        if (!isRaw) break;
                        runLength++;
                    }
                }

                if (isRaw)  // 使用原始编码
                {
                    // 写入原始数据块头（0-127表示原始数据）
                    writer.Write((byte)(runLength - 1));
                    // 写入所有像素数据
                    for (int i = 0; i < runLength; i++)
                    {
                        for (int b = 0; b < bytesPerPixel; b++)
                        {
                            writer.Write(_data[(currentPixel + i) * bytesPerPixel + b]);
                        }
                    }
                }
                else  // 使用RLE编码
                {
                    // 写入RLE数据块头（128-255表示RLE数据）
                    writer.Write((byte)(runLength + 127));
                    // 只写入一个像素数据（将被重复）
                    for (int b = 0; b < bytesPerPixel; b++)
                    {
                        writer.Write(_data[currentPixel * bytesPerPixel + b]);
                    }
                }

                currentPixel += runLength;
            }
        }
    }
}
