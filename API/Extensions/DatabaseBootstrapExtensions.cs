using API.DB;
using Microsoft.EntityFrameworkCore;

namespace API.Extensions;

public static class DatabaseBootstrapExtensions
{
    public static async Task EnsurePlayerSettingsTableAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<YarifyDbContext>();

        const string sql = @"
CREATE TABLE IF NOT EXISTS userplaybacksettings (
    user_id INT NOT NULL,
    shuffle_enabled TINYINT(1) NOT NULL DEFAULT 0,
    repeat_mode ENUM('Off','All','One') NOT NULL DEFAULT 'Off',
    autoplay_enabled TINYINT(1) NOT NULL DEFAULT 1,
    updated_at DATETIME NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
    PRIMARY KEY (user_id),
    CONSTRAINT fk_userplaybacksettings_users
        FOREIGN KEY (user_id) REFERENCES users(id)
        ON DELETE CASCADE
);";

        await db.Database.ExecuteSqlRawAsync(sql);
    }
}
