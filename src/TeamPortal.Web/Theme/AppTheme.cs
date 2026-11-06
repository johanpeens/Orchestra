using MudBlazor;
using TeamPortal.Application.Common;

namespace TeamPortal.Web.Theme;

public static class AppTheme
{
    /// <summary>
    /// Font stack from Frontend/FontUsage.md. Brand Sans first, then only the
    /// approved Google Fonts, terminated with a generic sans-serif.
    /// </summary>
    private static readonly string[] FontStack =
    [
        "Brand Sans",
        "Quicksand",
        "Mulish",
        "Be Vietnam Pro",
        "Mona Sans",
        "Manjari",
        "sans-serif"
    ];

    public static readonly MudTheme Default = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = BrandPalette.Purple100,
            PrimaryContrastText = BrandPalette.White,

            Secondary = BrandPalette.Orange100,
            SecondaryContrastText = BrandPalette.White,

            Tertiary = BrandPalette.Aqua100,
            TertiaryContrastText = BrandPalette.White,

            // Info is Gold, per the Buttons & Actions table in TeamConventions.
            Info = BrandPalette.Gold100,
            InfoContrastText = BrandPalette.White,

            Success = BrandPalette.Aqua100,
            SuccessContrastText = BrandPalette.White,

            Warning = BrandPalette.Orange80,
            WarningContrastText = BrandPalette.White,

            Error = BrandPalette.Red100,
            ErrorContrastText = BrandPalette.White,

            Dark = BrandPalette.Neutral80,

            Background = BrandPalette.Wash,
            Surface = BrandPalette.White,

            AppbarBackground = BrandPalette.PurpleDeep,
            AppbarText = BrandPalette.White,

            DrawerBackground = BrandPalette.White,
            DrawerText = BrandPalette.Neutral80,
            DrawerIcon = BrandPalette.Purple100,

            TextPrimary = BrandPalette.Neutral80,
            TextSecondary = BrandPalette.Neutral60,
            TextDisabled = BrandPalette.Neutral20,

            ActionDefault = BrandPalette.Neutral60,
            ActionDisabled = BrandPalette.Neutral20,

            LinesDefault = BrandPalette.Purple20,
            LinesInputs = BrandPalette.Purple40,
            TableLines = BrandPalette.Purple20,
            Divider = BrandPalette.Purple20
        },

        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = FontStack },
            H1 = new H1Typography { FontFamily = FontStack },
            H2 = new H2Typography { FontFamily = FontStack },
            H3 = new H3Typography { FontFamily = FontStack },
            H4 = new H4Typography { FontFamily = FontStack },
            H5 = new H5Typography { FontFamily = FontStack },
            H6 = new H6Typography { FontFamily = FontStack },
            Subtitle1 = new Subtitle1Typography { FontFamily = FontStack },
            Subtitle2 = new Subtitle2Typography { FontFamily = FontStack },
            Body1 = new Body1Typography { FontFamily = FontStack },
            Body2 = new Body2Typography { FontFamily = FontStack },
            Button = new ButtonTypography { FontFamily = FontStack },
            Caption = new CaptionTypography { FontFamily = FontStack },
            Overline = new OverlineTypography { FontFamily = FontStack }
        },

        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px"
        }
    };
}
