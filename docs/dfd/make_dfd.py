#!/usr/bin/env python3
"""
Makes the data flow diagrams of the SEAITT Legazpi LMS (level 0, level 1 and level 2 of every process) in docs/dfd/*.svg and *.png.

    sudo apt-get install graphviz        (on Windows: install Graphviz and put it in the PATH)
    python3 docs/dfd/make_dfd.py

The diagrams use the Gane-Sarson symbols:  a square = an external entity (a person or a service outside the system),
a rounded box with a number = a process, an open-ended rectangle "D1 | name" = a data store (the tables of the database),
an arrow = a data flow (what moves, written on the arrow). Change the lists below, run the script again, and the pictures are made again.
"""
import html, os, subprocess, textwrap, json, sys

OUT = os.path.dirname(os.path.abspath(__file__))

# ----------------------------------------------------------------------------------------------------- the external entities
ENT = {
    "ADM": "Administrator",
    "REG": "Registrar",
    "GC": "Guidance Counselor",
    "AC": "Academic Coordinator",
    "AP": "Academic Provider",
    "TCH": "Teacher",
    "STU": "Student",
    "SMS": "UniSMS\n(SMS service)",
    "ANY": "Any user\n(every role)",
}

# ----------------------------------------------------------------------------------------------------- the data stores (the tables of the database)
STORES = {
    "D0": ("Whole database", "all 63 tables (for the backup)"),
    "D1": ("Accounts and profiles", "users, roles, custom_roles, administrators, registrars, guidance_counselors, academic_coordinators, academic_providers, teachers, students, profile_photos"),
    "D2": ("Security records", "security_logs, login_otps"),
    "D3": ("System settings", "system_settings"),
    "D4": ("School structure", "school_years, terms, tracks, strands, courses, strand_courses, sections"),
    "D5": ("Enrollments and assignments", "enrollments, enrollment_courses, teaching_assignments"),
    "D6": ("Course library", "material_handouts, material_items, material_questions, material_question_images, material_deliveries"),
    "D7": ("Class work", "handouts, handout_items, quiz_questions, quiz_question_images, submissions, handout_reads, handout_views, item_views"),
    "D8": ("Grades and attendance", "final_grades, attendance_records, achievement_awards"),
    "D9": ("Referrals", "referrals"),
    "D10": ("Messages and notifications", "messages, notifications, friendships, notification_sounds"),
    "D11": ("Groups and files", "study_groups, study_group_members, group_posts, stored_files, stored_file_contents, shared_files, attachments, attachment_contents"),
    "D12": ("Calendar and announcements", "announcements, calendar_entries, calendar_entry_invites, holiday_exclusions"),
    "D13": ("Evaluations", "teacher_evaluations, evaluation_questions, evaluation_targets, evaluation_responses, evaluation_answers"),
    "D14": ("Backup files", "SQL files kept in the backup folder (not a table)"),
}

# ----------------------------------------------------------------------------------------------------- the diagrams
# each flow: (from, to, what moves[, "both"])  "both" = the process reads and writes the same store (an arrow at each end)
DIAGRAMS = []

def diagram(key, title, subtitle, processes, flows, store_labels=True, left=None, right=None, order=None):
    """store_labels=False: the arrows to and from the data stores have no words (level 1: the stores are described in the table of the document).
    left / right: the entities drawn at the left and at the right of the process (level 0)."""
    DIAGRAMS.append(dict(key=key, title=title, subtitle=subtitle, processes=processes, flows=flows, store_labels=store_labels, left=left, right=right, order=order))

# ---- level 0: the context diagram
diagram("level-0", "Level 0: context diagram", "The whole system as one process, with the people and the service outside it.",
    {"0": ("0", "SEAITT Legazpi LMS\n(Learning Management System)", "")},
    [
        ("ADM", "0", "Login details and code, accounts, roles, system settings, backup and archive requests"),
        ("0", "ADM", "Analytics, security logs, backup files, archived students"),
        ("REG", "0", "Student details, enrollment changes, drop-out"),
        ("0", "REG", "Enrollment lists, enrollment forms, student accounts"),
        ("GC", "0", "Concerns, notes, help given, schedule"),
        ("0", "GC", "Referrals (attendance, violations), the answers of the students"),
        ("AC", "0", "School setup, announcements, holidays, evaluation form"),
        ("0", "AC", "Evaluation results, class sections, analytics"),
        ("AP", "0", "Course library: syllabus, handouts, summative exams"),
        ("0", "AP", "Tracking of what was sent and posted"),
        ("TCH", "0", "Handouts, activities, quizzes, scores, attendance, final grades, messages"),
        ("0", "TCH", "Class lists, answers to check, gradebook, notifications"),
        ("STU", "0", "Answers and quizzes, evaluation answers, replies to the counselor, messages"),
        ("0", "STU", "Courses, handouts, grades, notifications, messages of the counselor"),
        ("0", "SMS", "Login code message (phone number and text)"),
        ("SMS", "0", "Delivery result"),
    ], left=["ADM", "REG", "GC", "AC"], right=["AP", "TCH", "STU", "SMS"])

