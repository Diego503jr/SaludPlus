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

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Ficha(int? citaId, int? consultaId)
        {
            ViewBag.CitaID = citaId ?? 0;
            ViewBag.ConsultaID = consultaId ?? 0;

            ViewBag.PacienteNombre = "Paciente Desconocido";
            ViewBag.TipoSangre = "N/D";
            ViewBag.Alergias = "Ninguna registrada";
            ViewBag.Antecedentes = "Ninguno registrado";

            int pacienteIdActual = 0;

            ViewBag.EsSoloLectura = (consultaId.HasValue && consultaId.Value > 0);
            // 1. Si estamos abriendo una consulta que ya existe
            if (ViewBag.EsSoloLectura)
            {
                var consulta = db.Consultas.Include(c => c.Pacientes).FirstOrDefault(c => c.ConsultaID == consultaId);
                if (consulta != null)
                {
                    pacienteIdActual = consulta.PacienteID;

                    ViewBag.PacienteNombre = consulta.Pacientes.Nombres + " " + consulta.Pacientes.Apellidos;
                    ViewBag.TipoSangre = string.IsNullOrEmpty(consulta.Pacientes.TipoSangre) ? "N/D" : consulta.Pacientes.TipoSangre;
                    ViewBag.Alergias = string.IsNullOrEmpty(consulta.Pacientes.Alergias) ? "Ninguna registrada" : consulta.Pacientes.Alergias;
                    ViewBag.Antecedentes = string.IsNullOrEmpty(consulta.Pacientes.AntecedentesMedicos) ? "Ninguno registrado" : consulta.Pacientes.AntecedentesMedicos;
                }
            }
            // 2. Si estamos creando una consulta nueva a partir de una cita
            else if (citaId.HasValue && citaId > 0)
            {
                var cita = db.Citas.Include(c => c.Pacientes).FirstOrDefault(c => c.CitaID == citaId);
                if (cita != null)
                {
                    pacienteIdActual = cita.PacienteID;

                    ViewBag.PacienteNombre = cita.Pacientes.Nombres + " " + cita.Pacientes.Apellidos;
                    ViewBag.TipoSangre = string.IsNullOrEmpty(cita.Pacientes.TipoSangre) ? "N/D" : cita.Pacientes.TipoSangre;
                    ViewBag.Alergias = string.IsNullOrEmpty(cita.Pacientes.Alergias) ? "Ninguna registrada" : cita.Pacientes.Alergias;
                    ViewBag.Antecedentes = string.IsNullOrEmpty(cita.Pacientes.AntecedentesMedicos) ? "Ninguno registrado" : cita.Pacientes.AntecedentesMedicos;
                }
            }

            if (pacienteIdActual > 0)
            {
                var historial = db.Consultas
                                  .Include("Medicos.Usuarios")
                                  .Where(c => c.PacienteID == pacienteIdActual && c.ConsultaID != consultaId)
                                  .OrderByDescending(c => c.FechaConsulta)
                                  .ToList();
                ViewBag.HistorialPrevio = historial;
            }
            else
            {
                ViewBag.HistorialPrevio = new List<Consultas>();
            }

            return View();
        }

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

        [HttpGet]
        public JsonResult Consultar(int id)
        {
            var consulta = db.Consultas
                .Where(c => c.ConsultaID == id)
                .Select(c => new
                {
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

            if (consulta == null) return Json(new { success = false, mensaje = "Consulta no encontrada" }, JsonRequestBehavior.AllowGet);

            return Json(new { success = true, data = consulta }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult Guardar(ConsultaFichaDTO payload)
        {
            Consultas obj = payload.ConsultaData;
            List<DetalleRecetaDTO> listaMedicamentos = payload.ListaReceta;

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    if (obj.ConsultaID == 0)
                    {
                        if (db.Consultas.Any(c => c.CitaID == obj.CitaID))
                            return Json(new { success = false, mensaje = "Esta cita ya tiene un expediente guardado." });

                        obj.FechaConsulta = DateTime.Now;

                        if (obj.CitaID != null)
                        {
                            var cita = db.Citas.Find(obj.CitaID);
                            if (cita != null)
                            {
                                obj.PacienteID = cita.PacienteID;
                                obj.MedicoID = cita.MedicoID;
                                cita.Estado = "Completada";
                                db.Entry(cita).State = EntityState.Modified;
                            }
                        }

                        db.Consultas.Add(obj);
                        db.SaveChanges();

                        if (listaMedicamentos != null && listaMedicamentos.Count > 0)
                        {
                            Recetas nuevaReceta = new Recetas
                            {
                                ConsultaID = obj.ConsultaID,
                                MedicoID = obj.MedicoID,
                                PacienteID = obj.PacienteID,
                                FechaEmision = DateTime.Now,
                                FechaVencimiento = DateTime.Now.AddDays(15),
                                Estado = "Emitida",
                                Observaciones = "Generada desde Consultorio"
                            };

                            db.Recetas.Add(nuevaReceta);
                            db.SaveChanges();

                            foreach (var item in listaMedicamentos)
                            {
                                DetalleReceta detalle = new DetalleReceta
                                {
                                    RecetaID = nuevaReceta.RecetaID,
                                    MedicamentoID = item.MedicamentoID,
                                    Dosis = item.Dosis,
                                    Cantidad = item.Cantidad,
                                    Indicaciones = item.Indicaciones,
                                    Estado = false 
                                };
                                db.DetalleReceta.Add(detalle);

                                var medDB = db.Medicamentos.Find(item.MedicamentoID);
                                if (medDB != null && medDB.Nombre != "MEDICAMENTO EXTERNO (Solo texto)")
                                {
                                    medDB.StockActual -= item.Cantidad;
                                    db.Entry(medDB).State = EntityState.Modified;
                                }
                            }
                            db.SaveChanges();
                        }

                        if (obj.ProximaRevision.HasValue)
                        {
                            Citas nuevaCita = new Citas
                            {
                                PacienteID = obj.PacienteID,
                                MedicoID = obj.MedicoID,
                                FechaCita = obj.ProximaRevision.Value,
                                HoraCita = new TimeSpan(8, 0, 0),
                                Motivo = "Cita de Control / Seguimiento",
                                Estado = "Pendiente",
                                Observaciones = "Generada automáticamente desde el consultorio.",
                                FechaCreacion = DateTime.Now
                            };
                            db.Citas.Add(nuevaCita);
                            db.SaveChanges();
                        }
                    }
                    else
                    {
                        var data = db.Consultas.Find(obj.ConsultaID);
                        if (data == null) return Json(new { success = false, mensaje = "El registro no existe." });

                        data.ExamenFisico = obj.ExamenFisico;
                        data.Diagnostico = obj.Diagnostico;
                        data.Tratamiento = obj.Tratamiento;
                        data.Indicaciones = obj.Indicaciones;
                        data.PesoKg = obj.PesoKg;
                        data.TallaCm = obj.TallaCm;
                        data.PresionArterial = obj.PresionArterial;
                        data.Temperatura = obj.Temperatura;
                        data.ProximaRevision = obj.ProximaRevision;

                        db.SaveChanges();
                    }

                    transaction.Commit();
                    return Json(new { success = true });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    string errorReal = ex.InnerException != null ? (ex.InnerException.InnerException != null ? ex.InnerException.InnerException.Message : ex.InnerException.Message) : ex.Message;
                    return Json(new { success = false, mensaje = errorReal });
                }
            }
        }

        [HttpGet]
        public JsonResult ObtenerCitasPendientes()
        {
            DateTime hoy = DateTime.Today;
            var citasDelDia = db.Citas
                .Where(c => (c.Estado == "Confirmada" || c.Estado == "Pendiente") && DbFunctions.TruncateTime(c.FechaCita) == hoy)
                .OrderBy(c => c.HoraCita)
                .Select(c => new
                {
                    Id = c.CitaID,
                    PacienteID = c.PacienteID,
                    MedicoID = c.MedicoID,
                    Nombres = c.Pacientes.Nombres + " " + c.Pacientes.Apellidos,
                    Motivo = c.Motivo,
                    Hora = c.HoraCita
                }).ToList();

            var listadoFormateado = citasDelDia.Select(c => new
            {
                Id = c.Id,
                Texto = $"[{c.Hora}] Cita #{c.Id} - {c.Nombres} ({c.Motivo})"
            }).ToList();

            return Json(listadoFormateado, JsonRequestBehavior.AllowGet);
        }

        public class ConsultaFichaDTO
        {
            public Consultas ConsultaData { get; set; }
            public List<DetalleRecetaDTO> ListaReceta { get; set; }
        }

        public class DetalleRecetaDTO
        {
            public int MedicamentoID { get; set; }
            public string Dosis { get; set; }
            public int Cantidad { get; set; }
            public string Indicaciones { get; set; }
        }
    }
}