using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DenominateGemBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Деномінація gems ×10 (GDD §8.6): баланс множиться, а різниця лягає в журнал
            // окремою нагородою — інакше сума транзакцій розійшлася б із балансом.
            migrationBuilder.Sql(@"
                INSERT INTO ""WalletTransactions"" (""Id"", ""WalletId"", ""Type"", ""Amount"", ""Reference"", ""CreatedAt"")
                SELECT gen_random_uuid(), ""Id"", 4, ""GemBalance"" * 9, 'denomination-x10', now()
                FROM ""PlayerWallets""
                WHERE ""GemBalance"" > 0;

                UPDATE ""PlayerWallets""
                SET ""GemBalance"" = ""GemBalance"" * 10, ""UpdatedAt"" = now()
                WHERE ""GemBalance"" > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""PlayerWallets""
                SET ""GemBalance"" = ""GemBalance"" / 10, ""UpdatedAt"" = now()
                WHERE ""GemBalance"" > 0;

                DELETE FROM ""WalletTransactions"" WHERE ""Reference"" = 'denomination-x10';");
        }
    }
}
