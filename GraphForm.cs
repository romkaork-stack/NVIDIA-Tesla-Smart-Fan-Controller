using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace TeslaFanController
{
    public class GraphForm : Form
    {
        private Panel _historyPanel;
        private List<int> _tempHistory = new List<int>();
        private List<int> _pwmHistory = new List<int>(); 
        private const int MaxPoints = 60; 
        
        private const int PaddingX = 35;
        private const int PaddingY = 20;

        public GraphForm(string gpuName)
        {
            this.Text = $"Cooling Monitor — {gpuName}";
            this.Size = new Size(860, 280);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            _historyPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(29, 39, 41) };
            
            typeof(Panel).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .SetValue(_historyPanel, true, null);

            _historyPanel.Paint += HistoryPanel_Paint;
            this.Controls.Add(_historyPanel);
        }

        public void AddData(int temp, int pwm)
        {
            _tempHistory.Add(temp);
            _pwmHistory.Add(pwm);

            if (_tempHistory.Count > MaxPoints)
            {
                _tempHistory.RemoveAt(0);
                _pwmHistory.RemoveAt(0);
            }
            _historyPanel.Invalidate(); 
        }

        private void HistoryPanel_Paint(object? sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int w = _historyPanel.Width - PaddingX * 2;
            int h = _historyPanel.Height - PaddingY * 2;

            using (Pen gridPen = new Pen(Color.FromArgb(50, 65, 68), 1))
            {
                int gridDivisions = 6;
                for (int i = 0; i <= gridDivisions; i++)
                {
                    int x = PaddingX + (w / gridDivisions) * i;
                    g.DrawLine(gridPen, x, PaddingY, x, PaddingY + h);
                }
                for (int i = 0; i <= 4; i++)
                {
                    int y = PaddingY + (h / 4) * i;
                    g.DrawLine(gridPen, PaddingX, y, PaddingX + w, y);
                }
            }

            using (Font font = new Font("Segoe UI", 8))
            using (Brush textBrush = new SolidBrush(Color.DarkGray))
            {
                g.DrawString("100% / °C", font, textBrush, 2, PaddingY - 6);
                g.DrawString("0%", font, textBrush, 12, PaddingY + h - 7);
                g.DrawString("-120s", font, textBrush, PaddingX - 8, PaddingY + h + 4);
                g.DrawString("Now", font, textBrush, PaddingX + w - 30, PaddingY + h + 4);
            }

            using (Pen borderPen = new Pen(Color.FromArgb(85, 105, 110), 1))
            {
                g.DrawRectangle(borderPen, PaddingX, PaddingY, w, h);
            }

            if (_tempHistory.Count < 2) return;

            float stepX = (float)w / (MaxPoints - 1); 
            float startX = PaddingX + w - (_tempHistory.Count - 1) * stepX;

            using (Pen pwmPen = new Pen(Color.FromArgb(17, 125, 187), 2))
            {
                for (int i = 0; i < _pwmHistory.Count - 1; i++)
                {
                    float x1 = startX + i * stepX;
                    float y1 = PaddingY + h - (_pwmHistory[i] / 100f * h);
                    float x2 = startX + (i + 1) * stepX;
                    float y2 = PaddingY + h - (_pwmHistory[i + 1] / 100f * h);
                    g.DrawLine(pwmPen, x1, y1, x2, y2);
                }
            }

            using (Pen tempPen = new Pen(Color.FromArgb(37, 175, 76), 2))
            {
                for (int i = 0; i < _tempHistory.Count - 1; i++)
                {
                    float x1 = startX + i * stepX;
                    float y1 = PaddingY + h - (_tempHistory[i] / 100f * h);
                    float x2 = startX + (i + 1) * stepX;
                    float y2 = PaddingY + h - (_tempHistory[i + 1] / 100f * h);
                    g.DrawLine(tempPen, x1, y1, x2, y2);
                }
            }

            int currentTemp = _tempHistory[_tempHistory.Count - 1];
            int currentPwm = _pwmHistory[_pwmHistory.Count - 1];

            using (Font font = new Font("Segoe UI", 11, FontStyle.Bold))
            {
                using (Brush tempBrush = new SolidBrush(Color.FromArgb(37, 175, 76)))
                {
                    g.DrawString($"Temp: {currentTemp}°C", font, tempBrush, PaddingX + 15, PaddingY + 15);
                }
                using (Brush pwmBrush = new SolidBrush(Color.FromArgb(17, 125, 187)))
                {
                    g.DrawString($"PWM: {currentPwm}%", font, pwmBrush, PaddingX + 140, PaddingY + 15);
                }
            }
        }
    }
}
