using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class LockoutFiveMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // An account is locked for 5 minutes after too many wrong passwords (it was 15, then 10). A database that kept one of the old values in System Settings
            // gets the new one; a number the administrator chose (anything else) is left alone.
            migrationBuilder.Sql("UPDATE `system_settings` SET `value` = '5' WHERE `key` = 'signin.lockout_minutes' AND `value` IN ('10', '15');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE `system_settings` SET `value` = '10' WHERE `key` = 'signin.lockout_minutes' AND `value` = '5';");
        }
    }
}
