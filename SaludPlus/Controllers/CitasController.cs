using SaludPlus.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using SaludPlus.Helpers;

namespace SaludPlus.Controllers
{
    [CustomAuthorize(Roles = "Medico,Recepcionista")]
    public class CitasController : Controller
    {
        private SaludPlussEntities1 db = new SaludPlussEntities1();

        // GET: Citas
        public ActionResult Index()
        {
            return View();
        }

        // GET: Citas/Calendario 
        public ActionResult Calendario()
        {
            return View();
        }

        // =======================================================
        // LISTAR CITAS 
        // =======================================================
        public JsonResult Listar()
        {
            try
            {
                var usuarioSesion = Session["User"] as Usuarios;
                if (usuarioSesion == null)
                {
                    return Json(new List<object>(), JsonRequestBehavior.AllowGet);
                }

                int usuarioId = usuarioSesion.UsuarioID;

                // 2. Buscamos el rol del usuario directamente en la base de datos
                var usuarioLogueado = db.Usuarios.Include("Roles").FirstOrDefault(u => u.UsuarioID == usuarioId);
                if (usuarioLogueado == null || usuarioLogueado.Roles == null)
                {
                    return Json(new List<object>(), JsonRequestBehavior.AllowGet);
                }

                string nombreRol = usuarioLogueado.Roles.Nombre.Trim().ToUpper();

                // Inicializamos la consulta base (apunta a todas las citas)
                var consultaCitas = db.Citas.AsQueryable();

                //Si es Médico, buscamos su MedicoID mediante la relación con UsuarioID
                if (nombreRol == "MEDICO" || nombreRol == "MÉDICO")
                {
                    var perfilMedico = db.Medicos.FirstOrDefault(m => m.UsuarioID == usuarioId);
                    if (perfilMedico != null)
                    {
                        consultaCitas = consultaCitas.Where(c => c.MedicoID == perfilMedico.MedicoID);
                    }
                    else
                    {
                        // Si no tiene perfil médico creado, devolvemos lista vacía para evitar colgar la vista
                        return Json(new List<object>(), JsonRequestBehavior.AllowGet);
                    }
                }
                // Si el rol es RECEPCIONISTA o ADMINISTRADOR, se ignora el bloque anterior y trae TODO de forma global.

                // 4. Ejecutamos la proyección LINQ
                var citas = consultaCitas.Select(c => new
                {
                    c.CitaID,
                    PacienteID = c.PacienteID,
                    PacienteNombre = c.Pacientes.Nombres + " " + c.Pacientes.Apellidos,
                    MedicoID = c.MedicoID,
                    MedicoNombre = c.Medicos.Usuarios.Nombres + " " + c.Medicos.Usuarios.Apellidos,
                    c.FechaCita,
                    c.HoraCita,
                    c.Motivo,
                    c.Estado,
                    c.Observaciones
                }).ToList();

                // 5. Formateamos strings para las columnas de la vista
                var resultadoFormateado = citas.Select(c => new
                {
                    c.CitaID,
                    c.PacienteID,
                    c.PacienteNombre,
                    c.MedicoID,
                    c.MedicoNombre,
                    FechaCitaStr = c.FechaCita.ToString("dd/MM/yyyy"),
                    HoraCitaStr = c.HoraCita.ToString(@"hh\:mm"),
                    c.Motivo,
                    c.Estado,
                    c.Observaciones
                }).ToList();

                return Json(resultadoFormateado, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // DETALLES / Id (Para cargar el modal de edición)
        [HttpGet]
        public JsonResult Consultar(int id)
        {
            var cita = db.Citas
                .Where(c => c.CitaID == id)
                .Select(c => new {
                    c.CitaID,
                    c.PacienteID,
                    c.MedicoID,
                    c.FechaCita,
                    c.HoraCita,
                    c.Motivo,
                    c.Estado,
                    c.Observaciones
                }).FirstOrDefault();

            if (cita == null)
            {
                return Json(new { success = false, mensaje = "Cita no encontrada" }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { success = true, data = cita }, JsonRequestBehavior.AllowGet);
        }

        // GUARDAR (AGENDAR Y EDITAR)
        [HttpPost]
        public JsonResult Guardar(Citas obj)
        {
            try
            {
                if (obj.CitaID == 0)
                {
                    // NUEVA CITA
                    obj.FechaCreacion = DateTime.Now;

                    if (string.IsNullOrEmpty(obj.Estado))
                    {
                        obj.Estado = "Pendiente";
                    }

                    db.Citas.Add(obj);
                }
                else
                {
                    // ACTUALIZAR CITA
                    var data = db.Citas.Find(obj.CitaID);
                    if (data == null)
                    {
                        return Json(new { success = false, mensaje = "El registro no existe." });
                    }

                    data.PacienteID = obj.PacienteID;
                    data.MedicoID = obj.MedicoID;
                    data.FechaCita = obj.FechaCita;
                    data.HoraCita = obj.HoraCita;
                    data.Motivo = obj.Motivo;
                    data.Observaciones = obj.Observaciones;

                    if (!string.IsNullOrEmpty(obj.Estado))
                    {
                        data.Estado = obj.Estado;
                    }
                }

                db.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = ex.Message });
            }
        }

        // CAMBIAR ESTADO (Confirmar, Cancelar)
        [HttpPost]
        public JsonResult CambiarEstado(int id, string nuevoEstado)
        {
            try
            {
                var data = db.Citas.Find(id);

                if (data != null)
                {
                    data.Estado = nuevoEstado;
                    db.Entry(data).State = System.Data.Entity.EntityState.Modified;
                    db.SaveChanges();

                    return Json(new { success = true });
                }

                return Json(new { success = false, mensaje = "La cita no existe." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = ex.Message });
            }
        }

        // OBTENER PACIENTES PARA EL SELECT (Solo los activos)
        [HttpGet]
        public JsonResult ObtenerPacientesActivos()
        {
            var pacientes = db.Pacientes.Where(p => p.Activo == true)
                .Select(p => new {
                    Id = p.PacienteID,
                    Texto = p.Nombres + " " + p.Apellidos
                }).ToList();
            return Json(pacientes, JsonRequestBehavior.AllowGet);
        }

        // OBTENER MÉDICOS PARA EL SELECT (Solo los activos)
        [HttpGet]
        public JsonResult ObtenerMedicosActivos()
        {
            var medicos = db.Medicos.Where(m => m.Activo == true)
                .Select(m => new {
                    Id = m.MedicoID,
                    Texto = m.Usuarios.Nombres + " " + m.Usuarios.Apellidos
                }).ToList();
            return Json(medicos, JsonRequestBehavior.AllowGet);
        }

        // =======================================================
        // ENDPOINT PARA FULLCALENDAR (FILTRADO INTELIGENTE POR ROL)
        // =======================================================
        [HttpGet]
        public JsonResult ObtenerEventosCalendario()
        {
            try
            {
                // 1. Usamos la misma validación de sesión de tu LoginController
                var usuarioSesion = Session["User"] as Usuarios;
                if (usuarioSesion == null)
                {
                    return Json(new List<object>(), JsonRequestBehavior.AllowGet);
                }

                int usuarioId = usuarioSesion.UsuarioID;

                var usuarioLogueado = db.Usuarios.Include("Roles").FirstOrDefault(u => u.UsuarioID == usuarioId);
                if (usuarioLogueado == null || usuarioLogueado.Roles == null)
                {
                    return Json(new List<object>(), JsonRequestBehavior.AllowGet);
                }

                string nombreRol = usuarioLogueado.Roles.Nombre.Trim().ToUpper();

                // Consulta base: ocultamos siempre las canceladas para mantener limpio el calendario
                var consultaCitas = db.Citas.Where(c => c.Estado != "Cancelada");

                // Aplicamos el filtro relacional solo si corresponde a un médico
                if (nombreRol == "MEDICO" || nombreRol == "MÉDICO")
                {
                    var perfilMedico = db.Medicos.FirstOrDefault(m => m.UsuarioID == usuarioId);
                    if (perfilMedico != null)
                    {
                        consultaCitas = consultaCitas.Where(c => c.MedicoID == perfilMedico.MedicoID);
                    }
                    else
                    {
                        return Json(new List<object>(), JsonRequestBehavior.AllowGet);
                    }
                }

                var citasLista = consultaCitas.ToList();

                // Mapeamos al formato exacto que pide FullCalendar incorporando extendedProps
                var eventos = citasLista.Select(c => new
                {
                    id = c.CitaID,
                    title = c.Pacientes.Nombres + " " + c.Pacientes.Apellidos,
                    start = c.FechaCita.ToString("yyyy-MM-dd") + "T" + c.HoraCita.ToString(@"hh\:mm\:ss"),

                    color = c.Estado == "Confirmada" ? "#198754" :
                            c.Estado == "Pendiente" ? "#ffc107" :
                            c.Estado == "Completada" ? "#0dcaf0" :
                            "#6c757d",
                    textColor = c.Estado == "Pendiente" ? "#000" : "#fff",

                    allDay = false,
                    extendedProps = new
                    {
                        motivo = c.Motivo
                    }
                }).ToList();

                return Json(eventos, JsonRequestBehavior.AllowGet);
            }
            catch (Exception)
            {
                return Json(new List<object>(), JsonRequestBehavior.AllowGet);
            }
        }
    }
}