using HospitalApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace HospitalApi.Controllers;

[ApiController]
[Route("api/patients")]
public class PatientsController : ControllerBase
{
    private readonly IPatientsService _patientsService;

    public PatientsController(IPatientsService patientsService)
    {
        _patientsService = patientsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPatients([FromQuery] string? search)
    {
        var patients = await _patientsService.GetPatientsAsync(search);

        return Ok(patients);
    }

    [HttpPost("{pesel}/bedassignments")]
    public async Task<IActionResult> AssignBed(
        [FromRoute] string pesel,
        [FromBody] AssignBedRequest? request)
    {
        var result = await _patientsService.AssignBedAsync(pesel, request);

        return StatusCode(result.StatusCode, result.Body);
    }
}