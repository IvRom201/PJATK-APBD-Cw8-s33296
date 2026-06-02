using HospitalApi.DTOs;

namespace HospitalApi.Services;

public interface IPatientService
{
    Task<IReadOnlyList<PatientDto>> GetPatientsAsync(
        string? search,
        CancellationToken cancellationToken
    );

    Task<ServiceResult<BedAssignmentDto>> AssignBedAsync(
        string pesel,
        AssignBedRequestDto request,
        CancellationToken cancellationToken
    );
}