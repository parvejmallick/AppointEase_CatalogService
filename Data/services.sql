CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE IF NOT EXISTS services (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name VARCHAR(255) NOT NULL UNIQUE,
  description TEXT NOT NULL,
  price NUMERIC(10, 2) NOT NULL CHECK (price >= 0),
  duration_minutes INT NOT NULL CHECK (duration_minutes > 0),
  description_embedding vector(1536),
  created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE services ADD COLUMN IF NOT EXISTS embedding_model TEXT;

CREATE UNIQUE INDEX IF NOT EXISTS ux_services_name ON services (name);

INSERT INTO
  services (id, name, description, price, duration_minutes)
VALUES
  (
    '10000000-0000-0000-0000-000000000001',
    'Women''s Salon & Spa',
    'Haircuts, coloring, waxing, facials, manicures, pedicures, and massage therapies.',
    25.00,
    60
  ),
  (
    '10000000-0000-0000-0000-000000000002',
    'Men''s Salon & Grooming',
    'Haircuts, beard styling, massages, and face care.',
    20.00,
    45
  ),
  (
    '10000000-0000-0000-0000-000000000003',
    'Laser Hair Reduction',
    'Advanced skin and laser hair reduction treatments delivered at home.',
    75.00,
    60
  ),
  (
    '10000000-0000-0000-0000-000000000004',
    'Home Deep Cleaning',
    'Full home cleaning, kitchen cleaning, and bathroom scrubbing.',
    100.00,
    180
  ),
  (
    '10000000-0000-0000-0000-000000000005',
    'Upholstery Cleaning',
    'Sofa, carpet, mattress, and curtain cleaning.',
    60.00,
    90
  ),
  (
    '10000000-0000-0000-0000-000000000006',
    'Pest Control',
    'Treatment for cockroaches, bed bugs, termites, and general disinfection.',
    80.00,
    120
  ),
  (
    '10000000-0000-0000-0000-000000000007',
    'AC Service & Repair',
    'Foam-jet cleaning, gas refill, installation, and repairs.',
    45.00,
    90
  ),
  (
    '10000000-0000-0000-0000-000000000008',
    'Major Appliances',
    'Repair and servicing for washing machines, refrigerators, microwaves, and televisions.',
    35.00,
    90
  ),
  (
    '10000000-0000-0000-0000-000000000009',
    'Water Purifiers (Native)',
    'RO service, filter replacement, and installation.',
    30.00,
    60
  ),
  (
    '10000000-0000-0000-0000-000000000010',
    'Laptop & Chimney Repair',
    'Quick diagnostic and repair options for laptops and kitchen chimneys.',
    35.00,
    90
  ),
  (
    '10000000-0000-0000-0000-000000000011',
    'Electrician',
    'Wiring, switch repairs, fan installation, and MCB fixes.',
    25.00,
    60
  ),
  (
    '10000000-0000-0000-0000-000000000012',
    'Plumber',
    'Leak fixes, tap and flush tank repairs, and pipe fittings.',
    25.00,
    60
  ),
  (
    '10000000-0000-0000-0000-000000000013',
    'Carpenter',
    'Furniture assembly, drilling, and woodwork repairs.',
    30.00,
    90
  )
ON CONFLICT (name) DO UPDATE
SET
  description = EXCLUDED.description,
  price = EXCLUDED.price,
  duration_minutes = EXCLUDED.duration_minutes;
