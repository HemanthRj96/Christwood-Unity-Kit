using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Named channel linking keys, switches and doors. Each colour also has its own symbol, so
    /// colour is never the only way to tell channels apart.
    /// </summary>
    public enum ChannelColor
    {
        Red,
        Blue,
        Green,
        Yellow,
    }

    public static class Channels
    {
        public const int Count = 4;

        /// <summary>The symbol shown on every piece of a channel.</summary>
        public static string Symbol(ChannelColor channel)
        {
            switch (channel)
            {
                case ChannelColor.Red: return "Circle";
                case ChannelColor.Blue: return "Square";
                case ChannelColor.Green: return "Triangle";
                default: return "Diamond";
            }
        }

        static readonly string[] s_Labels = { "Red (Circle)", "Blue (Square)", "Green (Triangle)", "Yellow (Diamond)" };

        /// <summary>Label used in inspectors and the HUD, for example "Red (Circle)".</summary>
        public static string Label(ChannelColor channel) => s_Labels[(int)channel];
    }
}
