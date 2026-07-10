using FluentMigrator;

namespace FanFlow.Infra.Migrations;

[Migration(20260710003)]
public class AddFacebookPixelIdToReleases : Migration
{
    public override void Up()
    {
        // NotNullable because every release requires a Facebook Pixel going forward
        // (enforced by ReleaseManager's validation); the default only exists to satisfy
        // the ALTER TABLE against any pre-existing rows.
        Alter.Table("Releases")
            .AddColumn("FacebookPixelId").AsString(50).NotNullable().WithDefaultValue("");
    }

    public override void Down()
    {
        Delete.Column("FacebookPixelId").FromTable("Releases");
    }
}
