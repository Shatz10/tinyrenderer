using System;
using System.IO;

namespace TinyRendererCS
{
    public struct TgaColor
    {
        public byte B, G, R, A;

        public TgaColor(byte r = 0, byte g = 0, byte b = 0, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

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

        public static TgaColor White => new(255, 255, 255, 255);
        public static TgaColor Red => new(255, 0, 0, 255);
        public static TgaColor Green => new(0, 255, 0, 255);
        public static TgaColor Blue => new(0, 0, 255, 255);
        public static TgaColor Black => new(0, 0, 0, 255);
    }

    public class TgaImage
    {
        public enum Format
        {
            Grayscale = 1,
            RGB = 3,
            RGBA = 4
        }

        private int _width;
        private int _height;
        private Format _format;
        private byte[] _data;

        public int Width => _width;
        public int Height => _height;
        public Format ImageFormat => _format;

        public TgaImage()
        {
            _width = 0;
            _height = 0;
            _format = Format.RGB;
            _data = Array.Empty<byte>();
        }

        public TgaImage(int width, int height, Format format)
        {
            _width = width;
            _height = height;
            _format = format;
            _data = new byte[width * height * (int)format];
        }

        public bool ReadTgaFile(string filename)
        {
            try
            {
                using var file = new FileStream(filename, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(file);

                // Read TGA header
                byte idLength = reader.ReadByte();
                byte colorMapType = reader.ReadByte();
                byte dataTypeCode = reader.ReadByte();
                ushort colorMapOrigin = reader.ReadUInt16();
                ushort colorMapLength = reader.ReadUInt16();
                byte colorMapDepth = reader.ReadByte();
                ushort xOrigin = reader.ReadUInt16();
                ushort yOrigin = reader.ReadUInt16();
                ushort width = reader.ReadUInt16();
                ushort height = reader.ReadUInt16();
                byte bitsPerPixel = reader.ReadByte();
                byte imageDescriptor = reader.ReadByte();

                _width = width;
                _height = height;
                _format = (Format)(bitsPerPixel / 8);

                // Skip image ID
                if (idLength > 0)
                    reader.ReadBytes(idLength);

                // Skip color map
                if (colorMapType != 0)
                    reader.ReadBytes(colorMapLength * (colorMapDepth / 8));

                _data = new byte[_width * _height * (int)_format];

                if (dataTypeCode == 2 || dataTypeCode == 3) // Uncompressed
                {
                    _data = reader.ReadBytes(_data.Length);
                }
                else if (dataTypeCode == 10 || dataTypeCode == 11) // RLE compressed
                {
                    LoadRleData(reader);
                }

                // Flip vertically if needed (TGA origin is bottom-left)
                if ((imageDescriptor & 0x20) == 0)
                    FlipVertically();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool WriteTgaFile(string filename, bool vflip = true, bool rle = true)
        {
            try
            {
                using var file = new FileStream(filename, FileMode.Create, FileAccess.Write);
                using var writer = new BinaryWriter(file);

                byte dataTypeCode = (byte)(rle ? ((int)_format == 1 ? 11 : 10) : ((int)_format == 1 ? 3 : 2));

                // Write TGA header
                writer.Write((byte)0);  // ID length
                writer.Write((byte)0);  // Color map type
                writer.Write(dataTypeCode);  // Data type code
                writer.Write((ushort)0);  // Color map origin
                writer.Write((ushort)0);  // Color map length
                writer.Write((byte)0);   // Color map depth
                writer.Write((ushort)0); // X origin
                writer.Write((ushort)0); // Y origin
                writer.Write((ushort)_width);
                writer.Write((ushort)_height);
                writer.Write((byte)((int)_format * 8)); // Bits per pixel
                writer.Write((byte)(vflip ? 0 : 0x20)); // Image descriptor

                if (rle)
                {
                    UnloadRleData(writer);
                }
                else
                {
                    writer.Write(_data);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public TgaColor Get(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _width || y >= _height)
                return new TgaColor();

            int offset = (y * _width + x) * (int)_format;
            var color = new TgaColor();

            switch (_format)
            {
                case Format.Grayscale:
                    color = new TgaColor(_data[offset], _data[offset], _data[offset], 255);
                    break;
                case Format.RGB:
                    color = new TgaColor(_data[offset + 2], _data[offset + 1], _data[offset], 255);
                    break;
                case Format.RGBA:
                    color = new TgaColor(_data[offset + 2], _data[offset + 1], _data[offset], _data[offset + 3]);
                    break;
            }

            return color;
        }

        public void Set(int x, int y, TgaColor color)
        {
            if (x < 0 || y < 0 || x >= _width || y >= _height)
                return;

            int offset = (y * _width + x) * (int)_format;

            switch (_format)
            {
                case Format.Grayscale:
                    _data[offset] = (byte)((color.R + color.G + color.B) / 3);
                    break;
                case Format.RGB:
                    _data[offset] = color.B;
                    _data[offset + 1] = color.G;
                    _data[offset + 2] = color.R;
                    break;
                case Format.RGBA:
                    _data[offset] = color.B;
                    _data[offset + 1] = color.G;
                    _data[offset + 2] = color.R;
                    _data[offset + 3] = color.A;
                    break;
            }
        }

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

                    for (int b = 0; b < bytesPerPixel; b++)
                    {
                        (_data[leftOffset + b], _data[rightOffset + b]) = (_data[rightOffset + b], _data[leftOffset + b]);
                    }
                }
            }
        }

        public void FlipVertically()
        {
            int bytesPerPixel = (int)_format;
            int lineSize = _width * bytesPerPixel;
            var line = new byte[lineSize];

            for (int y = 0; y < _height / 2; y++)
            {
                int topOffset = y * lineSize;
                int bottomOffset = (_height - 1 - y) * lineSize;

                Array.Copy(_data, topOffset, line, 0, lineSize);
                Array.Copy(_data, bottomOffset, _data, topOffset, lineSize);
                Array.Copy(line, 0, _data, bottomOffset, lineSize);
            }
        }

        private void LoadRleData(BinaryReader reader)
        {
            int pixelCount = _width * _height;
            int currentPixel = 0;
            int bytesPerPixel = (int)_format;

            while (currentPixel < pixelCount)
            {
                byte chunkHeader = reader.ReadByte();
                
                if (chunkHeader < 128) // Raw chunk
                {
                    chunkHeader++;
                    for (int i = 0; i < chunkHeader; i++)
                    {
                        for (int b = 0; b < bytesPerPixel; b++)
                        {
                            _data[currentPixel * bytesPerPixel + b] = reader.ReadByte();
                        }
                        currentPixel++;
                    }
                }
                else // RLE chunk
                {
                    chunkHeader -= 127;
                    var pixel = new byte[bytesPerPixel];
                    for (int b = 0; b < bytesPerPixel; b++)
                        pixel[b] = reader.ReadByte();

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

        private void UnloadRleData(BinaryWriter writer)
        {
            const int maxChunkLength = 128;
            int pixelCount = _width * _height;
            int currentPixel = 0;
            int bytesPerPixel = (int)_format;

            while (currentPixel < pixelCount)
            {
                int chunkStart = currentPixel;
                bool isRaw = true;
                byte runLength = 1;

                // Look ahead to determine if we should use RLE or raw encoding
                while (currentPixel + runLength < pixelCount && runLength < maxChunkLength)
                {
                    bool same = true;
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
                        if (runLength >= 4) isRaw = false;
                    }
                    else
                    {
                        if (!isRaw) break;
                        runLength++;
                    }
                }

                if (isRaw)
                {
                    writer.Write((byte)(runLength - 1));
                    for (int i = 0; i < runLength; i++)
                    {
                        for (int b = 0; b < bytesPerPixel; b++)
                        {
                            writer.Write(_data[(currentPixel + i) * bytesPerPixel + b]);
                        }
                    }
                }
                else
                {
                    writer.Write((byte)(runLength + 127));
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
