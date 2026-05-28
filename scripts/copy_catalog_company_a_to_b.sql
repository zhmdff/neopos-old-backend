-- NeoPos: şirkət A-dan B-yə kataloq köçürməsi (PostgreSQL)
-- Köçürülür: Workshops, Categories (iyerarxiya), Products, ProductVariants,
--             ProductWorkshops, ProductSets, ProductSetItems,
--             ProductSetChoiceGroups, ProductSetChoiceOptions
--
-- "Əməliyyat otağı" / mətbəx sexləri = "Workshops" cədvəli.
-- "KitchenOperations" sifariş tarixçəsidir — bura daxil deyil.
--
-- 1) Zəhmət olmasa əvvəl tam backup götürün.
-- 2) Aşağıdakı src_company, dst_company, created_by dəyərlərini dəyişin.
-- 3) gen_random_uuid üçün: PostgreSQL 13+ və ya:
--    CREATE EXTENSION IF NOT EXISTS pgcrypto;
-- 4) ImageUrl eyni saxlanılır — fayl yolları B mühitində də keçərlidirsə problemdir.
-- 5) Stock/qiymətlər olduğu kimi köçürülür; sıfırlamaq istəsəniz, COMMIT-dən əvvəl UPDATE əlavə edin.

DO $$
DECLARE
  src_company uuid := '00000000-0000-0000-0000-000000000001'; -- ▼ MƏNBƏ şirkət Id
  dst_company uuid := '00000000-0000-0000-0000-000000000002'; -- ▼ HƏDƏF şirkət Id
  created_by  text  := 'sql-catalog-copy';
