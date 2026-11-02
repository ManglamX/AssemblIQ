using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;

namespace SmartTorque.Web.Controllers;

public class JobAllocationViewModel
{
    public int? CurrentDepartmentId { get; set; }
    public string CurrentDepartmentName { get; set; } = string.Empty;
    public List<Department> AvailableDepartments { get; set; } = new();
    public List<JobItemDto> UnallocatedJobs { get; set; } = new();
    public List<JobItemDto> AllocatedJobs { get; set; } = new();
    public List<WorkerItemDto> Workers { get; set; } = new();
    public List<WorkerItemDto> Supervisors { get; set; } = new();
    public List<WorkerItemDto> DeptHeads { get; set; } = new();
    public List<LineItemDto> Lines { get; set; } = new();
    public List<ProductItemDto> AvailableProducts { get; set; } = new();
    public bool IsDepartmentAdmin { get; set; }
    public int? SupervisorLineId { get; set; }
    public string? SupervisorLineName { get; set; }
}

public class ProductItemDto
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
}

public class JobItemDto
{
    public int JobId { get; set; }
    public string JobNo { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int QuantityCompleted { get; set; }
    public PriorityLevel Priority { get; set; }
    public decimal EstimatedWorkload { get; set; }
    public string? AssignedWorkerId { get; set; }
    public string? AssignedWorkerName { get; set; }
    public int? AssemblyLineId { get; set; }
    public string? AssemblyLineName { get; set; }
    public JobStatus Status { get; set; }
    public DateOnly? DueDate { get; set; }
    public AllocationScopeType ScopeType { get; set; } = AllocationScopeType.FullProduct;
    public string? ScopeSummary { get; set; }
    public string? AllowedTemplateIds { get; set; }
    public string? AllowedStepIds { get; set; }
}

public class WorkerItemDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? EmployeeNo { get; set; }
    public string RoleName { get; set; } = "Worker";
    public int? CurrentAssemblyLineId { get; set; }
    public string? CurrentAssemblyLineName { get; set; }
    public int? WorkStationId { get; set; }
    public string? WorkStationName { get; set; }
    public decimal CurrentWorkload { get; set; }
    public int ActiveJobCount { get; set; }
}

public class LineItemDto
{
    public int AssemblyLineId { get; set; }
    public string AssemblyName { get; set; } = string.Empty;
    public string AssemblyCode { get; set; } = string.Empty;
    public List<StationItemDto> Stations { get; set; } = new();
}

public class StationItemDto
{
    public int WorkStationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public string StationCode { get; set; } = string.Empty;
}

