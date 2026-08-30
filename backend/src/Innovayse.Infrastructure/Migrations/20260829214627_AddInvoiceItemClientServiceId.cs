using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Innovayse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceItemClientServiceId : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// <para>
        /// Additive and nullable, on purpose. Every <c>invoice_items</c> row that exists today
        /// keeps a NULL <c>ClientServiceId</c> and <b>no backfill is performed</b>: an invoice
        /// line is a description, a unit price and a quantity, so inferring which service an old
        /// line was charged for would be a guess written into a financial record. The read side
        /// reports how many invoices carry no link rather than pretending the answer is "none".
        /// </para>
        /// <para>
        /// <b>The scaffolder also wanted to add a real <c>xmin</c> column to <c>invoices</c>, and
        /// that statement has been deleted by hand. Do not put it back.</b> <c>xmin</c> is a
        /// PostgreSQL system column that exists on every table; <c>InvoiceConfiguration</c> maps a
        /// shadow property onto it for optimistic concurrency and deliberately needs no DDL.
        /// <c>AddInvoiceGatewaySession</c> introduced that mapping without regenerating the model
        /// snapshot, so EF has believed the column was missing ever since and re-proposes it on
        /// every scaffold. <c>ALTER TABLE invoices ADD COLUMN xmin</c> is rejected outright by
        /// PostgreSQL -- "column name xmin conflicts with a system column name" -- and migrations
        /// are applied at startup in every environment except Testing, so shipping it would have
        /// stopped the API from booting against a live database.
        /// </para>
        /// <para>
        /// The regenerated snapshot now records the shadow property, which is what makes this the
        /// last migration that has to strip it.
        /// </para>
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClientServiceId",
                table: "invoice_items",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_ClientServiceId",
                table: "invoice_items",
                column: "ClientServiceId");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Drops the index and the column and nothing else. The matching <c>xmin</c> drop the
        /// scaffolder produced is gone for the reason given on <c>Up</c> -- it would have tried
        /// to remove a PostgreSQL system column.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invoice_items_ClientServiceId",
                table: "invoice_items");

            migrationBuilder.DropColumn(
                name: "ClientServiceId",
                table: "invoice_items");
        }
    }
}
