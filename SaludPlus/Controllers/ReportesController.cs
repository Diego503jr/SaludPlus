using SaludPlus.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using CrystalDecisions.CrystalReports.Engine;

namespace SaludPlus.Controllers
{
    public class ReportesController : Controller
    {
        private SaludPlussEntities1 db = new SaludPlussEntities1();

        // Vista Principal del Panel de Reportes
        public ActionResult Index()
        {
            if (Session["User"] == null)
            {
                return RedirectToAction("Login", "Account"); // O tu controlador de login
            }
            return View();
        }

        // ADMIN: Inventario de Medicamentos
        public ActionResult DescargarInventario()
        {
            // Validar Rol desde el Servidor
            var usuario = (Usuarios)Session["User"];
            var nombreRol = usuario?.Roles?.Nombre ?? "";

            if (nombreRol != "Administrador")
            {
                TempData["Error"] = "No tiene permisos para acceder al reporte de inventario.";
                return RedirectToAction("Index", "Home");
            }

            var medicamentosBase = db.Medicamentos.AsNoTracking().ToList();

            var listaFiltrada = medicamentosBase.Where(m => m.Activo == true || m.Activo == null).ToList();

            // Si la lista está vacía por algún problema de datos, evitamos que Crystal rompa la vista
            if (!listaFiltrada.Any())
            {
                TempData["Error"] = "No se encontraron medicamentos activos para generar el reporte.";
                return RedirectToAction("Index", "Home");
            }

            var datos = medicamentosBase
                .OrderBy(m => m.StockActual <= m.StockMinimo ? 0 : 1) // Prioriza críticos arriba
                .ThenBy(m => m.Nombre)
                .Select(m => new
                {
                    MedicamentoID = m.MedicamentoID,
                    Nombre = m.Nombre ?? "Sin Nombre",
                    Laboratorio = m.Laboratorio ?? "N/A",
                    Presentacion = m.Presentacion ?? "N/A",
                    ViaAdministracion = m.ViaAdministracion ?? "Oral",
                    StockActual = m.StockActual,
                    StockMinimo = m.StockMinimo,
                    // Evitamos que un precio NULL rompa el motor de Crystal Reports:
                    Precio = m.Precio ?? 0.00m,
                    EstadoStock = (m.StockActual <= m.StockMinimo) ? "CRÍTICO" : "ESTABLE"
                }).ToList();

            return GenerarPdfReporte("rptInventario.rpt", datos);
        }

