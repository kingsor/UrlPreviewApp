namespace UrlPreviewApp.Client.Logic;

/// <summary>
/// Recommended text lengths used by the inspection tabs and the score.
/// </summary>
public static class MetadataLimits
{
    public const int TitleMaxChars = 60;
    public const int DescriptionMaxChars = 160;
    public const int OgTitleMaxChars = 60;
    public const int OgDescriptionMaxChars = 160;
    public const int TwitterTitleMaxChars = 70;
    public const int TwitterDescriptionMaxChars = 200;

    public const int TitleMinChars = 30;
    public const int DescriptionMinChars = 120;

    public const int OgImageWidth = 1200;
    public const int OgImageHeight = 630;
    public const int TwitterImageWidth = 1200;
    public const int TwitterImageHeight = 628;
}
