IF COL_LENGTH('dbo.conversations', 'owner_only_messages') IS NULL
BEGIN
    ALTER TABLE dbo.conversations
    ADD owner_only_messages bit NOT NULL
        CONSTRAINT DF_conversations_owner_only_messages DEFAULT ((0));
END
GO
