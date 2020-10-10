using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GlobalLayer;
using MarketView.Data;

namespace MarketView.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OptionChainsEFController : ControllerBase
    {
        private readonly DBContext _context;

        public OptionChainsEFController(DBContext context)
        {
            _context = context;
        }

        // GET: api/OptionChainsEF
        [HttpGet]
        public async Task<ActionResult<IEnumerable<OptionChain>>> GetOptionChain()
        {
            return await _context.OptionChain.ToListAsync();
        }

        // GET: api/OptionChainsEF/5
        [HttpGet("{id}")]
        public async Task<ActionResult<OptionChain>> GetOptionChain(decimal id)
        {
            var optionChain = await _context.OptionChain.FindAsync(id);

            if (optionChain == null)
            {
                return NotFound();
            }

            return optionChain;
        }

        // PUT: api/OptionChainsEF/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        // more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
        [HttpPut("{id}")]
        public async Task<IActionResult> PutOptionChain(decimal id, OptionChain optionChain)
        {
            if (id != optionChain.Strike)
            {
                return BadRequest();
            }

            _context.Entry(optionChain).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!OptionChainExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/OptionChainsEF
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        // more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
        [HttpPost]
        public async Task<ActionResult<OptionChain>> PostOptionChain(OptionChain optionChain)
        {
            _context.OptionChain.Add(optionChain);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (OptionChainExists(optionChain.Strike))
                {
                    return Conflict();
                }
                else
                {
                    throw;
                }
            }

            return CreatedAtAction("GetOptionChain", new { id = optionChain.Strike }, optionChain);
        }

        // DELETE: api/OptionChainsEF/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<OptionChain>> DeleteOptionChain(decimal id)
        {
            var optionChain = await _context.OptionChain.FindAsync(id);
            if (optionChain == null)
            {
                return NotFound();
            }

            _context.OptionChain.Remove(optionChain);
            await _context.SaveChangesAsync();

            return optionChain;
        }

        private bool OptionChainExists(decimal id)
        {
            return _context.OptionChain.Any(e => e.Strike == id);
        }
    }
}
