using FluentMigrator;

namespace FanFlow.Infra.Migrations;

[Migration(20260711002)]
public class DropBackgroundImageUrlFromReleases : Migration
{
    public override void Up()
    {
        Delete.Column("BackgroundImageUrl").FromTable("Releases");
    }

    public override void Down()
    {
        Alter.Table("Releases")
            .AddColumn("BackgroundImageUrl").AsString(1000).NotNullable().WithDefaultValue("");
    }
}
