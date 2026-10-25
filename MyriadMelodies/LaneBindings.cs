using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace MyriadMelodies
{
    internal static class LaneBindings
    {
        public static Keys[] CreateDefaults() => new[]{Keys.A,Keys.S,Keys.D,Keys.J,Keys.K,Keys.L};
        public static bool IsSupported(Keys key)
        {
            if((key & Keys.Modifiers)!=0) return false;
            int code=(int)key;
            return (key>=Keys.A && key<=Keys.Z) || (key>=Keys.D0 && key<=Keys.D9) ||
                (key>=Keys.NumPad0 && key<=Keys.Divide) || (key>=Keys.F1 && key<=Keys.F24) ||
                key==Keys.Space || key==Keys.Return || key==Keys.Tab || key==Keys.Back ||
                (key>=Keys.Prior && key<=Keys.Down) || key==Keys.Insert || key==Keys.Delete ||
                code==186 || code==187 || code==188 || code==189 || code==190 || code==191 || code==192 ||
                code==219 || code==220 || code==221 || code==222 || code==226;
        }
        public static int[] Normalize(int[] values)
        {
            var fallback=Array.ConvertAll(CreateDefaults(),key=>(int)key);
            if(values==null || values.Length!=6) return fallback;
            var seen=new HashSet<int>();
            foreach(int value in values) if(!IsSupported((Keys)value) || !seen.Add(value)) return fallback;
            return (int[])values.Clone();
        }
        public static string DisplayName(Keys key)
        {
            if(key>=Keys.D0 && key<=Keys.D9) return ((int)key-(int)Keys.D0).ToString();
            if(key>=Keys.NumPad0 && key<=Keys.NumPad9) return "Num "+((int)key-(int)Keys.NumPad0);
            return key switch
            {
                Keys.Return=>"Enter",Keys.Space=>"Space",Keys.Back=>"Backspace",
                Keys.Prior=>"PgUp",Keys.Next=>"PgDn",Keys.Left=>"←",Keys.Right=>"→",Keys.Up=>"↑",Keys.Down=>"↓",
                Keys.OemSemicolon=>";",Keys.Oemplus=>"=",Keys.Oemcomma=>",",Keys.OemMinus=>"−",Keys.OemPeriod=>".",
                Keys.OemQuestion=>"/",Keys.Oemtilde=>"`",Keys.OemOpenBrackets=>"[",Keys.OemCloseBrackets=>"]",
                Keys.OemPipe=>"\\",Keys.OemQuotes=>"'",_=>key.ToString()
            };
        }
    }
}
