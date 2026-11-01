// ═══════════════════════════════════════════════════════════════════════════
// ThemeTokens — Centralized Color & Dimensional Design Tokens for v2.1
// USB Shield Pro v2.1
// (c) 2026 x0abood. All rights reserved.
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Drawing;

namespace UsbShieldPro.UI
{
    public enum AppTheme
    {
        CharcoalRed,
        Midnight,
        Graphite,
        Porcelain,
        Cloud,
        FollowWindows
    }

    public enum TypographyRole
    {
        Display,
        Title,
        Subtitle,
        Body,
        BodyStrong,
        Caption,
        Code
    }

    /// <summary>
    /// Centralized contract for all color tokens across every theme palette.
    /// Controls consume tokens through this interface exclusively.
    /// </summary>
    public interface IThemePalette
    {
        string Name { get; }
        bool IsDark { get; }

        // Surfaces
        Color BackgroundPrimary { get; }
        Color BackgroundSecondary { get; }
        Color Surface { get; }
        Color SurfaceElevated { get; }
        Color SurfaceHover { get; }
        Color SurfaceSelected { get; }

        // Borders
        Color Border { get; }
        Color BorderSubtle { get; }

        // Text
        Color TextPrimary { get; }
        Color TextSecondary { get; }
        Color TextMuted { get; }
        Color TextDisabled { get; }

        // Accents (Primary Brand Identity)
        Color Accent { get; }
        Color AccentHover { get; }
        Color AccentPressed { get; }
        Color AccentSoft { get; }
        Color FocusRing { get; }

        // Semantics
        Color Success { get; }
        Color SuccessSoft { get; }
        Color Warning { get; }
        Color WarningSoft { get; }
        Color Info { get; }
        Color InfoSoft { get; }
        Color Danger { get; }
        Color DangerSoft { get; }
    }

    // ─── 1. Charcoal Red — Flagship Signature Theme ──────────────────────────
    public class CharcoalRedPalette : IThemePalette
    {
        public string Name { get { return "CharcoalRed"; } }
        public bool IsDark { get { return true; } }

        public Color BackgroundPrimary   { get { return Color.FromArgb(13, 15, 17); } }    // #0D0F11
        public Color BackgroundSecondary { get { return Color.FromArgb(17, 20, 25); } }    // #111419
        public Color Surface             { get { return Color.FromArgb(23, 26, 31); } }    // #171A1F
        public Color SurfaceElevated     { get { return Color.FromArgb(30, 34, 40); } }    // #1E2228
        public Color SurfaceHover        { get { return Color.FromArgb(37, 42, 49); } }    // #252A31
        public Color SurfaceSelected     { get { return Color.FromArgb(32, 36, 44); } }    // #20242C (Neutral dark slate)

        public Color Border              { get { return Color.FromArgb(48, 54, 64); } }    // #303640
        public Color BorderSubtle        { get { return Color.FromArgb(33, 37, 44); } }    // #21252C

        public Color TextPrimary         { get { return Color.FromArgb(244, 245, 246); } } // #F4F5F6
        public Color TextSecondary       { get { return Color.FromArgb(174, 180, 189); } } // #AEB4BD
        public Color TextMuted           { get { return Color.FromArgb(130, 139, 153); } } // #828B99 (5.07:1 WCAG AA)
        public Color TextDisabled        { get { return Color.FromArgb(85, 92, 102); } }   // #555C66

        public Color Accent              { get { return Color.FromArgb(198, 45, 66); } }   // #C62D42 (Signature Crimson)
        public Color AccentHover         { get { return Color.FromArgb(219, 58, 80); } }   // #DB3A50
        public Color AccentPressed       { get { return Color.FromArgb(168, 32, 52); } }   // #A82034
        public Color AccentSoft          { get { return Color.FromArgb(45, 21, 27); } }   // #2D151B
        public Color FocusRing           { get { return Color.FromArgb(237, 86, 106); } }  // #ED566A

        public Color Success             { get { return Color.FromArgb(53, 199, 127); } }  // #35C77F
        public Color SuccessSoft         { get { return Color.FromArgb(19, 43, 32); } }    // #132B20
        public Color Warning             { get { return Color.FromArgb(228, 163, 56); } }  // #E4A338
        public Color WarningSoft         { get { return Color.FromArgb(46, 34, 17); } }    // #2E2211
        public Color Info                { get { return Color.FromArgb(75, 155, 250); } }  // #4B9BFA
        public Color InfoSoft            { get { return Color.FromArgb(20, 34, 54); } }    // #142236
        public Color Danger              { get { return Color.FromArgb(240, 68, 56); } }   // #F04438
        public Color DangerSoft          { get { return Color.FromArgb(47, 23, 22); } }    // #2F1716
    }

