using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <summary>
    /// The strands were first saved with only their short name (BUSSENT, EDUC, CSS-VGD, ...). This gives each one its full name,
    /// which the first page shows. A strand is only changed while its name is still the short one, so a name the administrator
    /// typed in School setup is never overwritten. The administrator can correct any of these names under Strands.
    /// </summary>
    public partial class FillStrandFullNames : Migration
    {
        // code, name before, full name
        private static readonly (string Code, string Short, string Full)[] Names =
        [
            ("BUSSENT", "BUSSENT", "Business and Entrepreneurship"),
            ("EDUC", "EDUC", "Education"),
            ("ENG", "ENG", "Engineering"),
            ("MED", "MED", "Medical and Health Sciences"),
            ("USP", "USP", "University Studies Preparatory"),
            ("CD1", "CSS-VGD", "Computer System Servicing - Video Game Development"),
            ("HT1", "KO-FBS", "Kitchen Operations - Food and Beverage Services"),
            ("HT2", "FBS-KO", "Food and Beverage Services - Kitchen Operations"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (code, shortName, full) in Names)
            {
                migrationBuilder.Sql($"UPDATE `strands` SET `name` = '{full}' WHERE `id` > 0 AND `code` = '{code}' AND `name` = '{shortName}';");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (code, shortName, full) in Names)
            {
                migrationBuilder.Sql($"UPDATE `strands` SET `name` = '{shortName}' WHERE `id` > 0 AND `code` = '{code}' AND `name` = '{full}';");
            }
        }
    }
}
