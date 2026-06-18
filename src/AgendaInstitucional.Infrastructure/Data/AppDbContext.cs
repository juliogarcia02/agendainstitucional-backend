using System;
using System.Collections.Generic;
using AgendaInstitucional.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace AgendaInstitucional.Infrastructure.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Auditorium> Auditoria { get; set; }

    public virtual DbSet<SolicitudServicio> SolicitudServicios { get; set; }

    public virtual DbSet<Solicitude> Solicitudes { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }

    public virtual DbSet<catComisione> catComisiones { get; set; }

    public virtual DbSet<Diputado> Diputados { get; set; }

    public virtual DbSet<ComisionDiputado> ComisionesDiputados { get; set; }

    public virtual DbSet<catSala> catSalas { get; set; }

    public virtual DbSet<catServicio> catServicios { get; set; }

    public virtual DbSet<catServicioResponsable> catServicioResponsables { get; set; }

    public virtual DbSet<catTipoEvento> catTipoEventos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Auditorium>(entity =>
        {
            entity.HasIndex(e => new { e.SolicitudId, e.FechaHora }, "IX_Auditoria_SolicitudId_FechaHora").IsDescending(false, true);

            entity.HasIndex(e => new { e.Tabla, e.FechaHora }, "IX_Auditoria_Tabla_FechaHora").IsDescending(false, true);

            entity.Property(e => e.Accion)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.FechaHora).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Ip).HasMaxLength(60);
            entity.Property(e => e.Llave).HasMaxLength(300);
            entity.Property(e => e.LoginSql)
                .HasMaxLength(128)
                .HasDefaultValueSql("(original_login())");
            entity.Property(e => e.Modulo).HasMaxLength(100);
            entity.Property(e => e.Tabla).HasMaxLength(128);
            entity.Property(e => e.UserAgent).HasMaxLength(400);
            entity.Property(e => e.Usuario).HasMaxLength(200);
        });

        modelBuilder.Entity<SolicitudServicio>(entity =>
        {
            entity.HasKey(e => new { e.SolicitudId, e.ServicioId });

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Servicio).WithMany(p => p.SolicitudServicios)
                .HasForeignKey(d => d.ServicioId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SolicitudServicios_catServicios");

            entity.HasOne(d => d.Solicitud).WithMany(p => p.SolicitudServicios)
                .HasForeignKey(d => d.SolicitudId)
                .HasConstraintName("FK_SolicitudServicios_Solicitudes");
        });

        modelBuilder.Entity<Solicitude>(entity =>
        {
            entity.Property(e => e.Asunto).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DependeParaIniciar).HasMaxLength(500);
            entity.Property(e => e.Estatus).HasDefaultValue(true);
            entity.Property(e => e.Evento).HasMaxLength(250);
            entity.Property(e => e.HoraFin).HasPrecision(0);
            entity.Property(e => e.HoraInicio).HasPrecision(0);
            entity.Property(e => e.Lugar).HasMaxLength(250);
            entity.Property(e => e.Direccion).HasMaxLength(1000);
            entity.Property(e => e.Municipio).HasMaxLength(150);
            entity.Property(e => e.OtroServicioExtra).HasMaxLength(250);
            entity.Property(e => e.ResponsableEvento).HasMaxLength(1000);
            entity.Property(e => e.UsuariosNotificarServicio).HasMaxLength(2000);

            entity.HasOne(d => d.Comision).WithMany(p => p.Solicitudes)
                .HasForeignKey(d => d.ComisionId)
                .HasConstraintName("FK_Solicitudes_catComisiones");

            entity.HasOne(d => d.Sala).WithMany(p => p.Solicitudes)
                .HasForeignKey(d => d.SalaId)
                .HasConstraintName("FK_Solicitudes_catSalas");

            entity.HasOne(d => d.TipoEvento).WithMany(p => p.Solicitudes)
                .HasForeignKey(d => d.TipoEventoId)
                .HasConstraintName("FK_Solicitudes_catTipoEventos");
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasIndex(e => e.Estatus, "IX_Usuarios_Estatus");

            entity.HasIndex(e => e.Rol, "IX_Usuarios_Rol");

            entity.HasIndex(e => e.Email, "UQ_Usuarios_Email").IsUnique();

            entity.HasIndex(e => e.Usuario1, "UQ_Usuarios_Usuario").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(320);
            entity.Property(e => e.Estatus).HasDefaultValue(true);
            entity.Property(e => e.NombreCompleto).HasMaxLength(150);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Rol)
                .HasMaxLength(50)
                .HasDefaultValue("usuario");
            entity.Property(e => e.Telefono).HasMaxLength(50);
            entity.Property(e => e.Usuario1)
                .HasMaxLength(50)
                .HasColumnName("Usuario");
        });

        modelBuilder.Entity<catComisione>(entity =>
        {
            entity.Property(e => e.comision).HasMaxLength(250);
        });

        modelBuilder.Entity<Diputado>(entity =>
        {
            entity.ToTable("diputados");
            entity.Property(e => e.Nombre).HasMaxLength(250);
        });

        modelBuilder.Entity<ComisionDiputado>(entity =>
        {
            entity.ToTable("comisiones_diputados");
            entity.HasKey(e => new { e.ComisionId, e.DiputadoId });

            entity.HasOne(d => d.Comision)
                .WithMany(p => p.ComisionesDiputados)
                .HasForeignKey(d => d.ComisionId)
                .HasConstraintName("FK_comisiones_diputados_catComisiones");

            entity.HasOne(d => d.Diputado)
                .WithMany(p => p.ComisionesDiputados)
                .HasForeignKey(d => d.DiputadoId)
                .HasConstraintName("FK_comisiones_diputados_diputados");
        });

        modelBuilder.Entity<catSala>(entity =>
        {
            entity.Property(e => e.sala).HasMaxLength(250);
        });

        modelBuilder.Entity<catServicio>(entity =>
        {
            entity.Property(e => e.servicio).HasMaxLength(250);
        });

        modelBuilder.Entity<catServicioResponsable>(entity =>
        {
            entity.Property(e => e.Observaciones).HasMaxLength(1000);
            entity.Property(e => e.ResponsableEmail).HasMaxLength(320);
            entity.Property(e => e.ResponsableNombre).HasMaxLength(250);
            entity.Property(e => e.ResponsableTelefono).HasMaxLength(50);

            entity.HasOne(d => d.Servicio).WithMany(p => p.catServicioResponsables)
                .HasForeignKey(d => d.ServicioId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_catServicioResponsables_catServicios");
        });

        modelBuilder.Entity<catTipoEvento>(entity =>
        {
            entity.Property(e => e.evento).HasMaxLength(250);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
