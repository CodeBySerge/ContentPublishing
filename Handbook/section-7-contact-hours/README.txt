Section 7.0 Contact & Hours Handoff

Source
- File: /home/runner/work/ContentPublishing/ContentPublishing/Handbook.txt
- Section: SECTION 7.0 - CONTACT & HOURS of OPERATION
- Source lines: 445-4439

Folder contents
- section7-contact-hours.raw.json: raw extracted province, manager, and lab/service contact data.
- section7-contact-hours.schema.sql: stable SQL schema for storing and updating the data dynamically.

Summary
- Regions: 11
- Manager/business area rows: 48
- Lab/service rows: 432
- Blank lab-name rows: 9
- Blank lab-telephone rows: 53

Notes for the next agent
- Preserve raw strings exactly during import because the source contains embedded line breaks and mixed time formats.
- Several rows are wrapped across multiple entries, so normalization should happen after the raw load.
- Phone fields can contain multiple numbers, extensions, or trailing separators.
- Call-out values may contain true callout labels or hotline text and should be reviewed during normalization.
- The raw extract preserves the original source table_id and also adds a unique table_key per region for downstream import mapping.
- Load one source_extract_audit row first, then stamp contact_region.extract_audit_id on imported region rows for traceability.
