--
-- PostgreSQL database dump
--

\restrict CQTkrSR6VpB2tdSKo2fEbJ58f9VipTzOupQI0Nrc7VwBw56pJJLzyCDaXsW7SMv

-- Dumped from database version 18.3
-- Dumped by pg_dump version 18.3

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: pg_trgm; Type: EXTENSION; Schema: -; Owner: -
--

CREATE EXTENSION IF NOT EXISTS pg_trgm WITH SCHEMA public;


--
-- Name: EXTENSION pg_trgm; Type: COMMENT; Schema: -; Owner: 
--

COMMENT ON EXTENSION pg_trgm IS 'text similarity measurement and index searching based on trigrams';


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: audit_logs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.audit_logs (
    id integer NOT NULL,
    user_id integer,
    action character varying(50) NOT NULL,
    details text,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.audit_logs OWNER TO postgres;

--
-- Name: audit_logs_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.audit_logs_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.audit_logs_id_seq OWNER TO postgres;

--
-- Name: audit_logs_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.audit_logs_id_seq OWNED BY public.audit_logs.id;


--
-- Name: categories; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.categories (
    id integer NOT NULL,
    name text NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.categories OWNER TO postgres;

--
-- Name: categories_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.categories_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.categories_id_seq OWNER TO postgres;

--
-- Name: categories_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.categories_id_seq OWNED BY public.categories.id;


--
-- Name: customers; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.customers (
    id integer NOT NULL,
    customer_name text NOT NULL,
    phone text,
    email text,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.customers OWNER TO postgres;

--
-- Name: customers_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.customers_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.customers_id_seq OWNER TO postgres;

--
-- Name: customers_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.customers_id_seq OWNED BY public.customers.id;


--
-- Name: error_logs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.error_logs (
    id integer NOT NULL,
    message text NOT NULL,
    stack_trace text,
    source text,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.error_logs OWNER TO postgres;

--
-- Name: error_logs_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.error_logs_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.error_logs_id_seq OWNER TO postgres;

--
-- Name: error_logs_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.error_logs_id_seq OWNED BY public.error_logs.id;


--
-- Name: inventory_batches; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.inventory_batches (
    id integer NOT NULL,
    medicine_id integer NOT NULL,
    supplier_id integer,
    batch_no text CONSTRAINT inventory_batches_batch_number_not_null NOT NULL,
    unit_cost numeric DEFAULT 0 CONSTRAINT inventory_batches_purchase_price_not_null NOT NULL,
    selling_price numeric DEFAULT 0 NOT NULL,
    remaining_units integer DEFAULT 0 CONSTRAINT inventory_batches_stock_qty_not_null NOT NULL,
    manufacture_date date,
    expiry_date date NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    quantity_units integer DEFAULT 0 NOT NULL,
    purchase_total_price numeric DEFAULT 0 NOT NULL,
    invoice_no text,
    invoice_date date,
    payment_status text DEFAULT 'Cash'::text NOT NULL
);


ALTER TABLE public.inventory_batches OWNER TO postgres;

--
-- Name: inventory_batches_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.inventory_batches_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.inventory_batches_id_seq OWNER TO postgres;

--
-- Name: inventory_batches_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.inventory_batches_id_seq OWNED BY public.inventory_batches.id;


--
-- Name: manufacturers; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.manufacturers (
    id integer NOT NULL,
    name text NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.manufacturers OWNER TO postgres;

--
-- Name: manufacturers_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.manufacturers_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.manufacturers_id_seq OWNER TO postgres;

--
-- Name: manufacturers_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.manufacturers_id_seq OWNED BY public.manufacturers.id;


--
-- Name: medicines; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.medicines (
    id integer NOT NULL,
    name text NOT NULL,
    generic_name text,
    category_id integer,
    manufacturer_id integer,
    dosage_form text,
    strength text,
    barcode text,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    gst_percent numeric DEFAULT 0 NOT NULL
);


ALTER TABLE public.medicines OWNER TO postgres;

--
-- Name: medicines_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.medicines_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.medicines_id_seq OWNER TO postgres;

--
-- Name: medicines_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.medicines_id_seq OWNED BY public.medicines.id;


--
-- Name: sale_items; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.sale_items (
    id integer NOT NULL,
    sale_id integer NOT NULL,
    medicine_id integer,
    batch_id integer,
    quantity integer DEFAULT 1 NOT NULL,
    unit_price numeric NOT NULL,
    subtotal numeric NOT NULL,
    returned_qty integer DEFAULT 0 NOT NULL
);


ALTER TABLE public.sale_items OWNER TO postgres;

--
-- Name: sale_items_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.sale_items_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.sale_items_id_seq OWNER TO postgres;

--
-- Name: sale_items_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.sale_items_id_seq OWNED BY public.sale_items.id;


--
-- Name: sales; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.sales (
    id integer NOT NULL,
    bill_no text NOT NULL,
    user_id integer NOT NULL,
    customer_id integer,
    total_amount numeric DEFAULT 0 NOT NULL,
    tax_amount numeric DEFAULT 0 NOT NULL,
    discount_amount numeric DEFAULT 0 NOT NULL,
    grand_total numeric DEFAULT 0 NOT NULL,
    sale_date timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    fbr_invoice_no text,
    fbr_response text,
    status character varying(20) DEFAULT 'Completed'::character varying NOT NULL,
    fbr_reported boolean DEFAULT false
);


ALTER TABLE public.sales OWNER TO postgres;

--
-- Name: sales_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.sales_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.sales_id_seq OWNER TO postgres;

--
-- Name: sales_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.sales_id_seq OWNED BY public.sales.id;


--
-- Name: settings; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.settings (
    key text NOT NULL,
    value text
);


ALTER TABLE public.settings OWNER TO postgres;

--
-- Name: suppliers; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.suppliers (
    id integer NOT NULL,
    name text NOT NULL,
    phone text,
    address text,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.suppliers OWNER TO postgres;

--
-- Name: suppliers_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.suppliers_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.suppliers_id_seq OWNER TO postgres;

--
-- Name: suppliers_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.suppliers_id_seq OWNED BY public.suppliers.id;


--
-- Name: users; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.users (
    id integer NOT NULL,
    username character varying(50) NOT NULL,
    password text NOT NULL,
    full_name text NOT NULL,
    role character varying(20) DEFAULT 'Admin'::character varying NOT NULL,
    status character varying(20) DEFAULT 'Active'::character varying NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    must_change_password boolean DEFAULT false NOT NULL
);


ALTER TABLE public.users OWNER TO postgres;

--
-- Name: users_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.users_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.users_id_seq OWNER TO postgres;

--
-- Name: users_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.users_id_seq OWNED BY public.users.id;


--
-- Name: audit_logs id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.audit_logs ALTER COLUMN id SET DEFAULT nextval('public.audit_logs_id_seq'::regclass);


--
-- Name: categories id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.categories ALTER COLUMN id SET DEFAULT nextval('public.categories_id_seq'::regclass);


--
-- Name: customers id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customers ALTER COLUMN id SET DEFAULT nextval('public.customers_id_seq'::regclass);


--
-- Name: error_logs id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.error_logs ALTER COLUMN id SET DEFAULT nextval('public.error_logs_id_seq'::regclass);


--
-- Name: inventory_batches id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.inventory_batches ALTER COLUMN id SET DEFAULT nextval('public.inventory_batches_id_seq'::regclass);


--
-- Name: manufacturers id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.manufacturers ALTER COLUMN id SET DEFAULT nextval('public.manufacturers_id_seq'::regclass);


--
-- Name: medicines id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.medicines ALTER COLUMN id SET DEFAULT nextval('public.medicines_id_seq'::regclass);


--
-- Name: sale_items id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sale_items ALTER COLUMN id SET DEFAULT nextval('public.sale_items_id_seq'::regclass);


--
-- Name: sales id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales ALTER COLUMN id SET DEFAULT nextval('public.sales_id_seq'::regclass);


--
-- Name: suppliers id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.suppliers ALTER COLUMN id SET DEFAULT nextval('public.suppliers_id_seq'::regclass);


--
-- Name: users id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users ALTER COLUMN id SET DEFAULT nextval('public.users_id_seq'::regclass);


--
-- Data for Name: audit_logs; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.audit_logs (id, user_id, action, details, created_at) FROM stdin;
1	1	Login	User Admin logged in successfully.	2026-03-15 02:22:46.764967+05
2	1	Login	User Admin logged in successfully.	2026-03-15 02:27:22.295061+05
3	1	Login	User Admin logged in successfully.	2026-03-15 02:38:37.810401+05
4	1	Login	User Admin logged in successfully.	2026-03-15 02:45:13.083356+05
5	1	Login	User Admin logged in successfully.	2026-03-15 02:53:03.967991+05
6	1	Login	User Admin logged in successfully.	2026-03-15 03:07:06.03434+05
7	1	Login	User Admin logged in successfully.	2026-03-15 03:10:03.983907+05
8	1	Login	User Admin logged in successfully.	2026-03-15 03:15:20.068781+05
9	1	Login	User Admin logged in successfully.	2026-03-15 03:48:00.458451+05
10	1	Login	User Admin logged in successfully.	2026-03-15 03:53:54.793307+05
11	1	Sale Created	Bill No: BILL-27192162, Total: 33.00	2026-03-15 03:55:52.772387+05
12	1	Login	User Admin logged in successfully.	2026-03-16 23:21:44.991533+05
13	1	Login	User Admin logged in successfully.	2026-03-16 23:34:03.964346+05
14	1	Login	User Admin logged in successfully.	2026-03-16 23:43:50.10764+05
15	1	Login	User Admin logged in successfully.	2026-03-17 00:14:44.78982+05
16	1	Login	User Admin logged in successfully.	2026-03-17 00:21:19.011431+05
17	1	Login	User Admin logged in successfully.	2026-03-17 01:48:07.551976+05
18	1	Login	User Admin logged in successfully.	2026-03-17 01:53:18.93315+05
19	1	Logout	User logged out.	2026-03-17 01:53:48.162865+05
20	1	Login	User Admin logged in successfully.	2026-03-17 01:53:56.075002+05
21	1	Login	User Admin logged in successfully.	2026-03-17 01:58:07.967027+05
22	1	Logout	User logged out.	2026-03-17 01:58:12.156494+05
23	1	Login	User Admin logged in successfully.	2026-03-17 01:58:19.438542+05
24	1	Login	User Admin logged in successfully.	2026-03-17 02:05:42.509996+05
25	1	Login	User Admin logged in successfully.	2026-03-17 02:14:27.739908+05
26	1	Login	User Admin logged in successfully.	2026-03-17 02:21:03.830759+05
27	1	Login	User Admin logged in successfully.	2026-03-17 02:26:56.525924+05
28	1	Login	User Admin logged in successfully.	2026-03-17 02:29:58.035233+05
29	1	Stock Adjustment	Manual adjustment for Batch 092: 9 -> 15. Reason: Corrected	2026-03-17 02:30:35.155942+05
30	1	Sale Created	Bill No: INV-26160666, Total: 219.45	2026-03-17 02:31:42.685291+05
31	1	Login	User Admin logged in successfully.	2026-03-17 02:34:15.109079+05
32	1	Logout	User logged out.	2026-03-17 02:34:35.281581+05
33	1	Login	User Admin logged in successfully.	2026-03-17 12:57:20.726975+05
35	1	Login	User Admin logged in successfully.	2026-03-17 13:14:44.80236+05
36	1	Login	User Admin logged in successfully.	2026-03-17 22:35:44.19698+05
37	1	Sale Created	Bill No: INV-26393299, Total: 105.60	2026-03-17 22:39:22.758414+05
38	1	Sale Created	Bill No: INV-68831607, Total: 26.40	2026-03-17 22:41:16.993399+05
39	1	Login	User Admin logged in successfully.	2026-03-17 22:47:32.561483+05
40	1	Sale Created	Bill No: INV-41533682, Total: 33.00	2026-03-17 22:47:44.213311+05
41	1	Login	User Admin logged in successfully.	2026-03-17 23:33:16.572708+05
42	1	Sale Created	Bill No: INV-53773412, Total: 33.00	2026-03-17 23:33:25.456528+05
43	1	Sale Created	Bill No: INV-34522335, Total: 33.00	2026-03-17 23:36:13.469511+05
44	1	Sale Created	Bill No: INV-54323231, Total: 33.00	2026-03-17 23:39:25.461348+05
45	1	Login	User Admin logged in successfully.	2026-03-17 23:43:30.419859+05
46	1	Sale Created	Bill No: INV-37928021, Total: 66.00	2026-03-17 23:43:43.857407+05
47	1	Sale Created	Bill No: INV-56921828, Total: 165.00	2026-03-17 23:44:25.708981+05
48	1	Login	User Admin logged in successfully.	2026-03-17 23:51:02.736856+05
49	1	Sale Created	Bill No: INV-03534780, Total: 33.00	2026-03-17 23:51:30.414921+05
50	1	Login	User Admin logged in successfully.	2026-03-17 23:54:40.506961+05
51	1	Sale Created	Bill No: INV-27877056, Total: 33.00	2026-03-17 23:55:02.844069+05
52	1	Login	User Admin logged in successfully.	2026-03-17 23:59:23.035185+05
53	1	Sale Created	Bill No: INV-56297127, Total: 33.00	2026-03-17 23:59:35.689068+05
54	1	Sale Created	Bill No: INV-03128413, Total: 207.90	2026-03-18 00:00:10.338606+05
55	1	Login	User Admin logged in successfully.	2026-03-18 00:04:30.325613+05
57	1	Sale Created	Bill No: INV-99505937, Total: 33.00	2026-03-18 00:04:40.006866+05
58	1	Login	User Admin logged in successfully.	2026-03-18 00:19:17.932627+05
59	1	Sale Created	Bill No: INV-43430855, Total: 33.00	2026-03-18 00:19:44.405016+05
60	1	Login	User Admin logged in successfully.	2026-03-18 00:22:51.271335+05
61	1	Login	User Admin logged in successfully.	2026-03-18 00:38:14.081518+05
62	1	Login	User Admin logged in successfully.	2026-03-18 00:49:48.565839+05
63	1	Sale Created	Bill No: INV-32696803, Total: 33.00	2026-03-18 00:50:13.311571+05
64	1	Login	User Admin logged in successfully.	2026-03-18 20:34:54.403918+05
65	1	Login	User Admin logged in successfully.	2026-03-18 21:09:33.952087+05
66	1	Login	User Admin logged in successfully.	2026-03-18 21:18:01.077566+05
67	1	Login	User Admin logged in successfully.	2026-03-18 21:25:12.833306+05
68	1	Login	User Admin logged in successfully.	2026-03-18 21:54:42.537549+05
69	1	Login	User Admin logged in successfully.	2026-03-18 22:35:15.556691+05
70	1	Login	User Admin logged in successfully.	2026-03-18 22:54:52.813773+05
71	1	Login	User Admin logged in successfully.	2026-03-18 23:29:28.524335+05
72	1	Login	User Admin logged in successfully.	2026-03-19 03:27:34.446509+05
74	1	Login	User Admin logged in successfully.	2026-03-19 03:30:40.170729+05
75	1	Login	User Admin logged in successfully.	2026-03-19 22:30:20.232454+05
76	1	Sale Created	Bill No: INV-32774462, Total: 66.33	2026-03-19 22:36:23.532144+05
77	1	Sale Created	Bill No: INV-93437267, Total: 36.00	2026-03-19 22:37:19.374687+05
78	1	Sale Created	Bill No: INV-30054808, Total: 33.00	2026-03-19 22:54:53.122208+05
79	1	Login	User Admin logged in successfully.	2026-03-19 23:16:15.879295+05
80	1	Login	User Admin logged in successfully.	2026-03-19 23:25:22.313189+05
81	1	Login	User Admin logged in successfully.	2026-03-19 23:30:24.539336+05
82	1	Login	User Admin logged in successfully.	2026-03-19 23:37:39.616862+05
83	1	Login	User Admin logged in successfully.	2026-03-19 23:50:50.753977+05
\.


--
-- Data for Name: categories; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.categories (id, name, created_at) FROM stdin;
1	Pain Killer	2026-03-15 02:22:36.801419+05
2	Antibiotic	2026-03-15 02:22:36.801419+05
3	Cough Syrup	2026-03-15 02:22:36.801419+05
6	Capsules	2026-03-15 02:46:46.743542+05
9	General	2026-03-18 22:57:44.942358+05
\.


--
-- Data for Name: customers; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.customers (id, customer_name, phone, email, created_at) FROM stdin;
\.


--
-- Data for Name: error_logs; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.error_logs (id, message, stack_trace, source, created_at) FROM stdin;
1	Catastrophic failure\r\n	\N	\N	2026-03-17 02:09:12.357391+05
2	Catastrophic failure\r\n	\N	\N	2026-03-17 02:14:49.096293+05
\.


--
-- Data for Name: inventory_batches; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.inventory_batches (id, medicine_id, supplier_id, batch_no, unit_cost, selling_price, remaining_units, manufacture_date, expiry_date, created_at, quantity_units, purchase_total_price, invoice_no, invoice_date, payment_status) FROM stdin;
12	14	2	DNG014	30.866666666666666666666666667	33.333333333333333333333333333	29	\N	2028-11-12	2026-03-19 03:07:35.09752+05	30	926		2026-03-19	Cash
15	17	2	ACG157	2.4	3	99	\N	2028-12-12	2026-03-19 03:07:35.09752+05	100	240		2026-03-19	Cash
5	5	3	092	28	33	97	\N	2026-05-21	2026-03-15 03:55:01.531059+05	10	280		2026-03-15	Cash
11	13	2	CNH006	13.07	15	100	\N	2029-01-01	2026-03-19 03:07:35.09752+05	100	1307.00		2026-03-19	Cash
6	6	3	H5925	30	35	30	\N	2030-10-24	2026-03-18 22:03:22.534846+05	30	900		2026-03-18	Cash
13	15	2	HFG008	14.571428571428571428571428571	17.857142857142857142857142857	14	\N	2027-12-05	2026-03-19 03:07:35.09752+05	14	204		2026-03-19	Cash
14	16	2	KSG010	22.5	28.571428571428571428571428571	14	\N	2027-12-12	2026-03-19 03:07:35.09752+05	14	315		2026-03-19	Cash
16	18	2	AEG002	15.6	20	20	\N	2028-10-29	2026-03-19 03:07:35.09752+05	20	312		2026-03-19	Cash
17	19	2	FRG016	27.266666666666666666666666667	30	30	\N	2027-11-11	2026-03-19 03:07:35.09752+05	30	818		2026-03-19	Cash
18	20	2	FSG020	31.928571428571428571428571429	35.714285714285714285714285714	14	\N	2027-10-24	2026-03-19 03:07:35.09752+05	14	447		2026-03-19	Cash
19	21	2	AYG010	23.857142857142857142857142857	28.571428571428571428571428571	14	\N	2027-07-15	2026-03-19 03:07:35.09752+05	14	334		2026-03-19	Cash
20	22	2	AZG008	31.928571428571428571428571429	35.714285714285714285714285714	14	\N	2027-08-15	2026-03-19 03:07:35.09752+05	14	447		2026-03-19	Cash
21	23	2	CCG015	31.714285714285714285714285714	35.714285714285714285714285714	14	\N	2027-11-03	2026-03-19 03:07:35.09752+05	14	444		2026-03-19	Cash
22	24	2	CAG011	31.142857142857142857142857143	35.714285714285714285714285714	14	\N	2027-08-08	2026-03-19 03:07:35.09752+05	14	436		2026-03-19	Cash
23	25	2	ANG082	3.715302491103202846975088968	4.2704626334519572953736654804	281	\N	2027-12-24	2026-03-19 03:07:35.09752+05	281	1044		2026-03-19	Cash
24	26	2	AMG076	31.571428571428571428571428571	35.714285714285714285714285714	28	\N	2027-12-03	2026-03-19 03:07:35.09752+05	28	884		2026-03-19	Cash
25	27	2	DVG015	21.035714285714285714285714286	25	28	\N	2027-11-17	2026-03-19 03:07:35.09752+05	28	589		2026-03-19	Cash
26	28	2	DWH004	37.285714285714285714285714286	42.857142857142857142857142857	28	\N	2028-01-05	2026-03-19 03:07:35.09752+05	28	1044		2026-03-19	Cash
27	29	2	FCG124	93.2	100	5	\N	2028-11-15	2026-03-19 03:07:35.09752+05	5	466		2026-03-19	Cash
28	30	2	APH002	2.55	3	200	\N	2031-02-02	2026-03-19 03:07:35.09752+05	200	510		2026-03-19	Cash
29	31	2	FJG046	170	200	15	\N	2027-09-27	2026-03-19 03:07:35.09752+05	15	2550		2026-03-19	Cash
30	32	2	FKG002	170	200	5	\N	2027-11-11	2026-03-19 03:07:35.09752+05	5	850		2026-03-19	Cash
31	33	2	FHG352	148.75	165	20	\N	2027-11-20	2026-03-19 03:07:35.09752+05	20	2975		2026-03-19	Cash
32	34	2	CHG052	32.785714285714285714285714286	39.285714285714285714285714286	14	\N	2027-11-03	2026-03-19 03:07:35.09752+05	14	459		2026-03-19	Cash
33	35	2	CGG035	32.357142857142857142857142857	37.857142857142857142857142857	14	\N	2027-11-16	2026-03-19 03:07:35.09752+05	14	453		2026-03-19	Cash
34	36	2	CPG010	28.3	33.333333333333333333333333333	30	\N	2028-09-09	2026-03-19 03:07:35.09752+05	30	849		2026-03-19	Cash
35	37	2	CMH001	16.966666666666666666666666667	20	30	\N	2029-01-12	2026-03-19 03:07:35.09752+05	30	509		2026-03-19	Cash
36	38	2	CNG012	22.633333333333333333333333333	25	30	\N	2028-10-02	2026-03-19 03:07:35.09752+05	30	679		2026-03-19	Cash
37	39	2	KVG011	476	550	1	\N	2027-12-12	2026-03-19 03:07:35.09752+05	1	476		2026-03-19	Cash
38	40	2	FNH002	314	400	1	\N	2028-01-01	2026-03-19 03:07:35.09752+05	1	314		2026-03-19	Cash
39	41	2	FLG080	114.5	150	2	\N	2027-09-10	2026-03-19 03:07:35.09752+05	2	229		2026-03-19	Cash
40	42	2	ERG053	16.666666666666666666666666667	20	30	\N	2027-10-10	2026-03-19 03:07:35.09752+05	30	500		2026-03-19	Cash
41	43	2	AYG023	13.666666666666666666666666667	16.666666666666666666666666667	15	\N	2029-07-21	2026-03-19 03:07:35.09752+05	15	205		2026-03-19	Cash
42	44	2	CAG152	3.05	4	100	\N	2030-10-02	2026-03-19 03:07:35.09752+05	100	305		2026-03-19	Cash
43	45	2	FMG106	101.25	116.66666666666666666666666667	12	\N	2027-10-11	2026-03-19 03:07:35.09752+05	12	1215		2026-03-19	Cash
44	46	2	DG0623	11.05	12.666666666666666666666666667	300	\N	2028-11-17	2026-03-19 03:07:35.09752+05	300	3315		2026-03-19	Cash
45	47	2	HGG346	170	208.33333333333333333333333333	12	\N	2027-12-14	2026-03-19 03:07:35.09752+05	12	2040		2026-03-19	Cash
46	48	2	SG0786	25.28	28	25	\N	2028-03-15	2026-03-19 03:07:35.09752+05	25	632		2026-03-19	Cash
47	49	2	KFG004	363.5	400	2	\N	2027-09-11	2026-03-19 03:07:35.09752+05	2	727		2026-03-19	Cash
48	50	2	AHH001	9.45	12.5	20	\N	2028-01-01	2026-03-19 03:07:35.09752+05	20	189		2026-03-19	Cash
49	51	2	DRH001	21.85	25	20	\N	2028-01-01	2026-03-19 03:07:35.09752+05	20	437		2026-03-19	Cash
50	52	2	DSG010	22.35	25	20	\N	2027-11-10	2026-03-19 03:07:35.09752+05	20	447		2026-03-19	Cash
51	53	2	AGG027	816	900	1	\N	2027-09-15	2026-03-19 03:07:35.09752+05	1	816		2026-03-19	Cash
52	54	2	DFH002	27.285714285714285714285714286	32.142857142857142857142857143	14	\N	2028-01-01	2026-03-19 03:07:35.09752+05	14	382		2026-03-19	Cash
53	55	2	EHH004	12.15	14.166666666666666666666666667	60	\N	2029-02-07	2026-03-19 03:07:35.09752+05	60	729		2026-03-19	Cash
54	56	2	EGG024	13.6	15.333333333333333333333333333	60	\N	2027-11-18	2026-03-19 03:07:35.09752+05	60	816		2026-03-19	Cash
55	57	2	ENG002	16.5	20	10	\N	2027-08-06	2026-03-19 03:07:35.09752+05	10	165		2026-03-19	Cash
56	58	2	5252026	765	840	5	\N	2028-02-02	2026-03-19 03:07:35.09752+05	5	3825		2026-03-19	Cash
57	59	2	FEG002	45.285714285714285714285714286	50.714285714285714285714285714	14	\N	2027-08-18	2026-03-19 03:07:35.09752+05	14	634		2026-03-19	Cash
\.


--
-- Data for Name: manufacturers; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.manufacturers (id, name, created_at) FROM stdin;
1	GSK	2026-03-15 02:22:36.801419+05
2	Abbott	2026-03-15 02:22:36.801419+05
3	Pfizer	2026-03-15 02:22:36.801419+05
\.


--
-- Data for Name: medicines; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.medicines (id, name, generic_name, category_id, manufacturer_id, dosage_form, strength, barcode, created_at, gst_percent) FROM stdin;
5	Azythra	Azithromycin	6	1	250	250	6168686102549	2026-03-15 03:54:47.439347+05	0
6	Anafortan Plus	Phloroglucinol	2	1		200	02450490	2026-03-18 22:03:07.543503+05	0
14	Andex		9	1	40	40	\N	2026-03-19 02:20:06.900714+05	0
15	BYSCARD		9	1	2.5	2.5	\N	2026-03-19 02:20:47.248236+05	0
16	BYSCARD		9	1	5	5	\N	2026-03-19 02:21:44.44511+05	0
17	CANDEREL		9	1	18	18	\N	2026-03-19 02:22:32.633088+05	0
18	Co-Renitec		9	1	10/20	10/20	\N	2026-03-19 02:24:33.936054+05	0
19	DEXTOP		9	1	30	30	\N	2026-03-19 02:25:16.328769+05	0
20	DEXTOP		9	1	60	60	\N	2026-03-19 02:26:09.131788+05	0
21	EMSYN		9	1	10	10	\N	2026-03-19 02:27:18.144238+05	0
22	EMSYN		9	1	25	25	\N	2026-03-19 02:27:55.581258+05	0
23	EMSYN MET		9	1	12.5+1000	12.5+1000	\N	2026-03-19 02:28:56.56958+05	0
24	EMSYM MET		9	1	12.5+500	12.5+500	\N	2026-03-19 02:29:45.286828+05	0
25	EXTOR		9	1	10/160	10/160	\N	2026-03-19 02:32:02.958621+05	0
26	EXTOR		9	1	5/160	5/160	\N	2026-03-19 02:32:49.994282+05	0
27	EZIUM		9	1	20	20	\N	2026-03-19 02:33:42.277048+05	0
28	EZIUM		9	1	40	40	\N	2026-03-19 02:34:24.88105+05	0
29	GRAVINATE		9	1	12.5	12.5	\N	2026-03-19 02:35:30.252496+05	0
30	GRAVINATE		9	1	50	50	\N	2026-03-19 02:36:22.948615+05	0
31	HYDRYLLIN DM SYRUP		9	1	120	120	\N	2026-03-19 02:37:50.331324+05	0
32	HYDRALLIN SUGAR FREE SYRUP		9	1	120	120	\N	2026-03-19 02:38:49.165749+05	0
33	HYDRALLIN SYRUP		9	1	120	120	\N	2026-03-19 02:40:16.520112+05	0
34	JENTIMET		9	1	50+1000	50+1000	\N	2026-03-19 02:41:12.215826+05	0
35	JENTIMET		9	1	50+500	50+500	\N	2026-03-19 02:42:07.157553+05	0
36	LAMNET		9	1	100	100	\N	2026-03-19 02:43:36.134448+05	0
37	LAMNET		9	1	25	25	\N	2026-03-19 02:44:11.717493+05	0
38	LAMNET		9	1	50	50	\N	2026-03-19 02:45:25.414144+05	0
39	LOVANZO-D		9	1	70+25	70+25	\N	2026-03-19 02:46:33.545131+05	0
40	Maltofer SYRUP		9	1	120	120	\N	2026-03-19 02:47:19.002108+05	0
41	METODINE DF SUSPENSION SYRUP		9	1	90	90	\N	2026-03-19 02:49:18.712205+05	0
42	Maltofer-fol		9	1	100/0.35	100/0.35	\N	2026-03-19 02:50:15.391222+05	0
43	METODINE DF		9	1			\N	2026-03-19 02:51:07.577132+05	0
44	METROZINE		9	1	400	400	\N	2026-03-19 02:51:52.318662+05	0
45	METROZINE SUSP		9	1	200	200	\N	2026-03-19 02:53:01.070407+05	0
46	NUBEROL FORTE		9	1			\N	2026-03-19 02:54:19.477797+05	0
47	PEDITRAL LIQ ORANGE		9	1			\N	2026-03-19 02:55:16.527977+05	0
48	PEDITRAL ORANGE SACHET		9	1			\N	2026-03-19 02:56:20.930024+05	0
49	PRU-CIC		9	1	2	2	\N	2026-03-19 02:57:13.576692+05	0
50	RENITEC		9	1	5	5	\N	2026-03-19 02:58:34.985014+05	0
51	ROTEC		9	1	50	50	\N	2026-03-19 02:59:07.261268+05	0
52	ROTEC		9	1	75	75	\N	2026-03-19 03:00:00.234961+05	0
53	Searle Ostegem OD (Blister)		9	1			\N	2026-03-19 03:00:43.620374+05	0
54	Selanz		9	1	30	30	\N	2026-03-19 03:01:36.159478+05	0
55	SPIROMIDE		9	1	20	20	\N	2026-03-19 03:02:25.946548+05	0
56	SPIROMIDE		9	1	40	40	\N	2026-03-19 03:03:13.944648+05	0
57	TORIB		9	1	60	60	\N	2026-03-19 03:04:00.530216+05	0
58	Venofer		9	1	100	100	\N	2026-03-19 03:05:02.108673+05	0
59	VOCINTI		9	1	20	20	\N	2026-03-19 03:05:43.890803+05	0
13	Aldomet 250MG		9	1	250	250		2026-03-19 02:19:10.227065+05	0
\.


--
-- Data for Name: sale_items; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.sale_items (id, sale_id, medicine_id, batch_id, quantity, unit_price, subtotal, returned_qty) FROM stdin;
1	1	5	5	1	33	33	0
2	2	5	5	7	33	231	0
3	3	5	5	4	33	132	0
4	4	5	5	1	33	33	0
5	5	5	5	1	33	33	0
6	6	5	5	1	33	33	0
7	7	5	5	1	33	33	0
8	8	5	5	1	33	33	0
9	9	5	5	2	33	66	0
10	10	5	5	5	33	165	0
11	11	5	5	1	33	33	0
12	12	5	5	1	33	33	0
13	13	5	5	1	33	33	0
14	14	5	5	7	33	231	0
15	15	5	5	1	33	33	0
16	16	5	5	1	33	33	0
17	17	5	5	1	33	33	0
18	18	5	5	1	33	33	0
19	18	14	12	1	33.333333333333333333333333333	33.333333333333333333333333333	0
20	19	5	5	1	33	33	0
21	19	17	15	1	3	3	0
22	20	5	5	1	33	33	0
\.


--
-- Data for Name: sales; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.sales (id, bill_no, user_id, customer_id, total_amount, tax_amount, discount_amount, grand_total, sale_date, fbr_invoice_no, fbr_response, status, fbr_reported) FROM stdin;
1	BILL-27192162	1	\N	33	0	0	33	2026-03-15 03:55:52.73971+05	\N	\N	Completed	f
2	INV-26160666	1	\N	231	0	11.55	219.45	2026-03-17 02:31:42.625846+05	\N	\N	Completed	f
3	INV-26393299	1	\N	132	0	26.4	105.6	2026-03-17 22:39:22.661868+05	SIM-FBR-20260317-E7EB9D20	{"status":"simulator", "invoice_no":"SIM-FBR-20260317-E7EB9D20"}	Completed	t
4	INV-68831607	1	\N	33	0	6.6	26.4	2026-03-17 22:41:16.883852+05	SIM-FBR-20260317-BF2E1450	{"status":"simulator", "invoice_no":"SIM-FBR-20260317-BF2E1450"}	Completed	t
5	INV-41533682	1	\N	33	0	0	33	2026-03-17 22:47:44.16558+05	SIM-FBR-20260317-CEE1CA19	{"status":"simulator", "invoice_no":"SIM-FBR-20260317-CEE1CA19"}	Completed	t
6	INV-53773412	1	\N	33	0	0	33	2026-03-17 23:33:25.389723+05	SIM-FBR-20260317-AF2F0BCC	{"status":"simulator", "invoice_no":"SIM-FBR-20260317-AF2F0BCC"}	Completed	t
7	INV-34522335	1	\N	33	0	0	33	2026-03-17 23:36:13.452828+05	SIM-FBR-20260317-381F92DE	{"status":"simulator", "invoice_no":"SIM-FBR-20260317-381F92DE"}	Completed	t
8	INV-54323231	1	\N	33	0	0	33	2026-03-17 23:39:25.432813+05	SIM-FBR-20260317-81DBA12F	{"status":"simulator", "invoice_no":"SIM-FBR-20260317-81DBA12F"}	Completed	t
9	INV-37928021	1	\N	66	0	0	66	2026-03-17 23:43:43.809465+05	SIM-FBR-20260317-2E6A7DBF	{"status":"simulator", "invoice_no":"SIM-FBR-20260317-2E6A7DBF"}	Completed	t
10	INV-56921828	1	\N	165	0	0	165	2026-03-17 23:44:25.693064+05	SIM-FBR-20260317-2FD847DA	{"status":"simulator", "invoice_no":"SIM-FBR-20260317-2FD847DA"}	Completed	t
11	INV-03534780	1	\N	33	0	0	33	2026-03-17 23:51:30.362429+05	SIM-FBR-20260317-0A3A5E02	{"status":"simulator", "invoice_no":"SIM-FBR-20260317-0A3A5E02"}	Completed	t
12	INV-27877056	1	\N	33	0	0	33	2026-03-17 23:55:02.796624+05	SIM-FBR-20260317-10FEB115	{"status":"simulator", "invoice_no":"SIM-FBR-20260317-10FEB115"}	Completed	t
13	INV-56297127	1	\N	33	0	0	33	2026-03-17 23:59:35.639503+05	SIM-FBR-20260317-CCF284EB	{"status":"simulator", "invoice_no":"SIM-FBR-20260317-CCF284EB"}	Completed	t
14	INV-03128413	1	\N	231	0	23.1	207.9	2026-03-18 00:00:10.313472+05	SIM-FBR-20260318-AC09B774	{"status":"simulator", "invoice_no":"SIM-FBR-20260318-AC09B774"}	Completed	t
15	INV-99505937	1	\N	33	0	0	33	2026-03-18 00:04:39.961417+05	SIM-FBR-20260318-181A6EE4	{"status":"simulator", "invoice_no":"SIM-FBR-20260318-181A6EE4"}	Completed	t
16	INV-43430855	1	\N	33	0	0	33	2026-03-18 00:19:44.354282+05	SIM-FBR-20260318-6DACD67E	{"status":"simulator", "invoice_no":"SIM-FBR-20260318-6DACD67E"}	Completed	t
17	INV-32696803	1	\N	33	0	0	33	2026-03-18 00:50:13.275643+05	SIM-FBR-20260318-70E96E51	{"status":"simulator", "invoice_no":"SIM-FBR-20260318-70E96E51"}	Completed	t
18	INV-32774462	1	\N	66.333333333333333333333333333	0	0	66.333333333333333333333333333	2026-03-19 22:36:23.288569+05	SIM-FBR-20260319-E151CF77	{"status":"simulator", "invoice_no":"SIM-FBR-20260319-E151CF77"}	Completed	t
19	INV-93437267	1	\N	36	0	0	36	2026-03-19 22:37:19.344193+05	SIM-FBR-20260319-885FCD67	{"status":"simulator", "invoice_no":"SIM-FBR-20260319-885FCD67"}	Completed	t
20	INV-30054808	1	\N	33	0	0	33	2026-03-19 22:54:53.005924+05	SIM-FBR-20260319-05F39174	{"status":"simulator", "invoice_no":"SIM-FBR-20260319-05F39174"}	Completed	t
\.


--
-- Data for Name: settings; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.settings (key, value) FROM stdin;
tax_rate	0.0
fbr_is_live	false
fbr_pos_id	DChemist-POS-001
fbr_api_url	https://ims.fbr.gov.pk/api/v3/Post/PostInvoice
fbr_token	
pharmacy_name	D. Chemist
pharmacy_address	Khewra Road, Choa Saidan Shah, District Chakwal
pharmacy_phone	+92-332-8787833
pharmacy_license	01-372-0011-134212M
pharmacy_ntn	I736466-5
printer_name	POS-80-Series
silent_print_enabled	true
last_backup_date	2026-03-19
\.


--
-- Data for Name: suppliers; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.suppliers (id, name, phone, address, created_at) FROM stdin;
1	ABC Pharma	0300-1234567	Phase 6, Hayatabad, Peshawar	2026-03-15 02:22:36.801419+05
2	Zeeshan	\N	\N	2026-03-15 02:47:42.867375+05
3	Hasnain	\N	\N	2026-03-15 03:55:01.508731+05
\.


--
-- Data for Name: users; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public.users (id, username, password, full_name, role, status, created_at, must_change_password) FROM stdin;
1	Admin	$2a$11$LL8f/SDPA6hfuLbKtFfA6.NrvZZxJh0IGRYE9J6cjDFlM6EgVzQri	Administrator	Admin	Active	2026-03-07 23:41:05.93975+05	f
\.


--
-- Name: audit_logs_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.audit_logs_id_seq', 83, true);


--
-- Name: categories_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.categories_id_seq', 9, true);


--
-- Name: customers_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.customers_id_seq', 1, false);


--
-- Name: error_logs_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.error_logs_id_seq', 2, true);


--
-- Name: inventory_batches_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.inventory_batches_id_seq', 57, true);


--
-- Name: manufacturers_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.manufacturers_id_seq', 3, true);


--
-- Name: medicines_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.medicines_id_seq', 59, true);


--
-- Name: sale_items_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.sale_items_id_seq', 22, true);


--
-- Name: sales_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.sales_id_seq', 20, true);


--
-- Name: suppliers_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.suppliers_id_seq', 3, true);


--
-- Name: users_id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.users_id_seq', 1, true);


--
-- Name: audit_logs audit_logs_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.audit_logs
    ADD CONSTRAINT audit_logs_pkey PRIMARY KEY (id);


--
-- Name: categories categories_name_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.categories
    ADD CONSTRAINT categories_name_key UNIQUE (name);


--
-- Name: categories categories_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.categories
    ADD CONSTRAINT categories_pkey PRIMARY KEY (id);


--
-- Name: customers customers_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.customers
    ADD CONSTRAINT customers_pkey PRIMARY KEY (id);


--
-- Name: error_logs error_logs_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.error_logs
    ADD CONSTRAINT error_logs_pkey PRIMARY KEY (id);


--
-- Name: inventory_batches inventory_batches_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.inventory_batches
    ADD CONSTRAINT inventory_batches_pkey PRIMARY KEY (id);


--
-- Name: manufacturers manufacturers_name_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.manufacturers
    ADD CONSTRAINT manufacturers_name_key UNIQUE (name);


--
-- Name: manufacturers manufacturers_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.manufacturers
    ADD CONSTRAINT manufacturers_pkey PRIMARY KEY (id);


--
-- Name: medicines medicines_barcode_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.medicines
    ADD CONSTRAINT medicines_barcode_key UNIQUE (barcode);


--
-- Name: medicines medicines_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.medicines
    ADD CONSTRAINT medicines_pkey PRIMARY KEY (id);


--
-- Name: sale_items sale_items_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sale_items
    ADD CONSTRAINT sale_items_pkey PRIMARY KEY (id);


--
-- Name: sales sales_bill_no_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales
    ADD CONSTRAINT sales_bill_no_key UNIQUE (bill_no);


--
-- Name: sales sales_fbr_invoice_no_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales
    ADD CONSTRAINT sales_fbr_invoice_no_key UNIQUE (fbr_invoice_no);


--
-- Name: sales sales_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales
    ADD CONSTRAINT sales_pkey PRIMARY KEY (id);


--
-- Name: settings settings_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.settings
    ADD CONSTRAINT settings_pkey PRIMARY KEY (key);


--
-- Name: suppliers suppliers_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.suppliers
    ADD CONSTRAINT suppliers_pkey PRIMARY KEY (id);


--
-- Name: users users_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (id);


--
-- Name: users users_username_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_username_key UNIQUE (username);


--
-- Name: idx_audit_logs_created_at; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_audit_logs_created_at ON public.audit_logs USING btree (created_at DESC);


--
-- Name: idx_batches_expiry; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_batches_expiry ON public.inventory_batches USING btree (expiry_date);


--
-- Name: idx_batches_expiry_date; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_batches_expiry_date ON public.inventory_batches USING btree (expiry_date);


--
-- Name: idx_batches_medicine_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_batches_medicine_id ON public.inventory_batches USING btree (medicine_id);


--
-- Name: idx_batches_stock_positive; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_batches_stock_positive ON public.inventory_batches USING btree (remaining_units) WHERE (remaining_units > 0);


--
-- Name: idx_error_logs_created_at; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_error_logs_created_at ON public.error_logs USING btree (created_at DESC);


--
-- Name: idx_medicines_barcode; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_medicines_barcode ON public.medicines USING btree (barcode);


--
-- Name: idx_medicines_generic_lower; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_medicines_generic_lower ON public.medicines USING btree (lower(generic_name));


--
-- Name: idx_medicines_generic_trgm; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_medicines_generic_trgm ON public.medicines USING gist (generic_name public.gist_trgm_ops);


--
-- Name: idx_medicines_name; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_medicines_name ON public.medicines USING btree (name);


--
-- Name: idx_medicines_name_lower; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_medicines_name_lower ON public.medicines USING btree (lower(name));


--
-- Name: idx_medicines_name_trgm; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_medicines_name_trgm ON public.medicines USING gist (name public.gist_trgm_ops);


--
-- Name: idx_sale_items_sale_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_sale_items_sale_id ON public.sale_items USING btree (sale_id);


--
-- Name: idx_sales_bill_no; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_sales_bill_no ON public.sales USING btree (bill_no);


--
-- Name: idx_sales_date_desc; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_sales_date_desc ON public.sales USING btree (sale_date DESC);


--
-- Name: idx_sales_sale_date; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_sales_sale_date ON public.sales USING btree (sale_date);


--
-- Name: audit_logs audit_logs_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.audit_logs
    ADD CONSTRAINT audit_logs_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE SET NULL;


--
-- Name: inventory_batches inventory_batches_medicine_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.inventory_batches
    ADD CONSTRAINT inventory_batches_medicine_id_fkey FOREIGN KEY (medicine_id) REFERENCES public.medicines(id) ON DELETE CASCADE;


--
-- Name: inventory_batches inventory_batches_supplier_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.inventory_batches
    ADD CONSTRAINT inventory_batches_supplier_id_fkey FOREIGN KEY (supplier_id) REFERENCES public.suppliers(id) ON DELETE RESTRICT;


--
-- Name: medicines medicines_category_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.medicines
    ADD CONSTRAINT medicines_category_id_fkey FOREIGN KEY (category_id) REFERENCES public.categories(id) ON DELETE SET NULL;


--
-- Name: medicines medicines_manufacturer_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.medicines
    ADD CONSTRAINT medicines_manufacturer_id_fkey FOREIGN KEY (manufacturer_id) REFERENCES public.manufacturers(id) ON DELETE SET NULL;


--
-- Name: sale_items sale_items_batch_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sale_items
    ADD CONSTRAINT sale_items_batch_id_fkey FOREIGN KEY (batch_id) REFERENCES public.inventory_batches(id);


--
-- Name: sale_items sale_items_medicine_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sale_items
    ADD CONSTRAINT sale_items_medicine_id_fkey FOREIGN KEY (medicine_id) REFERENCES public.medicines(id);


--
-- Name: sale_items sale_items_sale_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sale_items
    ADD CONSTRAINT sale_items_sale_id_fkey FOREIGN KEY (sale_id) REFERENCES public.sales(id) ON DELETE CASCADE;


--
-- Name: sales sales_customer_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales
    ADD CONSTRAINT sales_customer_id_fkey FOREIGN KEY (customer_id) REFERENCES public.customers(id);


--
-- Name: sales sales_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sales
    ADD CONSTRAINT sales_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id);


--
-- PostgreSQL database dump complete
--

\unrestrict CQTkrSR6VpB2tdSKo2fEbJ58f9VipTzOupQI0Nrc7VwBw56pJJLzyCDaXsW7SMv

