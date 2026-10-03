#!/usr/bin/env bash
# Regenerates the SQL scripts for MySQL Workbench from the Entity Framework migrations.
# Run it again after every new migration:
#
#   bash database/export-schema.sh
#
# It writes:
#   database/seaittlms_schema.sql            everything, for a NEW empty seaittlms schema
#   database/seaittlms_reset_and_rebuild.sql the same, but it first ERASES the seaittlms schema
#                                            (for starting over when the database got broken)
#   database/upgrades/<migration>.sql        one file per migration after the first, for a
#                                            database that already has the migration before it
#
# Needs the dotnet-ef tool (dotnet tool install --global dotnet-ef --version 9.0.20) and a
# reachable MySQL server: the tool asks the server which version it is, using the same
# connection string as the app (appsettings, user-secrets or ConnectionStrings__Default).
# On Windows run it in Git Bash.
set -euo pipefail

cd "$(dirname "$0")/.."
tmp="$(mktemp)"
trap 'rm -f "$tmp"' EXIT

# EF writes a UTF-8 byte-order mark and the line endings of the current OS. Remove both.
bom="$(printf '\357\273\277')"
clean() { sed -e "1s/^${bom}//" -e 's/\r$//' "$1"; }

full_header() {
  cat <<'HEADER'
-- =============================================================================
-- SEAITT e-LMS - database schema for MySQL 8 (MySQL Workbench friendly)
--
-- This file is GENERATED from the Entity Framework migrations in Data/Migrations.
-- Do not edit it by hand. To regenerate it: bash database/export-schema.sh
--
-- HOW TO RUN IT IN MYSQL WORKBENCH
--   1. Connect to your MySQL server.
--   2. File > Open SQL Script...  and pick this file.
--   3. Click the lightning bolt (Execute). You don't need to select anything first.
--   4. In the SCHEMAS panel: right-click > Refresh All, then open seaittlms.
--      The tables are under Tables, the per-role lists (v_students, v_teachers, ...) under Views.
--
-- Run it ONCE on an EMPTY seaittlms schema. If you already started the app in
-- Development, or you ran an older version of this file, the tables already exist:
-- do NOT run this file again (you would get "Table ... already exists").
-- Use the matching file in database/upgrades/ instead, see README.md.
--
-- It also records the migrations in __EFMigrationsHistory, so the app starts normally
-- afterwards and does not try to create the tables a second time.
-- It does not create any user account: the app creates the first administrator when
-- it starts (see README.md, "First-time setup").
--
-- Harmless messages you may see in the Output panel: "database exists" (the schema was
-- already there) and "Integer display width is deprecated" (MySQL's note about tinyint(1)).
-- =============================================================================

CREATE DATABASE IF NOT EXISTS `seaittlms` CHARACTER SET utf8mb4;
USE `seaittlms`;

HEADER
}

reset_header() {
  cat <<'HEADER'
-- =============================================================================
-- SEAITT e-LMS - RESET AND REBUILD the seaittlms database (MySQL Workbench friendly)
--
--   !!!  WARNING: THIS ERASES EVERYTHING IN THE seaittlms SCHEMA  !!!
--   Every table, view and row (users, students, teachers, ...) is deleted and the
--   database is built again from scratch. There is NO undo.
--
-- Use it only to start over, for example when tables were dropped or changed by hand and
-- Workbench shows "Error loading schema content ... Error Code: 1356".
-- To keep your data, use database/seaittlms_schema.sql (new schema) or database/upgrades/
-- (older schema) instead.
--
-- Afterwards the database is empty, except the five roles and the four tracks.
-- Start the app to create the first administrator (README.md, "First-time setup").
--
-- HOW TO RUN IT IN MYSQL WORKBENCH
--   File > Open SQL Script...  pick this file, then click the lightning bolt (Execute).
--
-- This file is GENERATED from the Entity Framework migrations in Data/Migrations.
-- Do not edit it by hand. To regenerate it: bash database/export-schema.sh
-- =============================================================================

DROP DATABASE IF EXISTS `seaittlms`;
CREATE DATABASE `seaittlms` CHARACTER SET utf8mb4;
USE `seaittlms`;

HEADER
}

upgrade_header() {
  cat <<'HEADER' | sed -e "s/@PREV@/$1/g" -e "s/@CUR@/$2/g"
-- =============================================================================
-- SEAITT e-LMS - UPGRADE script for MySQL 8 (MySQL Workbench friendly)
--
-- Brings a database that already has the migration
--     @PREV@
-- up to
--     @CUR@
-- Your existing data is kept.
--
-- This file is GENERATED from the Entity Framework migrations in Data/Migrations.
-- Do not edit it by hand. To regenerate it: bash database/export-schema.sh
--
-- USE IT ONLY IF the last row of this query is @PREV@:
--     SELECT * FROM __EFMigrationsHistory ORDER BY migration_id;
-- (If the last row is already @CUR@ or newer, you don't need this file. If it is older,
-- run the files in database/upgrades/ one by one, oldest first.)
--
-- HOW TO RUN IT IN MYSQL WORKBENCH
--   File > Open SQL Script...  pick this file, then click the lightning bolt (Execute).
--   Run it ONCE. It also records the upgrade in __EFMigrationsHistory.
--
-- If you start the app in Development instead, it applies these upgrades by itself.
-- =============================================================================

USE `seaittlms`;

HEADER
}

# Migration ids in order. Their file names start with a timestamp, so alphabetical is chronological.
ids=()
for f in Data/Migrations/[0-9]*.cs; do
  case "$f" in *.Designer.cs) continue ;; esac
  ids+=("$(basename "$f" .cs)")
done

# 1. The full script for a new database.
dotnet ef migrations script --output "$tmp"
{ full_header; clean "$tmp"; } > database/seaittlms_schema.sql
echo "Wrote database/seaittlms_schema.sql"

# 1b. The same script behind a DROP/CREATE DATABASE, to start over from nothing.
{ reset_header; clean "$tmp"; } > database/seaittlms_reset_and_rebuild.sql
echo "Wrote database/seaittlms_reset_and_rebuild.sql"

# 2. One upgrade script per migration after the first.
mkdir -p database/upgrades
rm -f database/upgrades/*.sql
for ((i = 1; i < ${#ids[@]}; i++)); do
  prev="${ids[$((i - 1))]}"
  cur="${ids[$i]}"
  dotnet ef migrations script "$prev" "$cur" --no-build --output "$tmp"
  { upgrade_header "$prev" "$cur"; clean "$tmp"; } > "database/upgrades/$cur.sql"
  echo "Wrote database/upgrades/$cur.sql"
done
