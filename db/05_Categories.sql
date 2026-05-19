-- 05_Categories.sql
-- Seed data only (table schema is managed by CreateDatabase.sql)

IF NOT EXISTS (SELECT 1 FROM dbo.Categories)
BEGIN
    INSERT INTO dbo.Categories
    (
        CategoryName,
        IsActive,
        CreatedAt,
        ParentCategoryId,
        DisplayOrder,
        ImageUrl,
        UpdatedAt
    )
    VALUES
    (N'主餐', 1, GETDATE(), NULL, 1, N'主餐.jpg', NULL),
    (N'飲料', 1, GETDATE(), NULL, 2, N'飲料.jpg', NULL),
    (N'甜點', 1, GETDATE(), NULL, 3, N'甜點.jpg', NULL),
    (N'湯品', 1, GETDATE(), NULL, 4, N'湯品.jpg', NULL),
    (N'附餐', 1, GETDATE(), NULL, 5, N'附餐.jpg', NULL);
END
GO
