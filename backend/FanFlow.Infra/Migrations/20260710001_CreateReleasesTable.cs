using FluentMigrator;

namespace FanFlow.Infra.Migrations;

[Migration(20260710001)]
public class CreateReleasesTable : Migration
{
    public override void Up()
    {
        Create.Table("Releases")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("TenantId").AsString(100).NotNullable()
            .WithColumn("Slug").AsString(200).NotNullable()
            .WithColumn("Title").AsString(200).NotNullable()
            .WithColumn("Headline").AsString(300).NotNullable()
            .WithColumn("Description").AsString(2000).NotNullable()
            .WithColumn("CoverImageUrl").AsString(1000).NotNullable()
            .WithColumn("BackgroundImageUrl").AsString(1000).NotNullable()
            .WithColumn("CtaText").AsString(100).NotNullable()
            .WithColumn("LinksJson").AsString(int.MaxValue).NotNullable()
            .WithColumn("Status").AsString(20).NotNullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().NotNullable();

        Create.Index("IX_Releases_TenantId")
            .OnTable("Releases")
            .OnColumn("TenantId");

        // Slugs are looked up globally (unauthenticated tracking requests resolve a release by
        // slug alone, regardless of tenant), so uniqueness is enforced globally, not per-tenant.
        Create.Index("UX_Releases_Slug")
            .OnTable("Releases")
            .OnColumn("Slug")
            .Unique();
    }

    public override void Down()
    {
        Delete.Table("Releases");
    }
}
