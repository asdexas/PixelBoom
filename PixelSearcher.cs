using System;
using System.Runtime.InteropServices;
using static PixelBoom.NativeMethods;

namespace PixelBoom
{
    public class PixelSearcher : IDisposable
    {
        private IntPtr _screenDC;
        private IntPtr _memDC;
        private IntPtr _bitmap;
        private IntPtr _oldBitmap;
        private byte[] _buffer;
        private int _bufWidth;
        private int _bufHeight;
        private int _bufX;
        private int _bufY;
        private bool _disposed;

        public PixelSearcher()
        {
            _screenDC = GetDC(IntPtr.Zero);
            _memDC = CreateCompatibleDC(_screenDC);
        }

        public void CaptureRegion(int x1, int y1, int x2, int y2)
        {
            _bufX = x1;
            _bufY = y1;
            _bufWidth = x2 - x1 + 1;
            _bufHeight = y2 - y1 + 1;

            if (_bitmap != IntPtr.Zero)
            {
                SelectObject(_memDC, _oldBitmap);
                DeleteObject(_bitmap);
            }

            _bitmap = CreateCompatibleBitmap(_screenDC, _bufWidth, _bufHeight);
            _oldBitmap = SelectObject(_memDC, _bitmap);
            BitBlt(_memDC, 0, 0, _bufWidth, _bufHeight, _screenDC, x1, y1, SRCCOPY);

            var bmi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = _bufWidth,
                    biHeight = -_bufHeight,
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0
                },
                bmiColors = new uint[1]
            };

            _buffer = new byte[_bufWidth * _bufHeight * 4];
            GetDIBits(_memDC, _bitmap, 0, (uint)_bufHeight, _buffer, ref bmi, DIB_RGB_COLORS);
        }

        public int PixelSearch(int x1, int y1, int x2, int y2, uint targetColor, int variation,
            out int foundX, out int foundY)
        {
            foundX = 0;
            foundY = 0;

            try
            {
                CaptureRegion(x1, y1, x2, y2);

                byte targetR = (byte)((targetColor >> 16) & 0xFF);
                byte targetG = (byte)((targetColor >> 8) & 0xFF);
                byte targetB = (byte)(targetColor & 0xFF);

                for (int y = 0; y < _bufHeight; y++)
                {
                    for (int x = 0; x < _bufWidth; x++)
                    {
                        int offset = (y * _bufWidth + x) * 4;
                        byte b = _buffer[offset];
                        byte g = _buffer[offset + 1];
                        byte r = _buffer[offset + 2];

                        if (Math.Abs(r - targetR) <= variation &&
                            Math.Abs(g - targetG) <= variation &&
                            Math.Abs(b - targetB) <= variation)
                        {
                            foundX = x + _bufX;
                            foundY = y + _bufY;
                            return 0;
                        }
                    }
                }
                return 1;
            }
            catch
            {
                return 2;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_bitmap != IntPtr.Zero)
                {
                    SelectObject(_memDC, _oldBitmap);
                    DeleteObject(_bitmap);
                }
                if (_memDC != IntPtr.Zero) DeleteDC(_memDC);
                if (_screenDC != IntPtr.Zero) ReleaseDC(IntPtr.Zero, _screenDC);
                _disposed = true;
            }
        }
    }
}
