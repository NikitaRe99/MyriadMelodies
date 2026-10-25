using System.Collections.Generic;
namespace MyriadMelodies
{
    internal static class BuiltInThemeNames
    {
        private static readonly Dictionary<string,string> Names=new Dictionary<string,string>
        {
            ["Легкая рябь (стандартный цвет)"]="Undulating Ripples",
            ["Чудесная идея"]="Fantastical Marvel",
            ["Полет воображения"]="Meditation on Floating Dreams",
            ["Скрытая нефритовая звезда"]="Hidden Jade Star",
            ["Красные тени отржают красное"]="Dappled Shadows Reflecting Red",
            ["Затянувшийся пир"]="Lingering Feast",
            ["Ледяной клык безмятежности - лучше не надо"]="Azure Icefang (not recommended)",
            ["Ветер и свобода"]="Wind and Freedom",
            ["Камень и контракты"]="Stone and Contracts",
            ["Молнии и вечность"]="Thunder and Eternity",
            ["Природная мудрость"]="Nature and Wisdom",
            ["Вода и справедливость"]="Water and Justice",
            ["Огонь и война"]="Fire and War",
            ["Одда небесам"]="Ode to Azure Skies",
            ["Солнечный свет сжигает мрак"]="Blazing Sunfire Scours the Gloom",
            ["Вечные мечты о безмятежности (лучше не использовать)"]="Eternal Dreams of Serenity (not recommended)",
            ["Горящие крылья сокола"]="Falcon Wings Ablaze",
            ["Мудрость морозной сакуры (лучше не использовать)"]="Wisdom of Cherried Frost (not recommended)",
            ["Пылающий метр"]="Red-Hot Meter",
            ["Луна и рай"]="Elysium and the Moon",
            ["Молитвы Морозной Луны"]="Frostmoon Prayers",
            ["Загадочная белая луна"]="Mysterious White Star",
            ["Затмевающее пламя"]="Eclipsing Flame",
            ["Убивающий демонов шторм"]="Demon-Slaying Gale",
            ["Оракул разума (лучше не использовать)"]="Oracle of Reason (not recommended)",
        };
        public static string EnglishName(string key)=>Names.TryGetValue(key,out string name)?name:key;
    }
}
