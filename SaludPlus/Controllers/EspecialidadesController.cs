using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using SaludPlus.Models;

namespace SaludPlus.Controllers
{
    public class EspecialidadesController : Controller
    {
        private SaludPlussEntities1 db = new SaludPlussEntities1();

        // GET: Especialidades
        public ActionResult Index()
        {
            return View();
        }

        public JsonResult Listar()
        {
            var espe = db.Especialidades
                .Select(x => new
                {
                    x.EspecialidadID,
                    x.Nombre,
                    x.Activo
                }).ToList();

            return Json(espe, JsonRequestBehavior.AllowGet);
        }
        // DETALLES/Id
        [HttpGet]
        public JsonResult Consultar(int id)
        {
            var espe = db.Especialidades.Where(x => x.EspecialidadID == id)
                .Select(x => new
                {
                    x.EspecialidadID,
                    x.Nombre,
                    x.Activo
                }).FirstOrDefault();

            if (espe == null)
            {
                return Json(new { success = false, mensaje = "Especialidad no encontrada." }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { success = true, data = espe }, JsonRequestBehavior.AllowGet);    
        }

        // GUARDAR (INSERT Y UPDATE)
        [HttpPost]
        public JsonResult Guardar(Especialidades espe)
        {
            try
            {
                if (espe.EspecialidadID == 0)
                {
                    db.Especialidades.Add(espe);
                }
                else
                {
                    var dt = db.Especialidades.Find(espe.EspecialidadID);

                    dt.Nombre = espe.Nombre;
                    dt.Activo = espe.Activo;
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
                var dt = db.Especialidades.Find(id);

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
    }
}
