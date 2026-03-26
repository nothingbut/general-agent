using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeneralAgent.Infrastructure.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionTagsAndFTS : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. 创建 session_tags 表（EF Core 生成）
            migrationBuilder.CreateTable(
                name: "session_tags",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Tag = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Emoji = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_tags", x => new { x.SessionId, x.Tag });
                    table.ForeignKey(
                        name: "FK_session_tags_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_tags_Tag",
                table: "session_tags",
                column: "Tag");

            // 2. 创建 FTS5 虚拟表（手动添加）
            migrationBuilder.Sql(@"
                CREATE VIRTUAL TABLE messages_fts USING fts5(
                    message_id UNINDEXED,
                    session_id UNINDEXED,
                    content,
                    role UNINDEXED,
                    created_at UNINDEXED
                );
            ");

            // 3. 创建触发器（自动同步 messages → messages_fts）
            migrationBuilder.Sql(@"
                CREATE TRIGGER messages_ai AFTER INSERT ON messages BEGIN
                    INSERT INTO messages_fts(message_id, session_id, content, role, created_at)
                    VALUES (new.Id, new.SessionId, new.Content, new.Role, new.CreatedAt);
                END;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER messages_ad AFTER DELETE ON messages BEGIN
                    DELETE FROM messages_fts WHERE message_id = old.Id;
                END;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER messages_au AFTER UPDATE ON messages BEGIN
                    DELETE FROM messages_fts WHERE message_id = old.Id;
                    INSERT INTO messages_fts(message_id, session_id, content, role, created_at)
                    VALUES (new.Id, new.SessionId, new.Content, new.Role, new.CreatedAt);
                END;
            ");

            // 4. 初始化 FTS 数据（从现有消息）
            migrationBuilder.Sql(@"
                INSERT INTO messages_fts(message_id, session_id, content, role, created_at)
                SELECT Id, SessionId, Content, Role, CreatedAt FROM messages;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 删除 session_tags 表
            migrationBuilder.DropTable(
                name: "session_tags");

            // 删除 FTS5 触发器
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS messages_ai;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS messages_ad;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS messages_au;");

            // 删除 FTS5 虚拟表
            migrationBuilder.Sql("DROP TABLE IF EXISTS messages_fts;");
        }
    }
}
