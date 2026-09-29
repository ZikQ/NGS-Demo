using TMPro;

namespace Assets.Scripts.Game.MOBA.UI
{ 
    public static class TextExtensions
    {
        public static void SetTextIfChanged(this TMP_Text text, string format, float value, ref float shown)
        {
            if (value == shown)
                return;

            shown = value;
            text.SetText(format, value);
        }

        public static void SetTextIfChanged(this TMP_Text text, string format, float first, float second,
            ref float shownFirst, ref float shownSecond)
        {
            if (first == shownFirst && second == shownSecond)
                return;

            shownFirst = first;
            shownSecond = second;
            text.SetText(format, first, second);
        }
    }
}
