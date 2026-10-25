
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MyriadMelodies.Controls;

namespace MyriadMelodies
{
    public sealed class FormElementCreator
    {
        public PreviewAreaView CreatePreviewArea(string key, byte areaNumber, float scale = 1f)
        {
            var container = new OverlayPanel
            {
                Size = new Size(100, 100),
                Margin = new Padding(0, 0, 8, 8),
                BackColor = Color.FromArgb(65, 76, 95),
                ForeColor = Color.FromArgb(237, 232, 218),
                BorderStyle = BorderStyle.None,
            };

            // Превью изображения
            var previewBox = new Panel
            {
                Name = "gpuPreview",
                Size = new Size(48, 48),
                Location = new Point(26, 24),
                BackColor = Color.Black,
                BorderStyle = BorderStyle.None,
            };

            // Заголовок
            var lblTitle = new Label
            {
                Name = "keyLabel",
                Text = key,
                Location = new Point(24, 5),
                Size = new Size(52,16),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                Font = new Font("Segoe UI", 8, FontStyle.Bold)
            };

            // Отображение действия
            var previwAction = new ColorSwatch
            {
                Name = "previwAction",
                Size = new Size(10, 10),
                Location = new Point(82, 8),
                SwatchColor = Color.FromArgb(77, 91, 117)

            };

            // Панель для отображения цвета
            var colorPanel = new ColorSwatch
            {
                Name = "colorPanel",
                Size = new Size(10, 10),
                Location = new Point(8, 8),
                SwatchColor = Color.White
            };
            // Текст с RGB значениями
            var lblColor = new Label
            {
                Name = "lblColor",
                Location = new Point(0, 78),
                Size = new Size(100, 18),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Consolas", 7.5f),
                ForeColor = Color.FromArgb(158, 170, 193),
                Text = "—"
            };

            

            container.Controls.Add(previewBox);
            container.Controls.Add(lblTitle);
            container.Controls.Add(previwAction);
            container.Controls.Add(colorPanel);
            container.Controls.Add(lblColor);

            if (scale != 1f)
            {
                container.Scale(new SizeF(scale, scale));
                container.Margin = new Padding(0, 0, (int)Math.Round(6 * scale), 0);
            }
            // Обрезаем само HWND после DPI-масштабирования: GPU-превью
            // остаётся без Bitmap и без копирования экранного кадра на CPU.
            UpdatePreviewRegion(previewBox);
            previewBox.SizeChanged += (sender, e) => UpdatePreviewRegion(previewBox);

            return new PreviewAreaView(
                areaNumber,
                container,
                previewBox,
                previwAction,
                colorPanel,
                lblColor,
                lblTitle);
        }

        private static void UpdatePreviewRegion(Panel previewBox)
        {
            using (var circle = new GraphicsPath())
            {
                circle.AddEllipse(previewBox.ClientRectangle);
                Region old = previewBox.Region;
                previewBox.Region = new Region(circle);
                old?.Dispose();
            }
        }
    }

    /// <summary>
    /// Хранит прямые ссылки на элементы предпросмотра, чтобы горячий UI-путь
    /// не выполнял Controls.Find и не создавал массивы найденных контролов.
    /// </summary>
    public sealed class PreviewAreaView : IDisposable
    {
        internal PreviewAreaView(
            byte areaNumber,
            Panel container,
            Panel gpuPreview,
            ColorSwatch actionIndicator,
            ColorSwatch colorIndicator,
            Label colorLabel,
            Label keyLabel)
        {
            AreaNumber = areaNumber;
            Container = container;
            GpuPreview = gpuPreview;
            ActionIndicator = actionIndicator;
            ColorIndicator = colorIndicator;
            ColorLabel = colorLabel;
            KeyLabel = keyLabel;
            Container.Click+=(s,e)=>RequestAssignment();
            foreach(Control control in Container.Controls) control.Click+=(s,e)=>RequestAssignment();
            CanAssign=true;
        }

        public byte AreaNumber { get; }
        public Panel Container { get; }
        public Panel GpuPreview { get; }
        private ColorSwatch ActionIndicator { get; }
        private ColorSwatch ColorIndicator { get; }
        private Label ColorLabel { get; }
        private Label KeyLabel { get; }
        public event EventHandler AssignmentRequested;
        private bool canAssign;
        private int? displayedArgb;
        public bool CanAssign
        {
            get=>canAssign;
            set
            {
                if(canAssign==value) return;
                canAssign=value;Container.Cursor=value?Cursors.Hand:Cursors.Default;
                foreach(Control control in Container.Controls) control.Cursor=Container.Cursor;
            }
        }
        private void RequestAssignment(){if(CanAssign) AssignmentRequested?.Invoke(this,EventArgs.Empty);}
        public void SetKeyName(string name)
        {
            KeyLabel.Text=name;
            Container.AccessibleName=MyriadMelodies.UiText.F("Зона {0}, клавиша {1}","Zone {0}, key {1}",AreaNumber,name);
        }

        public void ResizePreview(int width, int height, float scale)
        {
            Container.Size = new Size(width, height);
            int side = (int)Math.Round(48 * scale);
            GpuPreview.SetBounds((width - side) / 2, (int)Math.Round(24 * scale), side, side);
            ActionIndicator.SetBounds(width-(int)Math.Round(18*scale),(int)Math.Round(8*scale),(int)Math.Round(10*scale),(int)Math.Round(10*scale));
            ColorIndicator.SetBounds((int)Math.Round(8*scale),(int)Math.Round(8*scale),(int)Math.Round(10*scale),(int)Math.Round(10*scale));
            ColorLabel.SetBounds(0, (int)Math.Round(78 * scale), width, (int)Math.Round(18 * scale));
            KeyLabel.SetBounds((int)Math.Round(22*scale),(int)Math.Round(5*scale),Math.Max(1,width-(int)Math.Round(44*scale)),(int)Math.Round(16*scale));
        }

        public void Update(Color color, bool isPressed)
        {
            int argb=color.ToArgb();
            if(displayedArgb!=argb)
            {
                displayedArgb=argb;
                ColorIndicator.SwatchColor = color;
                ColorLabel.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            ActionIndicator.SwatchColor = isPressed ? Color.FromArgb(125, 235, 187) : Color.FromArgb(77, 91, 117);
        }

        public void Dispose()
        {
            Container.Dispose();
        }
    }
}
