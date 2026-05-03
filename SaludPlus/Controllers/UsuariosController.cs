using Microsoft.Ajax.Utilities;
using SaludPlus.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Services.Description;
using SaludPlus.Helpers;

namespace SaludPlus.Controllers
{
    public class UsuariosController : Controller
    {
        private SaludPlussEntities1 db = new SaludPlussEntities1();

        // GET: Usuarios
        public ActionResult Index()
        {
            return View();
        }

        // LISTAR USUARIOS
        public JsonResult Listar()
        {
            var usus = db.Usuarios
                .OrderBy(u => u.Nombres)
                .Select(u => new
                {
                    //Usuario
                    u.UsuarioID,
                    u.Nombres,
                    u.Apellidos,
                    u.Email,
                    u.Telefono,
                    u.Activo,
                    u.UltimoAcceso,
                    u.RolID,

                    //Roles
                    RolNombre = u.Roles != null ? u.Roles.Nombre : "Sin Rol"
                }).ToList();

            return Json(usus, JsonRequestBehavior.AllowGet);
        }

        // DETALLES/Id
        [HttpGet]
        public JsonResult Consultar(int id)
        {
            var usuario = db.Usuarios
                .Where(u => u.UsuarioID == id)
                .Select(u => new
                {
                    u.UsuarioID,
                    u.Nombres,
                    u.Apellidos,
                    u.Email,
                    u.Telefono,
                    u.Activo,
                    u.UltimoAcceso,
                    u.FechaCreacion,

                    RolNombre = u.Roles != null ? u.Roles.Nombre : "Sin Rol"
                }).ToList();

            if (usuario == null)
            {
                return Json(new { success = false, mensaje = "Médico no encontrado" }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { success = true, data = usuario }, JsonRequestBehavior.AllowGet);
        }

        // GUARDAR (INSERT Y UPDATE)
        [HttpPost]
        public JsonResult Guardar(Usuarios usu)
        {
            try
            {
                string claveHashed = SecurityHelper.GetSHA256(usu.PasswordHash);
                if (usu.UsuarioID == 0)
                {
                    // Registramos la pwd hasheada en la db y obtenemos la fecha actual de creacion
                    usu.PasswordHash = SecurityHelper.GetSHA256(usu.PasswordHash);
                    usu.FechaCreacion = DateTime.Now;
                    db.Usuarios.Add(usu);
                }
                else
                {
                    var dt = db.Usuarios.Find(usu.UsuarioID);

                    dt.Nombres = usu.Nombres;
                    dt.Apellidos = usu.Apellidos;
                    dt.Email = usu.Email;
                    // Verificamos si se va actualizar la pwd
                    if (usu.PasswordHash != null) dt.PasswordHash = SecurityHelper.GetSHA256(usu.PasswordHash);
                    dt.Telefono = usu.Telefono;
                    dt.Activo = usu.Activo;
                    dt.FechaCreacion = DateTime.Now;
                }

                db.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ELIMINAR
        [HttpPost]
        public JsonResult Eliminar(int id)
        {
            try
            {
                var dt = db.Usuarios.Find(id);

                if (dt != null)
                {
                    //Cambio de estado
                    dt.Activo = false;

                    db.Entry(dt).State = System.Data.Entity.EntityState.Modified;

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

        // LISTAR ROLES
        [HttpGet]
        public JsonResult ObtenerRoles()
        {
            var roles = db.Roles.Where(r => r.Activo == true)
                .Select(r => new
                {
                    Id = r.RolID,
                    Nombre = r.Nombre
                }).ToList();

            return Json(roles, JsonRequestBehavior.AllowGet);
        }
    }
}
