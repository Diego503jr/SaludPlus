using CrystalDecisions.CrystalReports.Engine;
using SaludPlus.Helpers;
using SaludPlus.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace SaludPlus.Controllers
{
    [CustomAuthorize(Roles = "Administrador,Recepcionista,Medico")]
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
        [HttpGet]
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

            var pacienteBase = db.Pacientes
                .AsNoTracking()
                .FirstOrDefault(p => p.DUI == duiLimpio);

            if (pacienteBase == null)
            {
                TempData["Error"] = "No se encontró ningún paciente registrado con el número de DUI ingresado.";
                return RedirectToAction("Index");
            }

            // Extraer consultas de manera independiente para ordenarlas
            var consultasPaciente = db.Consultas
                .AsNoTracking()
                .Where(c => c.PacienteID == pacienteBase.PacienteID)
                .OrderByDescending(c => c.FechaConsulta) // La más reciente primero
                .ToList();

            // Estructuras de datos preparadas para Crystal Reports
            System.Collections.IEnumerable datosReporte;
            System.Collections.IEnumerable datosReceta;

            if (consultasPaciente.Any())
            {
                // El paciente SÍ tiene consultas registradas
                datosReporte = consultasPaciente.Select(c => new
                {
                    PacienteID = pacienteBase.PacienteID,
                    Paciente = pacienteBase.Nombres + " " + pacienteBase.Apellidos,
                    DUI = pacienteBase.DUI,
                    FechaNacimiento = pacienteBase.FechaNacimiento,
                    Sexo = pacienteBase.Sexo,
                    TipoSangre = pacienteBase.TipoSangre,
                    Alergias = pacienteBase.Alergias ?? "Ninguna informada",
                    AntecedentesMedicos = pacienteBase.AntecedentesMedicos ?? "Sin antecedentes relevantes",
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

                // Extraer las recetas vinculadas a estas consultas específicas
                var IDsConsultas = consultasPaciente.Select(c => c.ConsultaID).ToList();
                datosReceta = db.DetalleReceta.AsNoTracking() 
                    .Where(dr => IDsConsultas.Contains(dr.Recetas.ConsultaID))
                    .Select(dr => new {
                        ConsultaID = dr.Recetas.ConsultaID,
                        Medicamento = dr.Medicamentos.Nombre,
                        Dosis = dr.Dosis,
                        Indicaciones = dr.Indicaciones
                    }).ToList();
            }
            else
            {
                // El paciente EXISTE pero NO tiene consultas (Efecto Tarjeta Médica Limpia)
                var pacienteVacio = new[]
                {
            new {
                PacienteID = pacienteBase.PacienteID,
                Paciente = (pacienteBase.Nombres + " " + pacienteBase.Apellidos).Trim(),
                DUI = pacienteBase.DUI,
                FechaNacimiento = pacienteBase.FechaNacimiento,
                Sexo = pacienteBase.Sexo ?? "-",
                TipoSangre = pacienteBase.TipoSangre ?? "N/A",
                Alergias = pacienteBase.Alergias ?? "Ninguna informada",
                AntecedentesMedicos = pacienteBase.AntecedentesMedicos ?? "Sin antecedentes relevantes",

                ConsultaID = -1,
                FechaConsulta = DateTime.Now,

                Medico = "No registra visitas",
                Especialidad = "-",
                MotivoConsulta = "HISTORIAL CLÍNICO NUEVO - Sin consultas previas.",
                Diagnostico = "-",
                Tratamiento = "-",
                PresionArterial = "N/A",
                PesoKg = 0.00m,
                Temperatura = 0.0m
            }
        };

                datosReporte = pacienteVacio.ToList();

                // para evitar que Crystal Reports rompa por nulos en el subreporte
                datosReceta = new[] {
            new { ConsultaID = -1, Medicamento = "", Dosis = "", Indicaciones = "" }
        }.Where(x => x.ConsultaID == -2).ToList(); 
            }

            return GenerarPdfHistorialConReceta("rptHistorialMedico.rpt", datosReporte, datosReceta);
        }

        [HttpPost]
        public JsonResult ValidarHistorial(string dui)
        {
            if (string.IsNullOrEmpty(dui))
            {
                return Json(new { existe = false, mensaje = "El número de DUI no puede estar vacío." });
            }

            string duiLimpio = dui.Trim();

            using (var db = new SaludPlussEntities1())
            {
                var existePaciente = db.Pacientes.Any(p => p.DUI == duiLimpio);

                if (!existePaciente)
                {
                    return Json(new
                    {
                        existe = false,
                        mensaje = "No se encontró ningún paciente registrado con el número de DUI ingresado."
                    });
                }
            }

            // Si el paciente existe (tenga o no consultas)
            return Json(new { existe = true });
        }

        private ActionResult GenerarPdfHistorialConReceta(string nombreReporte, IEnumerable datosPrincipales, IEnumerable datosReceta)
        {
            ReportDocument rd = new ReportDocument();
            string rutaReporte = Path.Combine(Server.MapPath("~/Reports"), nombreReporte);
            rd.Load(rutaReporte);

            // Seteamos los datos del reporte principal (Historial)
            rd.SetDataSource(datosPrincipales);

            rd.Subreports["subReceta.rpt"].SetDataSource(datosReceta);

            Stream stream = rd.ExportToStream(CrystalDecisions.Shared.ExportFormatType.PortableDocFormat);
            stream.Seek(0, SeekOrigin.Begin);

            rd.Close();
            rd.Dispose();

            return File(stream, "application/pdf");
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