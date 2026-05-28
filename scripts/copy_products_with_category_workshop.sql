-- Bir şirkətdən digərinə: Workshops + Categories + Products (+ variantlar, əlavə sexlər)
-- Tək blok — ayırmadan işlədin. Əvvəl BACKUP; uuid-ləri dəyişin.

DO $$
DECLARE
  src uuid := '00000000-0000-0000-0000-000000000001'; -- mənbə CompanyId
  dst uuid := '00000000-0000-0000-0000-000000000002'; -- hədəf CompanyId
  v_created_by text := 'sql-copy-products';
BEGIN
  -- Köhnə PostgreSQL: CREATE EXTENSION IF NOT EXISTS pgcrypto;
  CREATE TEMP TABLE _mw (old_id uuid PRIMARY KEY, new_id uuid NOT NULL) ON COMMIT DROP;
  CREATE TEMP TABLE _mc (old_id uuid PRIMARY KEY, new_id uuid NOT NULL) ON COMMIT DROP;
  CREATE TEMP TABLE _mp (old_id uuid PRIMARY KEY, new_id uuid NOT NULL) ON COMMIT DROP;

  INSERT INTO _mw SELECT w."Id", gen_random_uuid()
  FROM "Workshops" w WHERE w."CompanyId" = src AND NOT w."IsDeleted";

  INSERT INTO "Workshops" ("Id","CompanyId","NameAz","NameEn","NameRu","IsPrinting","PrinterType","PrinterValue","CreatedAt","CreatedBy","IsDeleted")
  SELECT m.new_id, dst, w."NameAz", w."NameEn", w."NameRu", w."IsPrinting", w."PrinterType", w."PrinterValue", NOW(), v_created_by, false
  FROM "Workshops" w JOIN _mw m ON m.old_id = w."Id";

  INSERT INTO _mc SELECT c."Id", gen_random_uuid()
  FROM "Categories" c WHERE c."CompanyId" = src AND NOT c."IsDeleted";

  INSERT INTO "Categories" ("Id","CompanyId","NameAz","NameEn","NameRu","OrderIndex","OrderIndexByQrMenu","ImageUrl","ParentCategoryId","CreatedAt","CreatedBy","IsDeleted")
  SELECT mc.new_id, dst, c."NameAz", c."NameEn", c."NameRu", c."OrderIndex", c."OrderIndexByQrMenu", c."ImageUrl",
         CASE WHEN c."ParentCategoryId" IS NULL THEN NULL ELSE p.new_id END, NOW(), v_created_by, false
  FROM "Categories" c
  JOIN _mc mc ON mc.old_id = c."Id"
  LEFT JOIN _mc p ON p.old_id = c."ParentCategoryId"
  WHERE c."CompanyId" = src AND NOT c."IsDeleted";

  INSERT INTO _mp SELECT p."Id", gen_random_uuid()
  FROM "Products" p WHERE p."CompanyId" = src AND NOT p."IsDeleted";

  INSERT INTO "Products" ("Id","CompanyId","NameAz","NameEn","NameRu","Barcode","OrderIndex","OrderIndexByQrMenu","Unit","Stock","CostPrice","MarkupValue","MarkupType","SalePrice","DeliveryPrice","ImageUrl","ShowInQr","ShowInTerminal","CategoryId","WorkshopId","CookingProcess","CreatedAt","CreatedBy","IsDeleted")
  SELECT mp.new_id, dst, p."NameAz", p."NameEn", p."NameRu", p."Barcode", p."OrderIndex", p."OrderIndexByQrMenu", p."Unit", p."Stock", p."CostPrice", p."MarkupValue", p."MarkupType", p."SalePrice", p."DeliveryPrice", p."ImageUrl", p."ShowInQr", p."ShowInTerminal", c.new_id, w.new_id, p."CookingProcess", NOW(), v_created_by, false
  FROM "Products" p
  JOIN _mp mp ON mp.old_id = p."Id"
  JOIN _mc c ON c.old_id = p."CategoryId"
  JOIN _mw w ON w.old_id = p."WorkshopId"
  WHERE p."CompanyId" = src AND NOT p."IsDeleted";

  INSERT INTO "ProductVariants" ("Id","CompanyId","NameAz","NameEn","NameRu","Price","DeliveryPrice","Barcode","OrderIndex","ProductId","CreatedAt","CreatedBy","IsDeleted")
  SELECT gen_random_uuid(), dst, v."NameAz", v."NameEn", v."NameRu", v."Price", v."DeliveryPrice", v."Barcode", v."OrderIndex", mp.new_id, NOW(), v_created_by, false
  FROM "ProductVariants" v JOIN _mp mp ON mp.old_id = v."ProductId"
  WHERE v."CompanyId" = src AND NOT v."IsDeleted";

  INSERT INTO "ProductWorkshops" ("Id","CompanyId","ProductId","WorkshopId","CreatedAt","CreatedBy","IsDeleted")
  SELECT gen_random_uuid(), dst, mp.new_id, mw.new_id, NOW(), v_created_by, false
  FROM "ProductWorkshops" x JOIN _mp mp ON mp.old_id = x."ProductId" JOIN _mw mw ON mw.old_id = x."WorkshopId"
  WHERE x."CompanyId" = src AND NOT x."IsDeleted";

  RAISE NOTICE 'Hazır: % -> %', src, dst;
END $$;
