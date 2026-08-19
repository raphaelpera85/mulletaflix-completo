namespace MulletaFlix.Database.Implementations;

public static class DatabaseNames
{
    /// <summary>
    /// Gets the main database name.
    /// Overridable via the <c>MulletaFlix_DATABASE_NAME</c> environment variable
    /// so integration tests can run against an isolated database.
    /// </summary>
    public static string Main
        => System.Environment.GetEnvironmentVariable("MulletaFlix_DATABASE_NAME") ?? "mulletaflix";
}
