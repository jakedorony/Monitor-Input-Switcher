// Theme.cs - light/dark palette and theme selection.
//
// Mode is a per-device preference (config.json "Theme": "System"|"Light"|
// "Dark"; never synced). "System" follows Windows' app theme setting.

using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MonitorSwitch
{
    enum ThemeMode { System, Light, Dark }

    class Palette
    {
        public bool IsDark;
        public Color Bg, Card, Border, Field, Track, Text, Muted, Accent, AccentText;
        public Color OkFg, OkBg, WarnFg, WarnBg, Hover;

        public static readonly Palette Light = new Palette
        {
            IsDark = false,
            Bg = Hex("#f3f3f5"), Card = Hex("#ffffff"), Border = Hex("#e3e3e8"),
            Field = Hex("#f7f7f9"), Track = Hex("#e6e6eb"),
            Text = Hex("#1b1b1f"), Muted = Hex("#5c5c66"),
            Accent = Hex("#0f5fcf"), AccentText = Hex("#ffffff"),
            OkFg = Hex("#3d6f3a"), OkBg = Hex("#e7f3e5"),
            WarnFg = Hex("#8a5a00"), WarnBg = Hex("#fbf0d5"),
            Hover = Hex("#eaeaef")
        };

        public static readonly Palette Dark = new Palette
        {
            IsDark = true,
            Bg = Hex("#1c1c21"), Card = Hex("#26262c"), Border = Hex("#35353d"),
            Field = Hex("#2c2c33"), Track = Hex("#2c2c33"),
            Text = Hex("#ececf1"), Muted = Hex("#9a9aa6"),
            Accent = Hex("#3b7bff"), AccentText = Hex("#ffffff"),
            OkFg = Hex("#7ed491"), OkBg = Hex("#1f3a26"),
            WarnFg = Hex("#f0c060"), WarnBg = Hex("#3a3020"),
            Hover = Hex("#30303a")
        };

        static Color Hex(string h)
        {
            return Color.FromArgb(
                Convert.ToInt32(h.Substring(1, 2), 16),
                Convert.ToInt32(h.Substring(3, 2), 16),
                Convert.ToInt32(h.Substring(5, 2), 16));
        }
    }

    static class Theme
    {
        public static ThemeMode Mode = ThemeMode.System;
        public static Palette Current { get; private set; } = Palette.Light;

        // Raised after Current changes; windows repaint themselves.
        public static event Action Changed;

        public static readonly Font Body = new Font("Segoe UI", 9.75f);
        public static readonly Font Small = new Font("Segoe UI", 8.75f);
        public static readonly Font Strong = new Font("Segoe UI", 9.75f, FontStyle.Bold);
        public static readonly Font Title = new Font("Segoe UI", 11.25f, FontStyle.Bold);
        public static readonly Font Hero = new Font("Segoe UI", 14.25f, FontStyle.Bold);
        public static readonly Font Caption = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        // Segoe MDL2 Assets ships with Windows 10+; used for the few glyphs we need.
        public static readonly Font Glyph = new Font("Segoe MDL2 Assets", 11f);
        public static readonly Font GlyphLarge = new Font("Segoe MDL2 Assets", 16f);

        public const string GlyphGear = "";
        public const string GlyphSun = "";
        public const string GlyphMoon = "";
        public const string GlyphCheck = "";
        public const string GlyphChevron = "";
        public const string GlyphMonitor = "";
        public const string GlyphSync = "";
        public const string GlyphWarn = "";
        public const string GlyphArrow = "";

        public static void Set(ThemeMode mode)
        {
            Mode = mode;
            Resolve();
        }

        // Recomputes Current from Mode (+ Windows setting) and notifies.
        public static void Resolve()
        {
            bool dark = Mode == ThemeMode.Dark || (Mode == ThemeMode.System && WindowsPrefersDark());
            var next = dark ? Palette.Dark : Palette.Light;
            if (ReferenceEquals(next, Current)) return;
            Current = next;
            var h = Changed;
            if (h != null) h();
        }

        public static bool WindowsPrefersDark()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
                {
                    object v = key == null ? null : key.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch { return false; }
        }

        // Makes the non-client title bar follow the palette (Windows 10 20H1+).
        public static void ApplyTitleBar(Form f)
        {
            try
            {
                int on = Current.IsDark ? 1 : 0;
                Native.DwmSetWindowAttribute(f.Handle, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
            }
            catch { }
        }

        public static ThemeMode Parse(string s)
        {
            ThemeMode m;
            return Enum.TryParse(s, true, out m) ? m : ThemeMode.System;
        }
    }
}