    // ─── 2. Midnight — Deep Navy Dark Theme ──────────────────────────────────
    public class MidnightPalette : IThemePalette
    {
        public string Name { get { return "Midnight"; } }
        public bool IsDark { get { return true; } }

        public Color BackgroundPrimary   { get { return Color.FromArgb(11, 15, 25); } }    // #0B0F19
        public Color BackgroundSecondary { get { return Color.FromArgb(15, 22, 38); } }    // #0F1626
        public Color Surface             { get { return Color.FromArgb(21, 30, 51); } }    // #151E33
        public Color SurfaceElevated     { get { return Color.FromArgb(28, 39, 66); } }    // #1C2742
        public Color SurfaceHover        { get { return Color.FromArgb(36, 50, 84); } }    // #243254
        public Color SurfaceSelected     { get { return Color.FromArgb(30, 58, 110); } }   // #1E3A6E

        public Color Border              { get { return Color.FromArgb(38, 53, 84); } }    // #263554
        public Color BorderSubtle        { get { return Color.FromArgb(27, 37, 59); } }    // #1B253B

        public Color TextPrimary         { get { return Color.FromArgb(240, 244, 252); } } // #F0F4FC
        public Color TextSecondary       { get { return Color.FromArgb(147, 164, 199); } } // #93A4C7
        public Color TextMuted           { get { return Color.FromArgb(103, 120, 158); } } // #67789E
        public Color TextDisabled        { get { return Color.FromArgb(65, 80, 112); } }   // #415070

        public Color Accent              { get { return Color.FromArgb(47, 111, 235); } }  // #2F6FEB (Royal Blue)
        public Color AccentHover         { get { return Color.FromArgb(68, 131, 253); } }  // #4483FD
        public Color AccentPressed       { get { return Color.FromArgb(32, 83, 184); } }   // #2053B8
        public Color AccentSoft          { get { return Color.FromArgb(23, 37, 68); } }    // #172544
        public Color FocusRing           { get { return Color.FromArgb(88, 147, 255); } }  // #5893FF

        public Color Success             { get { return Color.FromArgb(53, 199, 127); } }  // #35C77F
        public Color SuccessSoft         { get { return Color.FromArgb(19, 43, 32); } }    // #132B20
        public Color Warning             { get { return Color.FromArgb(235, 178, 70); } }  // #EBB246
        public Color WarningSoft         { get { return Color.FromArgb(43, 35, 19); } }    // #2B2313
        public Color Info                { get { return Color.FromArgb(88, 166, 255); } }  // #58A6FF
        public Color InfoSoft            { get { return Color.FromArgb(22, 40, 71); } }    // #162847
        public Color Danger              { get { return Color.FromArgb(240, 68, 56); } }   // #F04438
        public Color DangerSoft          { get { return Color.FromArgb(45, 24, 26); } }    // #2D181A
    }

    // ─── 3. Graphite — Neutral Obsidian Dark (Non-Red Steel Accent) ──────────
    public class GraphitePalette : IThemePalette
    {
        public string Name { get { return "Graphite"; } }
        public bool IsDark { get { return true; } }

        public Color BackgroundPrimary   { get { return Color.FromArgb(18, 18, 20); } }    // #121214
        public Color BackgroundSecondary { get { return Color.FromArgb(24, 24, 27); } }    // #18181B
        public Color Surface             { get { return Color.FromArgb(32, 32, 36); } }    // #202024
        public Color SurfaceElevated     { get { return Color.FromArgb(39, 39, 42); } }    // #27272A
        public Color SurfaceHover        { get { return Color.FromArgb(50, 50, 56); } }    // #323238
        public Color SurfaceSelected     { get { return Color.FromArgb(37, 46, 56); } }    // #252E38

        public Color Border              { get { return Color.FromArgb(63, 63, 70); } }    // #3F3F46
        public Color BorderSubtle        { get { return Color.FromArgb(45, 45, 50); } }    // #2D2D32

