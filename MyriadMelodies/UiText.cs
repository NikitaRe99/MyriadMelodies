using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using MyriadMelodies.Controls;

namespace MyriadMelodies
{
    internal static class UiText
    {
        public static bool English {get;private set;}
        public static void SetLanguage(string language){English=language=="en";}
        private static readonly Dictionary<string,string> En=new Dictionary<string,string>
        {
            ["Мириад мелодий"]="Myriad Melodies",["Захват"]="Capture",["Режим"]="Mode",["Тема"]="Theme",
            ["Зоны"]="Zones",["Допуск цвета"]="Color tolerance",["Непрозрачность, %"]="Opacity, %",["Поверх игры"]="Always on top",
            ["Поверх игры: оконный режим или без рамки"]="Overlay: windowed or borderless mode",["Язык"]="Language",
            ["Хранение"]="Storage",["Рядом с EXE"]="Beside EXE",
            ["Где сохранять настройки, темы и клавиши. При выборе текущее состояние копируется автоматически."]="Where to save settings, themes and keys. Current settings are copied when you switch.",
            ["Не удалось перенести настройки"]="Could not move settings",
            ["Не удалось записать настройки. Выберите папку с правом записи или используйте LocalAppData."]="Could not write settings. Choose a writable application folder or use LocalAppData.",
            ["Вертикальный режим"]="Vertical mode",["Режим распространения"]="Spread mode",
            ["▶  Старт"]="▶  Start",["■  Стоп"]="■  Stop",["● Готово"]="● Ready",["● В работе"]="● Running",
            ["● Считывание"]="● Sampling",["● Восстановление"]="● Recovering",["● Игра неактивна"]="● Game inactive",
            ["● Окно закрыто"]="● Window closed",["● Окно свёрнуто"]="● Minimized",["● Экран отключён"]="● Display offline",
            ["ОК"]="OK",["Отмена"]="Cancel",["Сохранить"]="Save",["Добавить"]="Add",["Удалить"]="Delete",["Сбросить"]="Reset",
            ["Закрыть"]="Close",["Открыть"]="Open",["Сбросить клавиши"]="Reset key bindings",["Настройки"]="Settings",
            ["Своя тема"]="Custom theme",["Название темы"]="Theme name",["Редактирование темы"]="Edit theme",
            ["Как назвать эту тему?"]="What would you like to name this theme?",["Считать цвета из игры"]="Sample colors from the game",
            ["Тип всполоха"]="Flash type",["Белый всполох"]="White flash",["Цветной · Лёгкая рябь"]="Pink · Undulating Ripples",
            ["Тёплый · Ода небесам"]="Warm · Ode to Azure Skies",["Жёлтый · Морозная сакура"]="Yellow · Cherried Frost",
            ["Лавандовый · Оракул разума"]="Lavender · Oracle of Reason",["Текущий цвет"]="Current color",
            ["Цвет всполоха"]="Flash color",["Цвет короткой ноты"]="Short note color",["Цвет длинной ноты"]="Long note color",
            ["В приложении: "]="App mode: ",["Копировать детали"]="Copy details",["Скопировано"]="Copied",["Буфер занят"]="Clipboard busy",
            ["Нажмите новую клавишу на клавиатуре."]="Press a new key on your keyboard.",
            ["Нажмите одну клавишу без Ctrl, Alt, Shift и Win. Esc — отмена."]="Press a single key without Ctrl, Alt, Shift or Win. Esc cancels.",
            ["Если клавиша занята, назначения двух зон поменяются местами. Esc — отмена."]="If the key is already assigned, the two zones will swap bindings. Esc cancels.",
            ["Нажмите, чтобы изменить клавишу этой зоны."]="Click to change this zone's key.",
            ["Удалить тему?"]="Delete theme?",["Название не задано"]="Missing theme name",["Введите название темы."]="Enter a theme name.",
            ["Название стиля не может быть пустым."]="Theme name cannot be empty.",["Тема с таким названием уже существует."]="A theme with this name already exists.",
            ["Тема больше не существует."]="This theme no longer exists.",["Это не встроенная тема."]="This is not a built-in theme.",
            ["Исходная тема не найдена."]="Original theme not found.",["Исходное название уже занято другой темой. Сначала переименуйте её."]="Another theme uses the original name. Rename that theme first.",
            ["Не удалось изменить тему"]="Could not update theme",["Не удалось добавить тему"]="Could not add theme",
            ["Не удалось изменить порядок тем"]="Could not reorder themes",["Не удалось считать цвета"]="Could not sample colors",
            ["Не удалось запустить захват"]="Could not start capture",["Не удалось изменить захват"]="Could not update capture",
            ["Ошибка GPU-захвата"]="GPU capture error",["Захват окна остановлен"]="Window capture stopped",
            ["Не удалось быстро получить цвета областей."]="Timed out waiting for zone colors.",
            ["Захват прерван. Повторите считывание цветов."]="Capture was interrupted. Please sample the colors again.",
            ["Окно игры свёрнуто. Разверните его и нажмите «Старт» ещё раз."]="The game window is minimized. Restore it and click Start again.",
            ["Разверните окно игры перед запуском захвата."]="Restore the game window before starting capture.",
            ["Окно игры закрыто. Запустите игру и выберите её окно в списке «Захват»."]="The game window is closed. Start the game and select its window under Capture.",
            ["Игровая область пока недоступна. Разверните окно игры и повторите запуск."]="The game area is unavailable. Restore the game window and try again.",
            ["Окно игры закрыто. Выберите источник заново."]="The game window is closed. Select the capture source again.",
            ["Окно игры закрыто. Выберите его заново."]="The game window is closed. Select it again.",
            ["Окно игры закрыто."]="The game window is closed.",["Окно игры закрыто"]="Game window closed",
            ["Окно игры свёрнуто."]="The game window is minimized.",["Окно игры не найдено."]="Game window not found.",
            ["Игра свёрнута · разверните окно"]="Game minimized · restore the window",["Игровая область пока недоступна"]="Game area unavailable",
            ["Пропорции уже 4:3 пока не проверены"]="Aspect ratios narrower than 4:3 are untested",
            ["Раскладка для пропорций уже 4:3 ещё не проверена."]="Layouts narrower than 4:3 have not been verified.",
            ["Рассчитанные зоны выходят за границы игрового поля."]="Calculated zones extend beyond the game area.",
            ["Не удалось определить игровую область."]="Could not locate the game area.",["Не удалось определить размеры окна игры."]="Could not read the game window dimensions.",
            ["Размер кадра окна изменился. Повторите запуск захвата."]="Window dimensions changed. Start capture again.",
            ["Windows Graphics Capture не поддерживается этой системой."]="Windows Graphics Capture is not supported on this system.",
            ["Все области должны целиком находиться на одном мониторе."]="All zones must fit on a single monitor.",
            ["Области должны находиться внутри игрового окна."]="All zones must fit inside the game window.",
            ["Нужна хотя бы одна область захвата."]="At least one capture zone is required.",["Размеры областей должны быть положительными."]="Zone dimensions must be positive.",
            ["Количество preview-контролов должно совпадать с количеством областей."]="Preview and zone counts must match.",
            ["Недостаточно элементов для результатов."]="The result array is too small.",
            ["Не удалось восстановить GPU-захват после трёх попыток."]="Could not recover GPU capture after three attempts.",
            ["Захватываемое окно было закрыто или изменено."]="The captured window was closed or changed.",
            ["Захватываемый монитор был отключён или изменён."]="The captured display was disconnected or changed.",
            ["Не удалось скомпилировать compute shader."]="Could not compile the compute shader.",["Ошибка компиляции compute shader."]="Compute shader compilation failed.",
            ["Genshin · окно закрыто"]="Genshin · window closed",["Genshin · окно недоступно"]="Genshin · window unavailable",
            ["Экран или окно Genshin. Запустите игру и раскройте список, чтобы обновить окна."]="Display or Genshin window. Start the game and reopen this list to refresh.",
            ["Значок карандаша слева в списке или F2 — изменить тему."]="Pencil or F2: edit. Drag a theme or use Alt+Up/Down to reorder.",
            ["Карандаш или F2 — изменить. Перетащите тему или Alt+↑/↓ — порядок."]="Pencil or F2: edit. Drag a theme or use Alt+Up/Down to reorder.",
            ["Перетащите панель за заголовок"]="Drag the header or an empty area to move the panel",
            ["Свернуть"]="Minimize",["Показать или скрыть зоны"]="Show or hide zones",
            ["Считать короткую ноту из зоны K, длинную — из зоны S"]="Sample the short note from zone 5 and the long note from zone 2",
            ["Вернуть A · S · D · J · K · L для всех шести зон."]="Restore A · S · D · J · K · L for all six zones.",
            ["Значение"]="Value",["Предпросмотр шести зон"]="Six-zone preview",
            ["6 зон · размеры по экрану игры"]="6 zones · sized to the game area",
            ["1. Перейдите в «Стили интерфейса».\n\n2. Выберите нужную вам тему.\n\n3. Убедитесь, что выбран нужный режим.\n\nПосле этого нажмите «ОК»: приложение считает цвета и предложит ввести название."]="1. Open Interface Styles in the game.\n\n2. Select the theme you want.\n\n3. Check that the correct mode is selected.\n\nThen click OK to sample the colors and enter a name."
        };
        private static readonly Dictionary<string,string> Ru=Reverse();
        private static Dictionary<string,string> Reverse(){var result=new Dictionary<string,string>();foreach(var pair in En)result.TryAdd(pair.Value,pair.Key);return result;}
        public static string T(string text)
        {
            if(text==null) return null;
            string source=Ru.TryGetValue(text,out string original)?original:text;
            return English && En.TryGetValue(source,out string translated)?translated:source;
        }
        public static string F(string ru,string en,params object[] values)=>string.Format(English?CultureInfo.GetCultureInfo("en-US"):CultureInfo.GetCultureInfo("ru-RU"),English?en:ru,values);
        public static void Apply(Control root,ToolTip tips=null)
        {
            if(root.Name!="keyLabel" && !Equals(root.Tag,"literal") && (root is Label || root is Button || root is CheckBox || root is Form))root.Text=T(root.Text);
            if(root.AccessibleName!=null)root.AccessibleName=T(root.AccessibleName);
            if(tips!=null){string tip=tips.GetToolTip(root);if(tip.Length>0)tips.SetToolTip(root,T(tip));}
            if(root is OverlayComboBox combo)
            {for(int i=0;i<combo.Items.Count;i++)if(combo.Items[i] is string item)combo.Items[i]=T(item);combo.Invalidate();}
            foreach(Control child in root.Controls)Apply(child,tips);
        }
    }
}
