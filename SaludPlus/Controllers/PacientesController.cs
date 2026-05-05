using SaludPlus.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace SaludPlus.Controllers
{
    public class PacientesController : Controller
    {
        private SaludPlussEntities1 db = new SaludPlussEntities1();

        // GET: Pacientes
        public ActionResult Index()
        {
            return View();
        }

        // LISTAR PACIENTES ACTIVOS
        public JsonResult Listar()
        {
            var pacientes = db.Pacientes
                .Where(p => p.Activo == true)
                .OrderBy(p => p.Apellidos)
                .Select(p => new
                {
                    p.PacienteID,
                    p.Nombres,
                    p.Apellidos,
                    p.DUI,
                    p.FechaNacimiento,
                    p.Sexo,
                    p.Telefono,
                    p.Email,
                    p.Direccion,
                    p.TipoSangre,
                    p.Alergias,
                    p.AntecedentesMedicos,
                    p.FechaRegistro,
                    p.Activo
                }).ToList();

            return Json(pacientes, JsonRequestBehavior.AllowGet);
        }

        // LISTAR PACIENTES INACTIVOS
        public JsonResult ListarHistorial()
        {
            var pacientes = db.Pacientes
                .Where(p => p.Activo == false)
                .Select(p => new
                {
                    p.PacienteID,
                    p.Nombres,
                    p.Apellidos,
                    p.DUI,
                    p.FechaNacimiento,
                    p.Sexo,
                    p.Telefono,
                    p.Email,
                    p.Direccion,
                    p.TipoSangre,
                    p.Alergias,
                    p.AntecedentesMedicos,
                    p.FechaRegistro,
                    p.Activo
                }).ToList();

            return Json(pacientes, JsonRequestBehavior.AllowGet);
        }

        // DETALLES/Id
        [HttpGet]
        public JsonResult Consultar(int id)
        {
            var paciente = db.Pacientes
                .Where(p => p.PacienteID == id)
                .Select(p => new {
                    p.PacienteID,
                    p.Nombres,
                    p.Apellidos,
                    p.DUI,
                    p.FechaNacimiento,
                    p.Sexo,
                    p.Telefono,
                    p.Email,
                    p.Direccion,
                    p.TipoSangre,
                    p.Alergias,
                    p.AntecedentesMedicos,
                    p.FechaRegistro,
                    p.Activo
                }).FirstOrDefault();

            if (paciente == null)
            {
                return Json(new { success = false, mensaje = "Paciente no encontrado" }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { success = true, data = paciente }, JsonRequestBehavior.AllowGet);
        }

        // GUARDAR (INSERT Y UPDATE)
        [HttpPost]
        public JsonResult Guardar(Pacientes obj)
        {
            try
            {
                // Validar que DUI sea único
                bool existe = db.Pacientes.Any(P =>
                    P.DUI == obj.DUI
                    && P.PacienteID != obj.PacienteID); // importante para UPDATE

                if (existe)
                {
                    return Json(new { success = false, mensaje = "El número de DUI ya está registrado." });
                }

                if (obj.PacienteID == 0)
                {
                    obj.FechaRegistro = DateTime.Now;
                    db.Pacientes.Add(obj);
                }
                else
                {
                    var data = db.Pacientes.Find(obj.PacienteID);

                    data.Nombres = obj.Nombres;
                    data.Apellidos = obj.Apellidos;
                    data.DUI = obj.DUI;
                    data.FechaNacimiento = obj.FechaNacimiento;
                    data.Sexo = obj.Sexo;
                    data.Telefono= obj.Telefono;
                    data.Email = obj.Email;
                    data.Direccion = obj.Direccion;
                    data.TipoSangre = obj.TipoSangre;
                    data.Alergias = obj.Alergias;
                    data.AntecedentesMedicos =obj.AntecedentesMedicos;
                    data.Activo = obj.Activo;

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
                var data = db.Pacientes.Find(id);

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
    }
}