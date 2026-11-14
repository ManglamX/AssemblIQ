using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartTorque.Data;

var services = new ServiceCollection();
var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json")
    .Build();

var conn = config.GetConnectionString("Default");
var opts = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(conn).Options;
using var db = new AppDbContext(opts);

var unit = db.JobUnits.Include(u => u.Job).FirstOrDefault(u => u.TokenNo.Contains("61A619815BE5AF4A0AA8F1"));
if (unit != null)
{
    Console.WriteLine($"Unit found: JobId={unit.JobId}, JobNo={unit.Job.JobNo}, UnitIndex={unit.UnitIndex}, Token={unit.TokenNo}, Status={unit.Status}");
}

var allUnits = db.JobUnits.Where(u => u.TokenNo.Contains("61A619815BE5AF4A0AA8F1")).ToList();
foreach(var u in allUnits) {
    Console.WriteLine($"JobUnit: JobId={u.JobId}, UnitIndex={u.UnitIndex}, Token={u.TokenNo}");
}

var sessions = db.Sessions.Include(s => s.Job).Include(s => s.Results).Where(s => s.UnitNo == 2).ToList();
foreach(var s in sessions) {
    Console.WriteLine($"Session {s.SessionId}: JobId={s.JobId}, JobNo={s.Job.JobNo}, CompId={s.ProductComponentId}, ResultsCount={s.Results.Count}, Status={s.Status}");
    foreach(var r in s.Results) {
        Console.WriteLine($"   Result {r.ResultId}: StepId={r.StepId}, Result={r.Result}, Torque={r.Torque}, Attempt={r.AttemptNo}, CapturedAt={r.CapturedAt}");
    }
}
