namespace SanjCorp3D.Api.Identity;

public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string Sales = "Sales";
    public const string Production = "Production";
    public const string Viewer = "Viewer";
    public static readonly string[] All = [Administrator, Sales, Production, Viewer];
}
