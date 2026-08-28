CREATE TABLE contact_region (
    region_id            INT IDENTITY PRIMARY KEY,
    region_name          NVARCHAR(200) NOT NULL UNIQUE,
    region_type          VARCHAR(30) NOT NULL,
    source_table_id      VARCHAR(20) NULL,
    display_order        INT NOT NULL,
    is_active            BIT NOT NULL DEFAULT 1
);

CREATE TABLE contact_unit (
    unit_id              INT IDENTITY PRIMARY KEY,
    region_id            INT NOT NULL FOREIGN KEY REFERENCES contact_region(region_id),
    parent_unit_id       INT NULL FOREIGN KEY REFERENCES contact_unit(unit_id),
    unit_type            VARCHAR(30) NOT NULL,
    unit_name            NVARCHAR(250) NOT NULL,
    source_row_order     INT NOT NULL,
    notes                NVARCHAR(500) NULL,
    is_active            BIT NOT NULL DEFAULT 1
);

CREATE TABLE contact_method (
    contact_method_id    INT IDENTITY PRIMARY KEY,
    unit_id              INT NOT NULL FOREIGN KEY REFERENCES contact_unit(unit_id),
    method_type          VARCHAR(30) NOT NULL,
    contact_value        NVARCHAR(100) NOT NULL,
    raw_value            NVARCHAR(250) NULL,
    is_primary           BIT NOT NULL DEFAULT 0,
    display_order        INT NOT NULL DEFAULT 1,
    is_active            BIT NOT NULL DEFAULT 1
);

CREATE TABLE operating_hours (
    operating_hours_id   INT IDENTITY PRIMARY KEY,
    unit_id              INT NOT NULL FOREIGN KEY REFERENCES contact_unit(unit_id),
    hours_type           VARCHAR(30) NOT NULL,
    raw_text             NVARCHAR(200) NULL,
    days_label           NVARCHAR(100) NULL,
    start_time           TIME NULL,
    end_time             TIME NULL,
    is_24_hours          BIT NOT NULL DEFAULT 0,
    is_on_call           BIT NOT NULL DEFAULT 0,
    is_active            BIT NOT NULL DEFAULT 1
);

CREATE TABLE source_extract_audit (
    audit_id             INT IDENTITY PRIMARY KEY,
    source_file          NVARCHAR(300) NOT NULL,
    source_section       NVARCHAR(50) NOT NULL,
    source_line_start    INT NOT NULL,
    source_line_end      INT NOT NULL,
    extracted_at_utc     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