# ---- level 1: the main processes
L1P = {
    "1": ("1.0", "Manage accounts\nand access", ""),
    "2": ("2.0", "Set up the school\nand enroll students", ""),
    "3": ("3.0", "Manage the\ncourse library", ""),
    "4": ("4.0", "Conduct classes\nand grading", ""),
    "5": ("5.0", "Handle guidance\nreferrals", ""),
    "6": ("6.0", "Communicate\nand share", ""),
    "7": ("7.0", "Evaluate teachers\nand analyze", ""),
    "8": ("8.0", "Administer\nthe system", ""),
}
diagram("level-1", "Level 1: the main processes", "The system in eight processes, with the data stores they use.", L1P,
    [
        # 1.0
        ("ANY", "1", "Login details, own settings"), ("1", "ANY", "Sign-in result, access to the pages"),
        ("ADM", "1", "Account details, roles and pages"), ("1", "ADM", "New email and password"),
        ("1", "SMS", "Login code message"), ("SMS", "1", "Delivery result"),
        ("1", "D1", "Accounts and profiles", "both"), ("1", "D2", "Login codes, security events", "both"), ("D3", "1", "Lockout rules, switches"),
        # 2.0
        ("AC", "2", "School years, terms, tracks, strands, courses, sections"),
        ("REG", "2", "Student details, enrollment changes, drop-out"), ("2", "REG", "Enrollment lists, forms, student accounts"),
        ("ADM", "2", "Teacher assignments, archive and restore requests"), ("2", "ADM", "Archived students, grades"),
        ("2", "D4", "School structure", "both"), ("2", "D5", "Enrollments, teaching assignments", "both"), ("2", "D1", "Student accounts and status", "both"),
        ("2", "D9", "Referrals closed (drop-out)"), ("D8", "2", "Grades for the archive"),
        # 3.0
        ("AP", "3", "Syllabus, handouts, exams, send and post commands"), ("3", "AP", "Sent, posted and waiting"),
        ("3", "TCH", "Handout drafts"), ("3", "STU", "Summative exams"), ("3", "AC", "Exam announcement"),
        ("3", "D6", "Course library", "both"), ("3", "D7", "Teacher copies, exam boxes"), ("D5", "3", "Teachers of the courses"), ("D4", "3", "Courses of the strands"),
        ("3", "D11", "Files of the handouts", "both"),
        # 4.0
        ("TCH", "4", "Handouts, activities, quizzes, scores, attendance, final grades"), ("4", "TCH", "Class lists, answers to check, gradebook"),
        ("STU", "4", "Answers, quiz attempts, reading"), ("4", "STU", "Handouts, activities, scores, attendance"),
        ("4", "D7", "Handouts, items, submissions", "both"), ("4", "D8", "Final grades, attendance", "both"), ("D5", "4", "Class lists"),
        ("4", "D11", "Files", "both"), ("4", "D10", "New handout and graded notices"),
        # 5.0
        ("GC", "5", "Concerns, notes, help given, schedule"), ("5", "GC", "Referral list, answers"),
        ("STU", "5", "OK, Going, Not going"), ("5", "STU", "Counselor's message, schedule"),
        ("D8", "5", "Attendance"), ("5", "D9", "Referrals", "both"), ("D5", "5", "Students and sections"), ("5", "D10", "Notices"), ("5", "D12", "Counseling day"),
        # 6.0
        ("ANY", "6", "Messages, posts, files, events, friend requests"), ("6", "ANY", "Conversations, notices, calendar, shared files"),
        ("AC", "6", "Announcements, holidays"), ("TCH", "6", "Announcement to the class"),
        ("6", "D10", "Messages, friends, notifications", "both"), ("6", "D11", "Groups, posts, files", "both"), ("6", "D12", "Announcements, events, holidays", "both"), ("D1", "6", "People and roles"),
        # 7.0
        ("AC", "7", "Evaluation questions and rounds"), ("STU", "7", "Evaluation answers"), ("7", "AC", "Evaluation results"),
        ("7", "ADM", "Student analytics"), ("7", "TCH", "Class analytics"), ("7", "GC", "Concern analytics"), ("7", "AP", "Library analytics"),
        ("7", "D13", "Evaluations", "both"), ("D5", "7", "Enrollments"), ("D7", "7", "Work done"), ("D8", "7", "Grades, attendance"), ("D9", "7", "Referrals"), ("D6", "7", "Library state"),
        # 8.0
        ("ADM", "8", "Settings, backup and restore requests"), ("8", "ADM", "Security logs, backup files"),
        ("8", "D3", "Settings", "both"), ("8", "D2", "Security events", "both"), ("8", "D14", "Backup files", "both"), ("8", "D0", "Whole database", "both"),
        ("8", "D9", "Automatic referrals"), ("8", "D1", "Archive Grade 12, delete old archives", "both"),
    ], store_labels=False, order=dict(
        entities=["SMS", "ANY", "ADM", "REG", "AC", "GC", "STU", "TCH", "AP"],
        processes=["1", "8", "2", "6", "5", "7", "4", "3"],
        stores=["D3", "D2", "D0", "D14", "D1", "D4", "D5", "D9", "D12", "D10", "D11", "D13", "D8", "D7", "D6"]))

