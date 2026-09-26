using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller;
using Microsoft.EntityFrameworkCore;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Contexts;
using MulletaFlix.Database.Implementations.Entities;

namespace MulletaFlix.Server.Implementations.Users;

/// <summary>
/// Manages the storage and retrieval of display preferences through Entity Framework.
/// </summary>
public sealed class DisplayPreferencesManager : IDisplayPreferencesManager
{
    private readonly IDbContextFactory<UsersDbContext> _dbContextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayPreferencesManager"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    public DisplayPreferencesManager(IDbContextFactory<UsersDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <inheritdoc />
    public DisplayPreferences GetDisplayPreferences(Guid userId, Guid itemId, string client)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var prefs = dbContext.DisplayPreferences
            .Include(pref => pref.HomeSections)
            .FirstOrDefault(pref =>
                pref.UserId.Equals(userId) && pref.Client == client && pref.ItemId.Equals(itemId));

        if (prefs is null)
        {
            prefs = new DisplayPreferences(userId, itemId, client);
            dbContext.DisplayPreferences.Add(prefs);
            // This method and the DbContext it uses are entirely synchronous (sync CreateDbContext,
            // no transaction awaited anywhere in this call graph), so call the genuine synchronous
            // SaveChanges API instead of blocking on the async one (sync-over-async / Achado B-7).
            dbContext.SaveChanges();
        }

        return prefs;
    }

    /// <inheritdoc />
    public ItemDisplayPreferences GetItemDisplayPreferences(Guid userId, Guid itemId, string client)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var prefs = dbContext.ItemDisplayPreferences
            .FirstOrDefault(pref => pref.UserId.Equals(userId) && pref.ItemId.Equals(itemId) && pref.Client == client);

        if (prefs is null)
        {
            prefs = new ItemDisplayPreferences(userId, Guid.Empty, client);
            dbContext.ItemDisplayPreferences.Add(prefs);
            // See GetDisplayPreferences above: this call graph is entirely synchronous, so use the
            // genuine synchronous SaveChanges API instead of sync-over-async (Achado B-7).
            dbContext.SaveChanges();
        }

        return prefs;
    }

    /// <inheritdoc />
    public IList<ItemDisplayPreferences> ListItemDisplayPreferences(Guid userId, string client)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        return dbContext.ItemDisplayPreferences
            .AsNoTracking()
            .Where(prefs => prefs.UserId.Equals(userId) && !prefs.ItemId.Equals(default) && prefs.Client == client)
            .ToList();
    }

    /// <inheritdoc />
    public Dictionary<string, string?> ListCustomItemDisplayPreferences(Guid userId, Guid itemId, string client)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        return dbContext.CustomItemDisplayPreferences
            .AsNoTracking()
            .Where(prefs => prefs.UserId.Equals(userId)
                            && prefs.ItemId.Equals(itemId)
                            && prefs.Client == client)
            .ToDictionary(prefs => prefs.Key, prefs => prefs.Value);
    }

    /// <inheritdoc />
    public void SetCustomItemDisplayPreferences(Guid userId, Guid itemId, string client, Dictionary<string, string?> customPreferences)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        dbContext.CustomItemDisplayPreferences.Where(prefs => prefs.UserId.Equals(userId)
                            && prefs.ItemId.Equals(itemId)
                            && prefs.Client == client)
                            .ExecuteDelete();

        foreach (var (key, value) in customPreferences)
        {
            dbContext.CustomItemDisplayPreferences
                .Add(new CustomItemDisplayPreferences(userId, itemId, client, key, value));
        }

        // See GetDisplayPreferences above: this call graph is entirely synchronous, so use the
        // genuine synchronous SaveChanges API instead of sync-over-async (Achado B-7).
        dbContext.SaveChanges();
    }

    /// <inheritdoc/>
    public void UpdateDisplayPreferences(DisplayPreferences displayPreferences)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        dbContext.DisplayPreferences.Attach(displayPreferences).State = EntityState.Modified;
        // See GetDisplayPreferences above: this call graph is entirely synchronous, so use the
        // genuine synchronous SaveChanges API instead of sync-over-async (Achado B-7).
        dbContext.SaveChanges();
    }

    /// <inheritdoc/>
    public void UpdateItemDisplayPreferences(ItemDisplayPreferences itemDisplayPreferences)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        dbContext.ItemDisplayPreferences.Attach(itemDisplayPreferences).State = EntityState.Modified;
        // See GetDisplayPreferences above: this call graph is entirely synchronous, so use the
        // genuine synchronous SaveChanges API instead of sync-over-async (Achado B-7).
        dbContext.SaveChanges();
    }
}

