using FluentMigrator;

namespace FanFlow.Infra.Migrations;

[Migration(20260711004)]
public class AddMetaEventIdToEvents : Migration
{
    public override void Up()
    {
        Alter.Table("Events")
            .AddColumn("MetaEventId").AsString(256).Nullable();
    }

    public override void Down()
    {
        Delete.Column("MetaEventId").FromTable("Events");
    }
}
