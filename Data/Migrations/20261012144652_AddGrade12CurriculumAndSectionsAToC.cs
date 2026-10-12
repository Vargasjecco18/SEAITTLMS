using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <summary>
    /// Two changes to the school's set-up data.
    /// <para>
    /// 1. Grade 12 gets a curriculum. Until now only grade 11 had subjects, so a grade 12 student could not be enrolled with any.
    /// Each strand takes the grade 12 version of its own subjects in the 1st term ("BUSINESS ACCOUNTING 2", ...) and six common
    /// subjects in the 2nd term, the same way grade 11 does. The names and codes are made up here (the school gave none for grade
    /// 12); change them in the subjects table if you like, nothing depends on their text.
    /// </para>
    /// <para>
    /// 2. The sections are A, B and C only. Sections A, B and C are added for grade 11 and grade 12 (for the current school year,
    /// for every schedule of every strand) and sections D and E that nobody uses are removed. A section that has students or a
    /// teacher stays, whatever its name. Running it again adds nothing twice.
    /// </para>
    /// </summary>
    public partial class AddGrade12CurriculumAndSectionsAToC : Migration
    {
        private static readonly string[][] Subjects12 =
        [
            // code, name
            ["BUSACCT2", "BUSINESS ACCOUNTING 2"],
            ["ADVMATH2", "ADVANCE MATHEMATICS 2"],
            ["FIL2", "FILIPINO 2"],
            ["CREACOMP2", "CREATIVE COMPOSITION 2"],
            ["FINMATH2", "FINITE MATH 2"],
            ["EMPOWERTECH2", "EMPOWERMENT TECHNOLOGY 2"],
            ["BIO2", "BIOLOGY 2"],
            ["SOCSCI2", "SOCIAL SCIENCES 2"],
            ["CSS2", "COMPUTER SYSTEM SERVICING 2"],
            ["KITCHENOPS2", "KITCHEN OPERATION 2"],
            ["FBSOPS2", "FOOD AND BEVERAGE OPERATIONS 2"],
            ["EFFCOMM2", "EFFECTIVE COMMUNICATION 2"],
            ["MABKOM2", "MABISANG KOMUNIKASYON 2"],
            ["GENMATH2", "GENERAL MATHEMATICS 2"],
            ["LIFECAREER2", "LIFE AND CAREER SKILLS 2"],
            ["GENSCI2", "GENERAL SCIENCE 2"],
            ["KASAYSAYAN2", "PAG-AARAL NG KASAYSAYAN AT LIPUNANG PILIPINO 2"],
        ];

        /// <summary>Grade 12, 1st term: the subjects of each strand.</summary>
        private static readonly string[][] FirstTerm12 =
        [
            // strand, subjects
            ["BUSSENT", "BUSACCT2", "ADVMATH2"],
            ["EDUC", "FIL2", "CREACOMP2"],
            ["ENG", "FINMATH2", "EMPOWERTECH2"],
            ["MED", "BIO2", "EMPOWERTECH2"],
            ["USP", "SOCSCI2", "BIO2"],
            ["CD1", "CSS2"],
            ["HT1", "KITCHENOPS2"],
            ["HT2", "FBSOPS2"],
        ];

        /// <summary>Grade 12, 2nd term: the same six subjects for every strand.</summary>
        private static readonly string[] SecondTerm12 = ["EFFCOMM2", "MABKOM2", "GENMATH2", "LIFECAREER2", "GENSCI2", "KASAYSAYAN2"];

        private static readonly string[] StrandCodes = ["BUSSENT", "EDUC", "ENG", "MED", "USP", "CD1", "HT1", "HT2"];

        private static string Q(string text) => "'" + text.Replace("'", "''") + "'";

        /// <summary>"SELECT 'a' AS `x`, 'b' AS `y` UNION ALL SELECT 'c', 'd'": a small table written inside a query.</summary>
        private static string Rows(string[] columnNames, IEnumerable<string[]> rows)
        {
            var parts = new List<string>();
            foreach (var row in rows)
            {
                var cells = row.Select((value, i) => parts.Count == 0 ? $"{Q(value)} AS `{columnNames[i]}`" : Q(value));
                parts.Add("SELECT " + string.Join(", ", cells));
            }

            return string.Join(" UNION ALL ", parts);
        }

        /// <summary>Inserts the sections with these names for the current school year, for every schedule of every strand, at each grade the strand has courses for.</summary>
        private static string InsertSections(params string[] names) =>
            "INSERT INTO `sections` (`school_year_id`, `strand_id`, `grade_level`, `schedule_id`, `name`, `created_at_utc`, `updated_at_utc`) " +
            "SELECT y.`id`, ss.`strand_id`, c.`grade_level`, ss.`schedule_id`, n.`name`, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6) " +
            "FROM (SELECT COALESCE((SELECT `school_year_id` FROM `terms` WHERE `is_current` = 1 ORDER BY `id` LIMIT 1), " +
            "                      (SELECT `id` FROM `school_years` ORDER BY `start_date` DESC, `id` DESC LIMIT 1)) AS `id`) y " +
            "JOIN `strand_schedules` ss " +
            "JOIN (SELECT DISTINCT `strand_id`, `grade_level` FROM `strand_subjects`) c ON c.`strand_id` = ss.`strand_id` " +
            "JOIN (" + Rows(["name"], names.Select(n => new[] { n })) + ") n " +
            "WHERE y.`id` IS NOT NULL " +
            "  AND NOT EXISTS (SELECT 1 FROM `sections` x WHERE x.`school_year_id` = y.`id` AND x.`strand_id` = ss.`strand_id` " +
            "                  AND x.`grade_level` = c.`grade_level` AND x.`schedule_id` = ss.`schedule_id` AND x.`name` = n.`name`);";

        /// <summary>A section nobody uses: no enrolled student and no teacher.</summary>
        private const string UnusedSection =
            "NOT EXISTS (SELECT 1 FROM `enrollments` e WHERE e.`section_id` = `sections`.`id`) " +
            "AND NOT EXISTS (SELECT 1 FROM `teaching_assignments` a WHERE a.`section_id` = `sections`.`id`)";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The grade 12 subjects.
            migrationBuilder.Sql(
                "INSERT INTO `subjects` (`code`, `name`, `is_active`, `created_at_utc`, `updated_at_utc`) " +
                "SELECT d.`code`, d.`name`, 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6) FROM (" +
                Rows(["code", "name"], Subjects12) +
                ") d WHERE NOT EXISTS (SELECT 1 FROM `subjects` s WHERE s.`code` = d.`code`);");

            // The grade 12 curriculum: which subjects each strand takes, 1st and 2nd term.
            var curriculum = FirstTerm12
                .SelectMany(r => r.Skip(1).Select(subject => new[] { r[0], "1", subject }))
                .Concat(StrandCodes.SelectMany(s => SecondTerm12.Select(subject => new[] { s, "2", subject })));

            migrationBuilder.Sql(
                "INSERT INTO `strand_subjects` (`strand_id`, `grade_level`, `term_number`, `subject_id`) " +
                "SELECT st.`id`, 12, d.`term_number`, sub.`id` FROM (" +
                Rows(["strand_code", "term_number", "subject_code"], curriculum) +
                ") d JOIN `strands` st ON st.`code` = d.`strand_code` JOIN `subjects` sub ON sub.`code` = d.`subject_code` " +
                "WHERE NOT EXISTS (SELECT 1 FROM `strand_subjects` x WHERE x.`strand_id` = st.`id` AND x.`grade_level` = 12 " +
                "AND x.`term_number` = d.`term_number` AND x.`subject_id` = sub.`id`);");

            // Sections A, B and C for both grades (grade 12 has courses now), and no D or E.
            migrationBuilder.Sql(InsertSections("A", "B", "C"));
            migrationBuilder.Sql(
                "DELETE FROM `sections` WHERE `id` > 0 AND `name` IN ('D', 'E') AND " + UnusedSection + ";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sections D and E come back (for grade 11, the only grade that had courses before this).
            migrationBuilder.Sql(InsertSections("D", "E"));

            // Grade 12 goes: its sections nobody uses, its curriculum, and its subjects that nothing refers to.
            migrationBuilder.Sql(
                "DELETE FROM `sections` WHERE `id` > 0 AND `grade_level` = 12 AND " + UnusedSection + ";");
            migrationBuilder.Sql(
                "DELETE FROM `strand_subjects` WHERE `strand_id` > 0 AND `grade_level` = 12 " +
                "AND NOT EXISTS (SELECT 1 FROM `sections` s WHERE s.`strand_id` = `strand_subjects`.`strand_id` AND s.`grade_level` = 12);");
            migrationBuilder.Sql(
                "DELETE FROM `subjects` WHERE `id` > 0 AND `code` IN (" + string.Join(", ", Subjects12.Select(s => Q(s[0]))) + ") " +
                "AND NOT EXISTS (SELECT 1 FROM `strand_subjects` x WHERE x.`subject_id` = `subjects`.`id`) " +
                "AND NOT EXISTS (SELECT 1 FROM `enrollment_subjects` x WHERE x.`subject_id` = `subjects`.`id`) " +
                "AND NOT EXISTS (SELECT 1 FROM `material_modules` x WHERE x.`subject_id` = `subjects`.`id`) " +
                "AND NOT EXISTS (SELECT 1 FROM `students` x WHERE x.`subject_id` = `subjects`.`id`) " +
                "AND NOT EXISTS (SELECT 1 FROM `teachers` x WHERE x.`subject_id` = `subjects`.`id`) " +
                "AND NOT EXISTS (SELECT 1 FROM `teaching_assignments` x WHERE x.`subject_id` = `subjects`.`id`);");
        }
    }
}
