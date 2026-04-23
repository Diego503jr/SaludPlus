using SaludPlus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SaludPlus.Controllers
{
    public class MedicosController : Controller
    {
        private SaludPlussEntities1 db = new SaludPlussEntities1();

        // GET: Médicos
        public ActionResult Index()
        {
            return View();
        }

        // LISTAR MEDICOS
        public JsonResult Listar()
        {
            var medicos = db.Medicos
                .Where(m => m.Usuarios.Activo == true)
                .OrderBy(m => m.Especialidades.Nombre) //Ordenar por especialidad
                .Select(m => new
                {
                    //Tabla Médicos
                    m.MedicoID,
                    m.NumeroLicencia,
                    m.Consultorio,
                    m.HoraEntrada,
                    m.HoraSalida,
                    m.Activo,
                    m.UsuarioID,
                    m.EspecialidadID,

                    //Tabla Usuarios
                    MedicoNombre = m.Usuarios.Nombres,
                    MedicoApellido = m.Usuarios.Apellidos,
                    MedicoEmail = m.Usuarios.Email,
                    MedicoTelefono = m.Usuarios.Telefono,

                    //Tabla Especialidades
                    MedicoEspecialidad = m.Especialidades.Nombre

                }).ToList();

            return Json(medicos, JsonRequestBehavior.AllowGet);
        }

        // DETALLES/Id
        [HttpGet]
        public JsonResult Consultar(int id)
        {
            var medico = db.Medicos
                .Where(m => m.MedicoID == id)
                .Select(m => new {
                    //Tabla Médicos
                    m.MedicoID,
                    m.NumeroLicencia,
                    m.Consultorio,
                    m.HoraEntrada,
                    m.HoraSalida,
                    m.Activo,

                    //Tabla Usuarios
                    UsuarioID = m.UsuarioID,
                    MedicoNombre = m.Usuarios.Nombres,
                    MedicoApellido = m.Usuarios.Apellidos,
                    MedicoEmail = m.Usuarios.Email,
                    MedicoTelefono = m.Usuarios.Telefono,

                    //Tabla Especialidades
                    EspecialidadID = m.EspecialidadID,
                    MedicoEspecialidad = m.Especialidades.Nombre
                }).FirstOrDefault();

            if (medico == null)
            {
                return Json(new { success = false, mensaje = "Médico no encontrado" }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { success = true, data = medico }, JsonRequestBehavior.AllowGet);
        }

        // GUARDAR (INSERT Y UPDATE)
        [HttpPost]
        public JsonResult Guardar(Medicos med)
        {
            try
            {
                if (med.MedicoID == 0)
                {
                    db.Medicos.Add(med);
                }
                else
                {
                    var data = db.Medicos.Find(med.MedicoID);

                    
                    data.NumeroLicencia = med.NumeroLicencia;
                    data.Consultorio = med.Consultorio;
                    data.HoraEntrada = med.HoraEntrada;
                    data.HoraSalida = med.HoraSalida;
                    data.Activo = med.Activo;
                    data.UsuarioID = med.UsuarioID;
                    data.EspecialidadID = med.EspecialidadID;
                }

                db.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = ex.Message });
            }
        }

        // ELIMINAR
        [HttpPost]
        public JsonResult Eliminar(int id)
        {
            try
            {
                var data = db.Usuarios.Find(id);

                if (data != null)
                {
                    //Cambio de estado
                    data.Activo = false;

                    db.Entry(data).State = System.Data.Entity.EntityState.Modified;

                    db.SaveChanges();
                    return Json(new { success = true });
                }

                return Json(new { success = false, mensaje = "El registro no existe." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = ex.Message });
            }
        }

        // Obtener información para llenado de ComboBox

        [HttpGet]
        public JsonResult ObtenerEspecialidadesActivas()
        {
            var especialidades = db.Especialidades.Where(e => e.Activo == true)
                .Select(e => new {
                    Id = e.EspecialidadID,
                    Nombre = e.Nombre
                }).ToList();
            return Json(especialidades, JsonRequestBehavior.AllowGet);
        }

        //[HttpGet]
        //public JsonResult ListarUsuariosMedicos()
        //{
        //    var lista = db.Usuarios
        //        .Select(u => new {
        //            UsuarioID = u.UsuarioID,
        //            NombreCompleto = u.Nombres + " " + u.Apellidos
        //        }).ToList();
        //    return Json(lista, JsonRequestBehavior.AllowGet);
        //}

        [HttpGet]
        public JsonResult ListarUsuariosMedicos()
        {
            // Filtros: 
            // 1. Que el RolID sea 2 (Médicos)
            // 2. Que el usuario esté Activo (Activo == true)
            // 3. Que NO exista registro en la tabla Medicos relacionado a ese UsuarioID
            var lista = db.Usuarios
                .Where(u => u.RolID == 2 &&
                            u.Activo == true &&
                            !db.Medicos.Any(m => m.UsuarioID == u.UsuarioID))
                .Select(u => new
                {
                    UsuarioID = u.UsuarioID,
                    NombreCompleto = u.Nombres + " " + u.Apellidos
                })
                .ToList();

            return Json(lista, JsonRequestBehavior.AllowGet);
        }

    }
}