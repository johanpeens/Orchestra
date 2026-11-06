namespace TeamPortal.Application.Common;

public static class BrandPalette
{
    // --- Primary: Purple -----------------------------------------------------

    /// <summary>
    /// Deeper than Purple100, used for the app bar and the login panel. The
    /// designs show the top bar noticeably darker than the brand purple, and a
    /// flat Purple100 bar reads as washed out against the page.
    /// </summary>
    public const string PurpleDeep = "#2E1A47";

    public const string Purple100 = "#4A2767";
    public const string Purple80 = "#67457B";
    public const string Purple60 = "#896D98";
    public const string Purple40 = "#AD9AB9";
    public const string Purple20 = "#D5CBDD";

    // --- Secondary: Orange ---------------------------------------------------
    public const string Orange100 = "#E55218";
    public const string Orange80 = "#EB7840";
    public const string Orange60 = "#F29D6E";
    public const string Orange40 = "#F7C09E";
    public const string Orange20 = "#FCE0CF";

    // --- Secondary: Gold -----------------------------------------------------
    public const string Gold100 = "#9A8445";
    public const string Gold80 = "#AD9A66";
    public const string Gold20 = "#E9E3D6";

    // --- Secondary: Aqua -----------------------------------------------------
    public const string Aqua100 = "#44B4A6";
    public const string Aqua80 = "#7BC4B8";
    public const string Aqua20 = "#E3F1EF";

    // --- Neutral -------------------------------------------------------------
    public const string Black = "#000000";
    public const string Neutral80 = "#222227";
    public const string Neutral60 = "#4C4A4F";
    public const string Neutral40 = "#7F7F85";
    public const string Neutral20 = "#B9B7BB";
    public const string White = "#FFFFFF";

    // --- Red -----------------------------------------------------------------
    public const string Red100 = "#CF1230";
    public const string Red20 = "#F6D7D0";

    /// <summary>Page background. A very light purple wash, not a palette colour.</summary>
    public const string Wash = "#F7F5F9";

    /// <summary>Fallback for a department created without a colour.</summary>
    public const string DefaultDepartmentColour = Purple100;

    /// <summary>
    /// The eight swatches offered by the Edit Department modal, screen 07.
    /// Order matters - it is the order they appear in the picker.
    /// </summary>
    // Named rather than inline, so the seeder and anything else that needs a
    // department colour can reach them. A colour that is not one of the swatches
    // cannot be chosen again once it is changed - see the note on the seeder.
    public const string SwatchBlue = "#2F6FE0";
    public const string SwatchGreen = "#2E9E5B";
    public const string SwatchPink = "#E0489B";

    public static readonly IReadOnlyList<string> DepartmentSwatches =
    [
        Purple100,
        Aqua100,
        Orange100,
        Red100,
        SwatchBlue,
        SwatchGreen,
        Gold100,
        SwatchPink
    ];
}
