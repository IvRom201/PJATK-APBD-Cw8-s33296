using HospitalApi.DTOs;
using HospitalApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace HospitalApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly IPatientService _patientService;

    public PatientsController(IPatientService patientService)
    {
        _patientService = patientService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PatientDto>>> GetPatients(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var patients = await _patientService.GetPatientsAsync(search, cancellationToken);

        return Ok(patients);
    }

    [HttpPost("{pesel}/bedassignments")]
    public async Task<ActionResult<BedAssignmentDto>> AssignBed(
        [FromRoute] string pesel,
        [FromBody] AssignBedRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _patientService.AssignBedAsync(
            pesel,
            request,
            cancellationToken
        );

        if (!result.IsSuccess)
        {
            return StatusCode(result.StatusCode, new
            {
                status = result.StatusCode,
                message = result.ErrorMessage
            });
        }

        return StatusCode(result.StatusCode, result.Data);
    }
}