        public Color TextPrimary         { get { return Color.FromArgb(244, 244, 245); } } // #F4F4F5
        public Color TextSecondary       { get { return Color.FromArgb(161, 161, 170); } } // #A1A1AA
        public Color TextMuted           { get { return Color.FromArgb(113, 113, 122); } } // #71717A
        public Color TextDisabled        { get { return Color.FromArgb(82, 82, 91); } }    // #52525B

        public Color Accent              { get { return Color.FromArgb(100, 116, 139); } } // #64748B (Cool Steel Slate - NON RED)
        public Color AccentHover         { get { return Color.FromArgb(126, 155, 184); } } // #7E9BB8
        public Color AccentPressed       { get { return Color.FromArgb(71, 85, 105); } }   // #475569
        public Color AccentSoft          { get { return Color.FromArgb(30, 37, 48); } }    // #1E2530
        public Color FocusRing           { get { return Color.FromArgb(148, 163, 184); } } // #94A3B8

        public Color Success             { get { return Color.FromArgb(34, 197, 94); } }   // #22C55E
        public Color SuccessSoft         { get { return Color.FromArgb(20, 43, 30); } }    // #142B1E
        public Color Warning             { get { return Color.FromArgb(234, 179, 8); } }   // #EAB308
        public Color WarningSoft         { get { return Color.FromArgb(45, 36, 14); } }    // #2D240E
        public Color Info                { get { return Color.FromArgb(56, 189, 248); } }  // #38BDF8
        public Color InfoSoft            { get { return Color.FromArgb(19, 38, 53); } }    // #132635
        public Color Danger              { get { return Color.FromArgb(239, 68, 68); } }   // #EF4444
        public Color DangerSoft          { get { return Color.FromArgb(47, 24, 27); } }    // #2F181B
    }

    // ─── 4. Porcelain — Warm Executive Light (Deep Indigo Accent) ────────────
    public class PorcelainPalette : IThemePalette
    {
        public string Name { get { return "Porcelain"; } }
        public bool IsDark { get { return false; } }

        public Color BackgroundPrimary   { get { return Color.FromArgb(249, 249, 247); } } // #F9F9F7 (Warm paper-white neutral)
        public Color BackgroundSecondary { get { return Color.FromArgb(242, 242, 238); } } // #F2F2EE (Warm neutral)
        public Color Surface             { get { return Color.FromArgb(255, 255, 255); } } // #FFFFFF
        public Color SurfaceElevated     { get { return Color.FromArgb(255, 255, 255); } } // #FFFFFF
        public Color SurfaceHover        { get { return Color.FromArgb(240, 238, 232); } } // #F0EEE8
        public Color SurfaceSelected     { get { return Color.FromArgb(232, 230, 223); } } // #E8E6DF

        public Color Border              { get { return Color.FromArgb(226, 224, 218); } } // #E2E0DA (Warm neutral border)
        public Color BorderSubtle        { get { return Color.FromArgb(238, 236, 232); } } // #EEECE8

        public Color TextPrimary         { get { return Color.FromArgb(24, 25, 27); } }    // #18191B
        public Color TextSecondary       { get { return Color.FromArgb(72, 75, 80); } }    // #484B50
        public Color TextMuted           { get { return Color.FromArgb(112, 116, 122); } } // #70747A
        public Color TextDisabled        { get { return Color.FromArgb(168, 172, 180); } } // #A8ACB4

        public Color Accent              { get { return Color.FromArgb(28, 58, 160); } }   // #1C3AA0 (Deep Indigo - NON RED)
        public Color AccentHover         { get { return Color.FromArgb(37, 70, 184); } }   // #2546B8
        public Color AccentPressed       { get { return Color.FromArgb(22, 46, 130); } }   // #162E82
        public Color AccentSoft          { get { return Color.FromArgb(238, 242, 255); } } // #EEF2FF
        public Color FocusRing           { get { return Color.FromArgb(37, 99, 235); } }   // #2563EB

        public Color Success             { get { return Color.FromArgb(30, 142, 84); } }   // #1E8E54
        public Color SuccessSoft         { get { return Color.FromArgb(230, 248, 240); } } // #E6F8F0
        public Color Warning             { get { return Color.FromArgb(168, 106, 0); } }   // #A86A00
        public Color WarningSoft         { get { return Color.FromArgb(255, 245, 229); } } // #FFF5E5
        public Color Info                { get { return Color.FromArgb(26, 110, 216); } }  // #1A6ED8
        public Color InfoSoft            { get { return Color.FromArgb(235, 244, 254); } } // #EBF4FE
        public Color Danger              { get { return Color.FromArgb(201, 36, 24); } }   // #C92418
        public Color DangerSoft          { get { return Color.FromArgb(253, 236, 235); } } // #FDECEB
    }

