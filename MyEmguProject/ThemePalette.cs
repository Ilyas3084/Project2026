using System.Drawing;

namespace MyEmguProject
{
    internal enum AppThemeMode
    {
        Light,
        Dark
    }

    internal readonly record struct AppThemeColors(
        Color BgPrimary,
        Color BgCanvas,
        Color BgPanel,
        Color SurfacePrimary,
        Color SurfaceSecondary,
        Color SurfaceTertiary,
        Color BorderSoft,
        Color BorderStrong,
        Color AccentCyan,
        Color AccentBlue,
        Color AccentMuted,
        Color AccentPill,
        Color TextPrimary,
        Color TextSecondary,
        Color KeyIdleColor,
        Color KeyIdleBorderColor,
        Color KeyActiveColor,
        Color KeyActiveBorderColor);

    internal static class AppTheme
    {
        private static readonly AppThemeColors LightTheme = new(
            Color.FromArgb(233, 247, 245),
            Color.FromArgb(224, 241, 239),
            Color.FromArgb(241, 250, 248),
            Color.FromArgb(249, 253, 252),
            Color.FromArgb(241, 249, 247),
            Color.FromArgb(251, 255, 254),
            Color.FromArgb(187, 220, 214),
            Color.FromArgb(123, 202, 192),
            Color.FromArgb(82, 199, 190),
            Color.FromArgb(43, 184, 172),
            Color.FromArgb(94, 127, 123),
            Color.FromArgb(245, 252, 251),
            Color.FromArgb(25, 52, 56),
            Color.FromArgb(92, 123, 126),
            Color.FromArgb(41, 185, 173),
            Color.FromArgb(153, 227, 218),
            Color.FromArgb(0, 150, 70),
            Color.FromArgb(110, 255, 170));

        private static readonly AppThemeColors DarkTheme = new(
            Color.FromArgb(8, 28, 36),
            Color.FromArgb(11, 36, 47),
            Color.FromArgb(15, 41, 54),
            Color.FromArgb(24, 53, 67),
            Color.FromArgb(20, 47, 60),
            Color.FromArgb(16, 39, 50),
            Color.FromArgb(72, 117, 136),
            Color.FromArgb(92, 170, 196),
            Color.FromArgb(90, 215, 255),
            Color.FromArgb(67, 166, 230),
            Color.FromArgb(126, 194, 215),
            Color.FromArgb(38, 77, 94),
            Color.WhiteSmoke,
            Color.FromArgb(126, 194, 215),
            Color.FromArgb(33, 110, 210),
            Color.FromArgb(125, 190, 255),
            Color.FromArgb(0, 150, 70),
            Color.FromArgb(110, 255, 170));

        public static AppThemeMode Mode { get; private set; } = AppThemeMode.Light;
        public static bool IsDark => Mode == AppThemeMode.Dark;
        public static AppThemeColors Current => IsDark ? DarkTheme : LightTheme;

        public static void SetMode(AppThemeMode mode)
        {
            Mode = mode;
        }

        public static Color ComboBoxBackground => IsDark ? Color.FromArgb(50, 50, 60) : Color.FromArgb(252, 254, 255);
        public static Color ComboBoxText => IsDark ? Color.WhiteSmoke : Current.TextPrimary;
        public static Color VideoPanelBackground => IsDark ? Color.Black : Color.FromArgb(226, 242, 240);
        public static Color PictureBoxBackground => IsDark ? Color.FromArgb(7, 19, 24) : Color.FromArgb(231, 245, 243);
        public static Color SwitchNumberBackground => IsDark ? Color.FromArgb(48, 24, 24, 28) : Color.FromArgb(180, 245, 250, 254);
        public static Color SwitchLabelBackground => IsDark ? Color.FromArgb(48, 20, 20, 24) : Color.FromArgb(210, 245, 250, 254);

        public static Color MenuHover => IsDark ? Color.FromArgb(42, 96, 118) : Color.FromArgb(229, 245, 242);
        public static Color MenuPressed => IsDark ? Color.FromArgb(57, 126, 154) : Color.FromArgb(213, 239, 235);
        public static Color MenuBackground => IsDark ? Current.SurfacePrimary : Color.FromArgb(249, 253, 252);
        public static Color MenuBorder => IsDark ? Current.BorderSoft : Color.FromArgb(187, 220, 214);
        public static Color MenuSeparator => IsDark ? Color.FromArgb(61, 92, 109) : Color.FromArgb(210, 229, 225);

        public static Color DialogBackground => IsDark ? Color.FromArgb(36, 36, 46) : Color.FromArgb(241, 248, 253);
        public static Color DialogSurface => IsDark ? Color.FromArgb(50, 50, 60) : Color.FromArgb(252, 254, 255);
        public static Color DialogText => IsDark ? Color.WhiteSmoke : Current.TextPrimary;
        public static Color DialogHint => IsDark ? Color.Gainsboro : Color.FromArgb(94, 116, 136);
        public static Color DialogPrimaryButtonBackground => IsDark ? Color.White : Current.AccentBlue;
        public static Color DialogPrimaryButtonText => IsDark ? Color.Black : Color.White;
        public static Color DialogSecondaryButtonBackground => Color.White;
        public static Color DialogSecondaryButtonText => IsDark ? Color.Black : Current.TextPrimary;
    }
}
