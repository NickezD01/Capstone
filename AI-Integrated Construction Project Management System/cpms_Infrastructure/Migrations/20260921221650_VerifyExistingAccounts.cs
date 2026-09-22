using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cpms_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VerifyExistingAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Email verification is retired: accounts are created verified by an
            // administrator. Backfill legacy unverified accounts that are not
            // locked out (locked supplier accounts stay locked out).
            migrationBuilder.Sql(@"
UPDATE UserAccounts
SET IsEmailVerified = 1
WHERE ISNULL(IsEmailVerified, 0) = 0
  AND (LockoutEnd IS NULL OR LockoutEnd <= GETUTCDATE());");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