    // ─── 5. Cloud — Cool Technical Light (Cobalt Blue Accent) ────────────────
    public class CloudPalette : IThemePalette
    {
        public string Name { get { return "Cloud"; } }
        public bool IsDark { get { return false; } }

        public Color BackgroundPrimary   { get { return Color.FromArgb(236, 240, 246); } } // #ECF0F6 (Cool blue-gray slate)
        public Color BackgroundSecondary { get { return Color.FromArgb(226, 232, 240); } } // #E2E8F0 (Cool slate)
        public Color Surface             { get { return Color.FromArgb(248, 250, 252); } } // #F8FAFC (Cool crisp surface)
        public Color SurfaceElevated     { get { return Color.FromArgb(255, 255, 255); } } // #FFFFFF
        public Color SurfaceHover        { get { return Color.FromArgb(239, 243, 248); } } // #EFF3F8
        public Color SurfaceSelected     { get { return Color.FromArgb(224, 233, 245); } } // #E0E9F5

        public Color Border              { get { return Color.FromArgb(203, 213, 225); } } // #CBD5E1 (Cool slate border)
        public Color BorderSubtle        { get { return Color.FromArgb(222, 229, 238); } } // #DEE5EE

        public Color TextPrimary         { get { return Color.FromArgb(15, 23, 42); } }    // #0F172A
        public Color TextSecondary       { get { return Color.FromArgb(51, 65, 85); } }    // #334155
        public Color TextMuted           { get { return Color.FromArgb(100, 116, 139); } } // #64748B
        public Color TextDisabled        { get { return Color.FromArgb(148, 163, 184); } } // #94A3B8

        public Color Accent              { get { return Color.FromArgb(29, 100, 236); } }  // #1D64EC (Cobalt Blue - NON RED)
        public Color AccentHover         { get { return Color.FromArgb(23, 81, 191); } }   // #1751BF
        public Color AccentPressed       { get { return Color.FromArgb(19, 64, 153); } }   // #134099
        public Color AccentSoft          { get { return Color.FromArgb(232, 240, 254); } } // #E8F0FE
        public Color FocusRing           { get { return Color.FromArgb(29, 100, 236); } }  // #1D64EC

        public Color Success             { get { return Color.FromArgb(18, 122, 69); } }   // #127A45
        public Color SuccessSoft         { get { return Color.FromArgb(227, 245, 236); } } // #E3F5EC
        public Color Warning             { get { return Color.FromArgb(150, 94, 0); } }    // #965E00
        public Color WarningSoft         { get { return Color.FromArgb(255, 243, 222); } } // #FFF3DE
        public Color Info                { get { return Color.FromArgb(21, 88, 176); } }   // #1558B0
        public Color InfoSoft            { get { return Color.FromArgb(229, 240, 253); } } // #E5F0FD
        public Color Danger              { get { return Color.FromArgb(184, 30, 18); } }   // #B81E12
        public Color DangerSoft          { get { return Color.FromArgb(252, 234, 233); } } // #FCEAE9
    }

    // ─── Dimensional & Motion Tokens ─────────────────────────────────────────
    public static class SpacingTokens
    {
        public const int Space2 = 2;
        public const int Space4 = 4;
        public const int Space6 = 6;
        public const int Space8 = 8;
        public const int Space10 = 10;
        public const int Space12 = 12;
        public const int Space14 = 14;
        public const int Space16 = 16;
        public const int Space20 = 20;
        public const int Space24 = 24;
        public const int Space32 = 32;
        public const int Space48 = 48;
    }

    public static class RadiusTokens
    {
        public const int RadiusNone = 0;
        public const int RadiusSmall = 4;
        public const int RadiusMedium = 6;
        public const int RadiusLarge = 8;
        public const int RadiusPill = 999;
    }

    public static class HeightTokens
    {
        public const int HeightSm = 28;
        public const int HeightMd = 34;
        public const int HeightLg = 42;
    }

    public static class IconTokens
    {
        public const int IconSm = 14;
        public const int IconMd = 18;
        public const int IconLg = 24;
        public const int IconHero = 36;
    }

    public static class MotionTokens
    {
        public const int DurationInstant = 0;
        public const int DurationFast = 120;
        public const int DurationNormal = 220;
        public const int DurationDrawer = 280;
    }
}
