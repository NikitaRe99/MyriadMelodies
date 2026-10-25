using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;

namespace MyriadMelodies
{
    internal enum ThemeFlashMode { White, Colored, OdeToHeaven, CherriedFrost, OracleOfReason }

    /// <summary>
    /// Полный набор цветов и поведения одного пункта списка стилей.
    /// </summary>
    internal sealed class ColorPaletteStyle
    {
        public ColorPaletteStyle(
            string name,
            Color shortPressColor,
            Color longPressColor,
            Color longPressFlashColor,
            bool usesStandardInput = false,
            bool isCustom = false,
            string builtInKey = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Название стиля не может быть пустым.", nameof(name));

            Name = name;
            ShortPressColor = shortPressColor;
            LongPressColor = longPressColor;
            LongPressFlashColor = longPressFlashColor;
            UsesStandardInput = usesStandardInput;
            IsCustom = isCustom;
            BuiltInKey = isCustom ? null : builtInKey ?? name;
        }

        public string Name { get; }
        public Color ShortPressColor { get; }
        public Color LongPressColor { get; }
        public Color LongPressFlashColor { get; }
        public ThemeFlashMode FlashMode => LongPressFlashColor.ToArgb() == ColorPaletteCatalog.GetFlashColor(ThemeFlashMode.White).ToArgb()
            ? ThemeFlashMode.White : LongPressFlashColor.ToArgb()==ColorPaletteCatalog.GetFlashColor(ThemeFlashMode.CherriedFrost).ToArgb() ? ThemeFlashMode.CherriedFrost
                : LongPressFlashColor.ToArgb()==ColorPaletteCatalog.GetFlashColor(ThemeFlashMode.OracleOfReason).ToArgb() ? ThemeFlashMode.OracleOfReason
                : LongPressFlashColor.ToArgb() == ColorPaletteCatalog.GetFlashColor(ThemeFlashMode.OdeToHeaven).ToArgb()
                ? ThemeFlashMode.OdeToHeaven : ThemeFlashMode.Colored;
        public bool UsesStandardInput { get; }
        public bool IsCustom { get; }
        public string BuiltInKey { get; }
        internal string EnglishDisplayName { get; set; }

        public override string ToString()
        {
            return ColorPaletteCatalog.GetDisplayName(this);
        }
    }

    /// <summary>
    /// Единственное место для добавления новых цветовых стилей. Новый элемент
    /// автоматически появится в ComboStyle вместе со своим названием.
    /// </summary>
    internal static class ColorPaletteCatalog
    {
        private sealed class StoredPaletteStyle
        {
            public StoredPaletteStyle()
            {
            }

            public string Name { get; set; }
            public int ShortPressArgb { get; set; }
            public int LongPressArgb { get; set; }
            public int LongPressFlashArgb { get; set; }
            public string FlashMode { get; set; }
            public bool IsBuiltInOverride { get; set; }
            public int PaletteVersion { get; set; }
            public string BuiltInKey { get; set; }
            public bool? UsesStandardInput { get; set; }
            public int? Order { get; set; }
            public bool IsOrderOnly { get; set; }
        }

        // Медиана среднего RGB в круглой зоне по двум удержаниям на видео
        // 20261025-1223-32.5633977.mp4. Видео сжато, поэтому цвет приближённый.
        public static Color GetFlashColor(ThemeFlashMode mode) => mode switch
        {
            ThemeFlashMode.Colored => FromRgb(0xF1C7FC),
            ThemeFlashMode.OdeToHeaven => FromRgb(0xEDE2C4),
            ThemeFlashMode.CherriedFrost => FromRgb(0xFEFCD3),
            ThemeFlashMode.OracleOfReason => FromRgb(0xDCDEFC),
            _ => FromRgb(0xFCFCFC)
        };
        public static string FlashPresetName(ThemeFlashMode mode)=>mode switch
        {
            ThemeFlashMode.White=>"Белый всполох",ThemeFlashMode.Colored=>"Цветной · Лёгкая рябь",
            ThemeFlashMode.OdeToHeaven=>"Тёплый · Ода небесам",ThemeFlashMode.CherriedFrost=>"Жёлтый · Морозная сакура",
            ThemeFlashMode.OracleOfReason=>"Лавандовый · Оракул разума",_=>"Текущий цвет"
        };
        public static string GetDisplayName(ColorPaletteStyle style)
        {
            if(!UiText.English || style.IsCustom) return style.Name;
            return style.EnglishDisplayName ??= GetEnglishDisplayName(style);
        }
        private static string GetEnglishDisplayName(ColorPaletteStyle style)
        {
            string BaseName(string name)
            {
                if(name==null)return null;
                int index=name.IndexOf('(');if(index>=0)name=name.Substring(0,index);
                index=name.IndexOf(" - лучше",StringComparison.OrdinalIgnoreCase);if(index>=0)name=name.Substring(0,index);
                return name.Trim();
            }
            if(!string.Equals(BaseName(style.Name),BaseName(style.BuiltInKey),StringComparison.OrdinalIgnoreCase))return style.Name;
            string translated=BuiltInThemeNames.EnglishName(style.BuiltInKey);
            if(style.Name.Contains("лучше") && !translated.Contains("not recommended"))translated+=" (not recommended)";
            return translated;
        }

