using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OrderManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "Name", "Price" },
                values: new object[,]
                {
                    { new Guid("8f3a1b20-0001-4a00-9c00-000000000001"), "Notebook Pro 14", 7499.00m },
                    { new Guid("8f3a1b20-0002-4a00-9c00-000000000002"), "Monitor 27\" 4K", 2890.00m },
                    { new Guid("8f3a1b20-0003-4a00-9c00-000000000003"), "Teclado mecânico", 649.90m },
                    { new Guid("8f3a1b20-0004-4a00-9c00-000000000004"), "Mouse sem fio", 199.90m },
                    { new Guid("8f3a1b20-0005-4a00-9c00-000000000005"), "Headset com microfone", 450.50m },
                    { new Guid("8f3a1b20-0006-4a00-9c00-000000000006"), "SSD NVMe 2TB", 980.00m },
                    { new Guid("8f3a1b20-0007-4a00-9c00-000000000007"), "Webcam Full HD", 320.00m },
                    { new Guid("8f3a1b20-0008-4a00-9c00-000000000008"), "Cadeira ergonômica", 1250.90m },
                    { new Guid("8f3a1b20-0009-4a00-9c00-000000000009"), "Dock USB-C", 699.00m },
                    { new Guid("8f3a1b20-000a-4a00-9c00-00000000000a"), "Impressora laser", 2340.00m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ProductId",
                table: "Orders",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Name",
                table: "Products",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Products_ProductId",
                table: "Orders",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Products_ProductId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ProductId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "Orders");
        }
    }
}
