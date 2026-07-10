using FluentMigrator;

namespace FanFlow.Infra.Migrations;

[Migration(20260710002)]
public class CreateEventsTable : Migration
{
    public override void Up()
    {
        Create.Table("Events")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("ReleaseId").AsGuid().NotNullable()
            .WithColumn("Type").AsString(30).NotNullable()
            .WithColumn("IpAddress").AsString(64).NotNullable()
            .WithColumn("UserAgent").AsString(500).NotNullable()
            .WithColumn("Referrer").AsString(1000).Nullable()
            .WithColumn("DestinationId").AsString(100).Nullable()
            .WithColumn("DwellTimeMs").AsInt32().Nullable()
            .WithColumn("Country").AsString(10).Nullable()
            .WithColumn("BotScore").AsInt32().Nullable()
            .WithColumn("Classification").AsString(10).Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable();

        Create.Index("IX_Events_ReleaseId")
            .OnTable("Events")
            .OnColumn("ReleaseId");

        // Feeds ListUnclassifiedAsync's poll query (WHERE "BotScore" IS NULL AND "Type" = ...).
        Create.Index("IX_Events_Type_BotScore")
            .OnTable("Events")
            .OnColumn("Type")
            .Ascending()
            .OnColumn("BotScore")
            .Ascending();

        // Feeds CountRecentByIpAsync's request-frequency bot signal.
        Create.Index("IX_Events_IpAddress_CreatedAt")
            .OnTable("Events")
            .OnColumn("IpAddress")
            .Ascending()
            .OnColumn("CreatedAt")
            .Ascending();
    }

    public override void Down()
    {
        Delete.Table("Events");
    }
}
