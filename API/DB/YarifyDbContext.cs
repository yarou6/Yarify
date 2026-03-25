using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Scaffolding.Internal;

namespace API.DB;

public partial class YarifyDbContext : DbContext
{
    public YarifyDbContext()
    {
    }

    public YarifyDbContext(DbContextOptions<YarifyDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Album> Albums { get; set; }

    public virtual DbSet<Follow> Follows { get; set; }

    public virtual DbSet<Genre> Genres { get; set; }

    public virtual DbSet<Likedsong> Likedsongs { get; set; }

    public virtual DbSet<Listeningevent> Listeningevents { get; set; }

    public virtual DbSet<Playbackqueue> Playbackqueues { get; set; }

    public virtual DbSet<Playlist> Playlists { get; set; }

    public virtual DbSet<Playlistsong> Playlistsongs { get; set; }

    public virtual DbSet<Refreshtoken> Refreshtokens { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Song> Songs { get; set; }

    public virtual DbSet<Songlyric> Songlyrics { get; set; }

    public virtual DbSet<Songstatsdaily> Songstatsdailies { get; set; }

    public virtual DbSet<Subscriptionplan> Subscriptionplans { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Userplan> Userplans { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseMySql("server=localhost;user=root;database=YarifyDB", Microsoft.EntityFrameworkCore.ServerVersion.Parse("10.4.32-mariadb"));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_general_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Album>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("albums");

            entity.HasIndex(e => e.ArtistUserId, "ix_albums_artist_user_id");

            entity.HasIndex(e => e.Title, "ix_albums_title");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.ArtistUserId).HasColumnType("int(11)");
            entity.Property(e => e.CoverPath).HasMaxLength(500);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.ArtistUser).WithMany(p => p.Albums)
                .HasForeignKey(d => d.ArtistUserId)
                .HasConstraintName("fk_albums_users_artist");
        });

