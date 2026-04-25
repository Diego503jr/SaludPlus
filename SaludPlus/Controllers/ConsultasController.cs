using SaludPlus.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace SaludPlus.Controllers
{
    public class ConsultasController : Controller
    {
        private SaludPlussEntities1 db = new SaludPlussEntities1();

        // GET: Consultas
        public ActionResult Index()
        {
            return View();
        }

        // GET: Consultas/Ficha
        public ActionResult Ficha(int? citaId, int? consultaId)
        {
            ViewBag.CitaID = citaId ?? 0;
            ViewBag.ConsultaID = consultaId ?? 0;
            ViewBag.PacienteNombre = "Paciente Desconocido"; // Valor por defecto

            // Si estamos abriendo una consulta que ya existe
            if (consultaId.HasValue && consultaId > 0)
            {
                var consulta = db.Consultas.Include(c => c.Pacientes).FirstOrDefault(c => c.ConsultaID == consultaId);
                if (consulta != null)
                {
                    ViewBag.PacienteNombre = consulta.Pacientes.Nombres + " " + consulta.Pacientes.Apellidos;
                }
            }
            // Si estamos creando una consulta nueva a partir de una cita
            else if (citaId.HasValue && citaId > 0)
            {
                var cita = db.Citas.Include(c => c.Pacientes).FirstOrDefault(c => c.CitaID == citaId);
                if (cita != null)
                {
                    ViewBag.PacienteNombre = cita.Pacientes.Nombres + " " + cita.Pacientes.Apellidos;
                }
            }

            return View();
        }

        // LISTAR CONSULTAS (Historial clínico general o filtrado por médico)
        public JsonResult Listar()
        {
            var consultas = db.Consultas
                .Select(c => new
                {
                    c.ConsultaID,
                    c.CitaID,
                    PacienteNombre = c.Pacientes.Nombres + " " + c.Pacientes.Apellidos,
                    MedicoNombre = c.Medicos.Usuarios.Nombres + " " + c.Medicos.Usuarios.Apellidos,
                    c.FechaConsulta,
                    c.MotivoConsulta,
                    c.Diagnostico
                })
                .OrderByDescending(c => c.FechaConsulta) 
                .ToList();

            return Json(consultas, JsonRequestBehavior.AllowGet);
        }

        // OBTENER DETALLES DE UNA CONSULTA ESPECÍFICA
        [HttpGet]
        public JsonResult Consultar(int id)
        {
            var consulta = db.Consultas
                .Where(c => c.ConsultaID == id)
                .Select(c => new {
                    c.ConsultaID,
                    c.CitaID,
                    c.MedicoID,
                    c.PacienteID,
                    c.FechaConsulta,
                    c.MotivoConsulta,
                    c.ExamenFisico,
                    c.Diagnostico,
                    c.Tratamiento,
                    c.Indicaciones,
                    c.PesoKg,
                    c.TallaCm,
                    c.PresionArterial,
                    c.Temperatura,
                    c.ProximaRevision
                }).FirstOrDefault();

            if (consulta == null)
            {
                return Json(new { success = false, mensaje = "Consulta no encontrada" }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { success = true, data = consulta }, JsonRequestBehavior.AllowGet);
        }

        // GUARDAR O ACTUALIZAR LA NOTA MÉDICA
        [HttpPost]
        public JsonResult Guardar(Consultas obj)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    if (obj.ConsultaID == 0)
                    {
                        bool existeConsulta = db.Consultas.Any(c => c.CitaID == obj.CitaID);
                        if (existeConsulta)
                        {
                            return Json(new { success = false, mensaje = "Esta cita ya tiene un expediente médico guardado. No se pueden crear consultas duplicadas para la misma cita." });
                        }

         
                        obj.FechaConsulta = DateTime.Now;

                        // Extramos IDs obligatorios y actualizar la cita relacionada
                        if (obj.CitaID != null)
                        {
                            var citaRelacionada = db.Citas.Find(obj.CitaID);
                            if (citaRelacionada != null)
                            {
                                // Le pasamos a la nueva consulta el Paciente y el Médico que estaban en la cita
                                obj.PacienteID = citaRelacionada.PacienteID;
                                obj.MedicoID = citaRelacionada.MedicoID;

                                // Cambiamos el estado de la Cita
                                citaRelacionada.Estado = "Completada";
                                db.Entry(citaRelacionada).State = EntityState.Modified;
                            }
                        }

                        // Agregamos la consulta a la base de datos ya con todos sus FKs completos
                        db.Consultas.Add(obj);
                    }
                    else
                    {
                        // 3. ACTUALIZAR CONSULTA EXISTENTE
                        var data = db.Consultas.Find(obj.ConsultaID);
                        if (data == null)
                        {
                            return Json(new { success = false, mensaje = "El registro no existe." });
                        }

                        data.ExamenFisico = obj.ExamenFisico;
                        data.Diagnostico = obj.Diagnostico;
                        data.Tratamiento = obj.Tratamiento;
                        data.Indicaciones = obj.Indicaciones;
                        data.PesoKg = obj.PesoKg;
                        data.TallaCm = obj.TallaCm;
                        data.PresionArterial = obj.PresionArterial;
                        data.Temperatura = obj.Temperatura;
                        data.ProximaRevision = obj.ProximaRevision;
                    }

                    db.SaveChanges();
                    transaction.Commit(); // Confirmamos que todo se guardó bien 

                    return Json(new { success = true });
                }
                catch (Exception ex)
                {
                    transaction.Rollback(); // Si hay error, deshacemos todo para no dejar datos corruptos

                    // Extraer el error real de SQL en lugar del error genérico de Entity Framework
                    string errorReal = ex.InnerException != null ?
                                      (ex.InnerException.InnerException != null ? ex.InnerException.InnerException.Message : ex.InnerException.Message)
                                      : ex.Message;

                    return Json(new { success = false, mensaje = errorReal });
                }
            }
        }

        // OBTENER CITAS PENDIENTES DEL DÍA 
        [HttpGet]
        public JsonResult ObtenerCitasPendientes()
        {
            DateTime hoy = DateTime.Today;

            var citasDelDia = db.Citas
                // Filtramos por estado y aseguramos que la fecha coincida exactamente con hoy
                .Where(c => (c.Estado == "Confirmada" || c.Estado == "Pendiente")
                            && DbFunctions.TruncateTime(c.FechaCita) == hoy)
                // Ordenamos por hora para que el médico vea la agenda en orden lógico
                .OrderBy(c => c.HoraCita)
                .Select(c => new {
                    Id = c.CitaID,
                    PacienteID = c.PacienteID,
                    MedicoID = c.MedicoID,
                    Nombres = c.Pacientes.Nombres + " " + c.Pacientes.Apellidos,
                    Motivo = c.Motivo,
                    Hora = c.HoraCita
                })
                .ToList(); // Lo traemos a memoria

            // Formateamos el texto en memoria para evitar errores de traducción a SQL
            var listadoFormateado = citasDelDia.Select(c => new {
                Id = c.Id,
                // Agregamos la hora al inicio para que se vea claro en el ComboBox
                Texto = $"[{c.Hora}] Cita #{c.Id} - {c.Nombres} ({c.Motivo})"
            }).ToList();

            return Json(listadoFormateado, JsonRequestBehavior.AllowGet);
        }
    }
}