        public const int DefaultStyleIndex = 4;
        public static int GetDefaultStyleIndex()=>Math.Max(0,MutableStyles.FindIndex(style=>style.BuiltInKey==PaletteTable[DefaultStyleIndex].Name));
        public static readonly Color NeutralColor = FromRgb(0xF5F5F5);

        // Цвета записаны как 0xRRGGBB.
        // Чтобы добавить стиль, достаточно добавить в таблицу одну строку.
        private static readonly (
            string Name,
            int ShortPress,
            int LongPress,
            int LongPressFlash,
            bool StandardInput)[] PaletteTable =
        {
            // Название                                      Короткое    Долгое      Всполох     Стандарт
            ("Легкая рябь (стандартный цвет)",              0xF1C35F,   0xA793F5,   0xF1C7FC,   true),
            ("Чудесная идея",                               0xFDEAA0,   0xFEC8AA,   0xFDFDFD,   false),
            ("Полет воображения",                           0xD5F574,   0xA3F7AB,   0xFCFCFC,   false),
            ("Скрытая нефритовая звезда",                   0xFDEB91,   0xFDCA87,   0xFCFCFC,   false),
            ("Красные тени отржают красное",                0xFEF088,   0xFEC69D,   0xFCFCFC,   false),
            ("Затянувшийся пир",                            0xC9F7E3,   0xB1D3FE,   0xFCFCFC,   false),
            ("Ледяной клык безмятежности - лучше не надо",  0x9FC2CC,   0xA4B3F2,   0xFCFCFC,   false),
            ("Ветер и свобода",                             0x92E3E6,   0xFCE980,   0xFCFCFC,   false),
            ("Камень и контракты",                          0xFED37C,   0xE8915A,   0xFCFCFC,   false),
            ("Молнии и вечность",                           0xB8AEF4,   0xF3BBF0,   0xFCFCFC,   false),
            ("Природная мудрость",                          0xB3F3D8,   0xD4F6A5,   0xFCFCFC,   false),
            ("Вода и справедливость",                       0xAEEBF2,   0xA7C7FD,   0xFCFCFC,   false),
            ("Огонь и война",                               0xF89F4F,   0xFEC139,   0xFCFCFC,   false),
            ("Одда небесам",                                0xEBF6AC,   0x93F1FA,   0xEDE2C4,   false),
            ("Солнечный свет сжигает мрак", 0xFCD75C, 0xF27736, 0xFCFCFC, false),
            ("Вечные мечты о безмятежности (лучше не использовать)", 0xFCD9FC, 0xDDEBFA, 0xFCFCFC, false),
            ("Горящие крылья сокола", 0xFA9846, 0xE53F3C, 0xFCFCFC, false),
            ("Мудрость морозной сакуры (лучше не использовать)", 0xFAE1ED, 0xE4F2F7, 0xFEFCD3, false),
            ("Пылающий метр", 0xF9C217, 0xC869DE, 0xFCFCFC, false),
            ("Луна и рай", 0xD7D9FF, 0xFEEFCF, 0xFCFCFC, false),
            ("Молитвы Морозной Луны", 0xB8E1F1, 0x81DDB1, 0xFCFCFC, false),
            ("Загадочная белая луна", 0xF3DAFA, 0xDDDEFB, 0xFCFCFC, false),
            ("Затмевающее пламя", 0xF8D1F9, 0xFED1CE, 0xFCFCFC, false),
            ("Убивающий демонов шторм", 0xF1DFB2, 0x93E9E4, 0xFCFCFC, false),
            ("Оракул разума (лучше не использовать)", 0xB5F6F7, 0xC7CAFE, 0xDCDEFC, false)
        };

