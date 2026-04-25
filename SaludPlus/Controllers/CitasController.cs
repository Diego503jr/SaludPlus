using SaludPlus.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace SaludPlus.Controllers
{
    public class CitasController : Controller
    {
        private SaludPlussEntities1 db = new SaludPlussEntities1();

        // GET: Citas
        public ActionResult Index()
        {
            return View();
        }

        // LISTAR CITAS 
        public JsonResult Listar()
        {
            var citas = db.Citas
                .Select(c => new
                {
                    c.CitaID,
                    PacienteID = c.PacienteID,
                    // Concatenamos Nombres y Apellidos del Paciente
                    PacienteNombre = c.Pacientes.Nombres + " " + c.Pacientes.Apellidos,
                    MedicoID = c.MedicoID,
                    MedicoNombre = c.Medicos.Usuarios.Nombres + " " + c.Medicos.Usuarios.Apellidos,
                    c.FechaCita,
                    c.HoraCita,
                    c.Motivo,
                    c.Estado,
                    c.Observaciones
                }).ToList();

            return Json(citas, JsonRequestBehavior.AllowGet);
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

                    // Si no se envía un estado desde la vista, por defecto es Pendiente
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

                    // Solo actualizamos el estado si se está modificando explícitamente
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
                    // nuevoEstado puede ser "Confirmada", "Cancelada", etc.
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
                    // Cruzamos con la tabla Usuarios para sacar el nombre
                    Texto = m.Usuarios.Nombres + " " + m.Usuarios.Apellidos
                }).ToList();
            return Json(medicos, JsonRequestBehavior.AllowGet);
        }

    }
}