-- NeoPos: şirkət A-dan kataloq (şöbə, kateqoriya, məhsul, variant, set) şirkət B-yə klon.
-- PostgreSQL. EF cədvəl adları: "Workshops", "Categories", "Products", ...
--
-- !!! Vacib: Aşağıdakıların hamısını birlikdə işlət (faylın sonundakı END $$; daxil).
--     Yalnız "DECLARE" / "company_a uuid" sətirlərini tək çalışdırmaq OLMAZ — "syntax error near uuid" verər.
--
-- 1) B şirkətində eyni kataloq varsa unique/indeks toqquşması ola bilər — tövsiyə: B boş kataloq.
-- 2) BEGIN-dən dərhal sonra company_a / company_b Guid-lərini özünə görə dəyiş.
-- 3) İstəsən əvvəlinə BEGIN; sonuna COMMIT; əlavə edib transaction edə bilərsən.

DO $clone$
DECLARE
  company_a uuid := '6157fb73-fe84-4e31-b726-56f4bf678758'::uuid; -- MƏNBƏ — öz Guid-inlə əvəz et
  company_b uuid := 'ca8da7e5-5228-4c01-89cb-d3166837267c'::uuid; -- HEDƏF — öz Guid-inlə əvəz et
  v_created_by text := 'catalog-clone';
