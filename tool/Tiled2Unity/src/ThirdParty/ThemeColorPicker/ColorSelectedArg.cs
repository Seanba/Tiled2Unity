using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace System.Windows.Forms
{
    public class ColorSelectedArg : EventArgs
    {
        Color _selectedColor;
        string _hexColor;

        public Color Color { get { return _selectedColor; } }
        public string HexColor { get { return _hexColor; } }
        public int R { get { return _selectedColor.R; } }
        public int G { get { return _selectedColor.R; } }
        public int B { get { return _selectedColor.B; } }

        public ColorSelectedArg(Color selectedColor)
        {
            _selectedColor = selectedColor;
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("#");
            sb.Append(BitConverter.ToString(new byte[] { _selectedColor.R }));
            sb.Append(BitConverter.ToString(new byte[] { _selectedColor.G }));
            sb.Append(BitConverter.ToString(new byte[] { _selectedColor.B }));
            _hexColor = sb.ToString();
        }
    }
}
