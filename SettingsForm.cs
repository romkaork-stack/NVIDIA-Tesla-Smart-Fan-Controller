using System;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;

namespace TeslaFanController
{
    public class SettingsForm : Form
    {
        private ComboBox _cmbPorts;
        private Button _btnSave;
        private Button _btnCancel;
        private Panel _graphPanel;

        private Point _pointA; 
        private Point _pointB; 

        private int _activePointIndex = -1; 
        private bool _isDragging = false;

        private const int PaddingX = 35;
        private const int PaddingY = 20;
        private string _defaultPort;

        public Action<string, int, int, int>? OnSettingsSaved { get; set; }

        public SettingsForm(string currentPort, int minTemp, int maxTemp, int minPwm)
        {
            _defaultPort = currentPort;
            _pointA = new Point(minTemp, minPwm);
            _pointB = new Point(maxTemp, 100); 

            this.Text = "GPU Cooling Control Panel";
            this.Size = new Size(540, 460);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            // Блок выбора COM-порта
            var lblPort = new Label { Text = "COM Port:", Left = 20, Top = 22, AutoSize = true };
            _cmbPorts = new ComboBox { Left = 210, Top = 18, Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            
            string[] ports = SerialPort.GetPortNames();
            _cmbPorts.Items.AddRange(ports);
            if (_cmbPorts.Items.Contains(currentPort)) _cmbPorts.SelectedItem = currentPort;
            else if (_cmbPorts.Items.Count > 0) _cmbPorts.SelectedIndex = 0;
            else _cmbPorts.Items.Add(currentPort); 

            // Графическая панель
            var lblGraphHint = new Label { Text = "Cooling Curve Editor (drag points with mouse):", Left = 20, Top = 60, AutoSize = true };
            
            _graphPanel = new Panel { Left = 20, Top = 85, Width = 485, Height = 240, BackColor = Color.FromArgb(29, 39, 41) };
            
            typeof(Panel).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .SetValue(_graphPanel, true, null);

            _graphPanel.Paint += GraphPanel_Paint;
            _graphPanel.MouseDown += GraphPanel_MouseDown;
            _graphPanel.MouseMove += GraphPanel_MouseMove;
            _graphPanel.MouseUp += GraphPanel_MouseUp;

            // Кнопки управления (Save / Cancel)
            _btnSave = new Button { Text = "Save", Left = 275, Top = 360, Width = 110, Height = 32 };
            _btnCancel = new Button { Text = "Cancel", Left = 395, Top = 360, Width = 110, Height = 32, DialogResult = DialogResult.Cancel };
            _btnSave.Click += BtnSave_Click;

            this.Controls.AddRange(new Control[] { lblPort, _cmbPorts, lblGraphHint, _graphPanel, _btnSave, _btnCancel });
        }

        private Point ValueToPixel(Point val)
        {
            int graphWidth = _graphPanel.Width - PaddingX * 2;
            int graphHeight = _graphPanel.Height - PaddingY * 2;

            int x = PaddingX + (int)(val.X / 100.0 * graphWidth);
            int y = PaddingY + graphHeight - (int)(val.Y / 100.0 * graphHeight); 
            return new Point(x, y);
        }

        private Point PixelToValue(Point pix)
        {
            int graphWidth = _graphPanel.Width - PaddingX * 2;
            int graphHeight = _graphPanel.Height - PaddingY * 2;

            int temp = (int)Math.Round(((double)(pix.X - PaddingX) / graphWidth) * 100);
            int pwm = (int)Math.Round(((double)(PaddingY + graphHeight - pix.Y) / graphHeight) * 100);

            return new Point(Math.Clamp(temp, 0, 100), Math.Clamp(pwm, 0, 100));
        }

        private void GraphPanel_Paint(object? sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int graphWidth = _graphPanel.Width - PaddingX * 2;
            int graphHeight = _graphPanel.Height - PaddingY * 2;

            using (Pen gridPen = new Pen(Color.FromArgb(50, 65, 68), 1))
            {
                for (int i = 0; i <= 4; i++)
                {
                    int x = PaddingX + (graphWidth / 4) * i;
                    g.DrawLine(gridPen, x, PaddingY, x, PaddingY + graphHeight);
                    
                    int y = PaddingY + (graphHeight / 4) * i;
                    g.DrawLine(gridPen, PaddingX, y, PaddingX + graphWidth, y);
                }
            }

            using (Font font = new Font("Segoe UI", 8))
            using (Brush textBrush = new SolidBrush(Color.DarkGray))
            {
                g.DrawString("100%", font, textBrush, 2, PaddingY - 6);
                g.DrawString("0%", font, textBrush, 8, PaddingY + graphHeight - 7);
                g.DrawString("0°C", font, textBrush, PaddingX - 8, PaddingY + graphHeight + 4);
                g.DrawString("100°C", font, textBrush, PaddingX + graphWidth - 15, PaddingY + graphHeight + 4);
            }

            Point pixA = ValueToPixel(_pointA);
            Point pixB = ValueToPixel(_pointB);

            using (Pen linePen = new Pen(Color.FromArgb(17, 125, 187), 2))
            {
                g.DrawLine(linePen, PaddingX, pixA.Y, pixA.X, pixA.Y); 
                g.DrawLine(linePen, pixA.X, pixA.Y, pixB.X, pixB.Y); 
                g.DrawLine(linePen, pixB.X, pixB.Y, PaddingX + graphWidth, pixB.Y); 
            }

            DrawNode(g, pixA, $"Min: {_pointA.X}°C / {_pointA.Y}%", _activePointIndex == 0);
            DrawNode(g, pixB, $"Max: {_pointB.X}°C / 100%", _activePointIndex == 1);
        }

        private void DrawNode(Graphics g, Point center, string label, bool isActive)
        {
            int radius = isActive ? 7 : 5;
            Color nodeColor = isActive ? Color.FromArgb(242, 156, 17) : Color.FromArgb(37, 175, 76);

            using (Brush brush = new SolidBrush(nodeColor))
            {
                g.FillEllipse(brush, center.X - radius, center.Y - radius, radius * 2, radius * 2);
            }

            using (Font font = new Font("Segoe UI", 8, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.White))
            {
                g.DrawString(label, font, textBrush, center.X - 40, center.Y - 22);
            }
        }

        private void GraphPanel_MouseDown(object? sender, MouseEventArgs e)
        {
            Point pixA = ValueToPixel(_pointA);
            Point pixB = ValueToPixel(_pointB);

            if (Math.Sqrt(Math.Pow(e.X - pixA.X, 2) + Math.Pow(e.Y - pixA.Y, 2)) < 10)
            {
                _activePointIndex = 0;
                _isDragging = true;
            }
            else if (Math.Sqrt(Math.Pow(e.X - pixB.X, 2) + Math.Pow(e.Y - pixB.Y, 2)) < 10)
            {
                _activePointIndex = 1;
                _isDragging = true;
            }

            if (_isDragging) _graphPanel.Invalidate();
        }

        private void GraphPanel_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!_isDragging || _activePointIndex == -1) return;

            Point val = PixelToValue(e.Location);

            if (_activePointIndex == 0)
            {
                if (val.X < _pointB.X)
                {
                    _pointA.X = val.X;
                    _pointA.Y = val.Y;
                }
            }
            else if (_activePointIndex == 1)
            {
                if (val.X > _pointA.X)
                {
                    _pointB.X = val.X;
                    _pointB.Y = 100; 
                }
            }

            _graphPanel.Invalidate(); 
        }

        private void GraphPanel_MouseUp(object? sender, MouseEventArgs e)
        {
            _isDragging = false;
            _activePointIndex = -1;
            _graphPanel.Invalidate();
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            string selectedPort = _cmbPorts.SelectedItem?.ToString() ?? _cmbPorts.Text;
            if (string.IsNullOrEmpty(selectedPort)) selectedPort = _defaultPort;

            OnSettingsSaved?.Invoke(selectedPort, _pointA.X, _pointB.X, _pointA.Y);
            this.Close();
        }
    }
}