BEGIN
  CREATE TEMP TABLE _map_workshop (old_id uuid PRIMARY KEY, new_id uuid NOT NULL) ON COMMIT DROP;
  CREATE TEMP TABLE _map_category (old_id uuid PRIMARY KEY, new_id uuid NOT NULL) ON COMMIT DROP;
  CREATE TEMP TABLE _map_product  (old_id uuid PRIMARY KEY, new_id uuid NOT NULL) ON COMMIT DROP;
  CREATE TEMP TABLE _map_pset     (old_id uuid PRIMARY KEY, new_id uuid NOT NULL) ON COMMIT DROP;
  CREATE TEMP TABLE _map_psgrp    (old_id uuid PRIMARY KEY, new_id uuid NOT NULL) ON COMMIT DROP;

  -- Workshops
  INSERT INTO _map_workshop (old_id, new_id)
  SELECT w."Id", gen_random_uuid()
  FROM "Workshops" w
  WHERE w."CompanyId" = src_company AND w."IsDeleted" = false;

  INSERT INTO "Workshops" (
    "Id", "CompanyId", "NameAz", "NameEn", "NameRu",
    "IsPrinting", "PrinterType", "PrinterValue",
    "CreatedAt", "CreatedBy", "IsDeleted"
  )
  SELECT
    m.new_id, dst_company,
    w."NameAz", w."NameEn", w."NameRu",
    w."IsPrinting", w."PrinterType", w."PrinterValue",
    NOW(), created_by, false
  FROM "Workshops" w
  JOIN _map_workshop m ON m.old_id = w."Id";

  -- Categories (ParentCategoryId xəritələnir)
  INSERT INTO _map_category (old_id, new_id)
  SELECT c."Id", gen_random_uuid()
  FROM "Categories" c
  WHERE c."CompanyId" = src_company AND c."IsDeleted" = false;

  INSERT INTO "Categories" (
    "Id", "CompanyId",
    "NameAz", "NameEn", "NameRu",
    "OrderIndex", "OrderIndexByQrMenu", "ImageUrl",
    "ParentCategoryId",
    "CreatedAt", "CreatedBy", "IsDeleted"
  )
  SELECT
    mc.new_id, dst_company,
    c."NameAz", c."NameEn", c."NameRu",
    c."OrderIndex", c."OrderIndexByQrMenu", c."ImageUrl",
    CASE WHEN c."ParentCategoryId" IS NULL THEN NULL ELSE pc.new_id END,
    NOW(), created_by, false
  FROM "Categories" c
  JOIN _map_category mc ON mc.old_id = c."Id"
  LEFT JOIN _map_category pc ON pc.old_id = c."ParentCategoryId"
  WHERE c."CompanyId" = src_company AND c."IsDeleted" = false;

  -- Products
  INSERT INTO _map_product (old_id, new_id)
  SELECT p."Id", gen_random_uuid()
  FROM "Products" p
  WHERE p."CompanyId" = src_company AND p."IsDeleted" = false;

  INSERT INTO "Products" (
    "Id", "CompanyId",
    "NameAz", "NameEn", "NameRu",
    "Barcode", "OrderIndex", "OrderIndexByQrMenu",
    "Unit", "Stock", "CostPrice", "MarkupValue", "MarkupType",
    "SalePrice", "DeliveryPrice", "ImageUrl",
    "ShowInQr", "ShowInTerminal",
    "CategoryId", "WorkshopId", "CookingProcess",
    "CreatedAt", "CreatedBy", "IsDeleted"
  )
  SELECT
    mp.new_id, dst_company,
    p."NameAz", p."NameEn", p."NameRu",
    p."Barcode", p."OrderIndex", p."OrderIndexByQrMenu",
    p."Unit", p."Stock", p."CostPrice", p."MarkupValue", p."MarkupType",
    p."SalePrice", p."DeliveryPrice", p."ImageUrl",
    p."ShowInQr", p."ShowInTerminal",
    cat.new_id, w.new_id, p."CookingProcess",
    NOW(), created_by, false
  FROM "Products" p
  JOIN _map_product mp ON mp.old_id = p."Id"
  JOIN _map_category cat ON cat.old_id = p."CategoryId"
  JOIN _map_workshop w ON w.old_id = p."WorkshopId"
  WHERE p."CompanyId" = src_company AND p."IsDeleted" = false;

  -- Variants
  INSERT INTO "ProductVariants" (
    "Id", "CompanyId",
    "NameAz", "NameEn", "NameRu",
    "Price", "DeliveryPrice", "Barcode", "OrderIndex",
    "ProductId",
    "CreatedAt", "CreatedBy", "IsDeleted"
  )
  SELECT
    gen_random_uuid(), dst_company,
    v."NameAz", v."NameEn", v."NameRu",
    v."Price", v."DeliveryPrice", v."Barcode", v."OrderIndex",
    mp.new_id,
    NOW(), created_by, false
  FROM "ProductVariants" v
  JOIN _map_product mp ON mp.old_id = v."ProductId"
  WHERE v."CompanyId" = src_company AND v."IsDeleted" = false;

  -- Əlavə sexlər
  INSERT INTO "ProductWorkshops" (
    "Id", "CompanyId", "ProductId", "WorkshopId",
    "CreatedAt", "CreatedBy", "IsDeleted"
  )
  SELECT
    gen_random_uuid(), dst_company, mp.new_id, mw.new_id,
    NOW(), created_by, false
  FROM "ProductWorkshops" pw
  JOIN _map_product mp ON mp.old_id = pw."ProductId"
  JOIN _map_workshop mw ON mw.old_id = pw."WorkshopId"
  WHERE pw."CompanyId" = src_company AND pw."IsDeleted" = false;

  -- ProductSets
  INSERT INTO _map_pset (old_id, new_id)
  SELECT ps."Id", gen_random_uuid()
  FROM "ProductSets" ps
  WHERE ps."CompanyId" = src_company AND ps."IsDeleted" = false;

  INSERT INTO "ProductSets" (
    "Id", "CompanyId", "ProductId", "Description",
    "CreatedAt", "CreatedBy", "IsDeleted"
  )
  SELECT m.new_id, dst_company, mp.new_id, ps."Description",
    NOW(), created_by, false
  FROM "ProductSets" ps
  JOIN _map_pset m ON m.old_id = ps."Id"
  JOIN _map_product mp ON mp.old_id = ps."ProductId"
  WHERE ps."CompanyId" = src_company AND ps."IsDeleted" = false;

  INSERT INTO _map_psgrp (old_id, new_id)
  SELECT g."Id", gen_random_uuid()
  FROM "ProductSetChoiceGroups" g
  WHERE g."CompanyId" = src_company AND g."IsDeleted" = false;

  INSERT INTO "ProductSetChoiceGroups" (
    "Id", "CompanyId", "ProductSetId",
    "NameAz", "MinChoices", "MaxChoices", "SortOrder",
    "CreatedAt", "CreatedBy", "IsDeleted"
  )
  SELECT
    mg.new_id, dst_company, mps.new_id,
    g."NameAz", g."MinChoices", g."MaxChoices", g."SortOrder",
    NOW(), created_by, false
  FROM "ProductSetChoiceGroups" g
  JOIN _map_psgrp mg ON mg.old_id = g."Id"
  JOIN _map_pset mps ON mps.old_id = g."ProductSetId"
  WHERE g."CompanyId" = src_company AND g."IsDeleted" = false;

  INSERT INTO "ProductSetChoiceOptions" (
    "Id", "CompanyId", "ProductSetChoiceGroupId", "ProductId",
    "Quantity", "SortOrder",
    "CreatedAt", "CreatedBy", "IsDeleted"
  )
  SELECT
    gen_random_uuid(), dst_company, mg.new_id, mp.new_id,
    o."Quantity", o."SortOrder",
    NOW(), created_by, false
  FROM "ProductSetChoiceOptions" o
  JOIN _map_psgrp mg ON mg.old_id = o."ProductSetChoiceGroupId"
  JOIN _map_product mp ON mp.old_id = o."ProductId"
  WHERE o."CompanyId" = src_company AND o."IsDeleted" = false;

  INSERT INTO "ProductSetItems" (
    "Id", "CompanyId", "ProductSetId", "ProductId",
    "Quantity",
    "CreatedAt", "CreatedBy", "IsDeleted"
  )
  SELECT
    gen_random_uuid(), dst_company, mps.new_id, mp.new_id,
    i."Quantity",
    NOW(), created_by, false
  FROM "ProductSetItems" i
  JOIN _map_pset mps ON mps.old_id = i."ProductSetId"
  JOIN _map_product mp ON mp.old_id = i."ProductId"
  WHERE i."CompanyId" = src_company AND i."IsDeleted" = false;

  RAISE NOTICE 'Kataloq köçürməsi bitdi: % -> %', src_company, dst_company;
END $$;
