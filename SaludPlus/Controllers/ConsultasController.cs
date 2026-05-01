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

            if (consulta == null)
            {
                return Json(new { success = false, mensaje = "Consulta no encontrada" }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { success = true, data = consulta }, JsonRequestBehavior.AllowGet);
        }

        // GUARDAR CONSULTA Y GENERAR RECETA AUTOMÁTICAMENTE
        [HttpPost]
        public JsonResult Guardar(ConsultaFichaDTO payload)
        {
            // Desempaquetamos los datos que vienen del JavaScript
            Consultas obj = payload.ConsultaData;
            List<DetalleRecetaDTO> listaMedicamentos = payload.ListaReceta;

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    if (obj.ConsultaID == 0)
                    {
                        // --- 1. GUARDAR LA CONSULTA ---
                        // Validamos que no haya duplicados
                        if (db.Consultas.Any(c => c.CitaID == obj.CitaID))
                        {
                            return Json(new { success = false, mensaje = "Esta cita ya tiene un expediente guardado." });
                        }

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
                        db.SaveChanges(); // Guardamos para que se genere el ID de la Consulta

                        // --- 2. GENERAR LA RECETA 
                        if (listaMedicamentos != null && listaMedicamentos.Count > 0)
                        {
                            Recetas nuevaReceta = new Recetas
                            {
                                ConsultaID = obj.ConsultaID,
                                MedicoID = obj.MedicoID,
                                PacienteID = obj.PacienteID,
                                FechaEmision = DateTime.Now,
                                FechaVencimiento = DateTime.Now.AddDays(15),
                                Estado = "Emitida", // Estado inicial para que Farmacia la vea amarilla
                                Observaciones = "Generada desde Consultorio"
                            };

                            db.Recetas.Add(nuevaReceta);
                            db.SaveChanges(); // Guardamos para generar el ID de la Receta

                            // --- 3. GUARDAR DETALLES Y DESCONTAR INVENTARIO ---
                            foreach (var item in listaMedicamentos)
                            {
                                DetalleReceta detalle = new DetalleReceta
                                {
                                    RecetaID = nuevaReceta.RecetaID,
                                    MedicamentoID = item.MedicamentoID,
                                    Dosis = item.Dosis,
                                    Cantidad = item.Cantidad,
                                    Indicaciones = item.Indicaciones,
                                    Estado = true // Inicializado como pendiente de despachar
                                };
                                db.DetalleReceta.Add(detalle);

                                // Buscamos la medicina en la BD para bajarle el stock
                                var medDB = db.Medicamentos.Find(item.MedicamentoID);

                                // Si NO es el comodín de "Solo Texto", le restamos el inventario
                                if (medDB != null && medDB.Nombre != "MEDICAMENTO EXTERNO (Solo texto)")
                                {
                                    medDB.StockActual -= item.Cantidad;
                                    db.Entry(medDB).State = EntityState.Modified;
                                }
                            }
                            db.SaveChanges(); // Confirmamos los detalles y el nuevo stock
                        }

                        // --- 4. PROGRAMAR PRÓXIMA CITA AUTOMÁTICA ---
                        if (obj.ProximaRevision.HasValue)
                        {
                            Citas nuevaCita = new Citas
                            {
                                PacienteID = obj.PacienteID,
                                MedicoID = obj.MedicoID,
                                FechaCita = obj.ProximaRevision.Value,
                                HoraCita = new TimeSpan(8, 0, 0), // Hora por defecto (8:00 AM)
                                Motivo = "Cita de Control / Seguimiento",
                                Estado = "Pendiente", 
                                Observaciones = "Generada automáticamente desde el consultorio por Próxima Revisión.",
                                FechaCreacion = DateTime.Now
                            };

                            db.Citas.Add(nuevaCita);
                            db.SaveChanges();
                        }
                    }
                    else
                    {
                        // ACTUALIZAR CONSULTA EXISTENTE (No tocamos la receta para evitar descuadres de inventario)
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
                .Select(c => new
                {
                    Id = c.CitaID,
                    PacienteID = c.PacienteID,
                    MedicoID = c.MedicoID,
                    Nombres = c.Pacientes.Nombres + " " + c.Pacientes.Apellidos,
                    Motivo = c.Motivo,
                    Hora = c.HoraCita
                })
                .ToList(); // Lo traemos a memoria

            // Formateamos el texto en memoria para evitar errores de traducción a SQL
            var listadoFormateado = citasDelDia.Select(c => new
            {
                Id = c.Id,
                // Agregamos la hora al inicio para que se vea claro en el ComboBox
                Texto = $"[{c.Hora}] Cita #{c.Id} - {c.Nombres} ({c.Motivo})"
            }).ToList();

            return Json(listadoFormateado, JsonRequestBehavior.AllowGet);
        }

        // =======================================================
        // DTOs: Clases auxiliares para recibir la Ficha + Receta
        // =======================================================
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