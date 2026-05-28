-- NeoPos: bir şirkət üçün Role + User yaratmaq (PostgreSQL).
--
-- 1) YOUR_COMPANY_ID — öz şirkət Guid-inlə əvəz et.
-- 2) Şifrə: API login kodu PasswordHash ilə birbaşa müqayisə edir — aşağıdakı "PasswordHash"
--    dəyəri Boss-da yazacağın şifrə ilə EYNİ olmalıdır (hash yox).
-- 3) Admin rol: IsAdmin = true, Permissions boş ola bilər.
-- 4) Adi rol: IsAdmin = false, Permissions massivində icazə int-ləri (Domain.Enums.Permission).

-- === PARAMETRLƏR (dəyiş) ===
-- Şirkət:
-- YOUR_COMPANY_ID

-- A) Yalnız ROL: aşağıdakı INSERT-i tək işlət, RETURNING-dən çıxan "Id"-ni sonra User-də RoleId kimi yaz.
/*
INSERT INTO "Roles" (...)
VALUES (...) RETURNING "Id";
*/

-- B) ROL + USER bir sorğuda (aşağıdakı blok — əsas nümunə; faylda yalnız bunu işlət)
WITH new_role AS (
  INSERT INTO "Roles" (
    "Id",
    "CompanyId",
    "NameAz",
    "NameEn",
    "NameRu",
    "IsAdmin",
    "Permissions",
    "CreatedAt",
    "CreatedBy",
    "LastModifiedAt",
    "LastModifiedBy",
    "DeletedAt",
    "DeletedBy",
    "IsDeleted"
  )
  VALUES (
    gen_random_uuid(),
    'YOUR_COMPANY_ID'::uuid,
    'Admin',
    'Admin',
    'Admin',
    true,
    '{}'::integer[],
    NOW(),
    'sql-seed',
    NULL,
    NULL,
    NULL,
    NULL,
    false
  )
  RETURNING "Id", "CompanyId"
)
INSERT INTO "Users" (
  "Id",
  "CompanyId",
  "FullName",
  "Username",
  "PasswordHash",
  "PinCode",
  "IsActive",
  "RoleId",
  "LinkedAccountId",
  "CreatedAt",
  "CreatedBy",
  "LastModifiedAt",
  "LastModifiedBy",
  "DeletedAt",
  "DeletedBy",
  "IsDeleted"
)
SELECT
  gen_random_uuid(),
  nr."CompanyId",
  'Sahib',
  'boss_admin',
  'Sifre123',
  NULL,
  true,
  nr."Id",
  NULL,
  NOW(),
  'sql-seed',
  NULL,
  NULL,
  NULL,
  NULL,
  false
FROM new_role nr
RETURNING "Id", "Username", "CompanyId", "RoleId";

-- C) Adi rol (admin deyil) + bəzi icazələr — nümunə: 1,7,13,20,21 (CreateCheck, ChangeWaiter, CloseCheck, StartCashShift, ViewReports)
/*
WITH new_role AS (
  INSERT INTO "Roles" (
    "Id", "CompanyId", "NameAz", "NameEn", "NameRu", "IsAdmin", "Permissions",
    "CreatedAt", "CreatedBy", "LastModifiedAt", "LastModifiedBy", "DeletedAt", "DeletedBy", "IsDeleted"
  )
  VALUES (
    gen_random_uuid(),
    'YOUR_COMPANY_ID'::uuid,
    'Kassir',
    'Cashier',
    'Кассир',
    false,
    ARRAY[1, 7, 13, 20, 21]::integer[],
    NOW(), 'sql-seed', NULL, NULL, NULL, NULL, false
  )
  RETURNING "Id", "CompanyId"
)
INSERT INTO "Users" (
  "Id", "CompanyId", "FullName", "Username", "PasswordHash", "PinCode", "IsActive", "RoleId",
  "LinkedAccountId", "CreatedAt", "CreatedBy", "LastModifiedAt", "LastModifiedBy", "DeletedAt", "DeletedBy", "IsDeleted"
)
SELECT
  gen_random_uuid(), nr."CompanyId", 'Kassir 1', 'kassir1', 'Parol456', NULL, true, nr."Id", NULL,
  NOW(), 'sql-seed', NULL, NULL, NULL, NULL, false
FROM new_role nr
RETURNING "Id", "Username", "RoleId";
*/