[Authorize(Roles = "Supervisor,Admin")]
public class SupervisorController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserContextService _userContext;
    private readonly ILogger<SupervisorController> _logger;

    public SupervisorController(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserContextService userContext,
        ILogger<SupervisorController> logger)
    {
        _db = db;
        _userManager = userManager;
        _userContext = userContext;
        _logger = logger;
    }

    [HttpGet]
    [Route("Supervisor/JobAllocation")]
    public async Task<IActionResult> JobAllocation(int? departmentId = null)
    {
        ViewData["Title"] = "Shop Floor Job Allocation";
        ViewData["ActiveNav"] = "JobAllocation";

        var currentUser = await _userManager.GetUserAsync(User);
        var ctx = await _userContext.GetCurrentUserContextAsync(User);

        // Effective department scoping
        int? effectiveDeptId = ctx?.GetEffectiveDepartmentId(departmentId);

        var vm = new JobAllocationViewModel();

        // Available departments
        if (ctx?.HasFullPlantAccess == true)
        {
            vm.AvailableDepartments = await _db.Departments
                .Where(d => d.IsActive && (!ctx.PlantId.HasValue || d.PlantId == ctx.PlantId.Value))
                .OrderBy(d => d.Name)
                .ToListAsync();

            if (!effectiveDeptId.HasValue && vm.AvailableDepartments.Count > 0)
            {
                effectiveDeptId = vm.AvailableDepartments[0].DepartmentId;
            }
        }
        else if (ctx?.DepartmentId.HasValue == true)
        {
            var myDept = await _db.Departments.FindAsync(ctx.DepartmentId.Value);
            if (myDept != null)
            {
                vm.AvailableDepartments = new List<Department> { myDept };
                effectiveDeptId = myDept.DepartmentId;
            }
        }

        vm.CurrentDepartmentId = effectiveDeptId;
        if (effectiveDeptId.HasValue)
        {
            var dept = await _db.Departments.FindAsync(effectiveDeptId.Value);
            vm.CurrentDepartmentName = dept?.Name ?? "Selected Department";
        }

        // 1. Fetch Lines and Stations in this Department
        var lines = await _db.AssemblyLines
            .Where(a => a.IsActive && (!effectiveDeptId.HasValue || a.DepartmentId == effectiveDeptId.Value))
            .Include(a => a.WorkStations)
            .OrderBy(a => a.AssemblyName)
            .ToListAsync();

        vm.Lines = lines.Select(l => new LineItemDto
        {
            AssemblyLineId = l.AssemblyLineId,
            AssemblyName = l.AssemblyName,
            AssemblyCode = l.AssemblyCode,
            Stations = l.WorkStations.Where(s => s.IsActive).Select(s => new StationItemDto
            {
                WorkStationId = s.WorkStationId,
                StationName = s.StationName,
                StationCode = s.StationCode
            }).ToList()
        }).ToList();

        // 2. Fetch Users in this Department and query roles to strictly differentiate Workers vs Supervisors vs Dept Heads
        var workerQuery = _db.Users
            .Where(u => u.IsActive && u.RegistrationStatus == RegistrationStatus.Approved);

        if (effectiveDeptId.HasValue)
        {
            workerQuery = workerQuery.Where(u => u.DepartmentId == effectiveDeptId.Value);
        }

        var deptUsers = await workerQuery
            .Include(u => u.CurrentAssemblyLine)
            .Include(u => u.WorkStation)
            .ToListAsync();

        var userRoleMapping = await (from ur in _db.UserRoles
                                     join r in _db.Roles on ur.RoleId equals r.Id
                                     select new { ur.UserId, RoleName = r.Name })
                                    .ToListAsync();

        // Fetch active jobs to compute workloads
        var activeJobs = await _db.Jobs
            .Where(j => j.Status != JobStatus.Completed && j.Status != JobStatus.Cancelled)
            .ToListAsync();

        var allDeptUserDtos = deptUsers.Select(w =>
        {
            var userJobs = activeJobs.Where(j => j.AssignedWorkerId == w.Id).ToList();
            var role = userRoleMapping.FirstOrDefault(r => r.UserId == w.Id)?.RoleName ?? w.RequestedRole ?? "Worker";
            return new WorkerItemDto
            {
                Id = w.Id,
                FullName = w.FullName,
                Email = w.Email ?? string.Empty,
                EmployeeNo = w.EmployeeNo,
                RoleName = role,
                CurrentAssemblyLineId = w.CurrentAssemblyLineId,
                CurrentAssemblyLineName = w.CurrentAssemblyLine?.AssemblyName,
                WorkStationId = w.WorkStationId,
                WorkStationName = w.WorkStation?.StationName,
                CurrentWorkload = userJobs.Sum(j => j.EstimatedWorkload),
                ActiveJobCount = userJobs.Count
            };
        }).ToList();

        // Strictly separate Workers from Supervisors and Department Admins
        vm.Workers = allDeptUserDtos.Where(u => u.RoleName == "Worker").OrderBy(w => w.CurrentWorkload).ThenBy(w => w.FullName).ToList();
        vm.Supervisors = allDeptUserDtos.Where(u => u.RoleName == "Supervisor").OrderBy(w => w.FullName).ToList();
        vm.DeptHeads = allDeptUserDtos.Where(u => u.RoleName == "Admin").OrderBy(w => w.FullName).ToList();

        // 3. Fetch Jobs for this Department
        var deptJobsQuery = _db.Jobs
            .Include(j => j.Product)
            .Include(j => j.AssignedWorker)
            .Include(j => j.AssemblyLine)
            .Where(j => j.Status != JobStatus.Completed && j.Status != JobStatus.Cancelled);

        if (effectiveDeptId.HasValue)
        {
            deptJobsQuery = deptJobsQuery.Where(j => j.Product.DepartmentId == effectiveDeptId.Value ||
                (j.AssemblyLine != null && j.AssemblyLine.DepartmentId == effectiveDeptId.Value));
        }

        var allDeptJobs = await deptJobsQuery.OrderBy(j => j.Priority).ThenBy(j => j.DueDate).ToListAsync();

        var isAdmin = User.IsInRole("Admin");
        vm.IsDepartmentAdmin = isAdmin || (ctx?.HasFullPlantAccess == true);

        // If line supervisor is assigned to a specific line, filter jobs to that line
        if (currentUser?.CurrentAssemblyLineId.HasValue == true && !isAdmin)
        {
            vm.SupervisorLineId = currentUser.CurrentAssemblyLineId.Value;
            var assignedLine = vm.Lines.FirstOrDefault(l => l.AssemblyLineId == vm.SupervisorLineId.Value);
            vm.SupervisorLineName = assignedLine?.AssemblyName;

            allDeptJobs = allDeptJobs.Where(j => j.AssemblyLineId == vm.SupervisorLineId.Value).ToList();
        }

        vm.UnallocatedJobs = allDeptJobs.Where(j => string.IsNullOrEmpty(j.AssignedWorkerId)).Select(MapJob).ToList();
        vm.AllocatedJobs = allDeptJobs.Where(j => !string.IsNullOrEmpty(j.AssignedWorkerId)).Select(MapJob).ToList();

        // 4. Fetch Products available for this Department for allocation
        var prodQuery = _db.Products.Where(p => p.IsActive);
        if (effectiveDeptId.HasValue)
        {
            prodQuery = prodQuery.Where(p => p.DepartmentId == effectiveDeptId.Value || p.DepartmentId == null);
        }
        vm.AvailableProducts = await prodQuery.OrderBy(p => p.ProductName).Select(p => new ProductItemDto
        {
            ProductId = p.ProductId,
            ProductCode = p.ProductCode,
            ProductName = p.ProductName
        }).ToListAsync();

        return View("~/Views/Supervisor/JobAllocation.cshtml", vm);
    }

    private static JobItemDto MapJob(Job j) => new()
    {
        JobId = j.JobId,
        JobNo = j.JobNo,
        ProductName = j.Product.ProductName,
        ProductCode = j.Product.ProductCode,
        Quantity = j.Quantity,
        QuantityCompleted = j.QuantityCompleted,
        Priority = j.Priority,
        EstimatedWorkload = j.EstimatedWorkload,
        AssignedWorkerId = j.AssignedWorkerId,
        AssignedWorkerName = j.AssignedWorker?.FullName,
        AssemblyLineId = j.AssemblyLineId,
        AssemblyLineName = j.AssemblyLine?.AssemblyName,
        Status = j.Status,
        DueDate = j.DueDate,
        ScopeType = j.ScopeType,
        ScopeSummary = j.ScopeSummary,
        AllowedTemplateIds = j.AllowedTemplateIds,
        AllowedStepIds = j.AllowedStepIds
    };

    [HttpGet]
    [Route("Supervisor/GetProductStructure")]
    public async Task<IActionResult> GetProductStructure(int productId)
    {
        var product = await _db.Products
            .Include(p => p.Components.OrderBy(c => c.AssemblyOrder))
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps.OrderBy(s => s.SequenceNo))
            .FirstOrDefaultAsync(p => p.ProductId == productId);

        if (product == null)
            return NotFound(new { success = false, message = "Product not found." });

        var existingMappings = await _db.ProductAssemblyMappings
            .Include(m => m.AssemblyLine)
            .Where(m => m.ProductId == productId)
            .ToListAsync();

        var allSteps = product.Components.SelectMany(c => c.Template.Steps).ToList();
        var totalStepsCount = allSteps.Count;

        var stepMappingLookup = new Dictionary<int, (int LineId, string LineCode, string LineName)>();
        var templateMappingLookup = new Dictionary<int, (int LineId, string LineCode, string LineName)>();

        foreach (var m in existingMappings)
        {
            var lineName = m.AssemblyLine?.AssemblyName ?? "Line " + m.AssemblyLineId;
            var lineCode = m.AssemblyLine?.AssemblyCode ?? "LINE";

            if (m.ScopeType == AllocationScopeType.FullProduct)
            {
                foreach (var s in allSteps)
                {
                    if (!stepMappingLookup.ContainsKey(s.TemplateStepId))
                        stepMappingLookup[s.TemplateStepId] = (m.AssemblyLineId, lineCode, lineName);
                }
                foreach (var c in product.Components)
                {
                    if (!templateMappingLookup.ContainsKey(c.TemplateId))
                        templateMappingLookup[c.TemplateId] = (m.AssemblyLineId, lineCode, lineName);
                }
            }
            else if (m.ScopeType == AllocationScopeType.SelectedTemplates && !string.IsNullOrEmpty(m.AllowedTemplateIds))
            {
                var tIds = m.AllowedTemplateIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToHashSet();
                foreach (var c in product.Components.Where(c => tIds.Contains(c.TemplateId)))
                {
                    if (!templateMappingLookup.ContainsKey(c.TemplateId))
                        templateMappingLookup[c.TemplateId] = (m.AssemblyLineId, lineCode, lineName);

                    foreach (var s in c.Template.Steps)
                    {
                        if (!stepMappingLookup.ContainsKey(s.TemplateStepId))
                            stepMappingLookup[s.TemplateStepId] = (m.AssemblyLineId, lineCode, lineName);
                    }
                }
            }
            else if (m.ScopeType == AllocationScopeType.SelectedSteps && !string.IsNullOrEmpty(m.AllowedStepIds))
            {
                var sIds = m.AllowedStepIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse);
                foreach (var sid in sIds)
                {
                    if (!stepMappingLookup.ContainsKey(sid))
                        stepMappingLookup[sid] = (m.AssemblyLineId, lineCode, lineName);
                }
            }
        }

        var mappedStepsCount = stepMappingLookup.Count;
        var isFullyMapped = totalStepsCount > 0 && mappedStepsCount >= totalStepsCount;

        var result = new
        {
            productId = product.ProductId,
            productCode = product.ProductCode,
            productName = product.ProductName,
            totalStepsCount,
            mappedStepsCount,
            isFullyMapped,
            existingMappings = existingMappings.Select(m => new
            {
                assemblyLineId = m.AssemblyLineId,
                assemblyCode = m.AssemblyLine?.AssemblyCode,
                assemblyName = m.AssemblyLine?.AssemblyName,
                isDefault = m.IsDefault,
                scopeType = (int)m.ScopeType,
                scopeSummary = m.ScopeSummary
            }).ToList(),
            components = product.Components.OrderBy(c => c.AssemblyOrder).Select(c =>
            {
                var hasTmplMap = templateMappingLookup.TryGetValue(c.TemplateId, out var tmplMap);
                return new
                {
                    productComponentId = c.ProductComponentId,
                    componentName = c.ComponentName,
                    assemblyOrder = c.AssemblyOrder,
                    templateId = c.TemplateId,
                    templateCode = c.Template.TemplateCode,
                    templateName = c.Template.Name,
                    isMapped = hasTmplMap,
                    mappedLineId = hasTmplMap ? tmplMap.LineId : (int?)null,
                    mappedLineCode = hasTmplMap ? tmplMap.LineCode : null,
                    mappedLineName = hasTmplMap ? tmplMap.LineName : null,
                    steps = c.Template.Steps.OrderBy(s => s.SequenceNo).Select(s =>
                    {
                        var hasStepMap = stepMappingLookup.TryGetValue(s.TemplateStepId, out var stepMap);
                        return new
                        {
                            stepId = s.TemplateStepId,
                            sequenceNo = s.SequenceNo,
                            label = s.Label,
                            instrument = s.Instrument.ToString(),
                            target = s.Target,
                            toleranceMinus = s.ToleranceMinus,
                            tolerancePlus = s.TolerancePlus,
                            toleranceMode = s.ToleranceMode.ToString(),
                            isMapped = hasStepMap,
                            mappedLineId = hasStepMap ? stepMap.LineId : (int?)null,
                            mappedLineCode = hasStepMap ? stepMap.LineCode : null,
                            mappedLineName = hasStepMap ? stepMap.LineName : null
                        };
                    }).ToList()
                };
            }).ToList()
        };

        return Json(result);
    }

    public record AllocateProductPackageModel(
        int ProductId,
        int TargetLineId,
        AllocationScopeType ScopeType,
        List<int>? TemplateIds,
        List<int>? StepIds,
        int Quantity,
        PriorityLevel Priority,
        DateOnly? DueDate,
        string? ScopeSummary
    );

    [HttpPost]
    [Route("Supervisor/AllocateProductPackage")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AllocateProductPackage([FromBody] AllocateProductPackageModel model)
    {
        var product = await _db.Products
            .Include(p => p.Components)
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps)
            .FirstOrDefaultAsync(p => p.ProductId == model.ProductId);

        if (product == null) return NotFound(new { success = false, message = "Product not found." });

        var line = await _db.AssemblyLines.FindAsync(model.TargetLineId);
        if (line == null) return NotFound(new { success = false, message = "Target assembly line / cell not found." });

        var user = await _userManager.GetUserAsync(User);

        // Generate unique Job No
        var todayStr = DateTime.UtcNow.ToString("yyMMdd");
        var existingCountToday = await _db.Jobs.CountAsync(j => j.JobNo.StartsWith($"JOB-{todayStr}-"));
        var jobNo = $"JOB-{todayStr}-{(existingCountToday + 1):D4}";

        string? allowedTemplates = null;
        string? allowedSteps = null;
        string summary = model.ScopeSummary?.Trim() ?? string.Empty;

        if (model.ScopeType == AllocationScopeType.SelectedTemplates && model.TemplateIds?.Count > 0)
        {
            allowedTemplates = string.Join(",", model.TemplateIds.Distinct());
            if (string.IsNullOrEmpty(summary))
            {
                var matchedTemplates = product.Components
                    .Where(c => model.TemplateIds.Contains(c.TemplateId))
                    .Select(c => c.ComponentName)
                    .Distinct();
                summary = $"Templates: {string.Join(", ", matchedTemplates)}";
            }
        }
        else if (model.ScopeType == AllocationScopeType.SelectedSteps && model.StepIds?.Count > 0)
        {
            allowedSteps = string.Join(",", model.StepIds.Distinct());

            // Determine parent templates for selected steps
            var parentTemplateIds = product.Components
                .Where(c => c.Template.Steps.Any(s => model.StepIds.Contains(s.TemplateStepId)))
                .Select(c => c.TemplateId)
                .Distinct()
                .ToList();
            allowedTemplates = string.Join(",", parentTemplateIds);

            if (string.IsNullOrEmpty(summary))
            {
                summary = $"Selected Operations ({model.StepIds.Count} Steps)";
            }
        }
        else
        {
            var lineMapping = await _db.ProductAssemblyMappings
                .FirstOrDefaultAsync(m => m.ProductId == product.ProductId && m.AssemblyLineId == line.AssemblyLineId);
            if (lineMapping != null && lineMapping.ScopeType != AllocationScopeType.FullProduct)
            {
                allowedTemplates = lineMapping.AllowedTemplateIds;
                allowedSteps = lineMapping.AllowedStepIds;
                summary = lineMapping.ScopeSummary ?? "Mapped Operations";
                model = model with { ScopeType = lineMapping.ScopeType };
            }
            else
            {
                summary = "Full Product Assembly";
            }
        }

        var newJob = new Job
        {
            JobNo = jobNo,
            ProductId = product.ProductId,
            AssemblyLineId = line.AssemblyLineId,
            Quantity = model.Quantity > 0 ? model.Quantity : 1,
            QuantityCompleted = 0,
            Priority = model.Priority,
            Status = JobStatus.Pending,
            DueDate = model.DueDate,
            EstimatedWorkload = Math.Max(1.0m, model.Quantity * 1.0m),
            ScopeType = model.ScopeType,
            AllowedTemplateIds = allowedTemplates,
            AllowedStepIds = allowedSteps,
            ScopeSummary = summary,
            CreatedAt = DateTime.UtcNow
        };

        _db.Jobs.Add(newJob);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Product {Product} allocated to Line {Line} as {JobNo} with Scope {Scope} by {User}",
            product.ProductName, line.AssemblyName, jobNo, summary, user?.Email);

        return Json(new { success = true, jobNo = newJob.JobNo, message = $"Job {newJob.JobNo} allocated to {line.AssemblyName} ({summary})." });
    }

    public record AllocateJobModel(int JobId, string WorkerId, int? AssemblyLineId, int? WorkStationId);

    [HttpPost]
    [Route("Supervisor/AllocateJob")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AllocateJob([FromBody] AllocateJobModel model)
    {
        var job = await _db.Jobs.FindAsync(model.JobId);
        if (job == null) return NotFound(new { success = false, message = "Job not found." });

        var worker = await _db.Users.FindAsync(model.WorkerId);
        if (worker == null) return NotFound(new { success = false, message = "Worker not found." });

        var isWorkerRole = await _userManager.IsInRoleAsync(worker, "Worker");
        if (!isWorkerRole && worker.RequestedRole != "Worker")
        {
            return BadRequest(new { success = false, message = "Only approved shop floor workers can be assigned to job operations." });
        }

        var supervisor = await _userManager.GetUserAsync(User);

        job.AssignedWorkerId = worker.Id;
        job.AssignedSupervisorId = supervisor?.Id;
        job.AssignedAt = DateTime.UtcNow;
        if (model.AssemblyLineId.HasValue)
        {
            job.AssemblyLineId = model.AssemblyLineId.Value;
            worker.CurrentAssemblyLineId = model.AssemblyLineId.Value;
        }
        if (model.WorkStationId.HasValue)
        {
            worker.WorkStationId = model.WorkStationId.Value;
        }

        if (job.Status == JobStatus.Pending)
        {
            job.Status = JobStatus.Pending; // Ready in worker queue
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Job {JobNo} allocated to worker {Worker} by supervisor {Supervisor}",
            job.JobNo, worker.FullName, supervisor?.Email);

        return Json(new { success = true, message = $"Job {job.JobNo} allocated to {worker.FullName}." });
    }

    public record DeallocateJobModel(int JobId);

    [HttpPost]
    [Route("Supervisor/DeallocateJob")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> DeallocateJob([FromBody] DeallocateJobModel model)
    {
        var job = await _db.Jobs.FindAsync(model.JobId);
        if (job == null) return NotFound(new { success = false, message = "Job not found." });

        job.AssignedWorkerId = null;
        job.AssignedAt = null;

        await _db.SaveChangesAsync();
        return Json(new { success = true, message = $"Job {job.JobNo} returned to unallocated pool." });
    }
}