using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReferralWording : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Round 44: the choices of "What happened" in the referrals are written with better grammar. The referrals that were saved with the old words get the new ones (a text typed through Others is not touched).
            migrationBuilder.Sql("UPDATE referrals SET detail = 'Absent for many days' WHERE detail = 'Absent many days' AND id > 0;");
            migrationBuilder.Sql("UPDATE referrals SET detail = 'Often late to class' WHERE detail = 'Often late' AND id > 0;");
            migrationBuilder.Sql("UPDATE referrals SET detail = 'Skipping classes' WHERE detail = 'Skips classes' AND id > 0;");
            migrationBuilder.Sql("UPDATE referrals SET detail = 'Absent without a valid reason' WHERE detail = 'Absent without a reason' AND id > 0;");
            migrationBuilder.Sql("UPDATE referrals SET detail = 'Disrespect toward a teacher' WHERE detail = 'Disrespect to a teacher' AND id > 0;");
            migrationBuilder.Sql("UPDATE referrals SET detail = 'Not following the uniform or ID rules' WHERE detail = 'Not following the uniform or ID rule' AND id > 0;");
            migrationBuilder.Sql("UPDATE referrals SET detail = 'Damaging school property' WHERE detail = 'Damage to school property' AND id > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE referrals SET detail = 'Absent many days' WHERE detail = 'Absent for many days' AND id > 0;");
            migrationBuilder.Sql("UPDATE referrals SET detail = 'Often late' WHERE detail = 'Often late to class' AND id > 0;");
            migrationBuilder.Sql("UPDATE referrals SET detail = 'Skips classes' WHERE detail = 'Skipping classes' AND id > 0;");
            migrationBuilder.Sql("UPDATE referrals SET detail = 'Absent without a reason' WHERE detail = 'Absent without a valid reason' AND id > 0;");
            migrationBuilder.Sql("UPDATE referrals SET detail = 'Disrespect to a teacher' WHERE detail = 'Disrespect toward a teacher' AND id > 0;");
            migrationBuilder.Sql("UPDATE referrals SET detail = 'Not following the uniform or ID rule' WHERE detail = 'Not following the uniform or ID rules' AND id > 0;");
            migrationBuilder.Sql("UPDATE referrals SET detail = 'Damage to school property' WHERE detail = 'Damaging school property' AND id > 0;");
        }
    }
}
