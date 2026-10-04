using FluentMigrator;

namespace Lingarr.Migrations.Migrations;

[Migration(22)]
public class M0022_SeedLocalAiHeaders : Migration
{
    public override void Up()
    {
        Insert.IntoTable("settings").Row(new
        {
            key = "local_ai_headers",
            value = ""
        });
    }

    public override void Down()
    {
        Delete.FromTable("settings").Row(new
        {
            key = "local_ai_headers"
        });
    }
}