# ---- level 2: every process in its steps
diagram("level-2-1", "Level 2: 1.0 Manage accounts and access", "Sign in, the login code, the session, the accounts and the own settings.",
    {"1": ("1.1", "Check the login", ""), "2": ("1.2", "Send and check\nthe login code", ""), "3": ("1.3", "Start and end\nthe session", ""),
     "4": ("1.4", "Manage accounts\nand roles", ""), "5": ("1.5", "Keep the own\nsettings", ""), "6": ("1.6", "Record the\nsecurity events", "")},
    [
        ("ANY", "1", "Email and password"), ("1", "ANY", "Deactivated, locked or wrong-password message"),
        ("D1", "1", "Account, password hash, status"), ("D3", "1", "Lockout rules, maintenance switch"), ("1", "D1", "Failed tries, lock time"),
        ("1", "2", "Administrator: password accepted"), ("1", "3", "Other roles: password accepted"),
        ("ANY", "2", "Code typed"), ("2", "SMS", "Phone number and code message"), ("SMS", "2", "Delivery result"), ("D3", "2", "Code switch"),
        ("2", "D2", "Hashed codes, tries", "both"), ("2", "3", "Code accepted"),
        ("3", "ANY", "Access to the pages of the role"), ("3", "D1", "Open sign-in, last login", "both"),
        ("ADM", "4", "New, edited or deactivated account, roles and pages"), ("4", "D1", "Accounts, roles, custom roles", "both"), ("4", "ADM", "Generated email and password"),
        ("ANY", "5", "New password, phone number, picture"), ("5", "D1", "Password hash, phone, picture", "both"), ("5", "ANY", "Saved or the reason it was not"),
        ("1", "6", "Sign-in event"), ("2", "6", "Code event"), ("4", "6", "Account event"), ("5", "6", "Password and phone event"), ("6", "D2", "Security log lines"),
    ])

