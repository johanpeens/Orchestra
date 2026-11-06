using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using MudBlazor;

namespace TeamPortal.Web.Components.Pages;

public partial class HomePage : ComponentBase
{
    /// <summary>
    /// The four places worth going, in the order somebody new should meet them:
    /// write the processes up, say who the people are, test what the bot made of
    /// it, then read exactly what it was given.
    /// </summary>
    private static readonly HomeTile[] Tiles =
    [
        new(
            Key: "process",
            Name: "Business Process",
            Purpose: "Every process this business runs, broken into ordered steps "
                   + "with the people responsible and screenshots of the screens involved.",
            OnClick: "Opens the folder list and the process flow",
            Href: "/processes",
            Icon: Icons.Material.Outlined.AccountTree),

        new(
            Key: "team",
            Name: "Team",
            Purpose: "Who works here, what they do and which business unit they "
                   + "belong to. These are the people the steps get assigned to.",
            OnClick: "Opens the staff list, filtered by business unit",
            Href: "/team",
            Icon: Icons.Material.Outlined.Groups),

        new(
            Key: "chat",
            Name: "Chat",
            Purpose: "Ask the bot anything a colleague might ask. It answers only "
                   + "from what is written up, so whatever it cannot answer is a gap "
                   + "in the documentation rather than a fault in the bot.",
            OnClick: "Opens the playground and starts a fresh conversation",
            Href: "/chat",
            Icon: Icons.Material.Outlined.ChatBubbleOutline),

        new(
            Key: "training-data",
            Name: "My Training Data",
            Purpose: "Everything the bot has been given, as one object you can walk "
                   + "through folder by folder. When an answer looks wrong, this is "
                   + "where you find out why.",
            OnClick: "Opens the record, collapsed, ready to copy",
            Href: "/package",
            Icon: Icons.Material.Outlined.DataObject)
    ];

    [Inject]
    private IWebHostEnvironment Environment { get; set; } = null!;

    /// <summary>Where a tile's artwork goes, if somebody adds one.</summary>
    private static string ArtPath(HomeTile tile) => $"img/tiles/{tile.Key}.svg";

    /// <summary>
    /// Whether that file is actually there. Checked rather than assumed: an img
    /// tag pointing at a file nobody has added yet renders a broken-image icon,
    /// which looks worse than the placeholder it replaced.
    /// </summary>
    private bool HasArt(HomeTile tile) =>
        Environment.WebRootFileProvider.GetFileInfo(ArtPath(tile)).Exists;
}

/// <summary>
/// One tile. <paramref name="Purpose"/> says what the section is FOR;
/// <paramref name="OnClick"/> says what pressing it does. They are different
/// questions and a tile that answers only the first leaves you guessing.
/// </summary>
/// <param name="Key">Also the artwork's filename - see HomePage.ArtPath.</param>
public sealed record HomeTile(
    string Key,
    string Name,
    string Purpose,
    string OnClick,
    string Href,
    string Icon);