        modelBuilder.Entity<Follow>(entity =>
        {
            entity.HasKey(e => new { e.SubscriberUserId, e.ArtistUserId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("follow");

            entity.HasIndex(e => e.ArtistUserId, "ix_follow_artist_user_id");

            entity.HasIndex(e => e.SubscriberUserId, "ix_follow_subscriber_user_id");

            entity.Property(e => e.SubscriberUserId).HasColumnType("int(11)");
            entity.Property(e => e.ArtistUserId).HasColumnType("int(11)");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'");

            entity.HasOne(d => d.ArtistUser).WithMany(p => p.FollowArtistUsers)
                .HasForeignKey(d => d.ArtistUserId)
                .HasConstraintName("fk_follow_artist_users");

            entity.HasOne(d => d.SubscriberUser).WithMany(p => p.FollowSubscriberUsers)
                .HasForeignKey(d => d.SubscriberUserId)
                .HasConstraintName("fk_follow_subscriber_users");
        });

        modelBuilder.Entity<Genre>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("genres");

            entity.HasIndex(e => e.Title, "uq_genres_title").IsUnique();

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.Title).HasMaxLength(100);
        });

        modelBuilder.Entity<Likedsong>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.SongId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("likedsongs");

            entity.HasIndex(e => e.SongId, "ix_likedsongs_song_id");

            entity.Property(e => e.UserId).HasColumnType("int(11)");
            entity.Property(e => e.SongId).HasColumnType("int(11)");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Song).WithMany(p => p.Likedsongs)
                .HasForeignKey(d => d.SongId)
                .HasConstraintName("fk_likedsongs_songs");

            entity.HasOne(d => d.User).WithMany(p => p.Likedsongs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_likedsongs_users");
        });

        modelBuilder.Entity<Listeningevent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("listeningevents");

            entity.HasIndex(e => e.SongId, "ix_listeningevents_song_id");

            entity.HasIndex(e => new { e.UserId, e.StartedAt }, "ix_listeningevents_user_started");

            entity.Property(e => e.Id).HasColumnType("bigint(20)");
            entity.Property(e => e.EndedAt).HasColumnType("datetime");
            entity.Property(e => e.PlayedMs).HasColumnType("int(11)");
            entity.Property(e => e.SongId).HasColumnType("int(11)");
            entity.Property(e => e.SourceId).HasColumnType("int(11)");
            entity.Property(e => e.SourceType).HasColumnType("enum('Playlist','Album','Search','Direct','Queue')");
            entity.Property(e => e.StartedAt).HasColumnType("datetime");
            entity.Property(e => e.UserId).HasColumnType("int(11)");

            entity.HasOne(d => d.Song).WithMany(p => p.Listeningevents)
                .HasForeignKey(d => d.SongId)
                .HasConstraintName("fk_listeningevents_songs");

            entity.HasOne(d => d.User).WithMany(p => p.Listeningevents)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_listeningevents_users");
        });

        modelBuilder.Entity<Playbackqueue>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("playbackqueue");

            entity.HasIndex(e => e.SongId, "ix_playbackqueue_song_id");

            entity.HasIndex(e => new { e.UserId, e.Position }, "uq_playbackqueue_user_position").IsUnique();

            entity.Property(e => e.Id).HasColumnType("bigint(20)");
            entity.Property(e => e.AddedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.Position).HasColumnType("int(11)");
            entity.Property(e => e.SongId).HasColumnType("int(11)");
            entity.Property(e => e.UserId).HasColumnType("int(11)");

            entity.HasOne(d => d.Song).WithMany(p => p.Playbackqueues)
                .HasForeignKey(d => d.SongId)
                .HasConstraintName("fk_playbackqueue_songs");

            entity.HasOne(d => d.User).WithMany(p => p.Playbackqueues)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_playbackqueue_users");
        });

        modelBuilder.Entity<Playlist>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("playlists");

            entity.HasIndex(e => e.IsPublic, "ix_playlists_is_public");

            entity.HasIndex(e => e.OwnerUserId, "ix_playlists_owner_user_id");

            entity.HasIndex(e => e.Title, "ix_playlists_title");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.CoverPath).HasMaxLength(500);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.OwnerUserId).HasColumnType("int(11)");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");

            entity.HasOne(d => d.OwnerUser).WithMany(p => p.Playlists)
                .HasForeignKey(d => d.OwnerUserId)
                .HasConstraintName("fk_playlists_users_owner");
        });

        modelBuilder.Entity<Playlistsong>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("playlistsongs");

            entity.HasIndex(e => e.SongId, "ix_playlistsongs_song_id");

            entity.HasIndex(e => new { e.PlaylistId, e.Position }, "uq_playlistsongs_playlist_position").IsUnique();

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.AddedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.PlaylistId).HasColumnType("int(11)");
            entity.Property(e => e.Position).HasColumnType("int(11)");
            entity.Property(e => e.SongId).HasColumnType("int(11)");

            entity.HasOne(d => d.Playlist).WithMany(p => p.Playlistsongs)
                .HasForeignKey(d => d.PlaylistId)
                .HasConstraintName("fk_playlistsongs_playlists");

            entity.HasOne(d => d.Song).WithMany(p => p.Playlistsongs)
                .HasForeignKey(d => d.SongId)
                .HasConstraintName("fk_playlistsongs_songs");
        });

        modelBuilder.Entity<Refreshtoken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("refreshtokens");

            entity.HasIndex(e => e.ExpiresAt, "ix_refreshtokens_expires_at");

            entity.HasIndex(e => e.RevokedAt, "ix_refreshtokens_revoked_at");

            entity.HasIndex(e => e.UserId, "ix_refreshtokens_user_id");

            entity.HasIndex(e => e.TokenHash, "uq_refreshtokens_token_hash").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("bigint(20)");
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.CreatedByIp)
                .HasColumnName("created_by_ip")
                .HasMaxLength(45);
            entity.Property(e => e.ExpiresAt)
                .HasColumnName("expires_at")
                .HasColumnType("datetime");
            entity.Property(e => e.ReplacedByTokenHash)
                .HasColumnName("replaced_by_token_hash")
                .HasMaxLength(64);
            entity.Property(e => e.RevokedAt)
                .HasColumnName("revoked_at")
                .HasColumnType("datetime");
            entity.Property(e => e.RevokedByIp)
                .HasColumnName("revoked_by_ip")
                .HasMaxLength(45);
            entity.Property(e => e.TokenHash)
                .HasColumnName("token_hash")
                .HasMaxLength(64);
            entity.Property(e => e.UserAgent)
                .HasColumnName("user_agent")
                .HasMaxLength(500);
            entity.Property(e => e.UserId)
                .HasColumnName("user_id")
                .HasColumnType("int(11)");

            entity.HasOne(d => d.User).WithMany(p => p.Refreshtokens)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_refreshtokens_users");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("roles");

            entity.HasIndex(e => e.Title, "uq_roles_title").IsUnique();

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.Title).HasMaxLength(30);
        });

        modelBuilder.Entity<Song>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("songs");

            entity.HasIndex(e => e.AlbumId, "ix_songs_album_id");

            entity.HasIndex(e => e.ArtistUserId, "ix_songs_artist_user_id");

            entity.HasIndex(e => e.ExternalId, "ix_songs_external_id");

            entity.HasIndex(e => e.SourceType, "ix_songs_source_type");

            entity.HasIndex(e => e.Title, "ix_songs_title");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.AlbumId).HasColumnType("int(11)");
            entity.Property(e => e.ArtistUserId).HasColumnType("int(11)");
            entity.Property(e => e.CoverPath).HasMaxLength(500);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.DurationSec).HasColumnType("int(11)");
            entity.Property(e => e.ExternalId).HasMaxLength(200);
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'");
            entity.Property(e => e.LocalPath).HasMaxLength(1000);
            entity.Property(e => e.PlayCount).HasColumnType("bigint(20)");
            entity.Property(e => e.SourceType)
                .HasDefaultValueSql("'Local'")
                .HasColumnType("enum('Local','Online')");
            entity.Property(e => e.StreamUrl).HasMaxLength(1000);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.TrackNumber).HasColumnType("int(11)");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Album).WithMany(p => p.Songs)
                .HasForeignKey(d => d.AlbumId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_songs_albums");

            entity.HasOne(d => d.ArtistUser).WithMany(p => p.Songs)
                .HasForeignKey(d => d.ArtistUserId)
                .HasConstraintName("fk_songs_users_artist");

            entity.HasMany(d => d.Genres).WithMany(p => p.Songs)
                .UsingEntity<Dictionary<string, object>>(
                    "Songgenre",
                    r => r.HasOne<Genre>().WithMany()
                        .HasForeignKey("GenreId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_songgenres_genres"),
                    l => l.HasOne<Song>().WithMany()
                        .HasForeignKey("SongId")
                        .HasConstraintName("fk_songgenres_songs"),
                    j =>
                    {
                        j.HasKey("SongId", "GenreId")
                            .HasName("PRIMARY")
                            .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });
                        j.ToTable("songgenres");
                        j.HasIndex(new[] { "GenreId" }, "ix_songgenres_genre_id");
                        j.IndexerProperty<int>("SongId").HasColumnType("int(11)");
                        j.IndexerProperty<int>("GenreId").HasColumnType("int(11)");
                    });
        });

        modelBuilder.Entity<Songlyric>(entity =>
        {
            entity.HasKey(e => new { e.SongId, e.LanguageCode })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("songlyrics");

            entity.Property(e => e.SongId).HasColumnType("int(11)");
            entity.Property(e => e.LanguageCode)
                .HasMaxLength(10)
                .HasDefaultValueSql("'und'");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.LyricsText).HasColumnType("mediumtext");
            entity.Property(e => e.SourceType)
                .HasDefaultValueSql("'Manual'")
                .HasColumnType("enum('Manual','Imported')");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Song).WithMany(p => p.Songlyrics)
                .HasForeignKey(d => d.SongId)
                .HasConstraintName("fk_songlyrics_songs");
        });

        modelBuilder.Entity<Songstatsdaily>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("songstatsdaily");

            entity.HasIndex(e => e.SongId, "ix_songstatsdaily_song_id");

            entity.HasIndex(e => new { e.StartDate, e.SongId }, "uq_songstatsdaily_date_song").IsUnique();

            entity.Property(e => e.Id).HasColumnType("bigint(20)");
            entity.Property(e => e.PlaysCount).HasColumnType("int(11)");
            entity.Property(e => e.SongId).HasColumnType("int(11)");
            entity.Property(e => e.UniqueListeners).HasColumnType("int(11)");

            entity.HasOne(d => d.Song).WithMany(p => p.Songstatsdailies)
                .HasForeignKey(d => d.SongId)
                .HasConstraintName("fk_songstatsdaily_songs");
        });

        modelBuilder.Entity<Subscriptionplan>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("subscriptionplans");

            entity.HasIndex(e => e.Title, "uq_subscriptionplans_title").IsUnique();

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .HasDefaultValueSql("'USD'")
                .IsFixedLength();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.MonthlyPrice).HasPrecision(10, 2);
            entity.Property(e => e.Title).HasMaxLength(50);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("users");

            entity.HasIndex(e => e.RoleId, "ix_users_role_id");

            entity.HasIndex(e => e.ArtistName, "uq_users_artist_name").IsUnique();

            entity.HasIndex(e => e.Email, "uq_users_email").IsUnique();

            entity.HasIndex(e => e.Login, "uq_users_login").IsUnique();

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.ArtistName).HasMaxLength(120);
            entity.Property(e => e.AvatarPath).HasMaxLength(500);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.DisplayName).HasMaxLength(50);
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'");
            entity.Property(e => e.LastLogin).HasColumnType("datetime");
            entity.Property(e => e.Login).HasMaxLength(50);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.RoleId).HasColumnType("int(11)");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_users_roles");
        });

        modelBuilder.Entity<Userplan>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PRIMARY");

            entity.ToTable("userplans");

            entity.HasIndex(e => e.ExpiresAt, "ix_userplans_expires_at");

            entity.HasIndex(e => e.PlanId, "ix_userplans_plan_id");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnType("int(11)");
            entity.Property(e => e.ExpiresAt).HasColumnType("datetime");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'");
            entity.Property(e => e.NextRenewAt).HasColumnType("datetime");
            entity.Property(e => e.PlanId).HasColumnType("int(11)");
            entity.Property(e => e.StartedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'Active'")
                .HasColumnType("enum('Active','Trial','Canceled','Expired')");

            entity.HasOne(d => d.Plan).WithMany(p => p.Userplans)
                .HasForeignKey(d => d.PlanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_userplans_plans");

            entity.HasOne(d => d.User).WithOne(p => p.Userplan)
                .HasForeignKey<Userplan>(d => d.UserId)
                .HasConstraintName("fk_userplans_users");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

