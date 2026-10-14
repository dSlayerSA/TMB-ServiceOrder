using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderManagement.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Demonstration data. The product catalogue is reference data the system
    /// needs to work; these orders exist so a clean clone opens on a dashboard
    /// with something in it, rather than on five empty panels.
    /// </summary>
    public partial class SeedDemoOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"-- Demonstration orders, so a fresh clone opens on a dashboard that has
-- something to show. Dates are relative to when the migration runs, which keeps
-- the 24h chart populated no matter when someone clones the repository.
--
-- Only terminal orders are seeded on purpose: a seeded Pendente or Processando
-- order would have no message behind it and would sit there forever, which
-- would look like a bug. Orders in flight come from using the app.
WITH seed (id, customer, product_id, product, amount, status, secs, outcome, age_min) AS (
    VALUES
      ('7a1c0000-0000-4a00-9c00-000000000001'::uuid, 'Mariana Alves', '8f3a1b20-0001-4a00-9c00-000000000001'::uuid, 'Notebook Pro 14', 7499.00::numeric(18,2), 'Finalizado', 5, 'Concluir', 1385),
      ('7a1c0000-0000-4a00-9c00-000000000002'::uuid, 'Rafael Moura', '8f3a1b20-0004-4a00-9c00-000000000004'::uuid, 'Mouse sem fio', 199.90::numeric(18,2), 'Finalizado', 4, 'Concluir', 1240),
      ('7a1c0000-0000-4a00-9c00-000000000003'::uuid, 'Beatriz Nunes', '8f3a1b20-0003-4a00-9c00-000000000003'::uuid, 'Teclado mecânico', 649.90::numeric(18,2), 'Finalizado', 6, 'Concluir', 1115),
      ('7a1c0000-0000-4a00-9c00-000000000004'::uuid, 'Thiago Marinho', '8f3a1b20-0005-4a00-9c00-000000000005'::uuid, 'Headset com microfone', 450.50::numeric(18,2), 'Falhou', 5, 'Falhar', 980),
      ('7a1c0000-0000-4a00-9c00-000000000005'::uuid, 'Camila Ferraz', '8f3a1b20-0008-4a00-9c00-000000000008'::uuid, 'Cadeira ergonômica', 1250.90::numeric(18,2), 'Finalizado', 5, 'Concluir', 845),
      ('7a1c0000-0000-4a00-9c00-000000000006'::uuid, 'Eduardo Baptista', '8f3a1b20-000a-4a00-9c00-00000000000a'::uuid, 'Impressora laser', 2340.00::numeric(18,2), 'Finalizado', 7, 'Concluir', 700),
      ('7a1c0000-0000-4a00-9c00-000000000007'::uuid, 'Larissa Fontes', '8f3a1b20-0007-4a00-9c00-000000000007'::uuid, 'Webcam Full HD', 320.00::numeric(18,2), 'Finalizado', 4, 'Concluir', 610),
      ('7a1c0000-0000-4a00-9c00-000000000008'::uuid, 'Gustavo Peixoto', '8f3a1b20-0006-4a00-9c00-000000000006'::uuid, 'SSD NVMe 2TB', 980.00::numeric(18,2), 'Finalizado', 5, 'Concluir', 495),
      ('7a1c0000-0000-4a00-9c00-000000000009'::uuid, 'Patrícia Lemos', '8f3a1b20-0009-4a00-9c00-000000000009'::uuid, 'Dock USB-C', 699.00::numeric(18,2), 'Finalizado', 6, 'Concluir', 380),
      ('7a1c0000-0000-4a00-9c00-000000000010'::uuid, 'Felipe Andrade', '8f3a1b20-0002-4a00-9c00-000000000002'::uuid, 'Monitor 27"" 4K', 2890.00::numeric(18,2), 'Falhou', 5, 'Falhar', 265),
      ('7a1c0000-0000-4a00-9c00-000000000011'::uuid, 'Juliana Rezende', '8f3a1b20-0003-4a00-9c00-000000000003'::uuid, 'Teclado mecânico', 649.90::numeric(18,2), 'Finalizado', 5, 'Concluir', 170),
      ('7a1c0000-0000-4a00-9c00-000000000012'::uuid, 'Bruno Carvalho', '8f3a1b20-0006-4a00-9c00-000000000006'::uuid, 'SSD NVMe 2TB', 980.00::numeric(18,2), 'Finalizado', 8, 'Concluir', 95),
      ('7a1c0000-0000-4a00-9c00-000000000013'::uuid, 'Aline Tavares', '8f3a1b20-000a-4a00-9c00-00000000000a'::uuid, 'Impressora laser', 2340.00::numeric(18,2), 'Cancelado', 10, 'Cancelar', 42),
      ('7a1c0000-0000-4a00-9c00-000000000014'::uuid, 'Renata Siqueira', '8f3a1b20-0001-4a00-9c00-000000000001'::uuid, 'Notebook Pro 14', 7499.00::numeric(18,2), 'Finalizado', 5, 'Concluir', 18)
),
placed AS (
    SELECT s.*, now() - make_interval(mins => s.age_min) AS created_at FROM seed s
),
timed AS (
    SELECT p.*,
           CASE WHEN p.status = 'Cancelado'
                -- Cancelled while still waiting for the worker, which is the
                -- path the cancel endpoint takes.
                THEN p.created_at + interval '35 seconds'
                ELSE p.created_at + interval '1 second' + make_interval(secs => p.secs)
           END AS settled_at
    FROM placed p
)
INSERT INTO ""Orders""
    (""Id"",""Customer"",""ProductId"",""Product"",""Amount"",""Status"",
     ""ProcessingDurationSeconds"",""ProcessingOutcome"",""CreatedAt"",""UpdatedAt"")
SELECT id, customer, product_id, product, amount, status, secs, outcome, created_at, settled_at
FROM timed;

-- Every order gets the history its route would have produced.
INSERT INTO ""OrderStatusHistories"" (""Id"",""OrderId"",""Status"",""ChangedAt"",""Note"")
SELECT gen_random_uuid(), o.""Id"", 'Pendente', o.""CreatedAt"", 'Pedido criado.'
FROM ""Orders"" o WHERE o.""Id""::text LIKE '7a1c0000-%';

INSERT INTO ""OrderStatusHistories"" (""Id"",""OrderId"",""Status"",""ChangedAt"",""Note"")
SELECT gen_random_uuid(), o.""Id"", 'Processando', o.""CreatedAt"" + interval '1 second',
       'Processamento iniciado pelo worker.'
FROM ""Orders"" o WHERE o.""Id""::text LIKE '7a1c0000-%' AND o.""Status"" <> 'Cancelado';

INSERT INTO ""OrderStatusHistories"" (""Id"",""OrderId"",""Status"",""ChangedAt"",""Note"")
SELECT gen_random_uuid(), o.""Id"", o.""Status"", o.""UpdatedAt"",
       CASE o.""Status""
           WHEN 'Finalizado' THEN 'Pedido finalizado pelo worker.'
           WHEN 'Falhou'     THEN 'Processamento falhou.'
           ELSE 'Cancelado pelo operador.'
       END
FROM ""Orders"" o WHERE o.""Id""::text LIKE '7a1c0000-%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""OrderStatusHistories""
WHERE ""OrderId"" IN (SELECT ""Id"" FROM ""Orders"" WHERE ""Id""::text LIKE '7a1c0000-%');

DELETE FROM ""Orders"" WHERE ""Id""::text LIKE '7a1c0000-%';");
        }
    }
}
