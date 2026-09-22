# How to run EFCore migrations

This shall provide context on how to work with the migrations of the database layer.

MulletaFlix runs on a single provider: MariaDB/MySQL (`MulletaFlix-MySQL`). SQLite was
removed — the server never creates a local database file, so every schema operation has
to be expressed in SQL that MariaDB accepts and that Pomelo can translate.

The provider registers its models through `MySqlDatabaseProvider`, and the design-time
factory lives in `Migrations/MySqlDesignTimeMulletaFlixDbFactory.cs`. When creating a new
migration, run the Entity Framework tool with the provider key so EFCore stores the
migration in the right assembly:

```cmd
dotnet ef migrations add MIGRATION_NAME --project "src/Jellyfin.Database/Jellyfin.Database.Implementations" -- --migration-provider MulletaFlix-MySQL
```

The example is made from the root folder of the repository.

If you get the error: `Run "dotnet tool restore" to make the "dotnet-ef" command available.` run `dotnet restore`.

If you get `System.UnauthorizedAccessException: Access to the path '...' is denied.` restore as sudo and then run `ef migrations` as sudo too.

## Plugin schemas

Plugins that keep their own tables do not own a database file. They receive the server's
provider and must point it at their own schema; IntroSkipper does this in
`PluginServiceRegistrator.WithIntroSkipperDatabase`, which overrides only the `database`
option. Because more than one context can share one schema, a plugin must not rely on
`Database.EnsureCreated()` alone: EF creates tables only when the schema is completely
empty. `IntroSkipper.Db.IntroSkipperSchema` shows the ordering-independent pattern.

