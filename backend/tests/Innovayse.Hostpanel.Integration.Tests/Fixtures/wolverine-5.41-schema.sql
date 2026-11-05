-- The message store schema Wolverine 5.41.0 created on PostgreSQL (schema wolverine), dumped with
-- pg_dump --schema-only --schema=wolverine from a database that a 5.41.0 host had started on. It is
-- the starting point WolverineStartupTests upgrades with the current version. The tables are the
-- framework's (lower case, owned by Wolverine), so they are exempt from the PascalCase rule.

CREATE SCHEMA wolverine;

CREATE TABLE wolverine.wolverine_agent_restrictions (
    id uuid NOT NULL,
    uri character varying NOT NULL,
    type character varying NOT NULL,
    node integer DEFAULT 0 NOT NULL
);

CREATE TABLE wolverine.wolverine_control_queue (
    id uuid NOT NULL,
    message_type character varying NOT NULL,
    node_id uuid NOT NULL,
    body bytea NOT NULL,
    posted timestamp with time zone DEFAULT now() NOT NULL,
    expires timestamp with time zone
);

CREATE TABLE wolverine.wolverine_dead_letters (
    id uuid NOT NULL,
    execution_time timestamp with time zone,
    body bytea NOT NULL,
    message_type character varying NOT NULL,
    received_at character varying,
    source character varying,
    exception_type character varying,
    exception_message character varying,
    sent_at timestamp with time zone,
    replayable boolean
);

CREATE TABLE wolverine.wolverine_incoming_envelopes (
    id uuid NOT NULL,
    status character varying NOT NULL,
    owner_id integer NOT NULL,
    execution_time timestamp with time zone,
    attempts integer DEFAULT 0,
    body bytea NOT NULL,
    message_type character varying NOT NULL,
    received_at character varying,
    keep_until timestamp with time zone
);

CREATE TABLE wolverine.wolverine_node_assignments (
    id character varying NOT NULL,
    node_id uuid,
    started timestamp with time zone DEFAULT now() NOT NULL
);

CREATE TABLE wolverine.wolverine_node_records (
    id integer NOT NULL,
    node_number integer NOT NULL,
    event_name character varying NOT NULL,
    "timestamp" timestamp with time zone DEFAULT now() NOT NULL,
    description character varying
);

CREATE SEQUENCE wolverine.wolverine_node_records_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

ALTER SEQUENCE wolverine.wolverine_node_records_id_seq OWNED BY wolverine.wolverine_node_records.id;

CREATE TABLE wolverine.wolverine_nodes (
    id uuid NOT NULL,
    node_number integer NOT NULL,
    description character varying NOT NULL,
    uri character varying NOT NULL,
    started timestamp with time zone DEFAULT now() NOT NULL,
    health_check timestamp with time zone DEFAULT now() NOT NULL,
    version character varying,
    capabilities text[]
);

CREATE SEQUENCE wolverine.wolverine_nodes_node_number_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

ALTER SEQUENCE wolverine.wolverine_nodes_node_number_seq OWNED BY wolverine.wolverine_nodes.node_number;

CREATE TABLE wolverine.wolverine_outgoing_envelopes (
    id uuid NOT NULL,
    owner_id integer NOT NULL,
    destination character varying NOT NULL,
    deliver_by timestamp with time zone,
    body bytea NOT NULL,
    attempts integer DEFAULT 0,
    message_type character varying NOT NULL
);

ALTER TABLE ONLY wolverine.wolverine_node_records ALTER COLUMN id SET DEFAULT nextval('wolverine.wolverine_node_records_id_seq'::regclass);

ALTER TABLE ONLY wolverine.wolverine_nodes ALTER COLUMN node_number SET DEFAULT nextval('wolverine.wolverine_nodes_node_number_seq'::regclass);

ALTER TABLE ONLY wolverine.wolverine_agent_restrictions
    ADD CONSTRAINT pkey_wolverine_agent_restrictions_id PRIMARY KEY (id);

ALTER TABLE ONLY wolverine.wolverine_control_queue
    ADD CONSTRAINT pkey_wolverine_control_queue_id PRIMARY KEY (id);

ALTER TABLE ONLY wolverine.wolverine_dead_letters
    ADD CONSTRAINT pkey_wolverine_dead_letters_id PRIMARY KEY (id);

ALTER TABLE ONLY wolverine.wolverine_incoming_envelopes
    ADD CONSTRAINT pkey_wolverine_incoming_envelopes_id PRIMARY KEY (id);

ALTER TABLE ONLY wolverine.wolverine_node_assignments
    ADD CONSTRAINT pkey_wolverine_node_assignments_id PRIMARY KEY (id);

ALTER TABLE ONLY wolverine.wolverine_node_records
    ADD CONSTRAINT pkey_wolverine_node_records_id PRIMARY KEY (id);

ALTER TABLE ONLY wolverine.wolverine_nodes
    ADD CONSTRAINT pkey_wolverine_nodes_id PRIMARY KEY (id);

ALTER TABLE ONLY wolverine.wolverine_outgoing_envelopes
    ADD CONSTRAINT pkey_wolverine_outgoing_envelopes_id PRIMARY KEY (id);

ALTER TABLE ONLY wolverine.wolverine_node_assignments
    ADD CONSTRAINT fkey_wolverine_node_assignments_node_id FOREIGN KEY (node_id) REFERENCES wolverine.wolverine_nodes(id) ON DELETE CASCADE;