diagram("level-2-2", "Level 2: 2.0 Set up the school and enroll students", "School years, curriculum, sections, enrollment, drop-out, archive and the teachers' courses.",
    {"1": ("2.1", "Maintain school\nyears and terms", ""), "2": ("2.2", "Maintain tracks,\nstrands, courses\nand curriculum", ""), "3": ("2.3", "Manage class\nsections", ""),
     "4": ("2.4", "Enroll a student", ""), "5": ("2.5", "Change an\nenrollment or\ndrop out", ""), "6": ("2.6", "Assign teachers\nto courses", ""),
     "7": ("2.7", "Archive and\nrestore students", ""), "8": ("2.8", "List and print\nenrollments", "")},
    [
        ("AC", "1", "School years and terms"), ("1", "D4", "School years, terms", "both"),
        ("AC", "2", "Tracks, strands, courses, units, Excel sheet"), ("2", "D4", "Tracks, strands, courses, curriculum", "both"),
        ("AC", "3", "Sections and advisers"), ("3", "D4", "Sections", "both"),
        ("REG", "4", "Name, LRN, phone, strand, section"), ("D4", "4", "Curriculum of the strand, sections"), ("4", "D1", "New student account"), ("4", "D5", "Enrollment and its courses"), ("4", "REG", "Email and first password"),
        ("REG", "5", "Edit or drop-out request"), ("5", "D5", "Enrollment status, section", "both"), ("5", "D1", "Account switched off"), ("5", "D9", "Open referrals closed"), ("5", "7", "Dropped-out student"),
        ("ADM", "6", "Teacher, section and courses"), ("D4", "6", "Sections and courses"), ("6", "D5", "Teaching assignments", "both"),
        ("ADM", "7", "Archive or restore request"), ("7", "D1", "Student status and account", "both"), ("D8", "7", "Grades for the export"), ("7", "ADM", "Archived list, grades in Excel"),
        ("D5", "8", "Enrollments"), ("D1", "8", "Student accounts"), ("8", "REG", "Lists, enrollment form, Excel"),
    ])

diagram("level-2-3", "Level 2: 3.0 Manage the course library", "The library of the academic provider and how it reaches the teachers and the students.",
    {"1": ("3.1", "Maintain syllabus\nand handouts\nof the library", ""), "2": ("3.2", "Maintain summative\nexams and\nquestions", ""), "3": ("3.3", "Send library items\nto the teachers", ""),
     "4": ("3.4", "Post summative\nexams to the\nstudents", ""), "5": ("3.5", "Track sending\nand posting", "")},
    [
        ("AP", "1", "Syllabus, handouts, files, links"), ("1", "D6", "Library handouts", "both"), ("1", "D11", "Attached files", "both"), ("D4", "1", "Courses of the strand"),
        ("AP", "2", "Exam questions and settings"), ("2", "D6", "Library exams and questions", "both"),
        ("AP", "3", "Send command"), ("D6", "3", "Library items"), ("D5", "3", "Teacher of each section"), ("3", "D7", "Draft copies for the course"), ("3", "D6", "Delivery record"), ("3", "TCH", "Drafts to post"), ("3", "D10", "Notice to the teacher"),
        ("AP", "4", "Post command"), ("D6", "4", "Exams"), ("4", "D7", "Exam boxes of the courses"), ("4", "STU", "Summative exams"), ("4", "D12", "Announcement of the exam"), ("4", "AC", "Exam announcement"),
        ("D6", "5", "Delivery records"), ("D7", "5", "Posted, scheduled or hidden"), ("5", "AP", "Tracking lists"), ("AP", "5", "Send a reminder"), ("5", "D10", "Reminder notices"),
    ])

diagram("level-2-4", "Level 2: 4.0 Conduct classes and grading", "Handouts, activities and quizzes, the students' work, grading, the gradebook, attendance and progress.",
    {"1": ("4.1", "Manage handouts", ""), "2": ("4.2", "Manage activities\nand quizzes", ""), "3": ("4.3", "Take the work\n(student)", ""),
     "4": ("4.4", "Grade the work\nand give feedback", ""), "5": ("4.5", "Keep the gradebook\nand final grades", ""), "6": ("4.6", "Record attendance", ""),
     "7": ("4.7", "Track reading\nand achievements", "")},
    [
        ("TCH", "1", "Title, files, slides, post date"), ("1", "D7", "Handouts", "both"), ("1", "D11", "Files", "both"), ("1", "D10", "New handout notice"), ("1", "STU", "Posted handouts"),
        ("TCH", "2", "Activities, quiz questions, due dates, settings"), ("2", "D7", "Items and questions", "both"),
        ("STU", "3", "Answers, files, quiz answers"), ("D7", "3", "Items and questions"), ("3", "D7", "Submissions"), ("3", "D11", "Answer files"), ("3", "STU", "Instructions, quiz, result"),
        ("D7", "4", "Submissions"), ("TCH", "4", "Scores and feedback"), ("4", "D7", "Scores and feedback"), ("4", "D10", "Graded notice"), ("4", "TCH", "Answers to check"), ("4", "STU", "Score and feedback"),
        ("D7", "5", "Scores"), ("D5", "5", "Students of the course"), ("TCH", "5", "Other scores, final grade, hide grades"), ("5", "D8", "Final grades", "both"), ("5", "TCH", "Gradebook, Excel"), ("5", "STU", "Grades (unless hidden)"),
        ("TCH", "6", "Present, late, absent"), ("D5", "6", "Class list"), ("6", "D8", "Attendance records"), ("6", "STU", "Own attendance"),
        ("STU", "7", "Pages opened, percent read"), ("7", "D7", "Views and reads", "both"), ("7", "D8", "Achievement awards"), ("7", "STU", "Progress bars, achievements"),
    ])

