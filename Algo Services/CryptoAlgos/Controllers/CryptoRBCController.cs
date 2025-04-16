using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CryptoAlgos.Controllers
{
    public class CryptoRBCController : Controller
    {
        // GET: CryptoRBCController
        public ActionResult Index()
        {
            return View();
        }

        // GET: CryptoRBCController/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: CryptoRBCController/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: CryptoRBCController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: CryptoRBCController/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: CryptoRBCController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: CryptoRBCController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: CryptoRBCController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}
