-- Orphan check for the foreign keys of the marketplace schema (generated from the EF model; do not edit by hand).
-- Run this on a database BEFORE applying the migration that adds the foreign keys. It only reads.
-- Every row with orphans > 0 will make the migration fail. For each one, either delete the orphan child rows or insert the missing parent,
-- then run this again until it returns no rows. A fresh database has nothing to fix.
-- Example: mysql -h <host> -u <user> -p <database> < docs/FK_ORPHAN_CHECK.sql

SELECT relation, orphans FROM (
  SELECT 'BusinessAddresses.BusinessId -> Businesses.Id' AS relation, COUNT(*) AS orphans FROM `BusinessAddresses` c LEFT JOIN `Businesses` p ON p.`Id` = c.`BusinessId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'BusinessAddresses.CityId -> Cities.Id' AS relation, COUNT(*) AS orphans FROM `BusinessAddresses` c LEFT JOIN `Cities` p ON p.`Id` = c.`CityId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'BusinessDocuments.BusinessId -> Businesses.Id' AS relation, COUNT(*) AS orphans FROM `BusinessDocuments` c LEFT JOIN `Businesses` p ON p.`Id` = c.`BusinessId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'BusinessUsers.BusinessId -> Businesses.Id' AS relation, COUNT(*) AS orphans FROM `BusinessUsers` c LEFT JOIN `Businesses` p ON p.`Id` = c.`BusinessId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'CartItems.VariantId -> ProductVariants.Id' AS relation, COUNT(*) AS orphans FROM `CartItems` c LEFT JOIN `ProductVariants` p ON p.`Id` = c.`VariantId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'Cities.StateId -> States.Id' AS relation, COUNT(*) AS orphans FROM `Cities` c LEFT JOIN `States` p ON p.`Id` = c.`StateId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'DeliveryZoneRates.FromZoneId -> DeliveryZones.Id' AS relation, COUNT(*) AS orphans FROM `DeliveryZoneRates` c LEFT JOIN `DeliveryZones` p ON p.`Id` = c.`FromZoneId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'DeliveryZoneRates.ToZoneId -> DeliveryZones.Id' AS relation, COUNT(*) AS orphans FROM `DeliveryZoneRates` c LEFT JOIN `DeliveryZones` p ON p.`Id` = c.`ToZoneId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'Favourites.ProductId -> Products.Id' AS relation, COUNT(*) AS orphans FROM `Favourites` c LEFT JOIN `Products` p ON p.`Id` = c.`ProductId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'Favourites.RetailerBusinessId -> Businesses.Id' AS relation, COUNT(*) AS orphans FROM `Favourites` c LEFT JOIN `Businesses` p ON p.`Id` = c.`RetailerBusinessId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'InvoiceSequences.SellerBusinessId -> Businesses.Id' AS relation, COUNT(*) AS orphans FROM `InvoiceSequences` c LEFT JOIN `Businesses` p ON p.`Id` = c.`SellerBusinessId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'Invoices.SellerOrderId -> SellerOrders.Id' AS relation, COUNT(*) AS orphans FROM `Invoices` c LEFT JOIN `SellerOrders` p ON p.`Id` = c.`SellerOrderId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'OrderItems.ProductId -> Products.Id' AS relation, COUNT(*) AS orphans FROM `OrderItems` c LEFT JOIN `Products` p ON p.`Id` = c.`ProductId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'OrderItems.SellerOrderId -> SellerOrders.Id' AS relation, COUNT(*) AS orphans FROM `OrderItems` c LEFT JOIN `SellerOrders` p ON p.`Id` = c.`SellerOrderId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'OrderItems.VariantId -> ProductVariants.Id' AS relation, COUNT(*) AS orphans FROM `OrderItems` c LEFT JOIN `ProductVariants` p ON p.`Id` = c.`VariantId` WHERE p.`Id` IS NULL AND c.`VariantId` IS NOT NULL
  UNION ALL
  SELECT 'OrderStatusHistories.SellerOrderId -> SellerOrders.Id' AS relation, COUNT(*) AS orphans FROM `OrderStatusHistories` c LEFT JOIN `SellerOrders` p ON p.`Id` = c.`SellerOrderId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'Orders.RetailerBusinessId -> Businesses.Id' AS relation, COUNT(*) AS orphans FROM `Orders` c LEFT JOIN `Businesses` p ON p.`Id` = c.`RetailerBusinessId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'Payments.OrderId -> Orders.Id' AS relation, COUNT(*) AS orphans FROM `Payments` c LEFT JOIN `Orders` p ON p.`Id` = c.`OrderId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'PayoutAccounts.BusinessId -> Businesses.Id' AS relation, COUNT(*) AS orphans FROM `PayoutAccounts` c LEFT JOIN `Businesses` p ON p.`Id` = c.`BusinessId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'PostalCodes.CityId -> Cities.Id' AS relation, COUNT(*) AS orphans FROM `PostalCodes` c LEFT JOIN `Cities` p ON p.`Id` = c.`CityId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'PriceSlabs.ProductId -> Products.Id' AS relation, COUNT(*) AS orphans FROM `PriceSlabs` c LEFT JOIN `Products` p ON p.`Id` = c.`ProductId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'ProductMedia.ProductId -> Products.Id' AS relation, COUNT(*) AS orphans FROM `ProductMedia` c LEFT JOIN `Products` p ON p.`Id` = c.`ProductId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'ProductVariants.ProductId -> Products.Id' AS relation, COUNT(*) AS orphans FROM `ProductVariants` c LEFT JOIN `Products` p ON p.`Id` = c.`ProductId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'Products.CategoryId -> Categories.Id' AS relation, COUNT(*) AS orphans FROM `Products` c LEFT JOIN `Categories` p ON p.`Id` = c.`CategoryId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'Products.SellerBusinessId -> Businesses.Id' AS relation, COUNT(*) AS orphans FROM `Products` c LEFT JOIN `Businesses` p ON p.`Id` = c.`SellerBusinessId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'Refunds.PaymentId -> Payments.Id' AS relation, COUNT(*) AS orphans FROM `Refunds` c LEFT JOIN `Payments` p ON p.`Id` = c.`PaymentId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'Refunds.SellerOrderId -> SellerOrders.Id' AS relation, COUNT(*) AS orphans FROM `Refunds` c LEFT JOIN `SellerOrders` p ON p.`Id` = c.`SellerOrderId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'ReturnClaimPhotos.ReturnClaimId -> ReturnClaims.Id' AS relation, COUNT(*) AS orphans FROM `ReturnClaimPhotos` c LEFT JOIN `ReturnClaims` p ON p.`Id` = c.`ReturnClaimId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'ReturnClaims.SellerOrderId -> SellerOrders.Id' AS relation, COUNT(*) AS orphans FROM `ReturnClaims` c LEFT JOIN `SellerOrders` p ON p.`Id` = c.`SellerOrderId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'SellerOrders.ManufacturerBusinessId -> Businesses.Id' AS relation, COUNT(*) AS orphans FROM `SellerOrders` c LEFT JOIN `Businesses` p ON p.`Id` = c.`ManufacturerBusinessId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'SellerOrders.OrderId -> Orders.Id' AS relation, COUNT(*) AS orphans FROM `SellerOrders` c LEFT JOIN `Orders` p ON p.`Id` = c.`OrderId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'Settlements.PaymentId -> Payments.Id' AS relation, COUNT(*) AS orphans FROM `Settlements` c LEFT JOIN `Payments` p ON p.`Id` = c.`PaymentId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'Settlements.SellerOrderId -> SellerOrders.Id' AS relation, COUNT(*) AS orphans FROM `Settlements` c LEFT JOIN `SellerOrders` p ON p.`Id` = c.`SellerOrderId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'Shipments.SellerOrderId -> SellerOrders.Id' AS relation, COUNT(*) AS orphans FROM `Shipments` c LEFT JOIN `SellerOrders` p ON p.`Id` = c.`SellerOrderId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'ShippingRates.FromCityId -> Cities.Id' AS relation, COUNT(*) AS orphans FROM `ShippingRates` c LEFT JOIN `Cities` p ON p.`Id` = c.`FromCityId` WHERE p.`Id` IS NULL
  UNION ALL
  SELECT 'ShippingRates.ToCityId -> Cities.Id' AS relation, COUNT(*) AS orphans FROM `ShippingRates` c LEFT JOIN `Cities` p ON p.`Id` = c.`ToCityId` WHERE p.`Id` IS NULL
) checks WHERE orphans > 0 ORDER BY relation;
