CREATE TABLE [dbo].[Asset]
(
	[Id] BIGINT NOT NULL PRIMARY KEY,
	[symbol] varchar(20) NOT NULL,
	[precision] INT,
	[deposit_status] varchar(20),
	[withdrawal_status] varchar(20),
	[base_withdrawal_fee] varchar(25),
	[minimum_withdrawal_amount] varchar(20),
)
