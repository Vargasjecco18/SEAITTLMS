using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class SchoolNameSeaittLegazpi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The name of the system is SEAITT Legazpi LMS. The short name of the school (System Settings) was "SEAITT"; a database that still has that value
            // gets the new one. A short name the administrator typed (anything else) is left alone.
            migrationBuilder.Sql("UPDATE `system_settings` SET `value` = 'SEAITT Legazpi' WHERE `key` = 'school.short_name' AND `value` = 'SEAITT';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE `system_settings` SET `value` = 'SEAITT' WHERE `key` = 'school.short_name' AND `value` = 'SEAITT Legazpi';");
        }
    }
}
