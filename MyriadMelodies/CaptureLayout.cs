using System;
using System.Drawing;

namespace MyriadMelodies
{
    /// <summary>
    /// Эмпирическая модель центров по кадрам 109–118. Размер интерфейса и
    /// геометрия дорожек масштабируются отдельно. Обе раскладки описаны
    /// формулами от центра поля без таблиц смещений отдельных клавиш.
    /// Коэффициенты подобраны по наблюдениям, а не взяты из исходников игры.
    /// Вход — границы игрового поля в физических пикселях рабочего стола.
    /// </summary>
    internal static class CaptureLayout
    {
        public static int GetAreaSize(Size field)
        {
            if (field.Width <= 0 || field.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(field));
            double uiScale = Math.Min(field.Width / 1920.0, field.Height / 1080.0);
            return Math.Max(1, (int)Math.Round(33 * uiScale));
        }

        public static Rectangle[] CreateRegions(Rectangle field, bool spread)
        {
            int side = GetAreaSize(field.Size);
            double aspect = field.Width / (double)field.Height;
            if (aspect < 4.0 / 3 - 0.001)
                throw new ArgumentException("Раскладка для пропорций уже 4:3 ещё не проверена.");

            // Единое правило для прямых дорожек, без массива X-смещений.
            // Коэффициенты подобраны по пяти разрешениям, единицы — пиксели
            // при базовом масштабе интерфейса 1920×1080.
            double uiScale = Math.Min(field.Width / 1920.0, field.Height / 1080.0);
            double step = 0.3 * field.Height - 107.6 * uiScale;
            double flatY = 0.8975 * field.Height - 48.3 * uiScale - 0.5;
            // z=0 для 16:9 и шире, z=1/3 для 16:10, z=1 для 4:3.
            // Это непрерывный параметр формы, отдельные профили не нужны.
            double z = Math.Clamp((field.Height / uiScale - 1080) / 360, 0, 1);
            var regions = new Rectangle[6];
            for (int i = 0; i < regions.Length; i++)
            {
                double centerX;
                double centerY;
                if (spread)
                {
                    // j=0 — внутренняя пара D/J, j=1 — S/K, j=2 — A/L.
                    double j = Math.Abs(i - 2.5) - 0.5;
                    double distance = uiScale * (
                        173.04 + j * (223.53 - 8.96 * j) +
                        z * (57.36 + j * (66.795 + 8.667 * j)) +
                        8.955 * z * z * j * (2 - j));
                    centerX = field.Left + field.Width / 2.0 + Math.Sign(i - 2.5) * distance;
                    centerY = field.Top + 0.836686 * field.Height -
                        uiScale * (33.621 + j * (31.05 + 19.567 * j));
                }
                else
                {
                    centerX = field.Left + field.Width / 2.0 + (i - 2.5) * step;
                    centerY = field.Top + flatY;
                }
                regions[i] = new Rectangle((int)Math.Round(centerX - side / 2.0),
                    (int)Math.Round(centerY - side / 2.0), side, side);
                if (!field.Contains(regions[i]))
                    throw new ArgumentException("Рассчитанные зоны выходят за границы игрового поля.");
            }
            return regions;
        }

    }
}
