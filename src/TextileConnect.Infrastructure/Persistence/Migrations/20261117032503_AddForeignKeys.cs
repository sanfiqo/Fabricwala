using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TextileConnect.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_SellerOrders_SellerOrderId",
                table: "OrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderStatusHistories_SellerOrders_SellerOrderId",
                table: "OrderStatusHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_ReturnClaimPhotos_ReturnClaims_ReturnClaimId",
                table: "ReturnClaimPhotos");

            migrationBuilder.DropForeignKey(
                name: "FK_ReturnClaims_SellerOrders_SellerOrderId",
                table: "ReturnClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_SellerOrders_Orders_OrderId",
                table: "SellerOrders");

            migrationBuilder.CreateIndex(
                name: "IX_ShippingRates_ToCityId",
                table: "ShippingRates",
                column: "ToCityId");

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_PaymentId",
                table: "Settlements",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_SellerOrderId",
                table: "Settlements",
                column: "SellerOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_PaymentId",
                table: "Refunds",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_SellerBusinessId",
                table: "Products",
                column: "SellerBusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_VariantId",
                table: "OrderItems",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SellerOrderId",
                table: "Invoices",
                column: "SellerOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryZoneRates_ToZoneId",
                table: "DeliveryZoneRates",
                column: "ToZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_VariantId",
                table: "CartItems",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessDocuments_BusinessId",
                table: "BusinessDocuments",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAddresses_CityId",
                table: "BusinessAddresses",
                column: "CityId");

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessAddresses_Businesses_BusinessId",
                table: "BusinessAddresses",
                column: "BusinessId",
                principalTable: "Businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessAddresses_Cities_CityId",
                table: "BusinessAddresses",
                column: "CityId",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessDocuments_Businesses_BusinessId",
                table: "BusinessDocuments",
                column: "BusinessId",
                principalTable: "Businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessUsers_Businesses_BusinessId",
                table: "BusinessUsers",
                column: "BusinessId",
                principalTable: "Businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_ProductVariants_VariantId",
                table: "CartItems",
                column: "VariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cities_States_StateId",
                table: "Cities",
                column: "StateId",
                principalTable: "States",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryZoneRates_DeliveryZones_FromZoneId",
                table: "DeliveryZoneRates",
                column: "FromZoneId",
                principalTable: "DeliveryZones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryZoneRates_DeliveryZones_ToZoneId",
                table: "DeliveryZoneRates",
                column: "ToZoneId",
                principalTable: "DeliveryZones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_SellerOrders_SellerOrderId",
                table: "Invoices",
                column: "SellerOrderId",
                principalTable: "SellerOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_ProductVariants_VariantId",
                table: "OrderItems",
                column: "VariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_SellerOrders_SellerOrderId",
                table: "OrderItems",
                column: "SellerOrderId",
                principalTable: "SellerOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Businesses_RetailerBusinessId",
                table: "Orders",
                column: "RetailerBusinessId",
                principalTable: "Businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderStatusHistories_SellerOrders_SellerOrderId",
                table: "OrderStatusHistories",
                column: "SellerOrderId",
                principalTable: "SellerOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Orders_OrderId",
                table: "Payments",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayoutAccounts_Businesses_BusinessId",
                table: "PayoutAccounts",
                column: "BusinessId",
                principalTable: "Businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PriceSlabs_Products_ProductId",
                table: "PriceSlabs",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductMedia_Products_ProductId",
                table: "ProductMedia",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Businesses_SellerBusinessId",
                table: "Products",
                column: "SellerBusinessId",
                principalTable: "Businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_Products_ProductId",
                table: "ProductVariants",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Refunds_Payments_PaymentId",
                table: "Refunds",
                column: "PaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Refunds_SellerOrders_SellerOrderId",
                table: "Refunds",
                column: "SellerOrderId",
                principalTable: "SellerOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReturnClaimPhotos_ReturnClaims_ReturnClaimId",
                table: "ReturnClaimPhotos",
                column: "ReturnClaimId",
                principalTable: "ReturnClaims",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReturnClaims_SellerOrders_SellerOrderId",
                table: "ReturnClaims",
                column: "SellerOrderId",
                principalTable: "SellerOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SellerOrders_Businesses_ManufacturerBusinessId",
                table: "SellerOrders",
                column: "ManufacturerBusinessId",
                principalTable: "Businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SellerOrders_Orders_OrderId",
                table: "SellerOrders",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Settlements_Payments_PaymentId",
                table: "Settlements",
                column: "PaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Settlements_SellerOrders_SellerOrderId",
                table: "Settlements",
                column: "SellerOrderId",
                principalTable: "SellerOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Shipments_SellerOrders_SellerOrderId",
                table: "Shipments",
                column: "SellerOrderId",
                principalTable: "SellerOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShippingRates_Cities_FromCityId",
                table: "ShippingRates",
                column: "FromCityId",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShippingRates_Cities_ToCityId",
                table: "ShippingRates",
                column: "ToCityId",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusinessAddresses_Businesses_BusinessId",
                table: "BusinessAddresses");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessAddresses_Cities_CityId",
                table: "BusinessAddresses");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessDocuments_Businesses_BusinessId",
                table: "BusinessDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessUsers_Businesses_BusinessId",
                table: "BusinessUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_CartItems_ProductVariants_VariantId",
                table: "CartItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Cities_States_StateId",
                table: "Cities");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryZoneRates_DeliveryZones_FromZoneId",
                table: "DeliveryZoneRates");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryZoneRates_DeliveryZones_ToZoneId",
                table: "DeliveryZoneRates");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_SellerOrders_SellerOrderId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_ProductVariants_VariantId",
                table: "OrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_SellerOrders_SellerOrderId",
                table: "OrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Businesses_RetailerBusinessId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderStatusHistories_SellerOrders_SellerOrderId",
                table: "OrderStatusHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Orders_OrderId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_PayoutAccounts_Businesses_BusinessId",
                table: "PayoutAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_PriceSlabs_Products_ProductId",
                table: "PriceSlabs");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductMedia_Products_ProductId",
                table: "ProductMedia");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Businesses_SellerBusinessId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariants_Products_ProductId",
                table: "ProductVariants");

            migrationBuilder.DropForeignKey(
                name: "FK_Refunds_Payments_PaymentId",
                table: "Refunds");

            migrationBuilder.DropForeignKey(
                name: "FK_Refunds_SellerOrders_SellerOrderId",
                table: "Refunds");

            migrationBuilder.DropForeignKey(
                name: "FK_ReturnClaimPhotos_ReturnClaims_ReturnClaimId",
                table: "ReturnClaimPhotos");

            migrationBuilder.DropForeignKey(
                name: "FK_ReturnClaims_SellerOrders_SellerOrderId",
                table: "ReturnClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_SellerOrders_Businesses_ManufacturerBusinessId",
                table: "SellerOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_SellerOrders_Orders_OrderId",
                table: "SellerOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_Settlements_Payments_PaymentId",
                table: "Settlements");

            migrationBuilder.DropForeignKey(
                name: "FK_Settlements_SellerOrders_SellerOrderId",
                table: "Settlements");

            migrationBuilder.DropForeignKey(
                name: "FK_Shipments_SellerOrders_SellerOrderId",
                table: "Shipments");

            migrationBuilder.DropForeignKey(
                name: "FK_ShippingRates_Cities_FromCityId",
                table: "ShippingRates");

            migrationBuilder.DropForeignKey(
                name: "FK_ShippingRates_Cities_ToCityId",
                table: "ShippingRates");

            migrationBuilder.DropIndex(
                name: "IX_ShippingRates_ToCityId",
                table: "ShippingRates");

            migrationBuilder.DropIndex(
                name: "IX_Settlements_PaymentId",
                table: "Settlements");

            migrationBuilder.DropIndex(
                name: "IX_Settlements_SellerOrderId",
                table: "Settlements");

            migrationBuilder.DropIndex(
                name: "IX_Refunds_PaymentId",
                table: "Refunds");

            migrationBuilder.DropIndex(
                name: "IX_Products_CategoryId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_SellerBusinessId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_VariantId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_SellerOrderId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryZoneRates_ToZoneId",
                table: "DeliveryZoneRates");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_VariantId",
                table: "CartItems");

            migrationBuilder.DropIndex(
                name: "IX_BusinessDocuments_BusinessId",
                table: "BusinessDocuments");

            migrationBuilder.DropIndex(
                name: "IX_BusinessAddresses_CityId",
                table: "BusinessAddresses");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_SellerOrders_SellerOrderId",
                table: "OrderItems",
                column: "SellerOrderId",
                principalTable: "SellerOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderStatusHistories_SellerOrders_SellerOrderId",
                table: "OrderStatusHistories",
                column: "SellerOrderId",
                principalTable: "SellerOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReturnClaimPhotos_ReturnClaims_ReturnClaimId",
                table: "ReturnClaimPhotos",
                column: "ReturnClaimId",
                principalTable: "ReturnClaims",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReturnClaims_SellerOrders_SellerOrderId",
                table: "ReturnClaims",
                column: "SellerOrderId",
                principalTable: "SellerOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SellerOrders_Orders_OrderId",
                table: "SellerOrders",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
