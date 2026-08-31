using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Domain.Entities;

namespace MercuryGo.Api.Data;

public class MercuryGoDbContext : DbContext
{
    public MercuryGoDbContext(DbContextOptions<MercuryGoDbContext> options) : base(options)
    {
    }

    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<DetallePedido> DetallesPedido => Set<DetallePedido>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Categoria ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.ToTable("categorias");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Descripcion).HasMaxLength(255);
            entity.Property(e => e.Activo).HasDefaultValue(true);

            entity.HasData(
                new Categoria { Id = 1, Nombre = "Almacén y Comestibles", Descripcion = "Aceites, azúcares y productos secos", Activo = true, CreadoEn = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc) },
                new Categoria { Id = 2, Nombre = "Harinas y Pastas", Descripcion = "Harinas, fideos y sémolas", Activo = true, CreadoEn = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc) },
                new Categoria { Id = 3, Nombre = "Infusiones", Descripcion = "Yerba mate, té y café", Activo = true, CreadoEn = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc) }
            );
        });

        // ── Producto ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Producto>(entity =>
        {
            entity.ToTable("productos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Descripcion).HasMaxLength(255);
            entity.Property(e => e.Precio).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.ImagenUrl).HasMaxLength(500);
            entity.Property(e => e.Disponible).HasDefaultValue(true);
            entity.Property(e => e.Activo).HasDefaultValue(true);

            entity.HasOne(p => p.Categoria)
                  .WithMany(c => c.Productos)
                  .HasForeignKey(p => p.CategoriaId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasData(
                new Producto { Id = 1, CategoriaId = 1, Nombre = "Aceite de Girasol 1.5L", Descripcion = "Caja x 12 unidades", Precio = 2400.00m, Disponible = true, Activo = true, CreadoEn = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc) },
                new Producto { Id = 2, CategoriaId = 2, Nombre = "Harina 000 1kg", Descripcion = "Pack x 10 unidades", Precio = 950.00m, Disponible = true, Activo = true, CreadoEn = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc) },
                new Producto { Id = 3, CategoriaId = 1, Nombre = "Arroz Largo Fino 1kg", Descripcion = "Bolsa x 10 unidades", Precio = 1800.00m, Disponible = false, Activo = true, CreadoEn = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc) },
                new Producto { Id = 4, CategoriaId = 2, Nombre = "Fideos Guiseros 500g", Descripcion = "Caja x 20 paquetes", Precio = 1100.00m, Disponible = true, Activo = true, CreadoEn = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc) },
                new Producto { Id = 5, CategoriaId = 1, Nombre = "Azúcar Común 1kg", Descripcion = "Fardo x 10 unidades", Precio = 1250.00m, Disponible = true, Activo = true, CreadoEn = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc) },
                new Producto { Id = 6, CategoriaId = 3, Nombre = "Yerba Mate 1kg", Descripcion = "Pack x 6 unidades", Precio = 3400.00m, Disponible = true, Activo = true, CreadoEn = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc) }
            );
        });

        // ── Cliente ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.ToTable("clientes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RazonSocial).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Cuit).HasMaxLength(13).IsRequired();
            entity.HasIndex(e => e.Cuit).IsUnique();
            entity.Property(e => e.Telefono).HasMaxLength(30);
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.Direccion).HasMaxLength(255);
            entity.Property(e => e.Localidad).HasMaxLength(100);
            entity.Property(e => e.Activo).HasDefaultValue(true);

            entity.HasData(
                new Cliente { Id = 1, RazonSocial = "Distribuidora El Norte S.R.L.", Cuit = "30-71234567-0", Telefono = "266-4123456", Email = "compras@elnorte.com.ar", Direccion = "Av. San Martín 1520", Localidad = "San Luis", Activo = true, CreadoEn = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc) },
                new Cliente { Id = 2, RazonSocial = "Supermercado La Familia S.A.", Cuit = "30-68901234-1", Telefono = "266-4234567", Email = "pedidos@lafamilia.com.ar", Direccion = "Belgrano 890", Localidad = "Villa Mercedes", Activo = true, CreadoEn = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc) },
                new Cliente { Id = 3, RazonSocial = "Almacenes Pérez Hnos.", Cuit = "20-30123456-9", Telefono = "266-4345678", Email = null, Direccion = "Mitre 345", Localidad = "San Luis", Activo = true, CreadoEn = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc) },
                new Cliente { Id = 4, RazonSocial = "Minimarket Los Pinos", Cuit = "27-40234567-2", Telefono = "266-4456789", Email = "lospinos@gmail.com", Direccion = "Colón 1200", Localidad = "Merlo", Activo = false, CreadoEn = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc) }
            );
        });

        // ── Pedido ────────────────────────────────────────────────────────────
        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.ToTable("pedidos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Numero).HasMaxLength(20).IsRequired();
            entity.HasIndex(e => e.Numero).IsUnique();
            entity.Property(e => e.Estado)
                  .HasConversion<string>()
                  .HasMaxLength(30)
                  .IsRequired();
            entity.Property(e => e.Observaciones).HasMaxLength(500);
            entity.Property(e => e.Activo).HasDefaultValue(true);

            entity.HasOne(p => p.Cliente)
                  .WithMany(c => c.Pedidos)
                  .HasForeignKey(p => p.ClienteId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasData(
                new Pedido { Id = 1, ClienteId = 1, Numero = "PED-000001", Estado = EstadoPedido.Entregado,    FechaPedido = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc), Observaciones = "Entregar en depósito trasero", Activo = true, CreadoEn = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc) },
                new Pedido { Id = 2, ClienteId = 2, Numero = "PED-000002", Estado = EstadoPedido.Despachado,   FechaPedido = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc), Observaciones = null, Activo = true, CreadoEn = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc) },
                new Pedido { Id = 3, ClienteId = 1, Numero = "PED-000003", Estado = EstadoPedido.EnPreparacion, FechaPedido = new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc), Observaciones = "Urgente, requiere factura A", Activo = true, CreadoEn = new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc) },
                new Pedido { Id = 4, ClienteId = 3, Numero = "PED-000004", Estado = EstadoPedido.Confirmado,   FechaPedido = new DateTime(2026, 8, 28, 0, 0, 0, DateTimeKind.Utc), Observaciones = null, Activo = true, CreadoEn = new DateTime(2026, 8, 28, 0, 0, 0, DateTimeKind.Utc) },
                new Pedido { Id = 5, ClienteId = 2, Numero = "PED-000005", Estado = EstadoPedido.Pendiente,    FechaPedido = new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc), Observaciones = null, Activo = true, CreadoEn = new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc) }
            );
        });

        // ── DetallePedido ─────────────────────────────────────────────────────
        modelBuilder.Entity<DetallePedido>(entity =>
        {
            entity.ToTable("detalles_pedido");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Cantidad).IsRequired();
            entity.Property(e => e.PrecioUnitario).HasPrecision(18, 2).IsRequired();

            entity.HasOne(d => d.Pedido)
                  .WithMany(p => p.Detalles)
                  .HasForeignKey(d => d.PedidoId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Producto)
                  .WithMany()
                  .HasForeignKey(d => d.ProductoId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Seed de líneas de pedido (precios históricos al momento del pedido)
            entity.HasData(
                // PED-000001
                new DetallePedido { Id = 1, PedidoId = 1, ProductoId = 1, Cantidad = 5,  PrecioUnitario = 2400.00m, CreadoEn = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc) },
                new DetallePedido { Id = 2, PedidoId = 1, ProductoId = 5, Cantidad = 10, PrecioUnitario = 1250.00m, CreadoEn = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc) },
                new DetallePedido { Id = 3, PedidoId = 1, ProductoId = 6, Cantidad = 3,  PrecioUnitario = 3400.00m, CreadoEn = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc) },
                // PED-000002
                new DetallePedido { Id = 4, PedidoId = 2, ProductoId = 2, Cantidad = 20, PrecioUnitario = 950.00m,  CreadoEn = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc) },
                new DetallePedido { Id = 5, PedidoId = 2, ProductoId = 4, Cantidad = 15, PrecioUnitario = 1100.00m, CreadoEn = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc) },
                // PED-000003
                new DetallePedido { Id = 6, PedidoId = 3, ProductoId = 1, Cantidad = 8,  PrecioUnitario = 2400.00m, CreadoEn = new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc) },
                new DetallePedido { Id = 7, PedidoId = 3, ProductoId = 6, Cantidad = 6,  PrecioUnitario = 3400.00m, CreadoEn = new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc) },
                // PED-000004
                new DetallePedido { Id = 8, PedidoId = 4, ProductoId = 5, Cantidad = 12, PrecioUnitario = 1250.00m, CreadoEn = new DateTime(2026, 8, 28, 0, 0, 0, DateTimeKind.Utc) },
                // PED-000005
                new DetallePedido { Id = 9, PedidoId = 5, ProductoId = 3, Cantidad = 4,  PrecioUnitario = 1800.00m, CreadoEn = new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc) },
                new DetallePedido { Id = 10, PedidoId = 5, ProductoId = 2, Cantidad = 8, PrecioUnitario = 950.00m,  CreadoEn = new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc) }
            );
        });
    }
}