diagram("level-2-5", "Level 2: 5.0 Handle guidance referrals", "From a concern (found by the system or written by the counselor) to the schedule and the solution.",
    {"1": ("5.1", "Find concerns\nautomatically", ""), "2": ("5.2", "Record a concern", ""), "3": ("5.3", "Write notes and\nhelp given", ""),
     "4": ("5.4", "Receive the\nstudent's answer", ""), "5": ("5.5", "Set the schedule", ""), "6": ("5.6", "Solve the concern", "")},
    [
        ("D8", "1", "Attendance marks"), ("D5", "1", "Students and sections"), ("1", "D9", "Referral (attendance)"), ("1", "D10", "Notice to the counselor"),
        ("GC", "2", "Student, concern, what happened"), ("2", "D9", "Referral (attendance or violation)"), ("2", "GC", "Recorded and earlier concerns"),
        ("GC", "3", "Help given, notes"), ("3", "D9", "Notes, help given, status", "both"), ("3", "D10", "Counselor's message"), ("3", "STU", "Box of the counselor"),
        ("STU", "4", "OK, closed (ignored), Going, Not going"), ("D10", "4", "Message to show again (the bell)"), ("4", "D9", "Seen, ignored, answer"), ("4", "D10", "Is going / is not going notice"), ("4", "GC", "Status and answer on the row"), ("4", "STU", "Box opened again"),
        ("GC", "5", "Day and time"), ("D9", "5", "Answer Going"), ("5", "D9", "Schedule"), ("5", "D12", "Counseling day in both calendars"), ("5", "D10", "Schedule notice"), ("5", "STU", "Day and time"),
        ("GC", "6", "Solved"), ("6", "D9", "Resolved, outcome"), ("6", "D10", "Case closed notice"),
    ])

diagram("level-2-6", "Level 2: 6.0 Communicate and share", "Messages, friends and groups, files, announcements, the calendar and the notifications.",
    {"1": ("6.1", "Send and read\nmessages", ""), "2": ("6.2", "Manage friends\nand groups", ""), "3": ("6.3", "Store and share\nfiles", ""),
     "4": ("6.4", "Publish announcements\nand holidays", ""), "5": ("6.5", "Manage the\ncalendar", ""), "6": ("6.6", "Show the\nnotifications", "")},
    [
        ("ANY", "1", "Message and attachment"), ("D1", "1", "Who may write to whom"), ("1", "D10", "Messages", "both"), ("1", "ANY", "Conversations"), ("1", "6", "New message"),
        ("ANY", "2", "Friend request, group, post"), ("2", "D10", "Friendships", "both"), ("2", "D11", "Groups, members, posts", "both"), ("D1", "2", "People that can be reached"), ("2", "ANY", "Friends and group chat"),
        ("ANY", "3", "File to keep or share"), ("3", "D11", "Stored and shared files", "both"), ("3", "ANY", "Download or preview"),
        ("AC", "4", "Announcement, holiday"), ("TCH", "4", "Announcement to the class"), ("4", "D12", "Announcements, removed holidays", "both"), ("4", "6", "New announcement"), ("4", "ANY", "Announcements"),
        ("ANY", "5", "Event and invite"), ("5", "D12", "Calendar entries and invites", "both"), ("5", "ANY", "Calendar with holidays and due dates"),
        ("6", "D10", "Notifications", "both"), ("6", "ANY", "Bell, sound, counts"),
    ])

