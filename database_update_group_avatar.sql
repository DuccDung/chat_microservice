IF COL_LENGTH('dbo.conversations', 'avatar_url') IS NULL
BEGIN
    ALTER TABLE dbo.conversations
    ADD avatar_url varchar(255) NULL;
END
GO
