using FluentMigrator;

namespace FanFlow.Infra.Migrations;

[Migration(20260711003)]
public class AddFbpFbcToEvents : Migration
{
    public override void Up()
    {
        Alter.Table("Events")
            .AddColumn("Fbp").AsString(256).Nullable()
            .AddColumn("Fbc").AsString(256).Nullable();
    }

    public override void Down()
    {
        Delete.Column("Fbp").FromTable("Events");
        Delete.Column("Fbc").FromTable("Events");
    }
}