diagram("level-2-7", "Level 2: 7.0 Evaluate teachers and analyze", "The teacher evaluation, and the analytics and reports for each role.",
    {"1": ("7.1", "Build the\nevaluation", ""), "2": ("7.2", "Collect the\nstudents' answers", ""), "3": ("7.3", "Work out the\nresults", ""),
     "4": ("7.4", "Compile the\nanalytics", ""), "5": ("7.5", "Export the\nreports", "")},
    [
        ("AC", "1", "Questions, kinds, rounds, due date"), ("D5", "1", "Teachers and sections to evaluate"), ("1", "D13", "Evaluation form and targets"),
        ("STU", "2", "Answers and comments"), ("D13", "2", "Questions"), ("2", "D13", "Anonymous answers"),
        ("D13", "3", "Answers"), ("3", "AC", "Results by teacher, course, question; comments"), ("3", "5", "Results"),
        ("D5", "4", "Enrollments"), ("D7", "4", "Work done"), ("D8", "4", "Grades and attendance"), ("D9", "4", "Referrals"), ("D6", "4", "Library state"),
        ("4", "ADM", "Student analytics"), ("4", "TCH", "Class analytics"), ("4", "GC", "Concern analytics"), ("4", "AP", "Library analytics"), ("4", "5", "Analytics data"),
        ("5", "AC", "Excel report"), ("5", "ADM", "Excel report"), ("5", "TCH", "Excel report"),
    ])

diagram("level-2-8", "Level 2: 8.0 Administer the system", "Settings, maintenance, the security log, backups and the tasks the system does by itself.",
    {"1": ("8.1", "Maintain the\nsystem settings", ""), "2": ("8.2", "Close and open the\nsystem for\nmaintenance", ""), "3": ("8.3", "Show and clear the\nsecurity log", ""),
     "4": ("8.4", "Back up the\ndatabase", ""), "5": ("8.5", "Restore a backup", ""), "6": ("8.6", "Run the automatic\ntasks", "")},
    [
        ("ADM", "1", "Lockout rules, limits, code switch, school names"), ("1", "D3", "Settings", "both"),
        ("ADM", "2", "Closed switch and message"), ("2", "D3", "Maintenance state", "both"), ("2", "ADM", "Closed or open"),
        ("ADM", "3", "Filter, export, clear"), ("D2", "3", "Log lines"), ("3", "D2", "Old lines cleared"), ("3", "ADM", "Security log, CSV"),
        ("ADM", "4", "Back up now"), ("D0", "4", "All tables"), ("4", "D14", "SQL backup file"), ("4", "ADM", "Download"),
        ("ADM", "5", "Upload or choose a backup"), ("D14", "5", "Backup file"), ("5", "D0", "Rebuilt tables"), ("5", "2", "System closed while restoring"),
        ("D3", "6", "Daily backup switch, log keeping time"), ("6", "D9", "Attendance referrals"), ("6", "D1", "Archive Grade 12, delete old archives", "both"), ("6", "D14", "Daily backup"), ("6", "D2", "Old lines cleared"),
    ])

# ----------------------------------------------------------------------------------------------------- Graphviz
ENT_FILL, ENT_LINE = "#dbeafe", "#1e3a8a"
PRO_FILL, PRO_LINE = "#fef3c7", "#92400e"
STO_FILL, STO_LINE = "#ecfdf5", "#065f46"

def esc(s):
    return html.escape(s).replace("\n", "<BR/>")

def wrap(label, width=26):
    return "\n".join(textwrap.wrap(label, width=width)) if "\n" not in label else label