BEGIN
  -- Köhnə workshop -> yeni Id
  CREATE TEMP TABLE _wmap (old_id uuid PRIMARY KEY, new_id uuid NOT NULL) ON COMMIT DROP;
  INSERT INTO _wmap (old_id, new_id)
  SELECT w."Id", gen_random_uuid()
  FROM "Workshops" w
  WHERE w."CompanyId" = company_a AND NOT w."IsDeleted";

  INSERT INTO "Workshops" (
    "Id", "CompanyId", "NameAz", "NameEn", "NameRu", "IsPrinting",
    "PrinterType", "PrinterValue", "CreatedAt", "CreatedBy",
    "LastModifiedAt", "LastModifiedBy", "DeletedAt", "DeletedBy", "IsDeleted"
  )
  SELECT
    m.new_id, company_b, w."NameAz", w."NameEn", w."NameRu", w."IsPrinting",
    w."PrinterType", w."PrinterValue", w."CreatedAt", v_created_by,
    w."LastModifiedAt", w."LastModifiedBy", w."DeletedAt", w."DeletedBy", w."IsDeleted"
  FROM "Workshops" w
  JOIN _wmap m ON m.old_id = w."Id";

  -- Kateqoriyalar: əvvəlcə köhnə->yeni id cütləri
  CREATE TEMP TABLE _cmap (old_id uuid PRIMARY KEY, new_id uuid NOT NULL) ON COMMIT DROP;
  INSERT INTO _cmap (old_id, new_id)
  SELECT c."Id", gen_random_uuid()
  FROM "Categories" c
  WHERE c."CompanyId" = company_a AND NOT c."IsDeleted";

  -- Dərinlik: valideynlər əvvəl insert üçün (bir INSERT içində sıra vacibdir)
  CREATE TEMP TABLE _cdepth ("Id" uuid PRIMARY KEY, depth int NOT NULL) ON COMMIT DROP;
  WITH RECURSIVE tree AS (
    SELECT c."Id", 0 AS depth
    FROM "Categories" c
    WHERE c."CompanyId" = company_a AND NOT c."IsDeleted" AND c."ParentCategoryId" IS NULL
    UNION ALL
    SELECT c."Id", t.depth + 1
    FROM "Categories" c
    INNER JOIN tree t ON c."ParentCategoryId" = t."Id"
    WHERE c."CompanyId" = company_a AND NOT c."IsDeleted"
  )
  INSERT INTO _cdepth ("Id", depth)
  SELECT "Id", MAX(depth) FROM tree GROUP BY "Id";

  INSERT INTO "Categories" (
    "Id", "CompanyId", "NameAz", "NameEn", "NameRu", "OrderIndex", "OrderIndexByQrMenu",
    "ImageUrl", "ParentCategoryId", "CreatedAt", "CreatedBy",
    "LastModifiedAt", "LastModifiedBy", "DeletedAt", "DeletedBy", "IsDeleted"
  )
  SELECT
    cm.new_id, company_b, c."NameAz", c."NameEn", c."NameRu", c."OrderIndex", c."OrderIndexByQrMenu",
    c."ImageUrl",
    CASE WHEN c."ParentCategoryId" IS NULL THEN NULL ELSE pcm.new_id END,
    c."CreatedAt", v_created_by,
    c."LastModifiedAt", c."LastModifiedBy", c."DeletedAt", c."DeletedBy", c."IsDeleted"
  FROM "Categories" c
  JOIN _cmap cm ON cm.old_id = c."Id"
  LEFT JOIN _cmap pcm ON pcm.old_id = c."ParentCategoryId"
  JOIN _cdepth d ON d."Id" = c."Id"
  WHERE c."CompanyId" = company_a AND NOT c."IsDeleted"
  ORDER BY d.depth, c."OrderIndex", c."Id";

  -- Məhsullar
  CREATE TEMP TABLE _pmap (old_id uuid PRIMARY KEY, new_id uuid NOT NULL) ON COMMIT DROP;
  INSERT INTO _pmap (old_id, new_id)
  SELECT p."Id", gen_random_uuid()
  FROM "Products" p
  WHERE p."CompanyId" = company_a AND NOT p."IsDeleted";

  INSERT INTO "Products" (
    "Id", "CompanyId", "NameAz", "NameEn", "NameRu", "Barcode", "OrderIndex", "OrderIndexByQrMenu",
    "Unit", "Stock", "CostPrice", "MarkupValue", "MarkupType", "SalePrice", "DeliveryPrice",
    "ImageUrl", "CategoryId", "WorkshopId", "CookingProcess",
    "CreatedAt", "CreatedBy", "LastModifiedAt", "LastModifiedBy", "DeletedAt", "DeletedBy", "IsDeleted"
  )
  SELECT
    pm.new_id, company_b, p."NameAz", p."NameEn", p."NameRu", p."Barcode", p."OrderIndex", p."OrderIndexByQrMenu",
    p."Unit", p."Stock", p."CostPrice", p."MarkupValue", p."MarkupType", p."SalePrice", p."DeliveryPrice",
    p."ImageUrl", catm.new_id, wm.new_id, p."CookingProcess",
    p."CreatedAt", v_created_by,
    p."LastModifiedAt", p."LastModifiedBy", p."DeletedAt", p."DeletedBy", p."IsDeleted"
  FROM "Products" p
  JOIN _pmap pm ON pm.old_id = p."Id"
  JOIN _cmap catm ON catm.old_id = p."CategoryId"
  JOIN _wmap wm ON wm.old_id = p."WorkshopId"
  WHERE p."CompanyId" = company_a AND NOT p."IsDeleted";

  -- Variantlar
  INSERT INTO "ProductVariants" (
    "Id", "CompanyId", "NameAz", "NameEn", "NameRu", "Price", "Barcode", "OrderIndex",
    "ProductId", "CreatedAt", "CreatedBy", "LastModifiedAt", "LastModifiedBy", "DeletedAt", "DeletedBy", "IsDeleted"
  )
  SELECT
    gen_random_uuid(), company_b, v."NameAz", v."NameEn", v."NameRu", v."Price", v."Barcode", v."OrderIndex",
    pm.new_id,
    v."CreatedAt", v_created_by,
    v."LastModifiedAt", v."LastModifiedBy", v."DeletedAt", v."DeletedBy", v."IsDeleted"
  FROM "ProductVariants" v
  JOIN _pmap pm ON pm.old_id = v."ProductId"
  WHERE v."CompanyId" = company_a AND NOT v."IsDeleted";

  -- ProductSets (ProductId üzrə unique)
  CREATE TEMP TABLE _smap (old_id uuid PRIMARY KEY, new_id uuid NOT NULL) ON COMMIT DROP;
  INSERT INTO _smap (old_id, new_id)
  SELECT s."Id", gen_random_uuid()
  FROM "ProductSets" s
  WHERE s."CompanyId" = company_a AND NOT s."IsDeleted";

  INSERT INTO "ProductSets" (
    "Id", "CompanyId", "ProductId", "Description",
    "CreatedAt", "CreatedBy", "LastModifiedAt", "LastModifiedBy", "DeletedAt", "DeletedBy", "IsDeleted"
  )
  SELECT
    sm.new_id, company_b, pm.new_id, s."Description",
    s."CreatedAt", v_created_by,
    s."LastModifiedAt", s."LastModifiedBy", s."DeletedAt", s."DeletedBy", s."IsDeleted"
  FROM "ProductSets" s
  JOIN _smap sm ON sm.old_id = s."Id"
  JOIN _pmap pm ON pm.old_id = s."ProductId"
  WHERE s."CompanyId" = company_a AND NOT s."IsDeleted";

  CREATE TEMP TABLE _gmap (old_id uuid PRIMARY KEY, new_id uuid NOT NULL) ON COMMIT DROP;
  INSERT INTO _gmap (old_id, new_id)
  SELECT g."Id", gen_random_uuid()
  FROM "ProductSetChoiceGroups" g
  WHERE g."CompanyId" = company_a AND NOT g."IsDeleted";

  INSERT INTO "ProductSetChoiceGroups" (
    "Id", "CompanyId", "ProductSetId", "NameAz", "MinChoices", "MaxChoices", "SortOrder",
    "CreatedAt", "CreatedBy", "LastModifiedAt", "LastModifiedBy", "DeletedAt", "DeletedBy", "IsDeleted"
  )
  SELECT
    gm.new_id, company_b, sm.new_id, g."NameAz", g."MinChoices", g."MaxChoices", g."SortOrder",
    g."CreatedAt", v_created_by,
    g."LastModifiedAt", g."LastModifiedBy", g."DeletedAt", g."DeletedBy", g."IsDeleted"
  FROM "ProductSetChoiceGroups" g
  JOIN _gmap gm ON gm.old_id = g."Id"
  JOIN _smap sm ON sm.old_id = g."ProductSetId"
  WHERE g."CompanyId" = company_a AND NOT g."IsDeleted";

  INSERT INTO "ProductSetChoiceOptions" (
    "Id", "CompanyId", "ProductSetChoiceGroupId", "ProductId", "Quantity", "SortOrder",
    "CreatedAt", "CreatedBy", "LastModifiedAt", "LastModifiedBy", "DeletedAt", "DeletedBy", "IsDeleted"
  )
  SELECT
    gen_random_uuid(), company_b, gm.new_id, pm.new_id, o."Quantity", o."SortOrder",
    o."CreatedAt", v_created_by,
    o."LastModifiedAt", o."LastModifiedBy", o."DeletedAt", o."DeletedBy", o."IsDeleted"
  FROM "ProductSetChoiceOptions" o
  JOIN _gmap gm ON gm.old_id = o."ProductSetChoiceGroupId"
  JOIN _pmap pm ON pm.old_id = o."ProductId"
  WHERE o."CompanyId" = company_a AND NOT o."IsDeleted";

  INSERT INTO "ProductSetItems" (
    "Id", "CompanyId", "ProductSetId", "ProductId", "Quantity",
    "CreatedAt", "CreatedBy", "LastModifiedAt", "LastModifiedBy", "DeletedAt", "DeletedBy", "IsDeleted"
  )
  SELECT
    gen_random_uuid(), company_b, sm.new_id, pm.new_id, i."Quantity",
    i."CreatedAt", v_created_by,
    i."LastModifiedAt", i."LastModifiedBy", i."DeletedAt", i."DeletedBy", i."IsDeleted"
  FROM "ProductSetItems" i
  JOIN _smap sm ON sm.old_id = i."ProductSetId"
  JOIN _pmap pm ON pm.old_id = i."ProductId"
  WHERE i."CompanyId" = company_a AND NOT i."IsDeleted";

  RAISE NOTICE 'Katalog klonu bitdi: % -> %', company_a, company_b;
END $clone$;
