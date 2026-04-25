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
                .OrderByDescending(c => c.FechaConsulta) // Ordenar de la más reciente a la más antigua
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
                        // 1. NUEVA CONSULTA
                        obj.FechaConsulta = DateTime.Now;
                        db.Consultas.Add(obj);

                        // 2. LÓGICA DE NEGOCIO: Actualizar la cita relacionada
                        if (obj.CitaID != null)
                        {
                            var citaRelacionada = db.Citas.Find(obj.CitaID);
                            if (citaRelacionada != null)
                            {
                                citaRelacionada.Estado = "Completada";
                                db.Entry(citaRelacionada).State = EntityState.Modified;
                            }
                        }
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
                        // Nota: Generalmente no se permite cambiar el paciente, médico o cita origen una vez creada.
                    }

                    db.SaveChanges();
                    transaction.Commit(); // Confirmamos que todo se guardó bien (Consulta y Cita)

                    return Json(new { success = true });
                }
                catch (Exception ex)
                {
                    transaction.Rollback(); // Si hay error, deshacemos todo para no dejar datos corruptos
                    return Json(new { success = false, mensaje = ex.Message });
                }
            }
        }

        // OBTENER CITAS PENDIENTES DEL DÍA (Para que el médico elija a quién atender)
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