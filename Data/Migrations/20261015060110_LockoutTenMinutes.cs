using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class LockoutTenMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // An account is locked for 10 minutes (it was 15) after too many wrong passwords. A database that kept the old value in System Settings gets the new one;
            // a number the administrator chose (anything but 15) is left alone.
            migrationBuilder.Sql("UPDATE `system_settings` SET `value` = '10' WHERE `key` = 'signin.lockout_minutes' AND `value` = '15';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE `system_settings` SET `value` = '15' WHERE `key` = 'signin.lockout_minutes' AND `value` = '10';");
        }
    }
}
