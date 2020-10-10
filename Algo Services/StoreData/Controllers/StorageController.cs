using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using GlobalLayer;
using Algos.TLogics;
using Algorithms.Utilities;
using ZMQFacade;
using System.Data;
using Microsoft.Extensions.Configuration;

namespace StoreData.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StorageController : Controller
    {
        // GET: StorageController
        public ActionResult Index()
        {
            return View();
        }

        // GET: StorageController/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: StorageController/Create
        public ActionResult Create()
        {
            return View();
        }

        [HttpGet]
        public void Get()
        {
            StartService();

            //var rng = new Random();
            //return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            //{
            //    Date = DateTime.Now.AddDays(index),
            //    TemperatureC = rng.Next(-20, 55),
            //    Summary = Summaries[rng.Next(Summaries.Length)]
            //})
            //.ToArray();
        }
        [HttpGet("startservice")]
        public void StartService()
        {
            //ZMQClient();
            //ExpiryTrade expiryTrade = new ExpiryTrade();

            //Storage storage = new Storage(false);
            //ZMQClient.ZMQSubcribeAllTicks(storage);
        }

        // POST: StorageController/Create
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

        // GET: StorageController/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: StorageController/Edit/5
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

        // GET: StorageController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: StorageController/Delete/5
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
