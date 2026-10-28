using MudBlazor;

namespace UrlPreviewApp.Client.Layout;

/// <summary>MudBlazor theme. The dark palette is ported from linkpreview's shadcn colors.</summary>
public static class AppTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#0e7490",
            Secondary = "#525252",
            Background = "#fafafa",
            Surface = "#ffffff",
            AppbarBackground = "#fafafa",
            AppbarText = "#0a0a0a",
            TextPrimary = "#0a0a0a",
            TextSecondary = "#737373",
            ActionDefault = "#525252",
            LinesDefault = "rgba(0,0,0,0.1)",
            Divider = "rgba(0,0,0,0.1)",
            Success = "#16a34a",
            Warning = "#ca8a04",
            Error = "#dc2626",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#1fb6d4",
            Secondary = "#a1a1a1",
            Background = "#0a0a0a",
            BackgroundGray = "#262626",
            Surface = "#171717",
            AppbarBackground = "#0a0a0a",
            AppbarText = "#fafafa",
            DrawerBackground = "#171717",
            TextPrimary = "#fafafa",
            TextSecondary = "#a1a1a1",
            ActionDefault = "#a1a1a1",
            LinesDefault = "rgba(255,255,255,0.1)",
            LinesInputs = "rgba(255,255,255,0.2)",
            Divider = "rgba(255,255,255,0.1)",
            TableLines = "rgba(255,255,255,0.1)",
            Success = "#22c55e",
            Warning = "#eab308",
            Error = "#ff6467",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Public Sans", "ui-sans-serif", "system-ui", "-apple-system", "Segoe UI", "sans-serif"],
            },
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "0.5rem" },
    };
}