def dot_of(d):
    used_entities, used_stores = set(), set()
    for f in d["flows"]:
        for end in f[:2]:
            if end in ENT: used_entities.add(end)
            if end in STORES: used_stores.add(end)
    lines = [
        "digraph G {",
        '  graph [rankdir=LR, splines=true, nodesep=0.45, ranksep=1.35, pad=0.4, bgcolor="white", fontname="Helvetica", fontsize=22, labelloc=t, labeljust=l,'
        f' label=<<B>{esc(d["title"])}</B><BR ALIGN="LEFT"/><FONT POINT-SIZE="13" COLOR="#475569">{esc(d["subtitle"])}</FONT><BR ALIGN="LEFT"/>>];',
        '  node [fontname="Helvetica", fontsize=13];',
        '  edge [fontname="Helvetica", fontsize=10.5, color="#334155", fontcolor="#0f172a", arrowsize=0.8, penwidth=1.2];',
    ]
    for key in sorted(used_entities):
        lines.append(f'  E_{key} [shape=box, style="filled", fillcolor="{ENT_FILL}", color="{ENT_LINE}", penwidth=1.8, fontsize=14, margin="0.28,0.18", label="{ENT[key]}"];')
    for key, (num, name, _) in d["processes"].items():
        lines.append(
            f'  P_{key} [shape=plain, label=<<TABLE BORDER="2" CELLBORDER="0" CELLSPACING="0" CELLPADDING="6" STYLE="ROUNDED" COLOR="{PRO_LINE}" BGCOLOR="{PRO_FILL}">'
            f'<TR><TD SIDES="B" BORDER="1" COLOR="{PRO_LINE}"><B>{esc(num)}</B></TD></TR><TR><TD>{esc(name)}</TD></TR></TABLE>>];')
    for key in sorted(used_stores, key=lambda k: int(k[1:])):
        name = STORES[key][0]
        lines.append(
            f'  S_{key} [shape=plain, label=<<TABLE BORDER="0" CELLBORDER="0" CELLSPACING="0" CELLPADDING="6" BGCOLOR="{STO_FILL}">'
            f'<TR><TD SIDES="LTB" BORDER="2" COLOR="{STO_LINE}"><B>{key}</B></TD><TD SIDES="TB" BORDER="2" COLOR="{STO_LINE}">{esc(name)}</TD></TR></TABLE>>];')
    def node(x):
        return f"E_{x}" if x in ENT else f"S_{x}" if x in STORES else f"P_{x}"
    if d["left"] or d["right"]:
        # the context diagram: some entities at the left of the process, the others at the right
        lines.append("  { rank=source; " + "; ".join(node(k) for k in d["left"] if k in used_entities) + "; }")
        lines.append("  { rank=sink; " + "; ".join(node(k) for k in d["right"] if k in used_entities) + "; }")
    else:
        # the others: the people and the service at the left, the data stores at the right, the processes between them
        lines.append("  { rank=source; " + "; ".join(node(k) for k in sorted(used_entities)) + "; }")
        if used_stores:
            lines.append("  { rank=sink; " + "; ".join(node(k) for k in sorted(used_stores, key=lambda k: int(k[1:]))) + "; }")
    # the order of the boxes in a column (so that the arrows cross as little as possible): the boxes of a column are chained with invisible arrows
    order = d.get("order")
    if order:
        for names, make, same in ((order["entities"], lambda k: f"E_{k}", False), (order["processes"], lambda k: f"P_{k}", True), (order["stores"], lambda k: f"S_{k}", False)):
            names = [make(k[2:] if k.startswith("D") and make("") == "S_" else k) if False else make(k) for k in names]
            chain = [n for n in names if (n[2:] in used_entities or n[2:] in used_stores or n[2:] in d["processes"])]
            if len(chain) > 1:
                body = " -> ".join(chain)
                lines.append((f"  {{ rank=same; {body} [style=invis]; }}" if same else f"  {body} [style=invis, constraint=false];"))
    for f in d["flows"]:
        a, b = f[0], f[1]
        label = wrap(f[2]) if (d["store_labels"] or (a not in STORES and b not in STORES)) else ""
        both = len(f) > 3 and f[3] == "both"
        attrs = (f'label="{label}"' if label else 'style=dashed') + (", dir=both" if both else "")
        lines.append(f'  {node(a)} -> {node(b)} [{attrs}];')
    lines.append("}")
    return "\n".join(lines)

def main():
    manifest = []
    for d in DIAGRAMS:
        dot = dot_of(d)
        base = os.path.join(OUT, "dfd-" + d["key"])
        open(base + ".dot", "w", encoding="utf-8").write(dot)
        subprocess.run(["dot", "-Tsvg", base + ".dot", "-o", base + ".svg"], check=True)
        subprocess.run(["dot", "-Tpng", "-Gdpi=110", base + ".dot", "-o", base + ".png"], check=True)
        os.remove(base + ".dot")
        manifest.append(dict(key=d["key"], title=d["title"], subtitle=d["subtitle"],
                             processes=[dict(id=n, name=nm.replace("\n", " ")) for n, nm, _ in d["processes"].values()],
                             flows=len(d["flows"])))
        print("made", os.path.basename(base) + ".svg / .png", f'({len(d["flows"])} flows)')
    json.dump(dict(diagrams=manifest, stores=STORES, entities=ENT), open(os.path.join(OUT, "dfd-manifest.json"), "w", encoding="utf-8"), indent=1)

if __name__ == "__main__":
    main()
