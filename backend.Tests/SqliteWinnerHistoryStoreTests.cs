using Backend.Sessions;
using Microsoft.Data.Sqlite;

namespace Backend.Tests;

public class SqliteWinnerHistoryStoreTests
{
    [Fact]
    public async Task Saved_winner_round_trips_through_a_fresh_connection()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cmsn_test_{Guid.NewGuid():N}.db");
        try
        {
            // First instance creates schema and writes.
            var store1 = new SqliteWinnerHistoryStore(path);
            await store1.SaveWinnerAsync("Quaxel");
            await store1.SaveWinnerAsync("Quaxel"); // duplicate name should be returned once via DISTINCT
            await store1.SaveWinnerAsync("Quapsel");

            // Second instance simulates a backend restart - schema already exists, data persists.
            var store2 = new SqliteWinnerHistoryStore(path);
            var names = await store2.GetAllWinnerNamesAsync();

            Assert.Equal(2, names.Count);
            Assert.Contains("Quaxel", names);
            Assert.Contains("Quapsel", names);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch { /* best-effort cleanup */ }
            }
        }
    }
}