        // MÉDICO / ADMIN: Historial Médico por Paciente
        [HttpPost]
        public ActionResult DescargarHistorial(string dui)
        {
            // Validar Rol desde el Servidor
            var usuario = (Usuarios)Session["User"];
            var nombreRol = usuario?.Roles?.Nombre ?? "";

            if (nombreRol != "Administrador" && nombreRol != "Medico")
            {
                TempData["Error"] = "No tiene permisos para consultar expedientes clínicos.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrEmpty(dui))
            {
                TempData["Error"] = "Debe ingresar un número de DUI válido.";
                return RedirectToAction("Index");
            }

            string duiLimpio = dui.Trim();

            // Realizamos la consulta cruzada extrayendo los datos desde la tabla Consultas
            var consultasBase = db.Consultas
                .AsNoTracking()
                .Where(c => c.Pacientes != null && c.Pacientes.DUI == duiLimpio)
                .OrderByDescending(c => c.FechaConsulta) // La consulta más reciente primero
                .ToList();

            // Si el paciente no registra consultas, validamos si existe en el sistema para dar un mensaje certero
            if (!consultasBase.Any())
            {
                var existePaciente = db.Pacientes.Any(p => p.DUI == duiLimpio);
                if (existePaciente)
                {
                    TempData["Error"] = "El paciente existe, pero no registra consultas médicas en su historial.";
                }
                else
                {
                    TempData["Error"] = "No se encontró ningún paciente registrado con el número de DUI ingresado.";
                }
                return RedirectToAction("Index");
            }

            // Proyectamos de forma segura hacia la estructura exacta esperada por tu .xsd
            var datos = consultasBase.Select(c => new
            {
                PacienteID = c.PacienteID,
                Paciente = c.Pacientes != null ? (c.Pacientes.Nombres + " " + c.Pacientes.Apellidos) : "Paciente Anónimo",
                DUI = c.Pacientes != null ? c.Pacientes.DUI : "N/A",
                FechaNacimiento = c.Pacientes != null ? (DateTime)c.Pacientes.FechaNacimiento : DateTime.Now,
                Sexo = c.Pacientes != null ? c.Pacientes.Sexo : "-",
                TipoSangre = c.Pacientes != null ? c.Pacientes.TipoSangre : "N/A",
                Alergias = c.Pacientes != null ? (c.Pacientes.Alergias ?? "Ninguna informada") : "Ninguna informada",
                AntecedentesMedicos = c.Pacientes != null ? (c.Pacientes.AntecedentesMedicos ?? "Sin antecedentes relevantes") : "Sin antecedentes relevantes",
                ConsultaID = c.ConsultaID,
                FechaConsulta = c.FechaConsulta,
                Medico = (c.Medicos != null && c.Medicos.Usuarios != null) ? (c.Medicos.Usuarios.Nombres + " " + c.Medicos.Usuarios.Apellidos) : "Médico No Asignado",
                Especialidad = (c.Medicos != null && c.Medicos.Especialidades != null) ? c.Medicos.Especialidades.Nombre : "General",
                MotivoConsulta = c.MotivoConsulta ?? "Sin motivo registrado",
                Diagnostico = c.Diagnostico ?? "Sin diagnóstico registrado",
                Tratamiento = c.Tratamiento ?? "Sin tratamiento registrado",
                PresionArterial = c.PresionArterial ?? "N/A",
                PesoKg = c.PesoKg ?? 0.00m,
                Temperatura = c.Temperatura ?? 0.0m
            }).ToList();

            return GenerarPdfReporte("rptHistorialMedico.rpt", datos);
        }

       

        // RECEPCIONISTA / ADMIN: Citas Atendidas y Canceladas
        public ActionResult DescargarCitas(DateTime fechaInicio, DateTime fechaFin)
        {
            var usuario = (Usuarios)Session["User"];
            var nombreRol = usuario?.Roles?.Nombre ?? "";

            if (nombreRol != "Administrador" && nombreRol != "Recepcionista")
            {
                TempData["Error"] = "No tiene permisos para acceder al reporte de control de citas.";
                return RedirectToAction("Index", "Home");
            }

            var estadosPermitidos = new List<string> { "Completada", "Cancelada" };

            var datos = db.Citas
                .AsNoTracking()
                .Where(c => c.FechaCita >= fechaInicio && c.FechaCita <= fechaFin && estadosPermitidos.Contains(c.Estado))
                .OrderByDescending(c => c.FechaCita)
                .ThenBy(c => c.HoraCita)
                .Select(c => new
                {
                    CitaID = c.CitaID,
                    FechaCita = c.FechaCita,
                    HoraCita = c.HoraCita,
                    Paciente = c.Pacientes.Nombres + " " + c.Pacientes.Apellidos,
                    TelefonoPaciente = c.Pacientes.Telefono,
                    Medico = c.Medicos.Usuarios.Nombres + " " + c.Medicos.Usuarios.Apellidos,
                    Especialidad = c.Medicos.Especialidades.Nombre,
                    Motivo = c.Motivo,
                    EstadoCita = c.Estado,
                    Observaciones = c.Observaciones
                }).ToList();

            return GenerarPdfReporte("rptCitasPeriodo.rpt", datos);
        }

        // Método genérico e higiénico para compilar el reporte en memoria y servir el PDF
        private ActionResult GenerarPdfReporte(string nombreReporte, System.Collections.IEnumerable datos)
        {
            try
            {
                ReportDocument rd = new ReportDocument();
                string rutaReporte = Path.Combine(Server.MapPath("~/Reports"), nombreReporte);
                rd.Load(rutaReporte);

                rd.SetDataSource(datos);

                Stream stream = rd.ExportToStream(CrystalDecisions.Shared.ExportFormatType.PortableDocFormat);
                stream.Seek(0, SeekOrigin.Begin);

                rd.Close();
                rd.Dispose();

                return File(stream, "application/pdf");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al compilar el reporte: " + ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose(); 
            }
            base.Dispose(disposing);
        }
    }
}