using FluentMigrator;

namespace FanFlow.Infra.Migrations;

[Migration(20260711001)]
public class AddArtistNameToReleases : Migration
{
    public override void Up()
    {
        // NotNullable to match the other required authored fields (e.g. Title); the default
        // only exists to satisfy the ALTER TABLE against any pre-existing rows.
        Alter.Table("Releases")
            .AddColumn("ArtistName").AsString(200).NotNullable().WithDefaultValue("");
    }

    public override void Down()
    {
        Delete.Column("ArtistName").FromTable("Releases");
    }
}
