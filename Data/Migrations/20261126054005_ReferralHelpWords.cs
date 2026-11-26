using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReferralHelpWords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Round 47: the choices of "What help did you give?" are written as actions. The referrals that were saved with the old words get the new ones (a text typed through Others is not touched).
            migrationBuilder.Sql("UPDATE referrals SET intervention = 'Talk with the student' WHERE intervention = 'Talked with the student' AND id > 0;");
            migrationBuilder.Sql("UPDATE referrals SET intervention = 'Call the parents/guardian' WHERE intervention = 'Called the parents' AND id > 0;");
            migrationBuilder.Sql("UPDATE referrals SET intervention = 'Talk with the teacher' WHERE intervention = 'Talked with the teacher' AND id > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE referrals SET intervention = 'Talked with the student' WHERE intervention = 'Talk with the student' AND id > 0;");
            migrationBuilder.Sql("UPDATE referrals SET intervention = 'Called the parents' WHERE intervention = 'Call the parents/guardian' AND id > 0;");
            migrationBuilder.Sql("UPDATE referrals SET intervention = 'Talked with the teacher' WHERE intervention = 'Talk with the teacher' AND id > 0;");
        }
    }
}
