USE GTSdb2026;
GO

-- Read-only verification for Phase 4.
SELECT
    g.ID AS GroupID,
    g.Name AS GroupName,
    g.BranchID
FROM dbo.User_Groups AS g
ORDER BY g.ID;
GO

SELECT
    s.ID AS ScreenID,
    s.Screen_Name,
    s.ScreenTypeID,
    s.ScreenNum,
    s.ScreenTypeName,
    s.ISShow
FROM dbo.User_Screens AS s
ORDER BY ISNULL(s.ScreenTypeID, 0), ISNULL(s.ScreenNum, 2147483647), s.ID;
GO

SELECT
    p.GroupID,
    g.Name AS GroupName,
    p.ScreenID,
    s.Screen_Name,
    p.Allow_Branch,
    p.Allow_Enter,
    p.Allow_Save,
    p.Allow_Edit,
    p.Allow_Delete,
    p.Allow_Print,
    p.Allow_Export
FROM dbo.User_Permission AS p
LEFT JOIN dbo.User_Groups AS g ON g.ID = p.GroupID
LEFT JOIN dbo.User_Screens AS s ON s.ID = p.ScreenID
ORDER BY p.GroupID, p.ScreenID;
GO