        private static readonly object Sync = new object();
        private static readonly HashSet<string> BuiltInOverrides = new HashSet<string>();
        private static readonly List<ColorPaletteStyle> MutableStyles = BuildStyles();
        public static IReadOnlyList<ColorPaletteStyle> Styles { get; } =
            MutableStyles.AsReadOnly();

        public static int GetLastUsedStyleIndex()
        {
            int fallbackIndex = GetDefaultStyleIndex();
            try
            {
                string path = GetLastUsedStylePath();
                if (!File.Exists(path))
                    return fallbackIndex;

                string savedName = File.ReadAllText(path).Trim();
                for (int i = 0; i < MutableStyles.Count; i++)
                {
                    if (string.Equals(
                        MutableStyles[i].Name,
                        savedName,
                        StringComparison.CurrentCultureIgnoreCase))
                    {
                        return i;
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return fallbackIndex;
        }

        public static ColorPaletteStyle AddCustomStyle(
            string name,
            Color shortPressColor,
            Color longPressColor,
            ThemeFlashMode flashMode = ThemeFlashMode.White,
            string storagePath = null)
        {
            string normalizedName = (name ?? string.Empty).Trim();
            if (normalizedName.Length == 0)
                throw new ArgumentException("Введите название темы.", nameof(name));

            lock (Sync)
            {
                foreach (ColorPaletteStyle style in MutableStyles)
                {
                    if (string.Equals(
                        style.Name,
                        normalizedName,
                        StringComparison.CurrentCultureIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            "Тема с таким названием уже существует.");
                    }
                }

                var newStyle = new ColorPaletteStyle(
                    normalizedName,
                    shortPressColor,
                    longPressColor,
                    GetFlashColor(flashMode),
                    usesStandardInput: flashMode == ThemeFlashMode.Colored,
                    isCustom: true);
                MutableStyles.Add(newStyle);
                try
                {
                    SaveCustomStyles(storagePath);
                }
                catch
                {
                    MutableStyles.Remove(newStyle);
                    throw;
                }

                return newStyle;
            }
        }

        public static bool RemoveCustomStyle(ColorPaletteStyle style, string storagePath = null)
        {
            if (style == null || !style.IsCustom) return false;
            lock (Sync)
            {
                int index = MutableStyles.IndexOf(style);
                if (index < 0) return false;
                MutableStyles.RemoveAt(index);
                try { SaveCustomStyles(storagePath); }
                catch
                {
                    MutableStyles.Insert(index, style);
                    throw;
                }
                return true;
            }
        }

        public static void MoveStyle(int from,int to,string storagePath=null)
        {
            lock(Sync)
            {
                if(from<0 || to<0 || from>=MutableStyles.Count || to>=MutableStyles.Count) throw new ArgumentOutOfRangeException();
                if(from==to) return;
                var style=MutableStyles[from];MutableStyles.RemoveAt(from);MutableStyles.Insert(to,style);
                try {SaveCustomStyles(storagePath);}
                catch {MutableStyles.RemoveAt(to);MutableStyles.Insert(from,style);throw;}
            }
        }

        public static ColorPaletteStyle UpdateStyle(ColorPaletteStyle style, string name,
            Color shortColor, Color longColor, Color flashColor, string storagePath = null)
            => UpdateStyleCore(style,name,shortColor,longColor,flashColor,style.UsesStandardInput,storagePath);

        public static ColorPaletteStyle UpdateStyleFromEditor(ColorPaletteStyle style,string name,
            Color shortColor,Color longColor,Color flashColor,bool standardInput)
            => UpdateStyleCore(style,name,shortColor,longColor,flashColor,standardInput,null);

        private static ColorPaletteStyle UpdateStyleCore(ColorPaletteStyle style,string name,
            Color shortColor,Color longColor,Color flashColor,bool standardInput,string storagePath)
        {
            lock (Sync)
            {
                int index = MutableStyles.IndexOf(style);
                if (index < 0) throw new InvalidOperationException("Тема больше не существует.");
                string normalizedName = (name ?? string.Empty).Trim();
                if (normalizedName.Length == 0) throw new ArgumentException("Введите название темы.");
                foreach (var other in MutableStyles)
                    if (!ReferenceEquals(other, style) && string.Equals(other.Name, normalizedName, StringComparison.CurrentCultureIgnoreCase))
                        throw new InvalidOperationException("Тема с таким названием уже существует.");
                var updated = new ColorPaletteStyle(normalizedName, shortColor, longColor, flashColor, standardInput, style.IsCustom, style.BuiltInKey);
                bool wasOverridden = BuiltInOverrides.Contains(style.BuiltInKey ?? string.Empty);
                if (!style.IsCustom) BuiltInOverrides.Add(style.BuiltInKey);
                MutableStyles[index] = updated;
                try { SaveCustomStyles(storagePath); }
                catch
                {
                    MutableStyles[index] = style;
                    if (!wasOverridden) BuiltInOverrides.Remove(style.BuiltInKey ?? string.Empty);
                    throw;
                }
                return updated;
            }
        }

        public static ColorPaletteStyle ResetBuiltInStyle(ColorPaletteStyle style, string storagePath = null)
        {
            lock (Sync)
            {
                int index = MutableStyles.IndexOf(style);
                if (index < 0 || style.IsCustom) throw new InvalidOperationException("Это не встроенная тема.");
                var row = Array.Find(PaletteTable, item => item.Name == style.BuiltInKey);
                if (row.Name == null) throw new InvalidOperationException("Исходная тема не найдена.");
                foreach(var other in MutableStyles)
                    if(!ReferenceEquals(other,style) && string.Equals(other.Name,row.Name,StringComparison.CurrentCultureIgnoreCase))
                        throw new InvalidOperationException("Исходное название уже занято другой темой. Сначала переименуйте её.");
                var original = new ColorPaletteStyle(row.Name, FromRgb(row.ShortPress), FromRgb(row.LongPress), FromRgb(row.LongPressFlash), row.StandardInput);
                bool wasOverridden = BuiltInOverrides.Remove(style.BuiltInKey);
                MutableStyles[index] = original;
                try { SaveCustomStyles(storagePath); }
                catch { MutableStyles[index] = style; if (wasOverridden) BuiltInOverrides.Add(style.BuiltInKey); throw; }
                return original;
            }
        }

        private static List<ColorPaletteStyle> BuildStyles()
        {
            var styles = new ColorPaletteStyle[PaletteTable.Length];
            for (int i = 0; i < PaletteTable.Length; i++)
            {
                var row = PaletteTable[i];
                styles[i] = new ColorPaletteStyle(
                    row.Name,
                    FromRgb(row.ShortPress),
                    FromRgb(row.LongPress),
                    FromRgb(row.LongPressFlash),
                    row.StandardInput);
            }

            var result = new List<ColorPaletteStyle>(styles);
            LoadCustomStyles(result);
            return result;
        }

        private static void LoadCustomStyles(List<ColorPaletteStyle> styles, string storagePath = null)
        {
            string path = storagePath ?? SettingsStorage.Default.GetReadPath(SettingsStorage.ThemesFile);
            if (!File.Exists(path))
                return;

            try
            {
                List<StoredPaletteStyle> storedStyles = JsonSerializer.Deserialize<
                    List<StoredPaletteStyle>>(File.ReadAllText(path));
                if (storedStyles == null)
                    return;

                // Названия применяются одновременно: A→B после B→C не
                // конфликтует с ещё не переименованным B при загрузке.
                var candidates=new List<ColorPaletteStyle>(styles);
                var changedKeys=new HashSet<string>();
                foreach(var stored in storedStyles)
                {
                    if(stored==null || !stored.IsBuiltInOverride || string.IsNullOrWhiteSpace(stored.Name)) continue;
                    string key=stored.BuiltInKey ?? stored.Name;
                    int index=candidates.FindIndex(item=>!item.IsCustom && item.BuiltInKey==key);
                    if(index<0 || !changedKeys.Add(key)) continue;
                    var original=candidates[index];
                    candidates[index]=new ColorPaletteStyle(stored.Name.Trim(),Color.FromArgb(stored.ShortPressArgb),
                        Color.FromArgb(stored.LongPressArgb),Color.FromArgb(stored.LongPressFlashArgb),
                        stored.UsesStandardInput ?? original.UsesStandardInput,false,key);
                }
                var names=new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
                bool unique=true;
                foreach(var candidate in candidates) if(!names.Add(candidate.Name)){unique=false;break;}
                if(unique)
                {
                    for(int i=0;i<candidates.Count;i++) styles[i]=candidates[i];
                    BuiltInOverrides.UnionWith(changedKeys);
                }

                foreach (StoredPaletteStyle stored in storedStyles)
                {
                    if (stored == null || string.IsNullOrWhiteSpace(stored.Name))
                        continue;

                    if (stored.IsBuiltInOverride)
                    {
                        continue;
                    }
                    if(stored.IsOrderOnly) continue;

                    bool duplicate = false;
                    foreach (ColorPaletteStyle existing in styles)
                    {
                        if (string.Equals(
                            existing.Name,
                            stored.Name.Trim(),
                            StringComparison.CurrentCultureIgnoreCase))
                        {
                            duplicate = true;
                            break;
                        }
                    }

                    if (!duplicate)
                    {
                        int flashArgb = stored.LongPressFlashArgb;
                        if (stored.PaletteVersion < 2 && stored.FlashMode == "Colored" && flashArgb == FromRgb(0xD2ADFF).ToArgb())
                            flashArgb = GetFlashColor(ThemeFlashMode.Colored).ToArgb();
                        styles.Add(new ColorPaletteStyle(
                            stored.Name.Trim(),
                            Color.FromArgb(stored.ShortPressArgb),
                            Color.FromArgb(stored.LongPressArgb),
                            Color.FromArgb(flashArgb),
                            usesStandardInput: stored.UsesStandardInput ??
                                (Math.Abs(Color.FromArgb(flashArgb).R-GetFlashColor(ThemeFlashMode.Colored).R)<20 &&
                                Math.Abs(Color.FromArgb(flashArgb).G-GetFlashColor(ThemeFlashMode.Colored).G)<20 &&
                                Math.Abs(Color.FromArgb(flashArgb).B-GetFlashColor(ThemeFlashMode.Colored).B)<20),
                            isCustom: true));
                    }
                }
                var ranks=new Dictionary<ColorPaletteStyle,int>();
                foreach(var stored in storedStyles)
                {
                    if(stored?.Order==null || string.IsNullOrWhiteSpace(stored.Name)) continue;
                    var style=styles.Find(item=>stored.BuiltInKey!=null ? item.BuiltInKey==stored.BuiltInKey :
                        string.Equals(item.Name,stored.Name.Trim(),StringComparison.CurrentCultureIgnoreCase));
                    if(style!=null && !ranks.ContainsKey(style)) ranks[style]=stored.Order.Value;
                }
                var originalPositions=new Dictionary<ColorPaletteStyle,int>();
                for(int i=0;i<styles.Count;i++) originalPositions[styles[i]]=i;
                styles.Sort((a,b)=>
                {
                    int x=ranks.TryGetValue(a,out int ra)?ra:int.MaxValue,y=ranks.TryGetValue(b,out int rb)?rb:int.MaxValue;
                    int compare=x.CompareTo(y);return compare!=0?compare:originalPositions[a].CompareTo(originalPositions[b]);
                });
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (JsonException)
            {
            }
        }

        private static void SaveCustomStyles(string storagePath = null)
        {
            var storedStyles = new List<StoredPaletteStyle>();
            for(int index=0;index<MutableStyles.Count;index++)
            {
                ColorPaletteStyle style=MutableStyles[index];
                bool overridden=!style.IsCustom && BuiltInOverrides.Contains(style.BuiltInKey);

                storedStyles.Add(new StoredPaletteStyle
                {
                    Name = style.Name,
                    ShortPressArgb = style.ShortPressColor.ToArgb(),
                    LongPressArgb = style.LongPressColor.ToArgb(),
                    LongPressFlashArgb = style.LongPressFlashColor.ToArgb(),
                    FlashMode = style.FlashMode.ToString(),
                    IsBuiltInOverride = overridden,
                    PaletteVersion = 2,
                    BuiltInKey = style.BuiltInKey,
                    UsesStandardInput = style.UsesStandardInput,
                    Order = index,
                    IsOrderOnly = !style.IsCustom && !overridden
                });
            }

            string path = storagePath ?? GetCustomStylesPath();
            string json = JsonSerializer.Serialize(
                storedStyles,
                new JsonSerializerOptions { WriteIndented = true });
            AtomicSettingsFile.Write(path,json);
        }
        public static void SaveSnapshot(string path){lock(Sync)SaveCustomStyles(path);}

        private static string GetCustomStylesPath()
        {
            return SettingsStorage.Default.GetWritePath(SettingsStorage.ThemesFile);
        }

        private static string GetLastUsedStylePath()
        {
            return SettingsStorage.Default.GetReadPath("last-color-theme.txt");
        }

        private static Color FromRgb(int rgb)
        {
            return Color.FromArgb(
                (rgb >> 16) & 0xFF,
                (rgb >> 8) & 0xFF,
                rgb & 0xFF);
        }
    }
}
