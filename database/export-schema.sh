#!/usr/bin/env bash
# Regenerates database/seaittlms_schema.sql (the script you run in MySQL Workbench)
# from the Entity Framework migrations. Run it again after every new migration:
#
#   bash database/export-schema.sh
#
# Needs the dotnet-ef tool (dotnet tool install --global dotnet-ef --version 9.0.20)
# and a reachable MySQL server: the tool asks the server which version it is, using the
# same connection string as the app (user-secrets or ConnectionStrings__Default).
# On Windows run it in Git Bash.
set -euo pipefail

cd "$(dirname "$0")/.."
out="database/seaittlms_schema.sql"
tmp="$(mktemp)"
trap 'rm -f "$tmp"' EXIT

dotnet ef migrations script --output "$tmp"

# EF writes a UTF-8 byte-order mark and the line endings of the current OS. Remove both.
bom="$(printf '\357\273\277')"

{
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
--   4. In the SCHEMAS panel: right-click > Refresh All, then open seaittlms > Tables.
--
-- Run it ONCE on an empty seaittlms schema. If you already started the app in
-- Development, the tables were created for you and you do NOT need this file.
--
-- It also records the migration in __EFMigrationsHistory, so the app starts normally
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
  sed -e "1s/^${bom}//" -e 's/\r$//' "$tmp"
} > "$out"

echo "Wrote $out"
