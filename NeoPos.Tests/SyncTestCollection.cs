namespace NeoPos.Tests;

/// <summary>Serializes sync/bootstrap integration tests that use SQLite in-memory databases.</summary>
[CollectionDefinition(Name)]
public sealed class SyncTestCollection : ICollectionFixture<SyncTestCollection>
{
    public const string Name = "SyncSqlite";
}
