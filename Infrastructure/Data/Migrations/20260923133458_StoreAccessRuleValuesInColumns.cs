using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class StoreAccessRuleValuesInColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BooleanValue",
                table: "PositionAccessRules",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateValue",
                table: "PositionAccessRules",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DropdownOptionId",
                table: "PositionAccessRules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "NumericValue",
                table: "PositionAccessRules",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodEnd",
                table: "PositionAccessRules",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodStart",
                table: "PositionAccessRules",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StringValue",
                table: "PositionAccessRules",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "PositionAccessRules"
                SET
                    "StringValue" = "Value" ->> 'string',
                    "NumericValue" = NULLIF("Value" ->> 'number', '')::double precision,
                    "DateValue" = NULLIF("Value" ->> 'date', '')::date,
                    "PeriodStart" = NULLIF("Value" #>> '{period,start}', '')::date,
                    "PeriodEnd" = NULLIF("Value" #>> '{period,end}', '')::date,
                    "BooleanValue" = NULLIF("Value" ->> 'boolean', '')::boolean,
                    "DropdownOptionId" = NULLIF("Value" ->> 'optionId', '')::uuid;
                """);

            migrationBuilder.DropColumn(
                name: "Value",
                table: "PositionAccessRules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Value",
                table: "PositionAccessRules",
                type: "jsonb",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "PositionAccessRules"
                SET "Value" = CASE
                    WHEN "StringValue" IS NOT NULL
                        THEN jsonb_build_object('string', "StringValue")
                    WHEN "NumericValue" IS NOT NULL
                        THEN jsonb_build_object('number', "NumericValue")
                    WHEN "DateValue" IS NOT NULL
                        THEN jsonb_build_object('date', "DateValue")
                    WHEN "PeriodStart" IS NOT NULL
                        THEN jsonb_build_object(
                            'period',
                            jsonb_strip_nulls(jsonb_build_object(
                                'start', "PeriodStart",
                                'end', "PeriodEnd")))
                    WHEN "BooleanValue" IS NOT NULL
                        THEN jsonb_build_object('boolean', "BooleanValue")
                    WHEN "DropdownOptionId" IS NOT NULL
                        THEN jsonb_build_object('optionId', "DropdownOptionId")
                    ELSE '{}'::jsonb
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "PositionAccessRules",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "BooleanValue",
                table: "PositionAccessRules");

            migrationBuilder.DropColumn(
                name: "DateValue",
                table: "PositionAccessRules");

            migrationBuilder.DropColumn(
                name: "DropdownOptionId",
                table: "PositionAccessRules");

            migrationBuilder.DropColumn(
                name: "NumericValue",
                table: "PositionAccessRules");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                table: "PositionAccessRules");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                table: "PositionAccessRules");

            migrationBuilder.DropColumn(
                name: "StringValue",
                table: "PositionAccessRules");
        }
    }
}
